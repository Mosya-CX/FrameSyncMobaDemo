using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class LobbyWireCodecTests
    {
        [Test]
        public void Identity_RoundTrip_PreservesSlotAccountAndVersions()
        {
            var versions =
                new FrameSyncVersionHandshake(
                    1,
                    2,
                    3,
                    4,
                    5);

            LobbyIdentity identity =
                LobbyWireCodec.ReadIdentity(
                    LobbyWireCodec.WriteIdentity(
                        1,
                        "LocalPlayer1",
                        versions));

            Assert.That(
                identity.PlayerSlot,
                Is.EqualTo(1));
            Assert.That(
                identity.AccountId,
                Is.EqualTo("LocalPlayer1"));
            Assert.That(
                identity.Versions
                    .SnapshotSchemaVersion,
                Is.EqualTo(5));
        }

        [Test]
        public void MalformedLobbyPayload_FailsDeterministically()
        {
            Assert.Throws<
                DeterministicSimulationException>(
                () => LobbyWireCodec
                    .ReadIdentity(
                        new byte[]
                        {
                            1,
                        }));
            Assert.Throws<
                DeterministicSimulationException>(
                () => LobbyWireCodec
                    .ReadMarker(
                        new byte[]
                        {
                            0,
                        }));
        }
    }
}
