using System;
using FrameSyncMoba.RuntimeConfig;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Lobby-scene NGO driver.
    ///
    /// Local development mode:
    ///   - Dedicated Server role starts the NGO server, binds the lobby bridge
    ///     with the frozen local allocation and loads GameScene once every
    ///     assigned client has locked a hero.
    ///   - Client role connects directly to the local Server, binds the lobby
    ///     bridge, waits for identity acceptance, drives hero select/lock
    ///     through the UI and loads GameScene after locking.
    ///
    /// UOS online mode:
    ///   - The client matchmaking/connection part is owned by
    ///     <see cref="LobbyFlowController"/>; this driver only registers the
    ///     session state.
    ///   - The Dedicated Server is started by ServerBootstrap; this driver
    ///     binds the lobby bridge with the allocation-derived slots.
    ///
    /// The deterministic runtime itself is initialized by GameBootstrap in
    /// GameScene; this driver never owns Gameplay.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class LocalNgoEndpointDriver :
        MonoBehaviour
    {
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private GlobalGameplayData
            globalGameplayData;
        [SerializeField] private NetworkManager
            networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private LobbyNetworkBridge lobbyBridge;
        [SerializeField] private ClientUiActionRouter uiActions;
        [SerializeField] private UIManager uiManager;

        [Header("Local endpoint")]
        [SerializeField] private string address =
            "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private int localPlayerSlot;

        [Header("Frozen local server allocation")]
        [SerializeField] private string matchId =
            "local-ngo-match";
        [SerializeField] private int startLeadTicks = 3;
        [SerializeField] private int gameModeId = 1;
        [SerializeField] private int mapConfigId = 1;
        [SerializeField] private uint initialRandomSeed =
            12345;
        [SerializeField] private LocalLobbySlotDefinition[]
            serverSlots =
            {
                new LocalLobbySlotDefinition
                {
                    PlayerSlot = 0,
                    AccountId = "LocalPlayer0",
                    TeamId = 1,
                    SpawnPointId = 0,
                    HeroConfigId = 1001,
                },
                new LocalLobbySlotDefinition
                {
                    PlayerSlot = 1,
                    AccountId = "LocalPlayer1",
                    TeamId = 2,
                    // RedTeam Player1 in FullMatchDeterministicMapConfig
                    // (spawn points 0-4 = blue team, 5-9 = red team).
                    SpawnPointId = 5,
                    HeroConfigId = 1001,
                },
            };

        public bool IsStarted { get; private set; }

        /// <summary>
        /// Local direct mode keeps the Main page visible until the player
        /// starts matchmaking; only then does the client connect. The
        /// Dedicated Server and UOS paths are unaffected.
        /// </summary>
        public void RequestLocalClientStart()
        {
            localClientStartRequested = true;
        }

        private bool endpointStarted;
        private bool gameplayProofLogged;
        private bool connectionWaitLogged;
        private bool clientConnectionNotified;
        private bool sceneLoadTriggered;
        private bool bridgeBound;
        private bool localClientStartRequested;
        private bool localStartWaitLogged;
        private double connectionStartedRealtime;

        private void Awake()
        {
            if (globalGameplayData == null)
                throw new InvalidOperationException(
                    "Lobby endpoint driver requires GlobalGameplayData " +
                    "to compute the deterministic version handshake.");
            dedicatedServer =
                GameSessionContext.IsDedicatedServer;
            GameSessionContext.FlowManagedExternally =
                true;
            GameSessionContext.Versions =
                GameSessionContext.ComputeVersions(
                    globalGameplayData);
            if (GameSessionContext.HeroDisplayTable ==
                null &&
                globalGameplayData != null)
                GameSessionContext.HeroDisplayTable =
                    globalGameplayData.HeroDisplayTable;
            GameSessionContext.LobbyBridge =
                lobbyBridge;
            GameSessionContext.UiActions =
                uiActions;
        }

        private void Start()
        {
            ResolveReferences();
            // The LobbyNetworkBridge must survive into GameScene: the client
            // sends Loaded/Ready and receives the authoritative payload
            // through it, and the server broadcasts the payload through it.
            // The bootstrap scene persists the NGO root; the Lobby scene's
            // bridge is persisted explicitly here.
            if (lobbyBridge != null)
            {
                GameSessionContext.LobbyBridge =
                    lobbyBridge;
                DontDestroyOnLoad(
                    lobbyBridge.gameObject);
            }
            networkManager.LogLevel =
                LogLevel.Developer;
            networkManager
                .OnClientDisconnectCallback +=
                OnClientDisconnected;
            networkManager.OnTransportFailure +=
                OnTransportFailure;
            // Endpoint binding is deferred to the first Update so the
            // persistent NGO root is fully stable after the Bootstrap -> Lobby
            // scene transition.
        }

        private void ResolveReferences()
        {
            if (networkManager == null)
                networkManager =
                    FindObjectOfType<NetworkManager>(true);
            if (transport == null &&
                networkManager != null &&
                networkManager.NetworkConfig != null)
                transport =
                    networkManager.NetworkConfig
                        .NetworkTransport as UnityTransport;
            if (lobbyBridge == null)
                lobbyBridge =
                    FindObjectOfType<LobbyNetworkBridge>(true);
            if (uiActions == null)
                uiActions =
                    FindObjectOfType<ClientUiActionRouter>(true);
        }

        private void StartServer()
        {
            networkManager
                .OnClientConnectedCallback +=
                OnServerClientConnected;
            if (!networkManager.StartServer())
                throw new InvalidOperationException(
                    "NGO local Dedicated Server failed to start.");
            IsStarted = true;
            Debug.Log(
                $"[LocalNGO] Server listening on {address}:{port}; " +
                $"waiting for {serverSlots.Length} verified clients.");
        }

        private void StartClient()
        {
            localPlayerSlot =
                ReadPlayerSlotArgument(
                    Environment
                        .GetCommandLineArgs(),
                    localPlayerSlot);
            string accountId =
                $"LocalPlayer{localPlayerSlot}";
            networkManager
                .OnClientConnectedCallback +=
                OnClientConnected;
            if (!networkManager.StartClient())
                throw new InvalidOperationException(
                    "NGO local Client failed to start.");
            connectionStartedRealtime =
                Time.realtimeSinceStartupAsDouble;
            BindClientBridge(
                localPlayerSlot,
                accountId);
            IsStarted = true;
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} connecting " +
                $"to {address}:{port}.");
        }

        private void BindServerBridge()
        {
            if (bridgeBound)
                return;
            LocalLobbySlotDefinition[] slots =
                GameSessionContext.ServerSlots ??
                serverSlots;
            lobbyBridge.BindServer(
                slots,
                matchId,
                startLeadTicks,
                gameModeId,
                mapConfigId,
                initialRandomSeed);
            lobbyBridge.AllHeroesLocked +=
                OnAllHeroesLocked;
            lobbyBridge.StartScheduled +=
                OnStartScheduled;
            bridgeBound = true;
            Debug.Log(
                $"[LocalNGO] Server lobby bound with {slots.Length} slots.");
        }

        private void BindClientBridge(
            int playerSlot,
            string accountId)
        {
            if (bridgeBound)
                return;
            lobbyBridge.BindClient(
                playerSlot,
                accountId,
                uiActions);
            lobbyBridge.IdentityAccepted +=
                OnIdentityAccepted;
            lobbyBridge.HeroLocked +=
                OnHeroLocked;
            lobbyBridge.ConfirmedCountChanged +=
                OnConfirmedCountChanged;
            lobbyBridge.LoadSceneRequested +=
                OnLoadSceneRequested;
            bridgeBound = true;
        }

        private void OnClientConnected(
            ulong clientId)
        {
            if (clientId !=
                networkManager.LocalClientId)
                return;
            NotifyClientConnectedOnce();
        }

        private void NotifyClientConnectedOnce()
        {
            if (dedicatedServer ||
                clientConnectionNotified ||
                networkManager == null ||
                !networkManager.IsConnectedClient)
                return;

            clientConnectionNotified = true;
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} transport connected " +
                $"as NGO client {networkManager.LocalClientId}.");
            lobbyBridge.NotifyClientConnected();
        }

        private void OnServerClientConnected(
            ulong clientId)
        {
            Debug.Log(
                $"[LocalNGO] Server accepted NGO client {clientId}; " +
                $"connectedClients={networkManager.ConnectedClientsIds.Count}.");
        }

        private void OnClientDisconnected(
            ulong clientId)
        {
            string endpoint = dedicatedServer
                ? "server"
                : $"client slot {localPlayerSlot}";
            Debug.LogError(
                $"[LocalNGO] {endpoint} observed NGO client {clientId} " +
                $"disconnect. reason='{networkManager.DisconnectReason}'.");
        }

        private void OnTransportFailure()
        {
            string endpoint = dedicatedServer
                ? "server"
                : $"client slot {localPlayerSlot}";
            Debug.LogError(
                $"[LocalNGO] Transport failure on {endpoint}; " +
                $"address={address}, port={port}, " +
                $"isListening={networkManager.IsListening}, " +
                $"disconnectReason='{networkManager.DisconnectReason}'.");
        }

        private void OnIdentityAccepted()
        {
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} identity accepted; " +
                "opening hero select.");
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Select);
        }

        private void OnHeroLocked()
        {
            Debug.Log(
                "[LocalNGO] Client locked hero; waiting for all players " +
                "before loading GameScene.");
        }

        private void OnConfirmedCountChanged(
            int confirmedCount)
        {
            GameFlowLuaBridge.ConfirmedHeroCount =
                confirmedCount;
        }

        private void OnLoadSceneRequested()
        {
            if (sceneLoadTriggered)
                return;
            sceneLoadTriggered = true;
            Debug.Log(
                "[LocalNGO] Server confirmed all heroes; loading GameScene.");
            if (uiManager != null)
                uiManager.ShowPage(UIPageId.Load);
            SceneManager.LoadScene(
                GameSessionContext.GameSceneName);
        }

        private void OnAllHeroesLocked()
        {
            if (sceneLoadTriggered)
                return;
            sceneLoadTriggered = true;
            Debug.Log(
                "[LocalNGO] All clients locked heroes; server loading GameScene.");
            SceneManager.LoadScene(
                GameSessionContext.GameSceneName);
        }

        private void OnStartScheduled(
            FrameSyncMoba.FrameSync.GameStartConfig config)
        {
            Debug.Log(
                $"[LocalNGO] Lobby start scheduled for '{config.MatchId}' " +
                $"at StartTick {config.StartTick}; awaiting GameScene runtime.");
            if (sceneLoadTriggered)
                return;
            sceneLoadTriggered = true;
            SceneManager.LoadScene(
                GameSessionContext.GameSceneName);
        }

        private void Update()
        {
            TryStartEndpoint();
            if (OwnsClientConnectionLifecycle(
                    GameSessionContext.FlowMode))
            {
                NotifyClientConnectedOnce();
                LogConnectionWaitOnce();
            }
            if (gameplayProofLogged ||
                GameSessionContext.Bootstrap == null ||
                !lobbyBridge.HasAppliedBootstrap ||
                GameSessionContext.Bootstrap.Runtime == null ||
                GameSessionContext.Bootstrap.Runtime.CurrentTick < 8)
                return;

            gameplayProofLogged = true;
            string endpoint = dedicatedServer
                ? "server"
                : $"client slot {localPlayerSlot}";
            string controlledUnit =
                dedicatedServer ||
                !GameSessionContext.Bootstrap
                    .IsLocalPlayerBound
                    ? "none"
                    : GameSessionContext.Bootstrap
                        .LocalControlledUnitUid
                        .ToString();
            Debug.Log(
                $"[LocalNGO] Gameplay active on {endpoint}: " +
                $"tick={GameSessionContext.Bootstrap.Runtime.CurrentTick}, " +
                $"controlledUnit={controlledUnit}.");
        }

        internal static bool OwnsClientConnectionLifecycle(
            FrameFlowMode flowMode)
        {
            // UOS matchmaking and connection are exclusively owned by
            // LobbyFlowController. Running the local-direct notification path
            // as well races LobbyNetworkBridge.BindClient and can report a
            // transport connection before the bridge has a client owner.
            return flowMode == FrameFlowMode.LocalDirect;
        }

        private void TryStartEndpoint()
        {
            if (endpointStarted)
                return;
            ResolveReferences();
            if (networkManager == null ||
                transport == null ||
                lobbyBridge == null ||
                !lobbyBridge.IsNetworkReady)
                return;
            ValidateOrThrow();

            if (dedicatedServer)
            {
                if (!GameSessionContext
                        .NetworkAlreadyStarted)
                    StartServer();
                BindServerBridge();
                endpointStarted = true;
                return;
            }

            if (GameSessionContext.FlowMode ==
                FrameFlowMode.UosOnline)
            {
                Debug.Log(
                    "[LocalNGO] UOS client LobbyFlowController owns " +
                    "matchmaking and connection.");
                endpointStarted = true;
                return;
            }

            if (!localClientStartRequested)
            {
                LogLocalStartWaitOnce();
                return;
            }
            transport.SetConnectionData(
                address,
                port);
            StartClient();
            endpointStarted = true;
        }

        private void LogLocalStartWaitOnce()
        {
            if (localStartWaitLogged)
                return;
            localStartWaitLogged = true;
            Debug.Log(
                "[LocalNGO] Local client is waiting on the Main page; " +
                "start matchmaking to connect to " +
                $"{address}:{port}.");
        }

        private void LogConnectionWaitOnce()
        {
            if (dedicatedServer ||
                connectionWaitLogged ||
                clientConnectionNotified ||
                networkManager == null ||
                networkManager.IsConnectedClient)
                return;

            double waitSeconds =
                Time.realtimeSinceStartupAsDouble -
                connectionStartedRealtime;
            if (waitSeconds < 10d)
                return;

            connectionWaitLogged = true;
            Debug.LogWarning(
                $"[LocalNGO] Client slot {localPlayerSlot} is still waiting " +
                $"for NGO transport connection after {waitSeconds:F1} " +
                $"seconds; address={address}, port={port}, " +
                $"isListening={networkManager.IsListening}, " +
                $"disconnectReason='{networkManager.DisconnectReason}'.");
        }

        private void ValidateOrThrow()
        {
            if (networkManager == null ||
                transport == null ||
                lobbyBridge == null)
                throw new InvalidOperationException(
                    "Local NGO endpoint references are incomplete.");
            if (GameSessionContext.Versions == null)
                throw new InvalidOperationException(
                    "Local NGO endpoint requires a deterministic version handshake.");
            if (!dedicatedServer &&
                uiActions == null)
                throw new InvalidOperationException(
                    "Local NGO Client requires ClientUiActionRouter.");
        }

        private static int ReadPlayerSlotArgument(
            string[] arguments,
            int fallback)
        {
            const string prefix =
                "--LocalPlayerSlot=";
            if (arguments != null)
                for (int i = 0;
                     i < arguments.Length;
                     i++)
                {
                    string argument =
                        arguments[i];
                    if (argument == null ||
                        !argument.StartsWith(
                            prefix,
                            StringComparison
                                .OrdinalIgnoreCase))
                        continue;
                    if (int.TryParse(
                            argument.Substring(
                                prefix.Length),
                            out int parsed) &&
                        parsed >= 0 &&
                        parsed < 10)
                        return parsed;
                    throw new ArgumentException(
                        "LocalPlayerSlot argument is invalid.");
                }
            return fallback;
        }

        private void OnDestroy()
        {
            if (networkManager != null)
            {
                networkManager
                    .OnClientConnectedCallback -=
                    OnClientConnected;
                networkManager
                    .OnClientConnectedCallback -=
                    OnServerClientConnected;
                networkManager
                    .OnClientDisconnectCallback -=
                    OnClientDisconnected;
                networkManager.OnTransportFailure -=
                    OnTransportFailure;
            }
            if (lobbyBridge != null)
            {
                lobbyBridge.IdentityAccepted -=
                    OnIdentityAccepted;
                lobbyBridge.HeroLocked -=
                    OnHeroLocked;
                lobbyBridge.ConfirmedCountChanged -=
                    OnConfirmedCountChanged;
                lobbyBridge.LoadSceneRequested -=
                    OnLoadSceneRequested;
                lobbyBridge.AllHeroesLocked -=
                    OnAllHeroesLocked;
                lobbyBridge.StartScheduled -=
                    OnStartScheduled;
            }
        }
    }
}
