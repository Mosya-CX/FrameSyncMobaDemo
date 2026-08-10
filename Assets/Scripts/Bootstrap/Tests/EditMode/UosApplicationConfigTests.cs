using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class UosApplicationConfigTests
    {
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase(" true ", true)]
        [TestCase("false", false)]
        [TestCase("1", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsProfileTestServer_RecognizesOnlyTrue(
            string environmentValue,
            bool expected)
        {
            Assert.That(
                UosApplicationConfig.IsProfileTestServer(
                    environmentValue),
                Is.EqualTo(expected));
        }

        [TestCase(true, null, true)]
        [TestCase(true, "false", true)]
        [TestCase(false, "true", true)]
        [TestCase(false, "false", false)]
        [TestCase(false, null, false)]
        public void IsProfileTestServer_PrefersMultiverseServerInfo(
            bool multiverseServerInfoFlag,
            string environmentValue,
            bool expected)
        {
            Assert.That(
                UosApplicationConfig.IsProfileTestServer(
                    multiverseServerInfoFlag,
                    environmentValue),
                Is.EqualTo(expected));
        }
    }
}
