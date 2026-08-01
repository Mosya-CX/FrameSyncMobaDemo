using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class LocalNgoEndpointSceneTests
    {
        [UnityTest]
        public IEnumerator ServerScene_StartsAndWaitsForLobbyBarrier()
        {
            AsyncOperation load =
                SceneManager.LoadSceneAsync(
                    "ServerBootstrap",
                    LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene =
                SceneManager.GetSceneByName(
                    "ServerBootstrap");
            Assert.That(
                scene.IsValid() && scene.isLoaded,
                Is.True);
            LocalNgoEndpointDriver driver =
                Object.FindObjectOfType<
                    LocalNgoEndpointDriver>();
            GameBootstrap bootstrap =
                Object.FindObjectOfType<
                    GameBootstrap>();

            Assert.That(driver, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(driver.IsStarted, Is.True);
            Assert.That(
                bootstrap.IsMatchReady,
                Is.False);

        }
    }
}
