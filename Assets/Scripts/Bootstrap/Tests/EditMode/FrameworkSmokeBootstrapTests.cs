using System.Reflection;
using FrameSyncMoba.FrameSync;
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
            DeterministicMapConfig mapConfig =
                AssetDatabase.LoadAssetAtPath<DeterministicMapConfig>(
                    "Assets/Fixtures/Framework/Config/NeutralDeterministicMapConfig.asset");
            Assert.That(global, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(abilityCatalog, Is.Not.Null);
            Assert.That(mapConfig, Is.Not.Null);

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
                SetField(
                    bootstrap,
                    "deterministicMapConfig",
                    mapConfig);
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
                        UseMapSpawnPoint = true,
                        SpawnPointId = 0,
                        PlayerControlled = true,
                        PlayerSlot = 0,
                    },
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 1,
                        UnitPrototypeId = 1001,
                        TeamId = 2,
                        UseMapSpawnPoint = true,
                        SpawnPointId = 1,
                        PlayerControlled = true,
                        PlayerSlot = 1,
                    },
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 2,
                        UnitPrototypeId = 1002,
                        TeamId = 1,
                        UseMapSpawnPoint = true,
                        SpawnPointId = 10,
                        MatchTopologyRole =
                            FrameSyncMoba.FrameSync.MatchTopologyRole.BlueBase,
                    },
                    new InitialUnitSpawnAuthoring
                    {
                        StableSpawnOrder = 3,
                        UnitPrototypeId = 1002,
                        TeamId = 2,
                        UseMapSpawnPoint = true,
                        SpawnPointId = 11,
                        MatchTopologyRole =
                            FrameSyncMoba.FrameSync.MatchTopologyRole.RedBase,
                    },
                });

                InvokeAwake(bootstrap);
                int executed = bootstrap.AdvanceSimulationByElapsedSeconds(1d);

                Assert.That(executed, Is.EqualTo(bootstrap.MaxLogicTicksPerUnityFrame));
                Assert.That(bootstrap.IsMatchReady, Is.True);
                Assert.That(bootstrap.Runtime.CurrentTick, Is.EqualTo(executed));
                Assert.That(bootstrap.UnitWorld.GetAllUnits().Count, Is.EqualTo(4));
                Assert.That(
                    bootstrap.UnitWorld.GetAllUnits()[0].UnitPrototypeId,
                    Is.EqualTo(1001));
                Assert.That(
                    bootstrap.UnitWorld.GetAllUnits()[0]
                        .ControlledByPlayerSlot,
                    Is.EqualTo(0));
                Assert.That(
                    bootstrap.Runtime.GoldIncome
                        .ConfirmedIncomeThroughTick,
                    Is.EqualTo(-1));
                Assert.That(
                    bootstrap.Runtime.MatchRule
                        .BlueBaseUnitUid.IsValid(),
                    Is.True);
                Assert.That(
                    bootstrap.Runtime.MatchRule
                        .RedBaseUnitUid.IsValid(),
                    Is.True);

                GameplaySnapshot wireSnapshot =
                    bootstrap.Runtime.TickPipeline
                        .CaptureAggregateSnapshot();
                Assert.That(
                    bootstrap.Runtime.TryGetControlledUnit(
                        0,
                        out UnitType playerZero),
                    Is.True);
                Assert.That(
                    bootstrap.Runtime.TryGetControlledUnit(
                        1,
                        out UnitType playerOne),
                    Is.True);
                var wireConfig =
                    new GameStartConfig(
                        "framework-full-wire",
                        1,
                        1,
                        2,
                        2,
                        new[]
                        {
                            new PlayerSlotConfig(
                                0,
                                "FixturePlayer0",
                                1,
                                new TeamId(1),
                                1001,
                                0),
                            new PlayerSlotConfig(
                                1,
                                "FixturePlayer1",
                                2,
                                new TeamId(2),
                                1001,
                                1),
                        },
                        bootstrap.Runtime
                            .CurrentTick,
                        wireSnapshot.RandomState
                            .State,
                        bootstrap.LocalVersions
                            .GameplayDataVersion);
                var wirePayload =
                    new GameBootstrapPayload(
                        wireConfig,
                        bootstrap.LocalVersions,
                        wireSnapshot,
                        bootstrap.Runtime
                            .CurrentTick,
                        bootstrap.Runtime
                            .CurrentTick,
                        wireSnapshot.RandomState
                            .State,
                        new[]
                        {
                            new PlayerSlotUnitMapping(
                                0,
                                playerZero.UnitUid),
                            new PlayerSlotUnitMapping(
                                1,
                                playerOne.UnitUid),
                        });
                byte[] firstWire =
                    BootstrapPayloadWireCodec.Write(
                        wirePayload);
                byte[] secondWire =
                    BootstrapPayloadWireCodec.Write(
                        BootstrapPayloadWireCodec
                            .Read(firstWire));
                Assert.That(
                    secondWire,
                    Is.EqualTo(firstWire));

                for (int i = 0; i < 30; i++)
                    bootstrap.AdvanceSimulationByElapsedSeconds(1d);
                Assert.That(
                    bootstrap.Runtime.MatchRule.CurrentPhase,
                    Is.EqualTo(
                        FrameSyncMoba.FrameSync.MatchPhase.Running));
                Assert.That(
                    bootstrap.UnitWorld.TryGetUnit(
                        bootstrap.Runtime.MatchRule
                            .RedBaseUnitUid,
                        out UnitType redBase),
                    Is.True);
                bootstrap.UnitWorld.RequestEnterDying(
                    redBase);
                bootstrap.UnitWorld.ConfirmUnitDeath(
                    redBase);
                bootstrap.AdvanceSimulationByElapsedSeconds(
                    1d);
                Assert.That(
                    bootstrap.Runtime.MatchRule.CurrentPhase,
                    Is.EqualTo(
                        FrameSyncMoba.FrameSync.MatchPhase.Ending));
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
