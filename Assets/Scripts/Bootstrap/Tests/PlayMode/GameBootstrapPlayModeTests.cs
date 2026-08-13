using System.Collections;
using System.Reflection;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class GameBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator ClientComposition_InitializesFromProjectAssets()
        {
            var root = new GameObject("TestClientBootstrap");
            root.SetActive(false);
            try
            {
                var camera = root.AddComponent<Camera>();
                var input = root.AddComponent<PlayerInputController>();
                var bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(
                    bootstrap,
                    "globalGameplayData",
                    LoadAsset<GlobalGameplayData>(
                        "8b0cdcd39dbb2964baebdd8475f1e60e"));
                SetReference(
                    bootstrap,
                    "unitRuntimeCatalog",
                    LoadAsset<UnitRuntimeCatalogAsset>(
                        "cf6a213803fa81b4cb7ac2699f40045b"));
                SetReference(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    LoadAsset<AbilityRuntimeCatalogAsset>(
                        "e09025f013ae7a8449335c6356fee5fb"));
                SetReference(
                    bootstrap,
                    "projectileRuntimeCatalog",
                    LoadAsset<ProjectileRuntimeCatalogAsset>(
                        "e548718fd0a6b7d4b87db7539574720f"));
                SetReference(
                    bootstrap,
                    "equipmentCatalog",
                    LoadAsset<EquipmentCatalogAsset>(
                        "eb9d7cfdf62385847aa5e2480b266dae"));
                SetReference(bootstrap, "playerInputController", input);
                SetReference(bootstrap, "gameplayCamera", camera);
                SetReference(
                    input,
                    "inputActions",
                    UnityEditor.AssetDatabase.LoadAssetAtPath<
                        InputActionAsset>(
                        "Assets/Input/PlayerInputActions.inputactions"));
                root.SetActive(true);
                yield return null;

                Assert.IsTrue(bootstrap.IsInitialized);
                Assert.NotNull(bootstrap.Runtime);
                Assert.NotNull(bootstrap.UnitWorld);
                Assert.NotNull(bootstrap.PhysicsWorld);
                Assert.That(
                    bootstrap.UnitWorld.EquipmentDatabase.Count,
                    Is.EqualTo(11),
                    "The formal composition root must bake the global equipment catalog into UnitWorld for the shop runtime.");

                var tickController =
                    new SimulationTickContextController();
                UnitUid controlledUid;
                tickController.BeginTick(
                    bootstrap.Runtime.CurrentTick,
                    ExecutionMode.ServerAuthority);
                try
                {
                    controlledUid = bootstrap.UnitWorld.SpawnUnit(
                        new UnitSpawnRequest(
                            1001,
                            new TeamId(1),
                            fp2.zero,
                            new fp2(fp.one, fp.zero)));
                }
                finally
                {
                    tickController.EndTick();
                }
                Assert.That(
                    bootstrap.UnitWorld.TryGetUnit(
                        controlledUid,
                        out UnitType controlledUnit),
                    Is.True,
                    "Formal hero spawn must register the controlled unit.");

                bootstrap.BindLocalPlayer(
                    controlledUnit,
                    playerSlot: 0,
                    clientId: 1);

                EquipmentDefinition firstEquipment =
                    bootstrap.UnitWorld.EquipmentDatabase
                        .AllDefinitions[0];
                EquipmentShopRequestCheck purchaseCheck = default;
                Assert.DoesNotThrow(
                    () => purchaseCheck =
                        bootstrap.Runtime.EquipmentShop
                            .RequestPurchase(
                                playerSlot: 0,
                                firstEquipment.Id),
                    "Binding the formal local player must inject the canonical shop command submitter.");
                Assert.IsTrue(purchaseCheck.Allowed);
                Assert.That(
                    bootstrap.Runtime.CommandCollector.CommandCount,
                    Is.EqualTo(1));
                GameplayCommand submittedCommand =
                    bootstrap.Runtime.CommandCollector
                        .GetCanonicalCommands()[0];
                Assert.That(
                    submittedCommand.Kind,
                    Is.EqualTo(GameplayCommandKind.EquipmentShop));
                Assert.That(
                    submittedCommand.ShopOperationType,
                    Is.EqualTo(
                        EquipmentShopCommandOperationType.Purchase));
                Assert.That(
                    submittedCommand.EquipmentId,
                    Is.EqualTo(firstEquipment.Id));
            }
            finally
            {
                Object.Destroy(root);
            }
        }

        private static T LoadAsset<T>(
            string guid)
            where T : Object
        {
            string path =
                UnityEditor.AssetDatabase.GUIDToAssetPath(
                    guid);
            T asset =
                UnityEditor.AssetDatabase
                    .LoadAssetAtPath<T>(path);
            Assert.NotNull(
                asset,
                $"Project asset {guid} must exist.");
            return asset;
        }

        private static void SetReference(
            Object target,
            string field,
            Object value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo, $"Missing serialized field {field}.");
            fieldInfo.SetValue(target, value);
        }
    }
}
