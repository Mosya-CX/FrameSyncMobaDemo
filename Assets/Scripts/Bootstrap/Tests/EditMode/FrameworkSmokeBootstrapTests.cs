using System.Reflection;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class FrameworkSmokeBootstrapTests
    {
        [Test]
        public void Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks()
        {
            GlobalGameplayData global = AssetDatabase.LoadAssetAtPath<GlobalGameplayData>(
                "Assets/Config/Runtime/GlobalGameplayData.asset");
            UnitRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    "Assets/Fixtures/Framework/Config/NeutralUnitRuntimeCatalog.asset");
            AbilityRuntimeCatalogAsset abilityCatalog =
                AssetDatabase.LoadAssetAtPath<AbilityRuntimeCatalogAsset>(
                    "Assets/Fixtures/Framework/Config/NeutralAbilityRuntimeCatalog.asset");
            Assert.That(global, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(abilityCatalog, Is.Not.Null);

            var root = new GameObject("FrameworkSmokeBootstrapTest");
            try
            {
                GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
                SetField(bootstrap, "globalGameplayData", global);
                SetField(bootstrap, "unitRuntimeCatalog", catalog);
                SetField(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    abilityCatalog);
                SetField(bootstrap, "dedicatedServer", true);
                SetField(bootstrap, "driveSimulationFromUnityUpdate", false);
                SetField(bootstrap, "initialUnitSpawns", new System.Collections.Generic.List<
                    InitialUnitSpawnAuthoring>
                {
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 0,
                        UnitPrototypeId = 1001,
                        TeamId = 1,
                        Position = Vector2.zero,
                        Forward = Vector2.up,
                    },
                });

                InvokeAwake(bootstrap);
                int executed = bootstrap.AdvanceSimulationByElapsedSeconds(1d);

                Assert.That(executed, Is.EqualTo(bootstrap.MaxLogicTicksPerUnityFrame));
                Assert.That(bootstrap.Runtime.CurrentTick, Is.EqualTo(executed));
                Assert.That(bootstrap.UnitWorld.GetAllUnits().Count, Is.EqualTo(1));
                Assert.That(
                    bootstrap.UnitWorld.GetAllUnits()[0].UnitPrototypeId,
                    Is.EqualTo(1001));
            }
            finally
            {
                UnitType[] units = UnityEngine.Object.FindObjectsOfType<UnitType>();
                for (int i = 0; i < units.Length; i++)
                    UnityEngine.Object.DestroyImmediate(units[i].gameObject);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetField<T>(GameBootstrap target, string name, T value)
        {
            FieldInfo field = typeof(GameBootstrap).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {name}.");
            field.SetValue(target, value);
        }

        private static void InvokeAwake(GameBootstrap bootstrap)
        {
            MethodInfo awake = typeof(GameBootstrap).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            try
            {
                awake.Invoke(bootstrap, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }
    }
}
