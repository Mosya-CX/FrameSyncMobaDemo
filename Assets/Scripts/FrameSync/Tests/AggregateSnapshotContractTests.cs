using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class AggregateSnapshotContractTests
    {
        [Test]
        public void Restore_UsesStableUnitUidAndRestoresRandomAndPhysicsState()
        {
            UnitWorld world = CreateWorld();
            UnitType first = Spawn(world, 10, 0);
            UnitType second = Spawn(world, 11, 1);
            first.MovementHandler.ForceSetPosition(new fp2(3, 4));
            second.MovementHandler.ForceSetPosition(new fp2(7, 8));
            first.PhysicsEntity.SetLogicPose(new fp2(3, 4), new fp2(fp.one, fp.zero));
            second.PhysicsEntity.SetLogicPose(new fp2(7, 8), new fp2(fp.zero, fp.one));

            var random = new DeterministicRandomService(123u);
            _ = random.NextUInt();
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld)
            {
                RandomService = random,
            };
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();
            var expectedRandom = new DeterministicRandomService(1u);
            expectedRandom.Restore(snapshot.RandomState);

            first.MovementHandler.ForceSetPosition(new fp2(99, 99));
            first.PhysicsEntity.TeleportLogicPosition(new fp2(99, 99));
            _ = random.NextUInt();

            pipeline.RestoreFromSnapshot(snapshot, 12);

            Assert.That(first.MovementHandler.Position, Is.EqualTo(new fp2(3, 4)));
            Assert.That(first.PhysicsEntity.Transform2D.Position, Is.EqualTo(new fp2(3, 4)));
            Assert.That(second.PhysicsEntity.Transform2D.Forward, Is.EqualTo(new fp2(fp.zero, fp.one)));
            Assert.That(random.NextUInt(), Is.EqualTo(expectedRandom.NextUInt()));
            Assert.That(pipeline.LocalSimulationTick, Is.EqualTo(12));
        }

        [Test]
        public void Restore_RejectsNonCanonicalOrMissingUnitIdentity()
        {
            UnitWorld world = CreateWorld();
            Spawn(world, 10, 0);
            Spawn(world, 11, 1);
            var pipeline = new SimulationTickPipeline(world, world.PhysicsWorld);
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();

            UnitSnapshot first = snapshot.UnitWorldState.Units[0];
            snapshot.UnitWorldState.Units[0] = snapshot.UnitWorldState.Units[1];
            snapshot.UnitWorldState.Units[1] = first;

            Assert.Throws<DeterministicSimulationException>(
                () => pipeline.RestoreFromSnapshot(snapshot));
        }

        [Test]
        public void SnapshotStore_WritesExplicitOuterSchemaAndNextTick()
        {
            var store = new SnapshotStore(4);
            GameplaySnapshot gameplay = GameplaySnapshot.CreateEmpty();

            store.Store(5, gameplay);

            Assert.That(store.TryGet(5, out RollbackFrameSnapshot snapshot), Is.True);
            Assert.That(snapshot.SnapshotTick, Is.EqualTo(6));
            Assert.That(snapshot.SnapshotSchemaVersion,
                Is.EqualTo(SnapshotStore.CurrentSnapshotSchemaVersion));
        }

        private static UnitWorld CreateWorld()
        {
            return new UnitWorld
            {
                StatDefinitionTable = new StatDefinitionTable(),
                PhysicsWorld = new PhysicsWorld(),
            };
        }

        private static UnitType Spawn(UnitWorld world, int prefabId, int tick)
        {
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = prefabId,
                RuntimeEntityPrefabId = prefabId,
                UnitKind = UnitKind.Hero,
                BaseStats = new StatPreset(),
            };
            return world.SpawnUnit(prototype, TeamId.Neutral, tick, fp.zero, fp.zero);
        }
    }
}
