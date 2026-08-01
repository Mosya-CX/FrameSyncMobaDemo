using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class BootstrapPayloadWireCodecTests
    {
        [Test]
        public void Payload_RoundTrip_IsCanonical()
        {
            UnitUid uid =
                new UnitUid(4, 1001, 0);
            GameplaySnapshot snapshot =
                GameplaySnapshot.CreateEmpty();
            snapshot.RandomState =
                new DeterministicRandomSnapshot(
                    123u);
            snapshot.UnitWorldState.Units =
                new[]
                {
                    new UnitSnapshot
                    {
                        UnitUid = uid,
                        TeamId = new TeamId(1),
                        UnitPrototypeId = 1001,
                        LifeState = LifeState.Alive,
                    },
                };
            var versions =
                new FrameSyncVersionHandshake(
                    1,
                    1,
                    1,
                    1,
                    (uint)GameplaySnapshot
                        .CurrentSchemaVersion);
            var config = new GameStartConfig(
                "wire-round-trip",
                1,
                1,
                1,
                1,
                new[]
                {
                    new PlayerSlotConfig(
                        0,
                        "account-0",
                        7,
                        new TeamId(1),
                        1001,
                        0),
                },
                4,
                123u,
                1);
            var payload =
                new GameBootstrapPayload(
                    config,
                    versions,
                    snapshot,
                    4,
                    4,
                    123u,
                    new[]
                    {
                        new PlayerSlotUnitMapping(
                            0,
                            uid),
                    });

            byte[] first =
                BootstrapPayloadWireCodec.Write(
                    payload);
            GameBootstrapPayload restored =
                BootstrapPayloadWireCodec.Read(
                    first);
            byte[] second =
                BootstrapPayloadWireCodec.Write(
                    restored);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(
                restored.PlayerSlotMappings[0]
                    .ControlledUnitUid,
                Is.EqualTo(uid));
            Assert.That(
                restored.InitialGameplaySnapshot
                    .RandomState.State,
                Is.EqualTo(123u));
        }

        [Test]
        public void Payload_TrailingBytes_AreRejected()
        {
            Assert.Throws<
                DeterministicSimulationException>(
                () => BootstrapPayloadWireCodec
                    .Read(new byte[]
                    {
                        1,
                        2,
                        3,
                    }));
        }
    }
}
