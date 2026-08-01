using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using NUnit.Framework;

namespace FrameSyncMoba.FrameSync.Tests
{
    public sealed class GameBootstrapPayloadContractTests
    {
        [Test]
        public void Payload_PreservesFrozenPlayerSlotMappings()
        {
            UnitUid uid = new UnitUid(0, 1001, 0);
            GameStartConfig config = CreateConfig();
            GameplaySnapshot snapshot = CreateSnapshot(
                uid,
                new TeamId(1));
            FrameSyncVersionHandshake versions =
                CreateVersions();
            var source = new[]
            {
                new PlayerSlotUnitMapping(0, uid),
            };

            var payload = new GameBootstrapPayload(
                config,
                versions,
                snapshot,
                3,
                3,
                123u,
                source);
            source[0] = default;
            PlayerSlotUnitMapping[] firstRead =
                payload.PlayerSlotMappings;
            firstRead[0] = default;

            Assert.That(
                payload.PlayerSlotMappings[0]
                    .ControlledUnitUid,
                Is.EqualTo(uid));
        }

        [Test]
        public void Payload_MappingTeamMismatch_FailsDeterministically()
        {
            UnitUid uid = new UnitUid(0, 1001, 0);

            Assert.Throws<DeterministicSimulationException>(
                () => new GameBootstrapPayload(
                    CreateConfig(),
                    CreateVersions(),
                    CreateSnapshot(
                        uid,
                        new TeamId(2)),
                    3,
                    3,
                    123u,
                    new[]
                    {
                        new PlayerSlotUnitMapping(
                            0,
                            uid),
                    }));
        }

        [Test]
        public void VersionHandshake_AnyCriticalMismatch_Fails()
        {
            FrameSyncVersionHandshake local =
                CreateVersions();
            var remote =
                new FrameSyncVersionHandshake(
                    1,
                    2,
                    1,
                    1,
                    (uint)GameplaySnapshot
                        .CurrentSchemaVersion);

            Assert.Throws<DeterministicSimulationException>(
                () => local.RequireExactMatch(
                    remote));
        }

        private static GameStartConfig CreateConfig()
        {
            return new GameStartConfig(
                "payload-test",
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
                3,
                123u,
                1u);
        }

        private static FrameSyncVersionHandshake
            CreateVersions()
        {
            return new FrameSyncVersionHandshake(
                1,
                1,
                1,
                1,
                (uint)GameplaySnapshot
                    .CurrentSchemaVersion);
        }

        private static GameplaySnapshot CreateSnapshot(
            UnitUid uid,
            TeamId teamId)
        {
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
                        TeamId = teamId,
                        UnitPrototypeId = 1001,
                    },
                };
            return snapshot;
        }
    }
}
