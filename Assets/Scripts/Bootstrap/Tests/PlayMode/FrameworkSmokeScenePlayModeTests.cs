using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class FrameworkSmokeScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator FrameworkSmokeScene_SpawnsNeutralUnitAndAdvancesTicks()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "FrameworkSmoke", LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;

            Scene scene = SceneManager.GetSceneByName("FrameworkSmoke");
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
            GameBootstrap bootstrap = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length && bootstrap == null; i++)
                bootstrap = roots[i].GetComponentInChildren<GameBootstrap>(true);
            Assert.That(bootstrap, Is.Not.Null);

            yield return new WaitForSecondsRealtime(0.2f);

            Assert.That(bootstrap.IsInitialized, Is.True);
            Assert.That(bootstrap.Runtime.CurrentTick, Is.GreaterThan(0));
            Assert.That(bootstrap.UnitWorld.GetAllUnits().Count, Is.EqualTo(1));
            Assert.That(
                bootstrap.UnitWorld.GetAllUnits()[0].UnitPrototypeId,
                Is.EqualTo(1001));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }
    }
}
