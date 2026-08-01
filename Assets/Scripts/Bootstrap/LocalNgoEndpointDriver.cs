using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Explicit local-development NGO owner. Live UOS allocation remains owned
    /// by the production application flow and is not simulated here.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class LocalNgoEndpointDriver :
        MonoBehaviour
    {
        [SerializeField] private bool dedicatedServer;
        [SerializeField] private GameBootstrap bootstrap;
        [SerializeField] private NetworkManager networkManager;
        [SerializeField] private UnityTransport transport;
        [SerializeField] private LobbyNetworkBridge lobbyBridge;
        [SerializeField] private ClientUiActionRouter uiActions;

        [Header("Local endpoint")]
        [SerializeField] private string address =
            "127.0.0.1";
        [SerializeField] private ushort port = 7777;
        [SerializeField] private int localPlayerSlot;
        [SerializeField] private bool automaticReady = true;

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
                    SpawnPointId = 1,
                    HeroConfigId = 1001,
                },
            };

        public bool IsStarted { get; private set; }
        private bool gameplayProofLogged;
        private bool connectionWaitLogged;
        private bool clientConnectionNotified;
        private double connectionStartedRealtime;

        private void Start()
        {
            ValidateOrThrow();
            networkManager.LogLevel =
                LogLevel.Developer;
            networkManager
                .OnClientDisconnectCallback +=
                OnClientDisconnected;
            networkManager.OnTransportFailure +=
                OnTransportFailure;
            transport.SetConnectionData(
                address,
                port);
            if (dedicatedServer)
                StartServer();
            else
                StartClient();
        }

        private void StartServer()
        {
            networkManager
                .OnClientConnectedCallback +=
                OnServerClientConnected;
            if (!networkManager.StartServer())
                throw new InvalidOperationException(
                    "NGO local Dedicated Server failed to start.");
            ActivateFrameSyncTransport();
            lobbyBridge.BindServer(
                bootstrap,
                serverSlots,
                matchId,
                startLeadTicks,
                gameModeId,
                mapConfigId,
                initialRandomSeed);
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
            ActivateFrameSyncTransport();
            lobbyBridge.BindClient(
                bootstrap,
                localPlayerSlot,
                accountId,
                uiActions);
            lobbyBridge.IdentityAccepted +=
                OnIdentityAccepted;
            IsStarted = true;
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} connecting " +
                $"to {address}:{port}.");
        }

        private void ActivateFrameSyncTransport()
        {
            bootstrap.BindFrameSyncNetworkRuntime();
            FrameSyncNetworkBridge bridge =
                networkManager.GetComponent<
                    FrameSyncNetworkBridge>();
            if (bridge == null)
                throw new InvalidOperationException(
                    "Local NGO endpoint requires FrameSyncNetworkBridge.");
            bridge.ActivateTransportHandlers();
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
                $"[LocalNGO] Client slot {localPlayerSlot} identity accepted.");
            if (automaticReady)
            {
                LocalLobbySlotDefinition slot =
                    GetLocalSlot();
                lobbyBridge.SubmitAutomaticReady(
                    slot.HeroConfigId);
            }
        }

        private void Update()
        {
            NotifyClientConnectedOnce();
            LogConnectionWaitOnce();
            if (gameplayProofLogged ||
                bootstrap == null ||
                !lobbyBridge.HasAppliedBootstrap ||
                bootstrap.Runtime == null ||
                bootstrap.Runtime.CurrentTick < 8)
                return;

            gameplayProofLogged = true;
            string endpoint = dedicatedServer
                ? "server"
                : $"client slot {localPlayerSlot}";
            string controlledUnit =
                dedicatedServer ||
                !bootstrap.IsLocalPlayerBound
                    ? "none"
                    : bootstrap.LocalControlledUnitUid
                        .ToString();
            Debug.Log(
                $"[LocalNGO] Gameplay active on {endpoint}: " +
                $"tick={bootstrap.Runtime.CurrentTick}, " +
                $"controlledUnit={controlledUnit}.");
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

        private LocalLobbySlotDefinition
            GetLocalSlot()
        {
            for (int i = 0;
                 i < serverSlots.Length;
                 i++)
                if (serverSlots[i]
                    .PlayerSlot ==
                    localPlayerSlot)
                    return serverSlots[i];
            throw new InvalidOperationException(
                $"No local allocation exists for PlayerSlot {localPlayerSlot}.");
        }

        private void ValidateOrThrow()
        {
            if (bootstrap == null ||
                networkManager == null ||
                transport == null ||
                lobbyBridge == null)
                throw new InvalidOperationException(
                    "Local NGO endpoint references are incomplete.");
            if (!bootstrap
                .UsesNetworkSimulation)
                throw new InvalidOperationException(
                    "Local NGO endpoint requires GameBootstrap network simulation mode.");
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
                lobbyBridge.IdentityAccepted -=
                    OnIdentityAccepted;
        }
    }
}
