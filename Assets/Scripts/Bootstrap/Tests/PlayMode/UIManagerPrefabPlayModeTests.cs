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

                AssertPage<
                    LobbyPanelController>(
                    manager,
                    UIPageId.Lobby);
                AssertPage<
                    HeroSelectPageController>(
                    manager,
                    UIPageId.HeroSelect);
                AssertPage<
                    AbilityCooldownPresenter>(
                    manager,
                    UIPageId.GameplayHud);
                AssertPage<
                    ShopPageController>(
                    manager,
                    UIPageId.Shop);
                AssertPage<
                    ResultPageController>(
                    manager,
                    UIPageId.Result);

                UIPanel hud =
                    manager.OpenPage(
                        UIPageId.GameplayHud);
                Assert.That(
                    hud.gameObject.activeSelf,
                    Is.True);
                Assert.That(hud.IsOpen, Is.True);
                Assert.That(
                    manager.ClosePage(
                        UIPageId.GameplayHud),
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

        private static void AssertPage<T>(
            UIManager manager,
            UIPageId pageId)
            where T : Component
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
                panel.GetComponentInChildren<T>(
                    true),
                Is.Not.Null,
                pageId.ToString());
        }
    }
}
