using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    /// <summary>
    /// Slice C acceptance: Lua page hosts boot in the client smoke scene,
    /// the flow bridge routes page transitions, and the Shop overlay reads
    /// the neutral equipment catalog.
    /// </summary>
    public sealed class UiLuaPagesSmokeTests
    {
        [UnityTest]
        public IEnumerator LuaPages_BootAndFlowRoutes()
        {
            AsyncOperation load =
                SceneManager.LoadSceneAsync(
                    "ClientFrameworkSmoke",
                    LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Scene scene =
                SceneManager.GetSceneByName(
                    "ClientFrameworkSmoke");
            UIManager manager = null;
            GameBootstrap bootstrap = null;
            GameObject[] roots =
                scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                manager ??=
                    roots[i].GetComponentInChildren<
                        UIManager>(true);
                bootstrap ??=
                    roots[i].GetComponentInChildren<
                        GameBootstrap>(true);
            }
            Assert.That(manager, Is.Not.Null);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(
                bootstrap.IsMatchReady,
                Is.True);
            InputSystemUIInputModule uiInputModule =
                manager.GetComponentInChildren<
                    InputSystemUIInputModule>(true);
            Assert.That(
                uiInputModule,
                Is.Not.Null,
                "UIManager must own an InputSystemUIInputModule.");
            Assert.That(
                uiInputModule.actionsAsset,
                Is.Not.Null,
                "UI input module must reference the player input actions.");
            Assert.That(
                uiInputModule.actionsAsset.name,
                Is.EqualTo("PlayerInputActions"),
                "UI input module must use the design-named actions asset.");
            Assert.That(
                uiInputModule.point,
                Is.Not.Null,
                "UI input module must bind the Point action.");
            Assert.That(
                uiInputModule.leftClick,
                Is.Not.Null,
                "UI input module must bind the LeftClick action.");

            UnityEngine.Debug.Log(
                "[UiLuaSmoke] sceneManager=" +
                (manager != null
                    ? manager.gameObject.scene.name
                    : "null") +
                " bridgeManager=" +
                (GameFlowLuaBridge.UiManager != null
                    ? GameFlowLuaBridge.UiManager
                        .gameObject.scene.name
                    : "null"));
            if (GameFlowLuaBridge.UiManager != null)
                manager = GameFlowLuaBridge.UiManager;
            Assert.That(
                manager.IsOpen(UIPageId.Main),
                Is.True,
                "Main page must open at boot.");
            Assert.That(
                manager.TryGetPage(
                    UIPageId.Main,
                    out UIPanel mainPanel),
                Is.True);
            Assert.That(
                mainPanel.HasLuaHost,
                Is.True,
                "Main page must own a Lua host.");

            Assert.That(
                manager.TryGetPage(
                    UIPageId.Select,
                    out UIPanel selectPanel),
                Is.True);
            Transform heroContent =
                selectPanel.transform.Find(
                    "HeroList/Viewport/Content");
            Assert.That(heroContent, Is.Not.Null);
            int heroCellCount = 0;
            for (int i = 0;
                 i < heroContent.childCount;
                 i++)
            {
                if (heroContent.GetChild(i).name
                    .StartsWith(
                        "HeroSelectCell_"))
                    heroCellCount++;
            }
            int expectedHeroCount =
                GameFlowLuaBridge.HeroSelectCount();
            Assert.That(
                expectedHeroCount,
                Is.GreaterThanOrEqualTo(1),
                "The hero display table must expose at least the test hero.");
            Assert.That(
                heroCellCount,
                Is.EqualTo(expectedHeroCount),
                "Select hero list must be preloaded at game entry.");

            // Clicking a hero cell must select it: SelectTip shows and the
            // Confirm button becomes pressable.
            Transform firstHeroCell =
                heroContent.GetChild(0);
            UnityEngine.UI.Button heroCellButton =
                firstHeroCell.GetComponentInChildren<
                    UnityEngine.UI.Button>(true);
            Assert.That(
                heroCellButton,
                Is.Not.Null,
                "Hero cell must own a Button on its icon.");
            heroCellButton.onClick.Invoke();
            yield return null;
            Transform heroSelectTip =
                firstHeroCell.Find("SelectTip");
            Assert.That(
                heroSelectTip,
                Is.Not.Null);
            Assert.That(
                heroSelectTip.gameObject.activeSelf,
                Is.True,
                "SelectTip must show on the selected hero cell.");
            UnityEngine.UI.Button confirmButton =
                selectPanel.transform.Find("ConfirmButton")
                    .GetComponent<UnityEngine.UI.Button>();
            Assert.That(
                confirmButton.interactable,
                Is.True,
                "ConfirmButton must be enabled after selecting a hero.");

            // Confirm is one-way: after confirming, further hero cells and the
            // Confirm button become inert.
            confirmButton.onClick.Invoke();
            yield return null;
            Assert.That(
                confirmButton.interactable,
                Is.False,
                "ConfirmButton must disable after confirming.");
            UnityEngine.UI.Button cellButtonAfter =
                firstHeroCell.GetComponentInChildren<
                    UnityEngine.UI.Button>(true);
            Assert.That(
                cellButtonAfter.interactable,
                Is.False,
                "Hero cells must disable after confirming.");

            GameFlowLuaBridge.UiManager.ShowPage(
                UIPageId.Match);
            Assert.That(
                GameFlowLuaBridge.UiManager.IsOpen(
                    UIPageId.Match),
                Is.True,
                "ShowPage(Match) must open the Match page.");

            GameFlowLuaBridge.UiManager.ShowPage(
                UIPageId.Main);
            GameFlowLuaBridge.StartMatchmaking();
            Assert.That(
                GameFlowLuaBridge.UiManager.IsOpen(
                    UIPageId.Match),
                Is.True,
                "StartMatchmaking bridge must switch to the Match page.");

            GameFlowLuaBridge.UiManager.ShowPage(
                UIPageId.HUD);
            Assert.That(
                manager.TryGetPage(
                    UIPageId.HUD,
                    out UIPanel hudPanel),
                Is.True);
            Assert.That(
                hudPanel.HasLuaHost,
                Is.True,
                "HUD must own a Lua host.");
            hudPanel.RefreshLuaHost();
            Assert.That(
                hudPanel.transform.Find(
                    "MatchPart"),
                Is.Not.Null,
                "HUD must own the MatchPart scoreboard.");
            Assert.That(
                hudPanel.transform.Find(
                    "StatusBar/Health/Text"),
                Is.Not.Null,
                "HUD must own the HealthText node.");
            Assert.That(
                hudPanel.transform.Find(
                    "AbilityBar/AbilitySlotQ/Icon"),
                Is.Not.Null,
                "HUD must own the active ability icon nodes.");
            Transform extendRoot =
                hudPanel.transform.Find(
                    "PropertyBar/ExtendProperty");
            Assert.That(
                extendRoot,
                Is.Not.Null,
                "HUD must own the ExtendProperty root.");
            Assert.That(
                extendRoot.gameObject.activeSelf,
                Is.False,
                "ExtendProperty must be hidden unless C is held.");
            Transform buffBar =
                hudPanel.transform.Find("BuffBar");
            Assert.That(
                buffBar,
                Is.Not.Null,
                "HUD must own the BuffBar.");
            Assert.That(
                buffBar.GetComponent<
                    FrameSyncMoba.LuaBridge.UIList>(),
                Is.Not.Null,
                "BuffBar must own a UIList for buff cells.");
            GameFlowLuaBridge.UiManager.ShowOverlay(
                UIPageId.Shop);
            Assert.That(
                GameFlowLuaBridge.UiManager.IsOpen(
                    UIPageId.Shop),
                Is.True,
                "Shop overlay must open above HUD.");
            Assert.That(
                GameFlowLuaBridge.GetShopItemCount(),
                Is.EqualTo(3),
                "Neutral equipment catalog must expose 3 items.");
            Assert.That(
                GameFlowLuaBridge.GetShopItemName(0),
                Is.Not.Null);

            Assert.That(
                manager.TryGetPage(
                    UIPageId.Shop,
                    out UIPanel shopPanel),
                Is.True);
            Transform content =
                shopPanel.transform.Find(
                    "ShopRoot/EquipmentList/Viewport/Content");
            Assert.That(content, Is.Not.Null);
            int cellCount = 0;
            for (int i = 0;
                 i < content.childCount;
                 i++)
            {
                if (content.GetChild(i).name
                    .StartsWith(
                        "EquipmentShopCell_"))
                    cellCount++;
            }
            Assert.That(
                cellCount,
                Is.EqualTo(3),
                "Catalog cells must be instantiated inside the EquipmentList Content.");
            Assert.That(
                shopPanel.transform.Find(
                    "ShopRoot/Detail/EquipmentName"),
                Is.Not.Null,
                "Shop Detail must be wired.");

            Transform detail =
                shopPanel.transform.Find(
                    "ShopRoot/Detail");
            Assert.That(detail, Is.Not.Null);
            Assert.That(
                detail.gameObject.activeSelf,
                Is.False,
                "Detail must be hidden before any cell is selected.");

            Transform firstCell =
                content.GetChild(0);
            UnityEngine.UI.Button cellButton =
                firstCell.GetComponentInChildren<
                    UnityEngine.UI.Button>(true);
            Assert.That(cellButton, Is.Not.Null);
            cellButton.onClick.Invoke();
            yield return null;
            Assert.That(
                detail.gameObject.activeSelf,
                Is.True,
                "Selecting a cell must show the Detail page.");
            TMPro.TextMeshProUGUI detailName =
                detail.Find("EquipmentName")
                    .GetComponent<TMPro.TextMeshProUGUI>();
            Assert.That(
                detailName.text,
                Is.Not.Empty,
                "Detail equipment name must be updated.");

            GameFlowLuaBridge.UiManager.HideOverlay(
                UIPageId.Shop);
            Assert.That(
                GameFlowLuaBridge.UiManager.IsOpen(
                    UIPageId.Shop),
                Is.False);

            GameFlowLuaBridge.UiManager.ShowPage(
                UIPageId.Result);
            Assert.That(
                GameFlowLuaBridge.UiManager.IsOpen(
                    UIPageId.Result),
                Is.True);
            Assert.That(
                GameFlowLuaBridge.UiManager.TryGetPage(
                    UIPageId.Result,
                    out UIPanel resultPanel),
                Is.True);
            Transform victoryIcon =
                resultPanel.transform.Find(
                    "VictoryIcon");
            Transform defeatIcon =
                resultPanel.transform.Find(
                    "DefeatIcon");
            Assert.That(victoryIcon, Is.Not.Null);
            Assert.That(defeatIcon, Is.Not.Null);
            bool draw = GameFlowLuaBridge.LastMatchDraw();
            bool localVictory =
                GameFlowLuaBridge.IsLocalTeamVictory();
            Assert.That(
                victoryIcon.gameObject.activeSelf,
                Is.EqualTo(localVictory));
            Assert.That(
                defeatIcon.gameObject.activeSelf,
                Is.EqualTo(
                    !localVictory && !draw));

            AsyncOperation unload =
                SceneManager.UnloadSceneAsync(scene);
            if (unload != null)
                yield return unload;
        }
    }
}
