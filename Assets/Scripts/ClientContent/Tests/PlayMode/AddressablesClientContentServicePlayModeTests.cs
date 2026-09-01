using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.ClientContent.Tests
{
    public sealed class AddressablesClientContentServicePlayModeTests
    {
        [UnityTest]
        public IEnumerator RepresentativeClientRoots_LoadAndReleaseAsynchronously()
        {
            var service = new AddressablesClientContentService();
            try
            {
                Task initialize = service.InitializeAsync(
                    CancellationToken.None);
                yield return WaitFor(initialize);
                Assert.That(initialize.Exception, Is.Null);
                Assert.That(service.IsInitialized, Is.True);

                Task<IPresentationAssetLease<GameObject>> unitFirst =
                    service.AcquirePrefabAsync(
                        "view/unit/1102",
                        CancellationToken.None);
                yield return WaitFor(unitFirst);
                AssertCompleted(unitFirst);

                Task<IPresentationAssetLease<GameObject>> unitSecond =
                    service.AcquirePrefabAsync(
                        "view/unit/1102",
                        CancellationToken.None);
                yield return WaitFor(unitSecond);
                AssertCompleted(unitSecond);
                Assert.That(
                    unitSecond.Result.Asset,
                    Is.SameAs(unitFirst.Result.Asset),
                    "Concurrent leases for one address must share the cached asset handle.");

                using (unitFirst.Result)
                using (unitSecond.Result)
                {
                    Assert.That(unitFirst.Result.Asset, Is.Not.Null);
                }

                yield return LoadAndReleasePrefab(
                    service,
                    "view/projectile/2106");
                yield return LoadAndReleasePrefab(
                    service,
                    "view/map/main");
                yield return LoadAndReleasePrefab(
                    service,
                    "vfx/3101");
                yield return LoadAndReleasePrefab(
                    service,
                    "ui/page/hud");
                yield return LoadAndReleasePrefab(
                    service,
                    "ui/indicator/direction");
                yield return LoadAndReleasePrefab(
                    service,
                    "ui/indicator/range-circle");
                yield return LoadAndReleasePrefab(
                    service,
                    "ui/indicator/ground-target");

                Task<IPresentationAssetLease<AudioClip>> audio =
                    service.AcquireAudioClipAsync(
                        "audio/1",
                        CancellationToken.None);
                yield return WaitFor(audio);
                AssertCompleted(audio);
                using (audio.Result)
                    Assert.That(audio.Result.Asset, Is.Not.Null);

                Task<IPresentationAssetLease<Sprite>> sprite =
                    service.AcquireSpriteAsync(
                        "ui/icon/05cc6a8fbb52ed246b0f3b4720325ef1",
                        CancellationToken.None);
                yield return WaitFor(sprite);
                AssertCompleted(sprite);
                using (sprite.Result)
                    Assert.That(sprite.Result.Asset, Is.Not.Null);
            }
            finally
            {
                service.Dispose();
            }
        }

        private static IEnumerator LoadAndReleasePrefab(
            AddressablesClientContentService service,
            string address)
        {
            Task<IPresentationAssetLease<GameObject>> load =
                service.AcquirePrefabAsync(
                    address,
                    CancellationToken.None);
            yield return WaitFor(load);
            AssertCompleted(load);
            using (load.Result)
                Assert.That(load.Result.Asset, Is.Not.Null, address);
        }

        private static IEnumerator WaitFor(Task task)
        {
            while (!task.IsCompleted)
                yield return null;
        }

        private static void AssertCompleted(Task task)
        {
            Assert.That(task.IsCanceled, Is.False);
            Assert.That(task.Exception, Is.Null);
            Assert.That(task.IsCompletedSuccessfully, Is.True);
        }
    }
}
