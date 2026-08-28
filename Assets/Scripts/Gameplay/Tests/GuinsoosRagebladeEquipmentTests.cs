using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class GuinsoosRagebladeEquipmentTests
    {
        private SimulationTickContextController controller;

        [SetUp]
        public void SetUp()
        {
            CombatEvents.Clear();
            controller = new SimulationTickContextController();
        }

        [TearDown]
        public void TearDown()
        {
            if (controller.IsTickActive)
                controller.EndTick();
            CombatEvents.Clear();
        }

        [Test]
        public void FormalCatalog_ContainsExpectedStatsRecipeAndModules()
        {
            EquipmentCatalogAsset catalog = LoadCatalog();
            EquipmentDatabase database = catalog.BakeOrThrow();

            Assert.That(database.Count, Is.EqualTo(11));
            Assert.That(database.TryGetDefinition(31004, out EquipmentDefinition recurve), Is.True);
            Assert.That(database.TryGetDefinition(31005, out EquipmentDefinition rageblade), Is.True);
            Assert.That(recurve.Value, Is.EqualTo(700));
            Assert.That(recurve.Recipe.Components.Length, Is.EqualTo(1));
            Assert.That(recurve.Recipe.Components[0].Item.Id, Is.EqualTo(31001));
            Assert.That(rageblade.Value, Is.EqualTo(3000));
            Assert.That(rageblade.Recipe.Components.Length, Is.EqualTo(3));
            Assert.That(rageblade.Recipe.Components[0].Item.Id, Is.EqualTo(31002));
            Assert.That(rageblade.Recipe.Components[1].Item.Id, Is.EqualTo(31004));
            Assert.That(rageblade.Recipe.Components[2].Item.Id, Is.EqualTo(31003));
            Assert.That(rageblade.BakedFixedStats.Length, Is.EqualTo(3));
            Assert.That(rageblade.Effects.Length, Is.EqualTo(2));
            Assert.That(rageblade.Effects[0].Modules[0], Is.TypeOf<OnHitBonusDamageModule>());
            Assert.That(rageblade.Effects[1].Modules[0], Is.TypeOf<BuffEquipmentModule>());
            Assert.That(rageblade.Effects[1].Modules[1], Is.TypeOf<OnHitRepeatModule>());

            BuffDefinition buff = LoadSeethingBuff();
            Assert.That(buff.DurationTicks, Is.EqualTo(90));
            Assert.That(buff.MaxStacks, Is.EqualTo(4));
            Assert.That(buff.GetEffects()[0], Is.TypeOf<StatModifierBuffEffect>());
        }

        [Test]
        public void GameScene_UsesCorePartitionInsteadOfDirectEquipmentCatalog()
        {
            EquipmentCatalogAsset expected = LoadCatalog();
            GlobalPrefabSubTableAsset core =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                    "Assets/Config/Formal/MatchContent/" +
                    "CoreGlobalPrefabSubTable.asset");
            Assert.That(core, Is.Not.Null);
            MatchContentAssetAddress equipment = null;
            for (int i = 0; i < core.ContentAssets.Count; i++)
            {
                if (core.ContentAssets[i].AssetKind !=
                    MatchContentAssetKind.EquipmentCatalog)
                    continue;
                equipment = core.ContentAssets[i];
                break;
            }
            Assert.That(equipment, Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<EquipmentCatalogAsset>(
                    equipment.Address),
                Is.SameAs(expected));
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/GameScene.unity",
                OpenSceneMode.Additive);
            try
            {
                UnityEngine.MonoBehaviour bootstrap = null;
                UnityEngine.GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length && bootstrap == null; i++)
                {
                    UnityEngine.MonoBehaviour[] components =
                        roots[i].GetComponentsInChildren<UnityEngine.MonoBehaviour>(true);
                    for (int componentIndex = 0;
                         componentIndex < components.Length;
                         componentIndex++)
                    {
                        var candidate = new SerializedObject(components[componentIndex]);
                        if (candidate.FindProperty("equipmentCatalog") != null)
                        {
                            bootstrap = components[componentIndex];
                            break;
                        }
                    }
                }
                Assert.That(bootstrap, Is.Not.Null);
                var serialized = new SerializedObject(bootstrap);
                Assert.That(
                    serialized.FindProperty("equipmentCatalog")
                        .objectReferenceValue,
                    Is.Null,
                    "GameScene must obtain equipment through the Core Addressable partition.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SixRealHits_StackBuffAndRepeatThirdFullStackOnHitOnce()
        {
            ScenarioResult first = RunSixHitScenario(100);
            ScenarioResult second = RunSixHitScenario(200);

            CollectionAssert.AreEqual(
                new[] { (fp)31, (fp)31, (fp)31, (fp)31, (fp)31, (fp)61 },
                first.DamageByHit);
            CollectionAssert.AreEqual(first.DamageByHit, second.DamageByHit);
            Assert.That(first.StacksAfterSixthHit, Is.EqualTo(4));
            Assert.That(first.CounterAfterFifthHit, Is.EqualTo(2));
            Assert.That(first.CounterAfterSixthHit, Is.EqualTo(0));
            Assert.That(second.StacksAfterSixthHit, Is.EqualTo(4));
            Assert.That(second.CounterAfterSixthHit, Is.EqualTo(0));
        }

        [Test]
        public void OnHitEquipmentEffects_ApplyToStructure()
        {
            ScenarioContext context = CreateScenario(
                250,
                UnitKind.Structure);

            fp damage = DealAttack(context, 1);

            Assert.That(damage, Is.EqualTo((fp)31));
        }

        [Test]
        public void TriggerCounter_RestoreReplaysSameRepeatedOnHit()
        {
            ScenarioContext context = CreateScenario(300);
            for (int hit = 1; hit <= 5; hit++)
                DealAttack(context, hit);

            var snapshot = EquipmentHandlerSnapshot.Empty;
            context.Attacker.EquipmentHandler.Capture(ref snapshot);
            Assert.That(ReadRepeatCounter(snapshot), Is.EqualTo(2));
            context.Attacker.EquipmentHandler.Capture(ref snapshot);
            Assert.That(snapshot.Slots.Count, Is.EqualTo(EquipmentHandler.SlotCount));
            Assert.That(ReadRepeatCounter(snapshot), Is.EqualTo(2));

            fp firstDamage = DealAttack(context, 6);
            context.Attacker.EquipmentHandler.Restore(snapshot);
            context.Attacker.EquipmentHandler.Resolve(default);
            fp replayDamage = DealAttack(context, 7);

            Assert.That(firstDamage, Is.EqualTo((fp)61));
            Assert.That(replayDamage, Is.EqualTo(firstDamage));
        }

        [Test]
        public void EquipmentModuleState_SurvivesDeathRespawnHandleRebuild()
        {
            ScenarioContext context = CreateScenario(400);
            for (int hit = 1; hit <= 5; hit++)
                DealAttack(context, hit);

            context.Attacker.EquipmentHandler.ClearForDeath();
            var afterDeath = EquipmentHandlerSnapshot.Empty;
            context.Attacker.EquipmentHandler.Capture(ref afterDeath);
            Assert.That(ReadRepeatCounter(afterDeath), Is.EqualTo(2));

            context.Attacker.EquipmentHandler.ClearForRespawn();
            var afterRespawn = EquipmentHandlerSnapshot.Empty;
            context.Attacker.EquipmentHandler.Capture(ref afterRespawn);
            Assert.That(ReadRepeatCounter(afterRespawn), Is.EqualTo(2));
        }

        private ScenarioResult RunSixHitScenario(int uidBase)
        {
            ScenarioContext context = CreateScenario(uidBase);
            var damage = new fp[6];
            for (int hit = 1; hit <= 6; hit++)
                damage[hit - 1] = DealAttack(context, hit);

            var snapshot = EquipmentHandlerSnapshot.Empty;
            context.Attacker.EquipmentHandler.Capture(ref snapshot);
            return new ScenarioResult
            {
                DamageByHit = damage,
                StacksAfterSixthHit = context.Attacker.BuffHandler.TryGetRuntime(
                    new BuffConfigId(31901),
                    out BuffRuntime runtime)
                        ? runtime.CurrentStacks
                        : 0,
                CounterAfterFifthHit = context.CounterAfterFifthHit,
                CounterAfterSixthHit = ReadRepeatCounter(snapshot),
            };
        }

        private ScenarioContext CreateScenario(
            int uidBase,
            UnitKind targetKind = UnitKind.Hero)
        {
            EquipmentCatalogAsset catalog = LoadCatalog();
            EquipmentDatabase database = catalog.BakeOrThrow();
            BuffDefinition buff = LoadSeethingBuff();
            var registry = new BuffDefinitionRegistry();
            registry.Register(buff);
            var world = new UnitWorld
            {
                EquipmentDatabase = database,
                BuffDefinitions = registry,
            };
            var combat = new CombatSystem(world, 0, 0);
            world.CombatSystem = combat;
            Unit attacker = UnitTestFactory.CreateUnit(
                new UnitUid(1, uidBase, 0),
                UnitKind.Hero,
                0,
                new TeamId(1));
            Unit target = UnitTestFactory.CreateUnit(
                new UnitUid(1, uidBase + 1, 0),
                targetKind,
                0,
                new TeamId(2));
            attacker.World = world;
            target.World = world;
            attacker.EquipmentHandler.DefinitionDatabase = database;
            target.EquipmentHandler.DefinitionDatabase = database;
            attacker.BuffHandler.DefinitionRegistry = registry;
            target.BuffHandler.DefinitionRegistry = registry;
            world.RegisterUnit(attacker);
            world.RegisterUnit(target);
            CombatEvents.TryResolveUnit = uid =>
                world.TryGetUnit(uid, out Unit resolved)
                    ? resolved
                    : null;
            Assert.That(database.TryGetDefinition(31005, out EquipmentDefinition rageblade), Is.True);
            Assert.That(attacker.EquipmentHandler.Add(rageblade, 0), Is.True);
            return new ScenarioContext
            {
                World = world,
                Combat = combat,
                Attacker = attacker,
                Target = target,
            };
        }

        private fp DealAttack(ScenarioContext context, int tick)
        {
            if (controller.IsTickActive)
                controller.EndTick();
            controller.BeginTick(tick, ExecutionMode.ServerAuthority);
            context.Target.StatHandler.SetCurrentHealth((fp)100);
            fp before = context.Target.StatHandler.CurrentHealth;
            context.Combat.BeginTick();
            context.Combat.SubmitDamage(
                UnitTestFactory.CreateDamageRequest(
                    context.Attacker.UnitUid,
                    context.Target.UnitUid,
                    fp.one,
                    DamageType.Physical,
                    CombatSourceType.Attack));
            context.Combat.SettleActiveRequests();
            context.Combat.EndTick();
            fp damage = before - context.Target.StatHandler.CurrentHealth;
            if (tick == 5)
            {
                var snapshot = EquipmentHandlerSnapshot.Empty;
                context.Attacker.EquipmentHandler.Capture(ref snapshot);
                context.CounterAfterFifthHit = ReadRepeatCounter(snapshot);
            }
            return damage;
        }

        private static int ReadRepeatCounter(in EquipmentHandlerSnapshot snapshot)
        {
            List<EquipmentSlotSnapshot> slots = snapshot.Slots;
            return slots[0].EffectStates[1].ModuleStates[1].TriggerCount;
        }

        private static EquipmentCatalogAsset LoadCatalog()
        {
            EquipmentCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<EquipmentCatalogAsset>(
                    "Assets/Config/Formal/Equipment/FormalEquipmentCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            return catalog;
        }

        private static BuffDefinition LoadSeethingBuff()
        {
            BuffDefinition buff = AssetDatabase.LoadAssetAtPath<BuffDefinition>(
                "Assets/Config/Formal/Buffs/Buff_SeethingStrike.asset");
            Assert.That(buff, Is.Not.Null);
            return buff;
        }

        private sealed class ScenarioContext
        {
            public UnitWorld World;
            public CombatSystem Combat;
            public Unit Attacker;
            public Unit Target;
            public int CounterAfterFifthHit;
        }

        private struct ScenarioResult
        {
            public fp[] DamageByHit;
            public int StacksAfterSixthHit;
            public int CounterAfterFifthHit;
            public int CounterAfterSixthHit;
        }
    }
}
