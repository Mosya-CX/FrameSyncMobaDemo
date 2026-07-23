using System.Collections.Generic;
using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class UnitWorldIntegrationTests
    {
        private UnitWorld world;
        private StatDefinitionTable definitionTable;
        private UnitPrototype heroProto;
        private UnitPrototype minionProto;

        [SetUp]
        public void SetUp()
        {
            world = new UnitWorld();

            definitionTable = new StatDefinitionTable();
            definitionTable.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            definitionTable.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });
            world.StatDefinitionTable = definitionTable;

            var heroStats = new StatPreset();
            heroStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 5m,
            });
            heroStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 600m,
                GrowthValue = 80m,
            });

            heroProto = new UnitPrototype
            {
                UnitPrototypeId = 10,
                Name = "Hero",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = heroStats,
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };

            var minionStats = new StatPreset();
            minionStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 20m,
                GrowthValue = 2m,
            });
            minionStats.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 300m,
                GrowthValue = 10m,
            });

            minionProto = new UnitPrototype
            {
                UnitPrototypeId = 20,
                Name = "Minion",
                RuntimeEntityPrefabId = 200,
                UnitKind = UnitKind.Minion,
                UnitSubKindId = 0,
                BaseStats = minionStats,
                BaseGoldValue = 50,
                BaseExperienceValue = 20,
            };
        }

        [Test]
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
        }

        [Test]
        public void SpawnUnit_DoubleSnapshot_RoundTrip()
        {
            Unit unit = world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);

            // Add stat modifiers
            unit.StatHandler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            unit.StatHandler.FinalizeTick();

            // Add combat modifiers
            unit.CombatModifiers.Attach(new CombatModifierRecord
            {
                Id = CombatModifierId.Create(1, "Test.Modifier"),
            });

            // Capture both snapshots
            StatHandlerSnapshot statSnap = default;
            unit.StatHandler.Capture(ref statSnap);

            CombatModifierSetSnapshot combatSnap = default;
            unit.CombatModifiers.Capture(ref combatSnap);

            fp originalAD = unit.StatHandler.GetStat(StatId.AttackDamage);
            int originalCombatCount = unit.CombatModifiers.Count;

            // Modify state
            unit.StatHandler.ClearModifiers();
            unit.CombatModifiers.Clear();

            // Restore both
            unit.StatHandler.Restore(in statSnap);
            unit.CombatModifiers.Restore(in combatSnap);

            Assert.AreEqual(originalAD, unit.StatHandler.GetStat(StatId.AttackDamage));
            Assert.AreEqual(originalCombatCount, unit.CombatModifiers.Count);
        }

        [Test]
        public void ClearForDeath_PreservesCombatModifiersOwnedBySourceSystems()
        {
            Unit unit = world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            unit.CombatModifiers.Attach(new CombatModifierRecord
            {
                Id = CombatModifierId.Create(1, "Test.Modifier"),
            });

            unit.ClearForDeath();

            Assert.AreEqual(1, unit.CombatModifiers.Count);
        }

        [Test]
        public void ClearForRespawn_DoesNotClearStatBaseValues()
        {
            Unit unit = world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            unit.StatHandler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            unit.StatHandler.FinalizeTick();

            fp beforeRespawn = unit.StatHandler.GetStat(StatId.AttackDamage);

            unit.ClearForDeath();
            unit.ClearForRespawn();

            // StatHandler base values are preserved across death/respawn (§D-009)
            // The dynamic modifier from above is still there because ClearForDeath
            // preserves both global collections; each source system owns its handles
            Assert.AreEqual(beforeRespawn, unit.StatHandler.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void ResetForPool_ResetsAllDynamicState()
        {
            Unit unit = world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            unit.StatHandler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)50m);
            unit.StatHandler.FinalizeTick();
            unit.CombatModifiers.Attach(new CombatModifierRecord
            {
                Id = CombatModifierId.Create(1, "Test.Modifier"),
            });

            unit.ResetForPool();

            Assert.AreEqual(LifeState.Alive, unit.LifeState);
            Assert.AreEqual(0, unit.CombatModifiers.Count);
            Assert.IsNotNull(unit.StatHandler);
            Assert.IsNotNull(unit.MovementHandler);
            Assert.IsNotNull(unit.AttackHandler);
            Assert.IsFalse(unit.UnitUid.IsValid());
        }

        [Test]
        public void ManyUnits_StableReadOrder()
        {
            var order1 = new List<UnitUid>();
            var order2 = new List<UnitUid>();

            // First world: spawn in one order
            var w1 = new UnitWorld();
            w1.StatDefinitionTable = definitionTable;
            w1.SpawnUnit(heroProto, TeamId.Neutral, 10, 0m, 0m);
            w1.SpawnUnit(minionProto, TeamId.Neutral, 9, 0m, 0m);
            w1.SpawnUnit(heroProto, TeamId.Neutral, 11, 0m, 0m);
            foreach (var u in w1.GetAllUnits())
            {
                order1.Add(u.UnitUid);
            }

            // Second world: spawn in reverse order, same ticks
            var w2 = new UnitWorld();
            w2.StatDefinitionTable = definitionTable;
            w2.SpawnUnit(heroProto, TeamId.Neutral, 11, 0m, 0m);
            w2.SpawnUnit(minionProto, TeamId.Neutral, 9, 0m, 0m);
            w2.SpawnUnit(heroProto, TeamId.Neutral, 10, 0m, 0m);
            foreach (var u in w2.GetAllUnits())
            {
                order2.Add(u.UnitUid);
            }

            // Both should be sorted by UnitUid (stable, independent of registration order)
            Assert.AreEqual(order1.Count, order2.Count);
            for (int i = 0; i < order1.Count; i++)
            {
                Assert.AreEqual(order1[i], order2[i]);
            }
        }
    }
}
