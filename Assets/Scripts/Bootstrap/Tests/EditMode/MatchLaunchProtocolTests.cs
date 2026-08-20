using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class MatchLaunchProtocolTests
    {
        [Test]
        public void Messages_RoundTripCanonically()
        {
            var applied =
                new BootstrapAppliedConfirmation(
                    "match-two-phase",
                    3);
            byte[] appliedBytes =
                MatchLaunchWireCodec
                    .WriteBootstrapApplied(applied);
            BootstrapAppliedConfirmation restoredApplied =
                MatchLaunchWireCodec
                    .ReadBootstrapApplied(appliedBytes);
            Assert.That(restoredApplied.MatchId,
                Is.EqualTo(applied.MatchId));
            Assert.That(restoredApplied.StartTick,
                Is.EqualTo(applied.StartTick));
            Assert.That(
                MatchLaunchWireCodec.WriteBootstrapApplied(
                    restoredApplied),
                Is.EqualTo(appliedBytes));

            var commit =
                new MatchLaunchCommit(
                    "match-two-phase",
                    3,
                    15_000L);
            byte[] commitBytes =
                MatchLaunchWireCodec
                    .WriteLaunchCommit(commit);
            MatchLaunchCommit restoredCommit =
                MatchLaunchWireCodec
                    .ReadLaunchCommit(commitBytes);
            Assert.That(restoredCommit.MatchId,
                Is.EqualTo(commit.MatchId));
            Assert.That(restoredCommit.StartTick,
                Is.EqualTo(commit.StartTick));
            Assert.That(
                restoredCommit.LaunchServerTimeMilliseconds,
                Is.EqualTo(
                    commit.LaunchServerTimeMilliseconds));
            Assert.That(
                MatchLaunchWireCodec.WriteLaunchCommit(
                    restoredCommit),
                Is.EqualTo(commitBytes));

            byte[] legacyVersionBytes =
                (byte[])commitBytes.Clone();
            legacyVersionBytes[4] = 1;
            legacyVersionBytes[5] = 0;
            Assert.Throws<DeterministicSimulationException>(
                () => MatchLaunchWireCodec.ReadLaunchCommit(
                    legacyVersionBytes));
        }

        [Test]
        public void Barrier_CompletesOnceAfterEveryFrozenClient()
        {
            GameStartConfig config = CreateConfig();
            var barrier = new BootstrapAppliedBarrier();
            barrier.Initialize(config);
            var confirmation =
                new BootstrapAppliedConfirmation(
                    config.MatchId,
                    config.StartTick);

            Assert.That(
                barrier.MarkApplied(11, confirmation),
                Is.False);
            Assert.That(barrier.AppliedCount,
                Is.EqualTo(1));
            Assert.That(
                barrier.MarkApplied(11, confirmation),
                Is.False,
                "duplicate confirmation must be idempotent");
            Assert.That(barrier.AppliedCount,
                Is.EqualTo(1));
            Assert.That(
                barrier.MarkApplied(22, confirmation),
                Is.True);
            Assert.That(barrier.IsComplete,
                Is.True);
            Assert.That(
                barrier.MarkApplied(22, confirmation),
                Is.False);
        }

        [Test]
        public void Barrier_RejectsWrongMatchOrUnknownClient()
        {
            GameStartConfig config = CreateConfig();
            var barrier = new BootstrapAppliedBarrier();
            barrier.Initialize(config);

            Assert.Throws<DeterministicSimulationException>(
                () => barrier.MarkApplied(
                    11,
                    new BootstrapAppliedConfirmation(
                        "other-match",
                        config.StartTick)));
            Assert.Throws<DeterministicSimulationException>(
                () => barrier.MarkApplied(
                    99,
                    new BootstrapAppliedConfirmation(
                        config.MatchId,
                        config.StartTick)));
        }

        private static GameStartConfig CreateConfig()
        {
            return new GameStartConfig(
                "match-two-phase",
                1,
                1,
                2,
                2,
                new[]
                {
                    new PlayerSlotConfig(
                        0,
                        "account-a",
                        11,
                        new TeamId(1),
                        1001,
                        0),
                    new PlayerSlotConfig(
                        1,
                        "account-b",
                        22,
                        new TeamId(2),
                        1002,
                        1),
                },
                3,
                123u,
                1);
        }
    }
}
