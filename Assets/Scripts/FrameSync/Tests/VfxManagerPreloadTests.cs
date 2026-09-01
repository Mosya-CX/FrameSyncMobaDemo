using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace FrameSyncMoba.FrameSync.Tests
{
    public sealed class VfxManagerPreloadTests
    {
        [Test]
        public void PreloadAsync_LoadsOnceAndCreatesOneInactivePoolInstance()
        {
            var prefab = new GameObject("VfxPrefab");
            var managerObject = new GameObject("VfxManager");
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            var loader = new FakeLoader(prefab);
            try
            {
                SetEntries(
                    library,
                    new VfxLibrary.VfxPrefabEntry
                    {
                        VfxDefId = 4001,
                        Address = "vfx/4001",
                    });
                VfxManager manager =
                    managerObject.AddComponent<VfxManager>();
                manager.SetLibrary(library);
                manager.SetAssetLoader(loader);

                manager.PreloadAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.That(loader.AcquireCount, Is.EqualTo(1));
                Assert.That(managerObject.transform.childCount, Is.EqualTo(1));
                Assert.That(
                    managerObject.transform.GetChild(0).gameObject.activeSelf,
                    Is.False);

                manager.PreloadAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.That(
                    loader.AcquireCount,
                    Is.EqualTo(1),
                    "The manager-owned lease must serve subsequent warmup.");
                Assert.That(
                    managerObject.transform.childCount,
                    Is.EqualTo(1),
                    "Warmup must not grow the pool when one instance is ready.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void PreloadAsync_SkipsVfxOwnedByUnselectedHeroes()
        {
            var prefab = new GameObject("VfxPrefab");
            var managerObject = new GameObject("VfxManager");
            var library = ScriptableObject.CreateInstance<VfxLibrary>();
            var loader = new FakeLoader(prefab);
            try
            {
                SetEntries(
                    library,
                    new VfxLibrary.VfxPrefabEntry
                    {
                        VfxDefId = 4001,
                        Address = "vfx/4001",
                        OwnerHeroConfigId = 1001,
                    },
                    new VfxLibrary.VfxPrefabEntry
                    {
                        VfxDefId = 3101,
                        Address = "vfx/3101",
                        OwnerHeroConfigId = 1002,
                    });
                VfxManager manager =
                    managerObject.AddComponent<VfxManager>();
                manager.SetLibrary(library);
                manager.SetAssetLoader(loader);

                manager.PreloadAsync(
                        new[] { 1001 },
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                Assert.That(loader.AcquireCount, Is.EqualTo(1));
                Assert.That(managerObject.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
                UnityEngine.Object.DestroyImmediate(prefab);
                UnityEngine.Object.DestroyImmediate(library);
            }
        }

        private static void SetEntries(
            VfxLibrary library,
            params VfxLibrary.VfxPrefabEntry[] entries)
        {
            FieldInfo field = typeof(VfxLibrary).GetField(
                "_entries",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(library, entries);
        }

        private sealed class FakeLoader : IClientPresentationAssetLoader
        {
            private readonly GameObject prefab;

            public FakeLoader(GameObject prefab)
            {
                this.prefab = prefab;
            }

            public int AcquireCount { get; private set; }

            public Task<IPresentationAssetLease<GameObject>>
                AcquirePrefabAsync(
                    string address,
                    CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AcquireCount++;
                return Task.FromResult<
                    IPresentationAssetLease<GameObject>>(
                    new Lease<GameObject>(prefab));
            }

            public Task<IPresentationAssetLease<AudioClip>>
                AcquireAudioClipAsync(
                    string address,
                    CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<IPresentationAssetLease<Sprite>> AcquireSpriteAsync(
                string address,
                CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class Lease<T> : IPresentationAssetLease<T>
            where T : UnityEngine.Object
        {
            public Lease(T asset)
            {
                Asset = asset;
            }

            public T Asset { get; }

            public void Dispose()
            {
            }
        }
    }
}
