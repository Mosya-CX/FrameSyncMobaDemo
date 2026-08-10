using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Validates the Bootstrap -> Lobby -> GameScene scene flow in local
    /// direct mode. A full two-process NGO match is covered by the packaged
    /// Server/Client executables (LocalNgoBuildMenu), not by editor PlayMode.
    /// </summary>
    public sealed class LocalNgoEndpointSceneTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetPersistentSession();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // The isolated client cannot reach a server; NGO may log its
            // socket failure asynchronously during teardown. Swallow those
            // expected transport logs so they never leak into later tests.
            LogAssert.ignoreFailingMessages = true;
            ResetPersistentSession();
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator
            ClientBootstrap_TransitionsToLobbyAndBindsSession()
        {
            AsyncOperation load =
                SceneManager.LoadSceneAsync(
                    GameSessionContext
                        .ClientBootstrapSceneName,
                    LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return WaitForScene(
                GameSessionContext.LobbySceneName);

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(
                    GameSessionContext.LobbySceneName),
                "ClientBootstrap must transition to Lobby.");
            Assert.That(
                GameSessionContext.FlowMode,
                Is.EqualTo(FrameFlowMode.LocalDirect));
            Assert.That(
                GameSessionContext.Versions.HasValue,
                Is.True,
                "The Lobby driver must compute the deterministic versions.");

            LocalNgoEndpointDriver driver =
                Object.FindObjectOfType<
                    LocalNgoEndpointDriver>();
            LobbyNetworkBridge bridge =
                Object.FindObjectOfType<
                    LobbyNetworkBridge>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);
            driver.RequestLocalClientStart();
            yield return WaitUntil(
                () => driver.IsStarted,
                300);
            Assert.That(driver.IsStarted, Is.True);
            Assert.That(
                bridge.IsBound,
                Is.True,
                "The client lobby bridge must be bound (direct connect).");
            Assert.That(
                bridge.HasAppliedBootstrap,
                Is.False,
                "Without a server, the client must not have applied a payload.");
            Assert.That(
                Object.FindObjectOfType<GameBootstrap>(),
                Is.Null,
                "Lobby must not initialize Gameplay.");
        }

        [UnityTest]
        public IEnumerator
            ClientBootstrap_OnlineFlowOverride_SelectsUosSession()
        {
            // Force the online flow without touching the scene serialized
            // value. The settings provider is stubbed to empty so Start
            // fails synchronously at the config gate and never touches the
            // network (a real config ID would trigger a live UOS login).
            UosApplicationConfig.FlowModeOverride = true;
            UosApplicationConfig
                .SettingsMatchmakingConfigIdProvider =
                () => null;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                AsyncOperation load =
                    SceneManager.LoadSceneAsync(
                        GameSessionContext
                            .ClientBootstrapSceneName,
                        LoadSceneMode.Single);
                Assert.That(load, Is.Not.Null);
                yield return load;
                yield return null;

                Assert.That(
                    GameSessionContext.FlowMode,
                    Is.EqualTo(
                        FrameFlowMode.UosOnline),
                    "The online flow override must drive the session mode.");
                Assert.That(
                    GameSessionContext.IsDedicatedServer,
                    Is.False);
            }
            finally
            {
                UosApplicationConfig.ResetTestState();
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [UnityTest]
        public IEnumerator
            ServerBootstrap_TransitionsToLobbyAndStartsServer()
        {
            AsyncOperation load =
                SceneManager.LoadSceneAsync(
                    GameSessionContext
                        .ServerBootstrapSceneName,
                    LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return WaitForScene(
                GameSessionContext.LobbySceneName);

            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(
                    GameSessionContext.LobbySceneName),
                "ServerBootstrap must transition to Lobby.");
            Assert.That(
                GameSessionContext.IsDedicatedServer,
                Is.True);

            LocalNgoEndpointDriver driver =
                Object.FindObjectOfType<
                    LocalNgoEndpointDriver>();
            LobbyNetworkBridge bridge =
                Object.FindObjectOfType<
                    LobbyNetworkBridge>();
            NetworkManager networkManager =
                Object.FindObjectOfType<NetworkManager>();
            Assert.That(driver, Is.Not.Null);
            Assert.That(bridge, Is.Not.Null);
            yield return WaitUntil(
                () => driver.IsStarted,
                300);
            Assert.That(driver.IsStarted, Is.True);
            yield return WaitUntil(
                () => networkManager.IsListening,
                300);
            Assert.That(
                bridge.IsBound,
                Is.True,
                "The server lobby bridge must be bound.");
            Assert.That(
                networkManager.IsListening,
                Is.True,
                "The local Dedicated Server must be listening.");
            Assert.That(
                Object.FindObjectOfType<GameBootstrap>(),
                Is.Null,
                "Lobby must not initialize Gameplay.");
        }

        private static IEnumerator WaitForScene(
            string sceneName,
            int maxFrames = 600)
        {
            int guard = 0;
            while (SceneManager.GetActiveScene().name !=
                   sceneName &&
                   guard++ < maxFrames)
                yield return null;
        }

        private static IEnumerator WaitUntil(
            System.Func<bool> condition,
            int maxFrames)
        {
            int guard = 0;
            while (!condition() &&
                   guard++ < maxFrames)
                yield return null;
        }

        private static void ResetPersistentSession()
        {
            NetworkManager[] managers =
                Object.FindObjectsOfType<NetworkManager>(true);
            for (int i = 0;
                 i < managers.Length;
                 i++)
            {
                NetworkManager manager = managers[i];
                if (manager.IsListening)
                    manager.Shutdown();
                Object.Destroy(manager.gameObject);
            }
            var uiManagers =
                Object.FindObjectsOfType<UIManager>(true);
            for (int i = 0;
                 i < uiManagers.Length;
                 i++)
                Object.Destroy(uiManagers[i].gameObject);
            var eventSystems =
                Object.FindObjectsOfType<
                    UnityEngine.EventSystems.EventSystem>(true);
            for (int i = 0;
                 i < eventSystems.Length;
                 i++)
                Object.Destroy(eventSystems[i].gameObject);
            GameSessionContext.ResetSession();
        }
    }
}
