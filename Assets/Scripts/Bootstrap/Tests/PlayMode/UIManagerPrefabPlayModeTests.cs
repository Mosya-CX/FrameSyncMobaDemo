using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class
        UIManagerPrefabPlayModeTests
    {
        private FakePresentationLoader ownedLoader;

        [UnitySetUp]
        public IEnumerator SetUpAddressablesLoader()
        {
            if (ClientPresentationServices.Loader != null)
                yield break;
            ownedLoader = new FakePresentationLoader();
            if (ClientPresentationServices.Loader == null)
                ClientPresentationServices.Register(ownedLoader);
            else
                ownedLoader = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownAddressablesLoader()
        {
            if (ownedLoader != null)
            {
                ClientSpriteRegistry.Clear();
                ClientPresentationServices.Unregister(ownedLoader);
                ownedLoader = null;
            }
            yield return null;
        }

        private sealed class FakePresentationLoader :
            IClientPresentationAssetLoader
        {
            private TaskCompletionSource<IPresentationAssetLease<Sprite>>
                spriteRequest;

            public int SpriteRequestCount { get; private set; }

            public Task<IPresentationAssetLease<GameObject>>
                AcquirePrefabAsync(
                    string address,
                    CancellationToken cancellationToken)
            {
                string pageName = address switch
                {
                    "ui/page/main" => "MainPanel",
                    "ui/page/match" => "MatchPanel",
                    "ui/page/select" => "SelectPanel",
                    "ui/page/load" => "LoadingPanel",
                    "ui/page/hud" => "GameplayHUD",
                    "ui/page/shop" => "ShopPanel",
                    "ui/page/result" => "ResultPanel",
                    _ => throw new System.InvalidOperationException(address),
                };
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/ClientContent/UI/{pageName}.prefab");
                return Task.FromResult<IPresentationAssetLease<GameObject>>(
                    new FakeLease<GameObject>(asset));
            }

            public Task<IPresentationAssetLease<AudioClip>>
                AcquireAudioClipAsync(
                    string address,
                    CancellationToken cancellationToken) =>
                throw new System.NotSupportedException();

            public Task<IPresentationAssetLease<Sprite>> AcquireSpriteAsync(
                string address,
                CancellationToken cancellationToken)
            {
                SpriteRequestCount++;
                spriteRequest =
                    new TaskCompletionSource<
                        IPresentationAssetLease<Sprite>>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                return spriteRequest.Task;
            }

            public void CompleteSpriteRequest(
                IPresentationAssetLease<Sprite> lease)
            {
                spriteRequest.SetResult(lease);
            }
        }

        private sealed class FakeLease<T> : IPresentationAssetLease<T>
            where T : UnityEngine.Object
        {
            public FakeLease(T asset) { Asset = asset; }
            public T Asset { get; }
            public bool IsDisposed { get; private set; }
            public void Dispose() { IsDisposed = true; }
        }

        [UnityTest]
        public IEnumerator SpriteClear_RejectsAnOlderAsyncGeneration()
        {
            const string address = "test/delayed-sprite";
            IClientPresentationAssetLoader previous =
                ClientPresentationServices.Loader;
            if (previous != null)
                ClientPresentationServices.Unregister(previous);
            var delayedLoader = new FakePresentationLoader();
            ClientPresentationServices.Register(delayedLoader);
            try
            {
                Assert.That(ClientSpriteRegistry.Resolve(address), Is.Null);
                while (delayedLoader.SpriteRequestCount == 0)
                    yield return null;

                Sprite asset = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Art/Icon/Hero/Aatrox.png");
                var lease = new FakeLease<Sprite>(asset);
                ClientSpriteRegistry.Clear();
                delayedLoader.CompleteSpriteRequest(lease);
                yield return null;
                yield return null;

                Assert.That(lease.IsDisposed, Is.True);
                Assert.That(
                    ClientSpriteRegistry.Resolve(address),
                    Is.Null,
                    "A cleared generation must not write its completed lease back into the cache.");
            }
            finally
            {
                ClientSpriteRegistry.Clear();
                ClientPresentationServices.Unregister(delayedLoader);
                if (previous != null)
                    ClientPresentationServices.Register(previous);
            }
        }

        [UnityTest]
        public IEnumerator
            DesignApi_ShowShowOverlayHideAndFocus()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/UI/UIManager.prefab");
            GameObject instance =
                Object.Instantiate(prefab);
            try
            {
                yield return null;
                UIManager manager =
                    instance.GetComponent<
                        UIManager>();
                while (!manager.Ready.IsCompleted)
                    yield return null;
                Assert.That(manager.Ready.IsFaulted, Is.False);

                manager.ShowPage(UIPageId.Main);
                Assert.That(
                    manager.IsOpen(UIPageId.Main),
                    Is.True);

                manager.ShowPage(UIPageId.HUD);
                Assert.That(
                    manager.IsOpen(UIPageId.HUD),
                    Is.True);
                Assert.That(
                    manager.IsOpen(UIPageId.Main),
                    Is.False,
                    "Switching the main page must close the previous one.");

                int slot = -1;
                int equipmentId = -1;
                manager.ShopOwnedEquipmentFocused +=
                    (s, e) =>
                    {
                        slot = s;
                        equipmentId = e;
                    };
                manager.ShowOverlay(
                    UIPageId.Shop);
                Assert.That(
                    manager.IsOpen(UIPageId.Shop),
                    Is.True);

                manager.FocusShopOwnedEquipment(
                    2,
                    101);
                Assert.That(slot, Is.EqualTo(2));
                Assert.That(
                    equipmentId,
                    Is.EqualTo(101));

                manager.HideOverlay(
                    UIPageId.Shop);
                Assert.That(
                    manager.IsOpen(UIPageId.Shop),
                    Is.False);

                manager.CloseAll();
                Assert.That(
                    manager.IsOpen(UIPageId.HUD),
                    Is.False);
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator
            PrefabOwnsPageLifecycleAndControllers()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ClientContent/UI/UIManager.prefab");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance =
                Object.Instantiate(prefab);
            try
            {
                yield return null;
                UIManager manager =
                    instance.GetComponent<
                        UIManager>();
                while (!manager.Ready.IsCompleted)
                    yield return null;
                Assert.That(manager.Ready.IsFaulted, Is.False);
                Assert.That(manager, Is.Not.Null);
                Assert.That(
                    manager.IsInitialized,
                    Is.True);

                AssertLuaPage(
                    manager,
                    UIPageId.Main);
                AssertLuaPage(
                    manager,
                    UIPageId.Match);
                AssertLuaPage(
                    manager,
                    UIPageId.Select);
                AssertLuaPage(
                    manager,
                    UIPageId.Load);
                AssertPageRegistered(
                    manager,
                    UIPageId.HUD);
                AssertLuaPage(
                    manager,
                    UIPageId.Shop);
                AssertLuaPage(
                    manager,
                    UIPageId.Result);

                UIPanel hud =
                    manager.OpenPage(
                        UIPageId.HUD);
                Assert.That(
                    hud.gameObject.activeSelf,
                    Is.True);
                Assert.That(hud.IsOpen, Is.True);
                Assert.That(
                    manager.ClosePage(
                        UIPageId.HUD),
                    Is.True);
                Assert.That(
                    hud.gameObject.activeSelf,
                    Is.False);
                Assert.That(
                    hud.IsOpen,
                    Is.False);
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static void AssertLuaPage(
            UIManager manager,
            UIPageId pageId)
        {
            Assert.That(
                manager.TryGetPage(
                    pageId,
                    out UIPanel panel),
                Is.True,
                pageId.ToString());
            Assert.That(panel, Is.Not.Null);
            UIPage page =
                panel.GetComponent<UIPage>();
            Assert.That(page, Is.Not.Null);
            Assert.That(
                page.PageId,
                Is.EqualTo(pageId));
            Assert.That(
                panel.HasLuaHost,
                Is.True,
                pageId.ToString());
        }

        private static void AssertPageRegistered(
            UIManager manager,
            UIPageId pageId)
        {
            Assert.That(
                manager.TryGetPage(
                    pageId,
                    out UIPanel panel),
                Is.True,
                pageId.ToString());
            Assert.That(panel, Is.Not.Null);
            Assert.That(
                panel.GetComponent<UIPage>()
                    .PageId,
                Is.EqualTo(pageId));
        }
    }
}
