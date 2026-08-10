using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class TowerAttackHandlerTests
    {
        [Test]
        public void HeroRamp_FirstHitIsBase_ThenMultipliesByOnePointFive()
        {
            fp baseDamage = (fp)180m;
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 0),
                Is.EqualTo((fp)180m));
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 1),
                Is.EqualTo((fp)270m));
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    baseDamage, 2),
                Is.EqualTo((fp)405m));
        }

        [Test]
        public void HeroRamp_CapsAtSixHundred()
        {
            fp baseDamage = (fp)180m;
            for (int hits = 3; hits < 20; hits++)
            {
                Assert.That(
                    TowerAttackHandler.ResolveRampDamage(
                        baseDamage, hits),
                    Is.EqualTo((fp)600m));
            }
        }

        [Test]
        public void Ramp_WithZeroHits_ReturnsBaseEvenForSmallBase()
        {
            Assert.That(
                TowerAttackHandler.ResolveRampDamage(
                    (fp)10m, 0),
                Is.EqualTo((fp)10m));
        }
    }
}
