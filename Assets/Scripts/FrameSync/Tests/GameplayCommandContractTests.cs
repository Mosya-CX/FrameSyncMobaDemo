using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync.Tests
{
    public sealed class GameplayCommandContractTests
    {
        [Test]
        public void AimFactories_ClearEveryUnusedPayloadField()
        {
            UnitUid target = new UnitUid(4, 8, 2);
            AimSnapshot unitAim = AimSnapshot.ForUnit(target);
            AimSnapshot pointAim = AimSnapshot.ForPoint(new fp2(3, 6));
            AimSnapshot directionAim = AimSnapshot.ForDirection(new fp2(10, 0));

            Assert.AreEqual(AimKind.Unit, unitAim.Kind);
            Assert.AreEqual(target, unitAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, unitAim.TargetPoint);
            Assert.AreEqual(fp2.zero, unitAim.Direction);

            Assert.AreEqual(AimKind.Point, pointAim.Kind);
            Assert.AreEqual(default(UnitUid), pointAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, pointAim.Direction);

            Assert.AreEqual(AimKind.Direction, directionAim.Kind);
            Assert.AreEqual(default(UnitUid), directionAim.TargetUnitUid);
            Assert.AreEqual(fp2.zero, directionAim.TargetPoint);
            Assert.LessOrEqual(
                fpmath.abs(directionAim.Direction.x - fp.one),
                fp.FromRaw(8));
            Assert.AreEqual(fp.zero, directionAim.Direction.y);
        }

        [Test]
        public void CanonicalBytes_IncludeHeaderIdentityAndMinimalMovePayload()
        {
            UnitUid controlled = new UnitUid(7, 9, 1);
            var header = new CommandHeader(
                12,
                44,
                3,
                controlled,
                20,
                GameplayCommandKind.None,
                17,
                0);
            GameplayCommand command = GameplayCommand.CreateMove(
                header, new fp2(5, 8));
            var writer = new CanonicalByteWriter(new byte[128]);

            command.WriteCanonicalBytes(writer);

            Assert.AreEqual((uint)12, command.CommandSeq);
            Assert.AreEqual(3, command.PlayerSlot);
            Assert.AreEqual(controlled, command.ControlledUnitUid);
            Assert.AreEqual(GameplayCommandKind.Move, command.Kind);
            Assert.AreEqual(16, command.Header.PayloadByteLength);
            Assert.AreEqual(58, writer.WrittenCount);
        }

        [Test]
        public void Collector_ProducesSameCanonicalOrderForDifferentInsertionOrder()
        {
            UnitUid unitA = new UnitUid(5, 1, 0);
            UnitUid unitB = new UnitUid(5, 2, 0);
            GameplayCommand a = GameplayCommand.CreateMove(
                Header(unitA, 2, 1, 9), new fp2(1, 0));
            GameplayCommand b = GameplayCommand.CreateMove(
                Header(unitB, 1, 2, 8), new fp2(2, 0));
            GameplayCommand c = GameplayCommand.CreateCastAbility(
                Header(unitA, 1, 3, 7),
                0,
                AbilitySignalVerb.Commit,
                AimSnapshot.None);

            var first = new CommandCollector();
            first.Collect(a);
            first.Collect(b);
            first.Collect(c);
            var second = new CommandCollector();
            second.Collect(c);
            second.Collect(a);
            second.Collect(b);

            var writerA = new CanonicalByteWriter(new byte[512]);
            var writerB = new CanonicalByteWriter(new byte[512]);
            first.WriteCanonicalBytes(writerA);
            second.WriteCanonicalBytes(writerB);

            CollectionAssert.AreEqual(
                writerA.GetWrittenSegment(),
                writerB.GetWrittenSegment());
            Assert.AreEqual(7, first.GetCanonicalCommands()[0].TargetTick);
            Assert.AreEqual(1, first.GetCanonicalCommands()[0].PlayerSlot);
        }

        [Test]
        public void MoveMerge_UsesHighestCommandSequenceForSameFormalKey()
        {
            UnitUid unit = new UnitUid(5, 1, 0);
            var collector = new CommandCollector();
            collector.Collect(GameplayCommand.CreateMove(
                Header(unit, 0, 8, 4), new fp2(8, 0)));
            collector.Collect(GameplayCommand.CreateMove(
                Header(unit, 0, 3, 4), new fp2(3, 0)));

            var commands = collector.GetCanonicalCommands();

            Assert.AreEqual(1, commands.Count);
            Assert.AreEqual((uint)8, commands[0].CommandSeq);
            Assert.AreEqual(new fp2(8, 0), commands[0].MoveTargetPoint);
        }

        private static CommandHeader Header(
            UnitUid controlled,
            int playerSlot,
            uint sequence,
            int targetTick)
        {
            return new CommandHeader(
                sequence,
                99,
                playerSlot,
                controlled,
                targetTick,
                GameplayCommandKind.None,
                targetTick - 1,
                0);
        }
    }
}
