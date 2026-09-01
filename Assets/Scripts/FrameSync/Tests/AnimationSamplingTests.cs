using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    [TestFixture]
    public sealed class AnimationSamplingTests
    {
        [TearDown]
        public void TearDown()
        {
            UnitAnimationSynchronizationSettings.Configure(
                UnitAnimationSynchronizationSettings
                    .DefaultSynchronizationRateHz,
                true);
        }

        [Test]
        public void PresentationTime_UsesConfiguredTickRateAndSubTickAlpha()
        {
            var atTwentyHz = new AnimationPresentationTime(
                9,
                20,
                0.5d);
            var atSixtyHz = new AnimationPresentationTime(
                9,
                60,
                0.5d);

            Assert.That(atTwentyHz.LogicTimeTicks,
                Is.EqualTo(9.5d).Within(0.000001d));
            Assert.That(atTwentyHz.LogicTimeSeconds,
                Is.EqualTo(0.475d).Within(0.000001d));
            Assert.That(atSixtyHz.LogicTimeSeconds,
                Is.EqualTo(9.5d / 60d).Within(0.000001d));
        }

        [Test]
        public void PresentationClock_DoesNotLeakAcrossWorldsAtSameTickRate()
        {
            var firstMatch = new UnitWorld { TickRate = 30 };
            var secondMatch = new UnitWorld { TickRate = 30 };

            AnimationPresentationClock.Publish(
                firstMatch,
                480,
                firstMatch.TickRate,
                0.75d);

            Assert.That(
                AnimationPresentationClock.TryGetCurrent(
                    firstMatch,
                    out AnimationPresentationTime firstTime),
                Is.True);
            Assert.That(firstTime.CompletedLogicTick, Is.EqualTo(480));
            Assert.That(
                AnimationPresentationClock.TryGetCurrent(
                    secondMatch,
                    out _),
                Is.False,
                "A new match must not observe the previous match clock.");

            AnimationPresentationClock.Publish(
                secondMatch,
                -1,
                secondMatch.TickRate,
                0d);
            AnimationPresentationClock.Clear(firstMatch);

            Assert.That(
                AnimationPresentationClock.TryGetCurrent(
                    secondMatch,
                    out AnimationPresentationTime secondTime),
                Is.True,
                "An outgoing match must not clear a newer match clock.");
            Assert.That(secondTime.CompletedLogicTick, Is.EqualTo(-1));

            AnimationPresentationClock.Clear(secondMatch);
            Assert.That(
                AnimationPresentationClock.TryGetCurrent(
                    secondMatch,
                    out _),
                Is.False);
        }

        [Test]
        public void Sampler_InterpolatesAtRateIndependentOfGameplayTickRate()
        {
            var sampler =
                new ConfigurableAnimationProgressSampler();

            float initial = sampler.Sample(
                0d,
                0f,
                0.2f,
                7,
                false,
                10f,
                true);
            float halfway = sampler.Sample(
                0.05d,
                0.1f,
                0.3f,
                7,
                false,
                10f,
                true);
            float boundary = sampler.Sample(
                0.1d,
                0.2f,
                0.4f,
                7,
                false,
                10f,
                true);

            Assert.That(initial, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(halfway, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(boundary, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void Sampler_WhenInterpolationDisabled_HoldsUntilOwnBoundary()
        {
            var sampler =
                new ConfigurableAnimationProgressSampler();

            Assert.That(sampler.Sample(
                    0d, 0f, 0.2f, 1, false, 10f, false),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sampler.Sample(
                    0.05d, 0.1f, 0.3f, 1, false, 10f, false),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(sampler.Sample(
                    0.1d, 0.2f, 0.4f, 1, false, 10f, false),
                Is.EqualTo(0.2f).Within(0.0001f));
        }

        [Test]
        public void Sampler_FrameSkipResamplesAtObservedTime()
        {
            var sampler =
                new ConfigurableAnimationProgressSampler();
            sampler.Sample(
                0d, 0f, 0.2f, 1, false, 10f, true);

            float afterSkip = sampler.Sample(
                0.45d,
                0.9f,
                1f,
                1,
                false,
                10f,
                true);

            Assert.That(afterSkip,
                Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void Sampler_LinearProgressMatchesAcrossRenderCadences()
        {
            var dense =
                new ConfigurableAnimationProgressSampler();
            var sparse =
                new ConfigurableAnimationProgressSampler();
            dense.Sample(0d, 0f, 0.1f, 4, false, 20f, true);
            sparse.Sample(0d, 0f, 0.1f, 4, false, 20f, true);

            double[] denseTimes = { 0.016d, 0.033d, 0.05d, 0.066d,
                0.083d, 0.1d, 0.116d, 0.133d };
            float denseResult = 0f;
            for (int i = 0; i < denseTimes.Length; i++)
            {
                double time = denseTimes[i];
                denseResult = dense.Sample(
                    time,
                    (float)(time * 2d),
                    (float)((time + 0.05d) * 2d),
                    4,
                    false,
                    20f,
                    true);
            }

            double[] sparseTimes = { 0.033d, 0.066d, 0.1d, 0.133d };
            float sparseResult = 0f;
            for (int i = 0; i < sparseTimes.Length; i++)
            {
                double time = sparseTimes[i];
                sparseResult = sparse.Sample(
                    time,
                    (float)(time * 2d),
                    (float)((time + 0.05d) * 2d),
                    4,
                    false,
                    20f,
                    true);
            }

            Assert.That(denseResult,
                Is.EqualTo(0.266f).Within(0.0001f));
            Assert.That(sparseResult,
                Is.EqualTo(denseResult).Within(0.0001f));
        }

        [Test]
        public void LoopPhase_RateChangeRebuildsFromLogicEpochAndWraps()
        {
            var tracker = new LoopAnimationPhaseTracker();
            tracker.Observe(3, 0d, 2d);
            Assert.That(tracker.EvaluateUnwrapped(0.25d),
                Is.EqualTo(0.5d).Within(0.000001d));

            tracker.Observe(3, 0.25d, 1d);
            double unwrapped = tracker.EvaluateUnwrapped(0.85d);
            Assert.That(unwrapped,
                Is.EqualTo(0.85d).Within(0.000001d));

            var sampler =
                new ConfigurableAnimationProgressSampler();
            float wrapped = sampler.Sample(
                0.85d,
                (float)unwrapped,
                0.95f,
                3,
                true,
                10f,
                true);
            Assert.That(wrapped, Is.EqualTo(0.85f).Within(0.0001f));
        }

        [Test]
        public void LoopPhase_DifferentObservationTimesShareLogicEpoch()
        {
            var early = new LoopAnimationPhaseTracker();
            var late = new LoopAnimationPhaseTracker();
            early.Observe(8, 0.1d, 2d);
            late.Observe(8, 0.6d, 2d);

            Assert.That(early.EvaluateUnwrapped(0.85d),
                Is.EqualTo(1.7d).Within(0.000001d));
            Assert.That(late.EvaluateUnwrapped(0.85d),
                Is.EqualTo(
                    early.EvaluateUnwrapped(0.85d))
                    .Within(0.000001d));

            early.Observe(8, 0.4d, 2d);
            Assert.That(early.EvaluateUnwrapped(0.85d),
                Is.EqualTo(
                    late.EvaluateUnwrapped(0.85d))
                    .Within(0.000001d));
        }

        [Test]
        public void LoopPhase_RateChangeImmediatelyRebuildsSampleSegment()
        {
            var tracker = new LoopAnimationPhaseTracker();
            var sampler =
                new ConfigurableAnimationProgressSampler();
            tracker.Observe(5, 0d, 1d);
            sampler.Sample(
                0d, 0f, 0.1f, 5, true, 10f, true);

            bool anchorChanged = tracker.Observe(5, 0.05d, 0d);
            if (anchorChanged)
                sampler.Clear();
            float anchoredPhase = (float)tracker
                .EvaluateUnwrapped(0.05d);
            float stopped = sampler.Sample(
                0.075d,
                anchoredPhase,
                anchoredPhase,
                5,
                true,
                10f,
                true);

            Assert.That(anchorChanged, Is.True);
            Assert.That(stopped,
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Sampler_TimeRegressionResetsToRestoredProgress()
        {
            var sampler =
                new ConfigurableAnimationProgressSampler();
            sampler.Sample(2d, 0.6f, 0.7f, 9, false, 20f, true);

            float restored = sampler.Sample(
                1d,
                0.25f,
                0.3f,
                9,
                false,
                20f,
                true);

            Assert.That(restored, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void Sampler_InvalidSynchronizationRateFailsVisibly()
        {
            var sampler =
                new ConfigurableAnimationProgressSampler();

            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                sampler.Sample(
                    0d,
                    0f,
                    0.1f,
                    1,
                    false,
                    float.NaN,
                    true));
        }

        [Test]
        public void AttackMotionTime_MapsStartImpactAndReadyWithoutExitJump()
        {
            var snapshot = new AttackAnimationSnapshot
            {
                IsAttacking = true,
                AttackStartLogicTick = 100,
                ImpactLogicTick = 104,
                NextAttackReadyLogicTick = 112,
            };

            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 100d),
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 102d),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 104d),
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 108d),
                Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 112d),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot, 115d),
                Is.EqualTo(1f).Within(0.0001f));
        }


        [Test]
        public void AttackSampling_NonAlignedImpactBoundaryStaysContinuous()
        {
            const int tickRate = 30;
            const float samplingRate = 20f;
            var snapshot = new AttackAnimationSnapshot
            {
                IsAttacking = true,
                AttackStartLogicTick = 0,
                ImpactLogicTick = 4,
                NextAttackReadyLogicTick = 10,
            };
            var sampler =
                new ConfigurableAnimationProgressSampler();

            float before = SampleAttack(
                sampler,
                snapshot,
                3.5d,
                1,
                tickRate,
                samplingRate);
            float nearBoundary = SampleAttack(
                sampler,
                snapshot,
                3.75d,
                1,
                tickRate,
                samplingRate);
            float atBoundary = SampleAttack(
                sampler,
                snapshot,
                4d,
                1,
                tickRate,
                samplingRate);
            snapshot.ImpactCommitted = true;
            float committed = SampleAttack(
                sampler,
                snapshot,
                4d,
                2,
                tickRate,
                samplingRate);
            var readySampler =
                new ConfigurableAnimationProgressSampler();
            float beforeReady = SampleAttack(
                readySampler,
                snapshot,
                9.5d,
                2,
                tickRate,
                samplingRate);
            float atReady = SampleAttack(
                readySampler,
                snapshot,
                10d,
                2,
                tickRate,
                samplingRate);
            snapshot.IsAttacking = false;
            float exited = SampleAttack(
                readySampler,
                snapshot,
                10d,
                3,
                tickRate,
                samplingRate);

            Assert.That(before,
                Is.EqualTo(0.4375f).Within(0.0001f));
            Assert.That(nearBoundary,
                Is.EqualTo(0.46875f).Within(0.0001f));
            Assert.That(atBoundary,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(committed,
                Is.EqualTo(atBoundary).Within(0.0001f));
            Assert.That(beforeReady,
                Is.EqualTo(0.958333f).Within(0.0001f));
            Assert.That(atReady,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(exited,
                Is.EqualTo(atReady).Within(0.0001f));
        }

        private static float SampleAttack(
            ConfigurableAnimationProgressSampler sampler,
            in AttackAnimationSnapshot snapshot,
            double nowLogicTicks,
            int stateKey,
            int tickRate,
            float samplingRate)
        {
            double intervalTicks = tickRate / samplingRate;
            double nextLogicTicks = nowLogicTicks + intervalTicks;
            if (!snapshot.ImpactCommitted)
            {
                nextLogicTicks = System.Math.Min(
                    nextLogicTicks,
                    snapshot.ImpactLogicTick);
            }
            nextLogicTicks = System.Math.Min(
                nextLogicTicks,
                snapshot.NextAttackReadyLogicTick);
            return sampler.Sample(
                nowLogicTicks / tickRate,
                UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot,
                    nowLogicTicks),
                UnitAnimationDriver.EvaluateAttackMotionTime(
                    snapshot,
                    nextLogicTicks),
                stateKey,
                false,
                samplingRate,
                true,
                (nextLogicTicks - nowLogicTicks) / tickRate);
        }
    }
}
