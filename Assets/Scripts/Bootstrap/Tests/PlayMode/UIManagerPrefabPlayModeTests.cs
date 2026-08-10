using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class
        UIManagerPrefabPlayModeTests
    {
        [UnityTest]
        public IEnumerator
            DesignApi_ShowShowOverlayHideAndFocus()
        {
            GameObject prefab =
                Resources.Load<GameObject>(
                    "Prefab/UI/UIManager");
            GameObject instance =
                Object.Instantiate(prefab);
            try
            {
                yield return null;
                UIManager manager =
                    instance.GetComponent<
                        UIManager>();

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
            GameObject prefab =
                Resources.Load<GameObject>(
                    "Prefab/UI/UIManager");
            Assert.That(prefab, Is.Not.Null);
            GameObject instance =
                Object.Instantiate(prefab);
            try
            {
                yield return null;
                UIManager manager =
                    instance.GetComponent<
                        UIManager>();
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
