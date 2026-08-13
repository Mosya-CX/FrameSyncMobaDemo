using NUnit.Framework;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class SpawnUnitTests
    {
        private UnitWorld world;
        private StatDefinitionTable definitionTable;
        private UnitPrototype prototype;

        [SetUp]
        public void SetUp()
        {
            world = new UnitWorld();
            definitionTable = StatTestHelpers.CreateDefaultTable();
            world.StatDefinitionTable = definitionTable;

            prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
            Name = "Varus",
                RuntimeEntityPrefabId = 99,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = StatTestHelpers.CreateSimplePreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
            };
        }

        [Test]
        public void SpawnUnit_ReturnsUnitWithCorrectIdentity()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.AreEqual(UnitKind.Hero, unit.UnitKind);
            Assert.AreEqual(TeamId.Neutral, unit.TeamId);
            Assert.AreEqual(1, unit.UnitPrototypeId);
            Assert.AreEqual(300, unit.BaseGoldValue);
            Assert.AreEqual(100, unit.BaseExperienceValue);
            Assert.AreEqual(LifeState.Alive, unit.LifeState);
        }

        [Test]
        public void SpawnUnit_AllocatesDeterministicUid()
        {
            Unit unit1 = world.SpawnUnit(prototype, TeamId.Neutral, 100, 0m, 0m);
            Unit unit2 = world.SpawnUnit(prototype, TeamId.Neutral, 100, 0m, 0m);

            // Same tick, same prefab → sequential spawn sequence
            Assert.AreEqual(100, unit1.UnitUid.SpawnLogicTick);
            Assert.AreEqual(prototype.RuntimeEntityPrefabId, (int)unit1.UnitUid.RuntimeEntityPrefabId);
            Assert.AreEqual((byte)0, unit1.UnitUid.SpawnSequenceInTick);

            Assert.AreEqual(100, unit2.UnitUid.SpawnLogicTick);
            Assert.AreEqual(prototype.RuntimeEntityPrefabId, (int)unit2.UnitUid.RuntimeEntityPrefabId);
            Assert.AreEqual((byte)1, unit2.UnitUid.SpawnSequenceInTick);
        }

        [Test]
        public void SpawnUnit_SameInput_SameUid()
        {
            var world1 = new UnitWorld();
            world1.StatDefinitionTable = definitionTable;
            Unit u1 = world1.SpawnUnit(prototype, TeamId.Neutral, 50, 0m, 0m);

            var world2 = new UnitWorld();
            world2.StatDefinitionTable = definitionTable;
            Unit u2 = world2.SpawnUnit(prototype, TeamId.Neutral, 50, 0m, 0m);

            Assert.AreEqual(u1.UnitUid, u2.UnitUid);
        }

        [Test]
        public void SpawnUnit_DifferentTick_DifferentUid()
        {
            Unit u1 = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            Unit u2 = world.SpawnUnit(prototype, TeamId.Neutral, 2, 0m, 0m);

            Assert.AreNotEqual(u1.UnitUid, u2.UnitUid);
        }

        [Test]
        public void SpawnUnit_InitializesStatHandler()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.StatHandler);
            Assert.AreEqual(unit.UnitUid, unit.StatHandler.OwnerUid);
            Assert.AreEqual(1, unit.StatHandler.Level);

            // Check base stats from preset (level 1, no growth)
            fp ad = unit.StatHandler.GetStat(StatId.AttackDamage);
            fp hp = unit.StatHandler.GetStat(StatId.MaxHealth);
            fp armor = unit.StatHandler.GetStat(StatId.Armor);

            Assert.AreEqual((fp)100m, ad);
            Assert.AreEqual((fp)500m, hp);
            Assert.AreEqual((fp)30m, armor);
        }

        [Test]
        public void SpawnUnit_StatHandlerLevelGrowth()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 1m, 0m);

            // Level 1: base only
            Assert.AreEqual((fp)100m, unit.StatHandler.GetStat(StatId.AttackDamage));

            unit.StatHandler.Level = 2;
            unit.StatHandler.FinalizeTick();

            // Level 2 with growthC=1,growthD=0: base + growth * 1 * (1 + 0*1) = 100 + 10 = 110
            Assert.AreEqual((fp)110m, unit.StatHandler.GetStat(StatId.AttackDamage));
        }

        [Test]
        public void SpawnUnit_CreatesEmptyCombatModifierSet()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsNotNull(unit.CombatModifiers);
            Assert.AreEqual(0, unit.CombatModifiers.Count);
        }

        [Test]
        public void SpawnUnit_Registration_UnitIsFindable()
        {
            Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.IsTrue(world.TryGetUnit(unit.UnitUid, out Unit resolved));
            Assert.AreSame(unit, resolved);
        }

        [Test]
        public void SpawnUnit_GetAllUnits_ReturnsSpawnedUnits()
        {
            world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);

            Assert.AreEqual(2, world.GetAllUnits().Count);
        }

        [Test]
        public void SpawnUnit_WithNullPrototype_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
            {
                world.SpawnUnit(null, TeamId.Neutral, 1, 0m, 0m);
            });
        }

        [Test]
        public void SpawnUnit_WithoutStatDefinitionTable_Throws()
        {
            var worldNoTable = new UnitWorld();
            worldNoTable.SpawnUnit(prototype, TeamId.Neutral, 1, 0m, 0m);
            worldNoTable.StatDefinitionTable = null;
            var controller = new SimulationTickContextController();
            controller.BeginTick(2, ExecutionMode.ServerAuthority);
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    worldNoTable.SpawnUnit(new UnitSpawnRequest(
                        prototype.UnitPrototypeId,
                        TeamId.Neutral,
                        fp2.zero,
                        new fp2(fp.one, fp.zero))));
            }
            finally
            {
                controller.EndTick();
            }
        }

        [Test]
        public void SpawnUnit_MultipleTeams_GetByTeamWorks()
        {
            var team1 = new TeamId(1);
            var team2 = new TeamId(2);

            Unit redUnit = world.SpawnUnit(prototype, team1, 1, 0m, 0m);
            Unit blueUnit = world.SpawnUnit(prototype, team2, 1, 0m, 0m);

            var redUnits = world.GetUnitsByTeam(team1);
            var blueUnits = world.GetUnitsByTeam(team2);

            Assert.AreEqual(1, redUnits.Count);
            Assert.AreSame(redUnit, redUnits[0]);
            Assert.AreEqual(1, blueUnits.Count);
            Assert.AreSame(blueUnit, blueUnits[0]);
        }

        [Test]
        public void SpawnUnit_CanRunActiveGameplayThisTick_FalseDuringSpawnTick()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(100, ExecutionMode.ServerAuthority);
            try
            {
                Unit unit = world.SpawnUnit(prototype, TeamId.Neutral, 100, 0m, 0m);
                Assert.IsFalse(unit.CanRunActiveGameplayThisTick);
            }
            finally
            {
                controller.EndTick();
            }
        }
    }
}
