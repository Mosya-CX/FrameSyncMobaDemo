using NUnit.Framework;

namespace FrameSyncMoba.RuntimeConfig.Editor.Tests
{
    public sealed class DeterministicTimeAuthoringTests
    {
        [TestCase(20, 9)]
        [TestCase(30, 14)]
        [TestCase(40, 18)]
        [TestCase(60, 27)]
        public void PointFourFiveSeconds_Ceil_BakesExpectedTicks(
            int tickRate,
            int expectedTicks)
        {
            DurationAuthoring duration =
                DurationAuthoring.FromSeconds(0.45m);

            Assert.That(
                duration.BakeTicks(tickRate),
                Is.EqualTo(expectedTicks));
        }

        [TestCase(20, 20)]
        [TestCase(30, 30)]
        [TestCase(60, 60)]
        public void OneSecond_BakesAtConfiguredTickRate(
            int tickRate,
            int expectedTicks)
        {
            var duration = new DurationAuthoring(
                1_000,
                DurationRoundingPolicy.Ceil);

            Assert.That(
                duration.BakeTicks(tickRate),
                Is.EqualTo(expectedTicks));
        }

        [TestCase(20, 30)]
        [TestCase(30, 45)]
        [TestCase(60, 90)]
        public void OnePointFiveSeconds_BakesAtConfiguredTickRate(
            int tickRate,
            int expectedTicks)
        {
            var duration = new DurationAuthoring(
                1_500,
                DurationRoundingPolicy.Ceil);

            Assert.That(
                duration.BakeTicks(tickRate),
                Is.EqualTo(expectedTicks));
        }

        [Test]
        public void RoundingPolicies_AreExplicitAndIntegerOnly()
        {
            var ceil = new DurationAuthoring(
                125,
                DurationRoundingPolicy.Ceil);
            var nearest = new DurationAuthoring(
                125,
                DurationRoundingPolicy.Nearest);
            var floor = new DurationAuthoring(
                125,
                DurationRoundingPolicy.Floor);

            Assert.That(ceil.BakeTicks(20), Is.EqualTo(3));
            Assert.That(nearest.BakeTicks(20), Is.EqualTo(3));
            Assert.That(floor.BakeTicks(20), Is.EqualTo(2));
        }

        [TestCase(1)]
        [TestCase(8)]
        [TestCase(14)]
        [TestCase(30)]
        [TestCase(420)]
        public void LegacyThirtyTickMigration_PreservesBakedCount(
            int legacyTicks)
        {
            DurationAuthoring migrated =
                DurationAuthoring.FromLegacyTicks(legacyTicks);

            Assert.That(
                migrated.BakeTicks(30),
                Is.EqualTo(legacyTicks));
        }

        [TestCase(10)]
        [TestCase(20)]
        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void SupportedTickRate_AcceptsFiveTickSteps(int tickRate)
        {
            Assert.That(
                () => DeterministicTimeConversion
                    .ValidateSupportedTickRate(tickRate),
                Throws.Nothing);
        }

        [TestCase(0)]
        [TestCase(9)]
        [TestCase(11)]
        [TestCase(121)]
        public void SupportedTickRate_RejectsOutOfContractValues(
            int tickRate)
        {
            Assert.That(
                () => DeterministicTimeConversion
                    .ValidateSupportedTickRate(tickRate),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Conversion_ThrowsOnCheckedOverflow()
        {
            Assert.That(
                () => DurationAuthoring.FromSeconds(
                    decimal.MaxValue),
                Throws.TypeOf<System.OverflowException>());
        }
    }
}
