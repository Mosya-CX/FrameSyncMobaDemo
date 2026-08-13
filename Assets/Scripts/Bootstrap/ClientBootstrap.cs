using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Client process startup scene. Owns account bootstrap and the UOS client
    /// session, marks the NGO network root persistent and then loads the Lobby
    /// scene. It never initializes Gameplay or Lobby UI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClientBootstrap :
        MonoBehaviour
    {
        [SerializeField] private bool
            enableOnlineApplicationFlow;
        [SerializeField] private GlobalGameplayData
            globalGameplayData;
        [SerializeField] private NetworkManager
            networkManager;

        private void Awake()
        {
            GameSessionContext.ResetSession();
            GameSessionContext.IsDedicatedServer =
                false;
            FrameSyncDiagnosticsUnityHost.EnsureInitialized(
                false);
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
                        "Client online flow requires NetworkManager.");
                string configId =
                    UosApplicationConfig
                        .ResolveMatchmakingConfigId();
                if (string.IsNullOrWhiteSpace(configId))
                    throw new InvalidOperationException(
                        "Client online flow requires a UOS Matchmaking " +
                        "config ID. Configure it in the UOS Launcher " +
                        "environment settings or pass " +
                        $"{UosApplicationConfig.MatchmakingConfigIdArg}" +
                        "=<id> on the command line.");
                GameSessionContext.ClientFlow =
                    new ClientApplicationFlow(
                        new TestAccountBootstrapService(
                            new PlayerPrefsTestAccountPersistence()),
                        new UosClientSession(),
                        new UosMatchmakingApplicationClient(
                            configId,
                            UosApplicationConfig
                                .ResolveRegionId()),
                        new NgoConnectionService(
                            networkManager));
                await GameSessionContext.ClientFlow
                    .InitializeAccountAsync(
                        Environment
                            .GetCommandLineArgs());
                Debug.Log(
                    "[ClientBootstrap] UOS session ready for account " +
                    $"'{GameSessionContext.ClientFlow.AccountSession.TestAccountId}'.");
                LoadLobby();
            }
            catch (Exception exception)
            {
                Debug.LogException(
                    exception,
                    this);
                Debug.LogError(
                    "[ClientBootstrap] UOS account initialization failed; " +
                    "staying in the bootstrap scene.");
            }
        }

        private void LoadLobby()
        {
            Debug.Log(
                "[ClientBootstrap] Loading Lobby scene.");
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
    }
}
