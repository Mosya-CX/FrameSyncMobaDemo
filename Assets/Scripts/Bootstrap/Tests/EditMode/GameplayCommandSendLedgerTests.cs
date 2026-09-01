using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class GameplayCommandSendLedgerTests
    {
        private static readonly UnitUid UnitUid =
            new UnitUid(0, 1101, 1);

        [Test]
        public void UnchangedCollector_BuildsOnlyOneReliableBundleCandidate()
        {
            var collector = new CommandCollector();
            var ledger = new GameplayCommandSendLedger();
            collector.Collect(CreateToggle(10, 1));

            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out ulong revision,
                    out var first),
                Is.True);
            Assert.That(first, Has.Count.EqualTo(1));
            ledger.CommitSuccessfulSend(revision, first);

            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out _,
                    out _),
                Is.False,
                "Repeated Unity Updates must not wrap unchanged commands " +
                "in new reliable Bundles.");
        }

        [Test]
        public void AdjacentToggleInputs_SendTheirDistinctCommandSequences()
        {
            var collector = new CommandCollector();
            var ledger = new GameplayCommandSendLedger();
            collector.Collect(CreateToggle(10, 1));
            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out ulong firstRevision,
                    out var first),
                Is.True);
            ledger.CommitSuccessfulSend(firstRevision, first);

            collector.Collect(CreateToggle(11, 2));
            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out ulong secondRevision,
                    out var second),
                Is.True);
            Assert.That(second, Has.Count.EqualTo(1));
            Assert.That(second[0].CommandSeq, Is.EqualTo(2u));
            ledger.CommitSuccessfulSend(secondRevision, second);
        }

        [Test]
        public void RebuiltCollector_DoesNotResendSuccessfulIdentity()
        {
            var collector = new CommandCollector();
            var ledger = new GameplayCommandSendLedger();
            GameplayCommand toggle = CreateToggle(10, 1);
            collector.Collect(toggle);
            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out ulong revision,
                    out var commands),
                Is.True);
            ledger.CommitSuccessfulSend(revision, commands);

            collector.ConsumeCanonicalCommands(10);
            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out _,
                    out _),
                Is.False);
            collector.Collect(toggle);

            Assert.That(
                ledger.TryBuildUnsentCommands(
                    collector,
                    out _,
                    out _),
                Is.False,
                "Rollback/rebuild may reintroduce a command locally but a " +
                "successfully queued identity must not be sent again.");
        }

        private static GameplayCommand CreateToggle(
            int targetTick,
            uint commandSequence)
        {
            var header = new CommandHeader(
                commandSequence,
                7,
                0,
                UnitUid,
                targetTick,
                GameplayCommandKind.None,
                targetTick - 1,
                0);
            return GameplayCommand.CreateCastAbility(
                header,
                1,
                AbilitySignalVerb.Commit,
                AimSnapshot.None);
        }
    }
}
