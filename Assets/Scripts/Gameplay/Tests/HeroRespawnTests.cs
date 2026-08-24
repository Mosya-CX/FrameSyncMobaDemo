using FrameSyncMoba.Deterministic;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class HeroRespawnTests
    {
        private SimulationTickContextController _controller;
        private UnitWorld _world;
        private UnitPrototype _prototype;
        private CombatSystem _combat;
        private UnitDisposePolicyTable _disposePolicies;

        [SetUp]
        public void SetUp()
        {
            _controller =
                new SimulationTickContextController();
            _world = new UnitWorld
            {
                StatDefinitionTable =
                    CreateStatTable(),
                TickRate = 30,
            };
            _disposePolicies =
                UnityEngine.ScriptableObject
                    .CreateInstance<
                        UnitDisposePolicyTable>();
            _disposePolicies.Entries.Add(
                new UnitDisposePolicyEntry
                {
                    Id = 0,
                    Kind =
                        UnitDisposePolicyKind
                            .KeepAlive,
                });
            _world.DisposePolicyTable =
                _disposePolicies;
            _world.RespawnTimer =
                new RespawnTimer(_world);
            _prototype = new UnitPrototype
            {
                UnitPrototypeId = 1,
            Name = "Varus",
                RuntimeEntityPrefabId = 100,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 0,
                BaseStats = CreateHeroPreset(),
                BaseGoldValue = 300,
                BaseExperienceValue = 100,
                RespawnConfig =
                    UnitRespawnConfig.HeroDefault,
            };
            // 5s base (150 ticks at 30) + 0.5s (15 ticks) per elapsed minute.
            _combat = new CombatSystem(
                _world,
                150,
                15);
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller.IsTickActive)
            {
                _controller.EndTick();
            }
            UnityEngine.Object.DestroyImmediate(
                _disposePolicies);
            UnitTestFactory
                .DestroyCreatedObjects();
        }

        private void BeginTick(int tick)
        {
            _controller.BeginTick(
                tick,
                ExecutionMode
                    .ServerAuthority);
        }

        [Test]
        public void RespawnDelay_UsesBasePlusElapsedMinutes()
        {
            var hero = _world.SpawnUnit(
                _prototype,
                new TeamId(1),
                1,
                0m,
                0m);
            BeginTick(3 * 1800 + 17);
            Assert.AreEqual(
                150 + 3 * 15,
                _combat.GetRespawnDelay(hero));
        }

        [Test]
        public void RespawnPosition_IsCapturedFromInitialSpawn()
        {
            var hero = _world.SpawnUnit(
                _prototype,
                new TeamId(1),
                1,
                0m,
                0m);
            var spawnPos =
                new fp2((fp)10, (fp)20);
            BeginTick(1);
            UnitUid uid = _world.SpawnUnit(
                new UnitSpawnRequest(
                    _prototype.UnitPrototypeId,
                    GameplayParticipantId.Explicit(2),
                    new TeamId(1),
                    spawnPos,
                    new fp2(
                        fp.one,
                        fp.zero)));
            Assert.IsTrue(
                _world.TryGetUnit(
                    uid,
                    out Unit heroAtPos));
            Assert.AreEqual(
                spawnPos.x,
                heroAtPos.RespawnPosition.x);
            Assert.AreEqual(
                spawnPos.y,
                heroAtPos.RespawnPosition.y);
        }

        [Test]
        public void CompleteRespawn_TeleportsToHomeSpawnPosition()
        {
            var seed = _world.SpawnUnit(
                _prototype,
                new TeamId(1),
                1,
                0m,
                0m);
            var spawnPos =
                new fp2((fp)10, (fp)20);
            BeginTick(1);
            UnitUid uid = _world.SpawnUnit(
                new UnitSpawnRequest(
                    _prototype.UnitPrototypeId,
                    GameplayParticipantId.Explicit(2),
                    new TeamId(1),
                    spawnPos,
                    new fp2(
                        fp.one,
                        fp.zero)));
            Assert.IsTrue(
                _world.TryGetUnit(
                    uid,
                    out Unit hero));

            // Move the hero away; respawn must bring it back.
            hero.PhysicsEntity.SetLogicPose(
                new fp2((fp)50, (fp)60),
                hero.PhysicsEntity
                    .Transform2D.Forward);
            hero.MovementHandler.ForceSetPosition(
                new fp2((fp)50, (fp)60));

            _world.RequestEnterDying(hero);
            _world.ConfirmUnitDeath(hero);
            _world.RespawnTimer.RegisterDeath(
                uid,
                1,
                0);
            _controller.EndTick();

            BeginTick(2);
            _world.RespawnTimer.Tick(2);
            Assert.AreEqual(
                LifeState.Alive,
                hero.LifeState);
            Assert.AreEqual(
                spawnPos.x,
                hero.PhysicsEntity
                    .Transform2D.Position.x);
            Assert.AreEqual(
                spawnPos.y,
                hero.PhysicsEntity
                    .Transform2D.Position.y);
        }

        private static StatDefinitionTable
            CreateStatTable()
        {
            return UnitTestFactory
                .CreateDefaultStatTable();
        }

        private static StatPreset
            CreateHeroPreset()
        {
            return UnitTestFactory
                .CreateDefaultPreset();
        }
    }
}
