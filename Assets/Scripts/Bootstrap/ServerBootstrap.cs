using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using Unity.Netcode;
using Unity.UOS.Multiverse;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Dedicated Server process startup scene. Local mode loads the Lobby
    /// scene and lets the Lobby driver start the NGO server. UOS mode reads
    /// the allocation, starts the server, notifies UOS Ready and derives the
    /// Lobby slots deterministically from the allocated players.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerBootstrap :
        MonoBehaviour
    {
        [SerializeField] private bool
            enableOnlineApplicationFlow;
        [SerializeField] private GlobalGameplayData
            globalGameplayData;
        [SerializeField] private NetworkManager
            networkManager;
        [SerializeField] private int defaultHeroConfigId =
            1001;

        private void Awake()
        {
            GameSessionContext.ResetSession();
            GameSessionContext.IsDedicatedServer =
                true;
            GameSessionContext.FlowManagedExternally =
                true;
            enableOnlineApplicationFlow =
                UosApplicationConfig.IsOnlineFlowRequested(
                    enableOnlineApplicationFlow);
            GameSessionContext.FlowMode =
                enableOnlineApplicationFlow
                    ? FrameFlowMode.UosOnline
                    : FrameFlowMode.LocalDirect;
            GameSessionContext.Versions =
                GameSessionContext.ComputeVersions(
                    globalGameplayData);
            GameSessionContext.HeroDisplayTable =
                globalGameplayData != null
                    ? globalGameplayData.HeroDisplayTable
                    : null;
            SharedGameplayChecksum.DetailedLoggingEnabled =
                Array.IndexOf(
                    Environment.GetCommandLineArgs(),
                    "-checksumDetail") >= 0;
            MarkNetworkRootPersistent();
        }

        private async void Start()
        {
            if (!enableOnlineApplicationFlow)
            {
                LoadLobby();
                return;
            }

            try
            {
                if (networkManager == null)
                    throw new InvalidOperationException(
                        "Server online flow requires NetworkManager.");
                await MultiverseSDK.Initialize();
                var serverInfo = await MultiverseSDK.Instance
                    .GetServerInfoAsync();
                if (UosApplicationConfig.IsProfileTestServer(
                        serverInfo.IsTestServer,
                        Environment.GetEnvironmentVariable(
                            UosApplicationConfig
                                .ProfileTestServerEnvironmentVariable)))
                {
                    await BootUosProfileTestServerAsync();
                    return;
                }
                GameSessionContext.ServerFlow =
                    new DedicatedServerApplicationFlow(
                        new UosDedicatedServerPlatform(
                            true),
                        new NgoConnectionService(
                            networkManager));
                await GameSessionContext.ServerFlow
                    .BootAsync();
                GameSessionContext.ServerSlots =
                    BuildAllocationSlots(
                        GameSessionContext.ServerFlow
                            .Allocation,
                        defaultHeroConfigId);
                GameSessionContext.NetworkAlreadyStarted =
                    true;
                Debug.Log(
                    "[ServerBootstrap] UOS allocation read and server " +
                    $"listening for match " +
                    $"'{GameSessionContext.ServerFlow.Allocation.MatchId}'.");
                LoadLobby();
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    this);
                Debug.LogError(
                    "[ServerBootstrap] UOS Dedicated Server boot failed; " +
                    "staying in the bootstrap scene.");
            }
        }

        private async Task BootUosProfileTestServerAsync()
        {
            var network = new NgoConnectionService(
                networkManager);
            network.StartServer();
            GameSessionContext.NetworkAlreadyStarted = true;
            await MultiverseSDK.Instance.ReadyAsync();
            Debug.Log(
                "[ServerBootstrap] UOS Profile test server is listening " +
                "and Ready; Matchmaking allocation is intentionally " +
                "skipped for IS_TEST_SERVER=true.");
        }

        private void LoadLobby()
        {
            Debug.Log(
                "[ServerBootstrap] Loading Lobby scene.");
            SceneManager.LoadScene(
                GameSessionContext.LobbySceneName);
        }

        private void MarkNetworkRootPersistent()
        {
            GameObject[] roots =
                SceneManager.GetActiveScene()
                    .GetRootGameObjects();
            for (int i = 0;
                 i < roots.Length;
                 i++)
            {
                GameObject root = roots[i];
                if (root.GetComponent<NetworkManager>() !=
                    null ||
                    root.GetComponent<
                        FrameSyncNetworkBridge>() !=
                    null ||
                    root.GetComponent<
                        LobbyNetworkBridge>() !=
                    null ||
                    root.GetComponent<
                        ClientUiActionRouter>() !=
                    null)
                    DontDestroyOnLoad(root);
            }
        }

        /// <summary>
        /// Deterministic framework-level allocation: sorted account IDs, team
        /// by alternating PlayerSlot and spawn point by slot. Production
        /// team/spawn/hero rules remain matchmaking-config content.
        /// </summary>
        private static LocalLobbySlotDefinition[]
            BuildAllocationSlots(
                DedicatedServerAllocation allocation,
                int heroConfigId)
        {
            string[] ids = allocation.AccountIds;
            if (ids.Length == 0 ||
                ids.Length > 10)
                throw new InvalidOperationException(
                    "UOS allocation player count must be 1-10.");
            var slots =
                new LocalLobbySlotDefinition[ids.Length];
            for (int i = 0;
                 i < ids.Length;
                 i++)
            {
                int teamId = i % 2 + 1;
                int teamIndex = i / 2;
                // FullMatchDeterministicMapConfig: spawn points 0-4 are the
                // blue team and 5-9 are the red team, so red slots must not
                // reuse blue spawn ids.
                int spawnPointId =
                    teamId == 1
                        ? teamIndex
                        : 5 + teamIndex;
                slots[i] =
                    new LocalLobbySlotDefinition
                    {
                        PlayerSlot = i,
                        AccountId = ids[i],
                        TeamId = teamId,
                        SpawnPointId = spawnPointId,
                        HeroConfigId = heroConfigId,
                    };
            }
            return slots;
        }
    }
}
