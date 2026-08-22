using System.Collections;
using System.Reflection;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class HeroTestSceneEquipmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildWorld_LoadsFormalEquipmentCatalogForShop()
        {
            var root = new GameObject("HeroTestEquipmentProbe");
            root.SetActive(false);
            try
            {
                HeroTestDriver driver =
                    root.AddComponent<HeroTestDriver>();
                MethodInfo buildWorld = typeof(HeroTestDriver)
                    .GetMethod(
                        "BuildWorld",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(buildWorld, Is.Not.Null);
                buildWorld.Invoke(driver, null);

                FieldInfo databaseField = typeof(HeroTestDriver)
                    .GetField(
                        "equipmentDatabase",
                        BindingFlags.Instance |
                        BindingFlags.NonPublic);
                Assert.That(databaseField, Is.Not.Null);
                var database = databaseField.GetValue(driver)
                    as EquipmentDatabase;

                Assert.That(database, Is.Not.Null);
                Assert.That(database.Count, Is.EqualTo(11));
                Assert.That(
                    database.TryGetDefinition(
                        31005,
                        out EquipmentDefinition rageblade),
                    Is.True);
                Assert.That(
                    rageblade.Name,
                    Is.EqualTo("Guinsoo's Rageblade"));
                for (int i = 0;
                     i < database.AllDefinitions.Count;
                     i++)
                {
                    EquipmentDefinition definition =
                        database.AllDefinitions[i];
                    Assert.That(
                        definition.Icon,
                        Is.Not.Null,
                        definition.Name + " must have an icon.");
                    Assert.That(
                        ContainsCjk(definition.Name),
                        Is.False,
                        definition.name + " Name must be English.");
                    Assert.That(
                        ContainsCjk(definition.Description),
                        Is.False,
                        definition.name + " Description must be English.");
                }
            }
            finally
            {
                Object.Destroy(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalTickShop_UsesFormalGoldPurchaseRecipeAndUndo()
        {
            var root = new GameObject("HeroTestShopFlowProbe");
            root.SetActive(false);
            HeroTestDriver driver = null;
            try
            {
                driver = root.AddComponent<HeroTestDriver>();
                Invoke(driver, "BuildWorld");
                Invoke(driver, "SpawnHero");
                Invoke(driver, "ConfigureTestShop");
                Invoke(driver, "BindTestHudBridge");

                PlayerCommandRequester requester =
                    GetField<PlayerCommandRequester>(
                        driver,
                        "playerCommandRequester");
                Assert.That(requester, Is.Not.Null);
                Assert.That(
                    requester.ControlledUnit,
                    Is.SameAs(driver.Hero),
                    "HeroTest commands must share the formal player requester.");
                Assert.That(requester.NextCommandSeq, Is.EqualTo(1));

                Assert.That(
                    driver.Hero.UnitPrototypeId,
                    Is.EqualTo(1002),
                    "HeroTest must spawn the formal Aatrox prototype.");
                int[] expectedAbilityIds =
                    { 10021, 10022, 10023, 10024 };
                for (byte slot = 0; slot < expectedAbilityIds.Length; slot++)
                {
                    Assert.That(
                        driver.Hero.AbilityHandler
                            .GetAbilityDef(slot)?.AbilityId,
                        Is.EqualTo(expectedAbilityIds[slot]),
                        $"HeroTest slot {slot} must use Aatrox's formal loadout.");
                }

                EquipmentShopRuntime shop =
                    GetField<EquipmentShopRuntime>(
                        driver,
                        "equipmentShop");
                Assert.That(shop, Is.Not.Null);
                Assert.That(
                    shop.GetCurrentAvailableGold(0),
                    Is.EqualTo(10000));
                Assert.That(
                    GameFlowLuaBridge.GetHudGold(),
                    Is.EqualTo(10000),
                    "HeroTest HUD must display the formal initial gold baseline.");
                Assert.That(
                    GameFlowLuaBridge.GetCurrentGold(),
                    Is.EqualTo(10000));

                PassiveAbilityRuntime passive =
                    driver.Hero.AbilityHandler.FixedPassive;
                Assert.That(passive, Is.Not.Null);
                AbilityPassiveRuntimeState passiveState =
                    passive.EffectRuntime.State;
                passiveState.NextReadyLogicTick =
                    driver.CurrentTick + 30;
                passive.EffectRuntime.State = passiveState;
                Assert.That(
                    GameFlowLuaBridge
                        .GetPassiveCooldownRemainingSeconds(),
                    Is.EqualTo(1f).Within(.001f),
                    "HeroTest must project fixed-passive cooldown state to HUD.");
                Assert.That(
                    GameFlowLuaBridge
                        .GetPassiveCooldownTotalSeconds(),
                    Is.GreaterThan(0f));

                EquipmentShopRequestCheck purchase =
                    shop.RequestPurchase(0, 31001);
                Assert.That(
                    purchase.Allowed,
                    Is.True,
                    purchase.FailureReason.ToString());
                Assert.That(
                    requester.NextCommandSeq,
                    Is.EqualTo(2),
                    "Shop and QWER must advance one shared local command sequence.");
                ExecuteSubmittedCommand(driver);

                Assert.That(
                    driver.Hero.EquipmentHandler
                        .GetSlotDef(0)?.Id,
                    Is.EqualTo(31001));
                Assert.That(
                    shop.GetCurrentAvailableGold(0),
                    Is.EqualTo(9750));
                Assert.That(
                    GameFlowLuaBridge.GetHudGold(),
                    Is.EqualTo(9750),
                    "HUD gold must read the same formal shop balance after purchase.");
                Assert.That(
                    shop.CalculatePurchasePrice(0, 31004),
                    Is.EqualTo(450),
                    "Owning Dagger must visibly discount Recurve Bow.");
                Assert.That(
                    shop.CanUndo(
                        0,
                        shop.GetCurrentAvailableGold(0),
                        out EquipmentShopFailureReason failure),
                    Is.True,
                    failure.ToString());

                purchase = shop.RequestPurchase(0, 31004);
                Assert.That(
                    purchase.Allowed,
                    Is.True,
                    purchase.FailureReason.ToString());
                ExecuteSubmittedCommand(driver);

                Assert.That(
                    driver.Hero.EquipmentHandler
                        .HasDefinition(
                            GetField<EquipmentDatabase>(
                                    driver,
                                    "equipmentDatabase")
                                .GetDefinition(31001)),
                    Is.False,
                    "The consumed Dagger must no longer be marked owned.");
                Assert.That(
                    driver.Hero.EquipmentHandler
                        .GetSlotDef(0)?.Id,
                    Is.EqualTo(31004));
                Assert.That(
                    shop.GetCurrentAvailableGold(0),
                    Is.EqualTo(9300));

                EquipmentShopRequestCheck undo =
                    shop.RequestUndo(0);
                Assert.That(
                    undo.Allowed,
                    Is.True,
                    undo.FailureReason.ToString());
                ExecuteSubmittedCommand(driver);
                Assert.That(
                    driver.Hero.EquipmentHandler
                        .GetSlotDef(0)?.Id,
                    Is.EqualTo(31001));
                Assert.That(
                    shop.GetCurrentAvailableGold(0),
                    Is.EqualTo(9750));
            }
            finally
            {
                if (driver?.Hero != null)
                    Object.Destroy(driver.Hero.gameObject);
                Object.Destroy(root);
            }

            yield return null;
        }

        private static void ExecuteSubmittedCommand(
            HeroTestDriver driver)
        {
            Assert.That(
                driver.DebugExecuteOneTick(),
                Is.Empty);
            Assert.That(
                driver.DebugExecuteOneTick(),
                Is.Empty);
        }

        private static void Invoke(
            HeroTestDriver driver,
            string methodName)
        {
            MethodInfo method = typeof(HeroTestDriver)
                .GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(driver, null);
        }

        private static T GetField<T>(
            HeroTestDriver driver,
            string fieldName)
            where T : class
        {
            FieldInfo field = typeof(HeroTestDriver)
                .GetField(
                    fieldName,
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(driver) as T;
        }

        private static bool ContainsCjk(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] >= '\u4e00' &&
                    value[i] <= '\u9fff')
                    return true;
            }
            return false;
        }
    }
}
