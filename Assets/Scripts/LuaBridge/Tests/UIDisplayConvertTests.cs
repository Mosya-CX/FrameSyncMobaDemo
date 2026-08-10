using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.LuaBridge.Tests
{
    /// <summary>
    /// Fixed conversion rules from design v9.1 section 8: resources floor,
    /// ordinary stats round, percents and 0..1 rates stay consistent.
    /// </summary>
    public sealed class UIDisplayConvertTests
    {
        [Test]
        public void ResourceInt_Floors_And_ClampsAtZero()
        {
            Assert.That(
                UIDisplayConvert.ResourceInt((fp)100.8m),
                Is.EqualTo(100));
            Assert.That(
                UIDisplayConvert.ResourceInt((fp)(-3.2m)),
                Is.EqualTo(0));
        }

        [Test]
        public void StatInt_Rounds()
        {
            Assert.That(
                UIDisplayConvert.StatInt((fp)185.6m),
                Is.EqualTo(186));
            Assert.That(
                UIDisplayConvert.StatInt((fp)184.4m),
                Is.EqualTo(184));
        }

        [Test]
        public void Decimal2_RoundsToTwoPlaces()
        {
            Assert.That(
                UIDisplayConvert.Decimal2((fp)1.326m),
                Is.EqualTo(1.33f));
        }

        [Test]
        public void PercentInt_MultipliesByHundred()
        {
            Assert.That(
                UIDisplayConvert.PercentInt((fp)0.4m),
                Is.EqualTo(40));
        }

        [Test]
        public void Rate01_ClampsAndHandlesZeroMax()
        {
            Assert.That(
                UIDisplayConvert.Rate01(
                    (fp)50m,
                    (fp)100m),
                Is.EqualTo(0.5f));
            Assert.That(
                UIDisplayConvert.Rate01(
                    (fp)120m,
                    (fp)100m),
                Is.EqualTo(1f));
            Assert.That(
                UIDisplayConvert.Rate01(
                    (fp)10m,
                    fp.zero),
                Is.EqualTo(0f));
        }
    }
}
