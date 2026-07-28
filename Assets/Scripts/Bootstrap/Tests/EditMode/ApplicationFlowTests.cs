using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class ApplicationFlowTests
    {
        [Test]
        public void TestAccount_CommandLineOverridesPersistedIdentity()
        {
            var persistence = new MemoryPersistence("persisted");
            var service = new TestAccountBootstrapService(
                persistence,
                () => "generated");

            ClientAccountSession session = service.Resolve(
                new[] { "--TestAccountId=command-line" });

            Assert.AreEqual(
                "command-line",
                session.TestAccountId);
            Assert.AreEqual(
                "command-line",
                persistence.Value);
        }

        [Test]
        public void Lobby_RequiresEveryAssignedPlayerAtFullReadyBarrier()
        {
            var lobby = new LobbySessionFlowNetwork(2);
            lobby.Assign(0, "a", 10, new TeamId(1), 0);
            lobby.Assign(1, "b", 11, new TeamId(2), 1);
            Ready(lobby, 0, 100);

            Assert.IsFalse(lobby.CanScheduleStart());
            Assert.Throws<InvalidOperationException>(() =>
                lobby.ScheduleStart(
                    "match",
                    1,
                    1,
                    2,
                    20,
                    3,
                    99,
                    1));

            Ready(lobby, 1, 101);
            GameStartConfig config = lobby.ScheduleStart(
                "match",
                1,
                1,
                2,
                20,
                3,
                99,
                1);

            Assert.AreEqual(23, config.StartTick);
            Assert.AreEqual(2, config.PlayerSlots.Length);
            Assert.AreEqual(0, config.PlayerSlots[0].PlayerSlot);
            Assert.AreEqual(1, config.PlayerSlots[1].PlayerSlot);
        }

        [Test]
        public void VersionHandshake_RejectsAnyCriticalMismatch()
        {
            var local = new FrameSyncVersionHandshake(
                1,
                2,
                3,
                CommandHeader.CurrentSchemaVersion,
                GameplaySnapshot.CurrentSchemaVersion);
            var remote = new FrameSyncVersionHandshake(
                1,
                2,
                4,
                CommandHeader.CurrentSchemaVersion,
                GameplaySnapshot.CurrentSchemaVersion);

            Assert.Throws<
                FrameSyncMoba.Deterministic
                    .DeterministicSimulationException>(
                () => local.RequireExactMatch(remote));
        }

        [Test]
        public void ClientFlow_ReachesLobbyOnlyAfterNgoConnects()
        {
            var persistence = new MemoryPersistence(null);
            var session = new FakeClientSession();
            var matchmaking = new FakeMatchmaking();
            var connection = new FakeConnection();
            var flow = new ClientApplicationFlow(
                new TestAccountBootstrapService(
                    persistence,
                    () => "account"),
                session,
                matchmaking,
                connection);

            flow.InitializeAccountAsync(Array.Empty<string>())
                .GetAwaiter().GetResult();
            flow.BeginMatchmakingAsync()
                .GetAwaiter().GetResult();
            Assert.IsTrue(
                flow.PollAssignmentAsync()
                    .GetAwaiter().GetResult());
            Assert.AreEqual(
                ClientApplicationState.ConnectingServer,
                flow.State);
            Assert.IsFalse(flow.PollConnection());

            connection.Connected = true;
            Assert.IsTrue(flow.PollConnection());
            Assert.AreEqual(
                ClientApplicationState.Lobby,
                flow.State);
        }

        [Test]
        public void WireCodec_RoundTripsAuthorityAndRecoveryEnvelopes()
        {
            UnitUid uid = new UnitUid(0, 10, 1);
            GameplayCommand command =
                GameplayCommand.CreateMove(
                    new CommandHeader(
                        1,
                        7,
                        0,
                        uid,
                        2,
                        GameplayCommandKind.None,
                        1,
                        0),
                    new fp2(fp.one, fp.zero));
            AuthorityFrame frame = AuthorityFrame.Create(
                2,
                3,
                4,
                new[] { command },
                AuthorityFrameFlags.None,
                123);
            AuthorityFrame decoded =
                FrameSyncWireCodec.ReadAuthorityFrame(
                    FrameSyncWireCodec.WriteAuthorityFrame(
                        frame));
            Assert.AreEqual(frame.Tick, decoded.Tick);
            Assert.AreEqual(
                frame.SharedGameplayChecksum,
                decoded.SharedGameplayChecksum);
            Assert.AreEqual(
                command.MoveTargetPoint,
                decoded.DecodeCommands()[0].MoveTargetPoint);

            var response = new AuthorityRecoveryResponse(
                9,
                new[] { frame });
            AuthorityRecoveryResponse decodedResponse =
                FrameSyncWireCodec.ReadRecoveryResponse(
                    FrameSyncWireCodec.WriteRecoveryResponse(
                        response));
            Assert.AreEqual(
                9u,
                decodedResponse.RequestSequence);
            Assert.AreEqual(
                1,
                decodedResponse.AuthorityFrames.Length);

            var result = new MatchResultState(
                "match",
                1,
                2,
                new TeamId(1),
                MatchEndReason.BaseDestroyed);
            MatchResultState decodedResult =
                FrameSyncWireCodec.ReadMatchResult(
                    FrameSyncWireCodec.WriteMatchResult(
                        result));
            Assert.AreEqual("match", decodedResult.MatchId);
            Assert.AreEqual(
                MatchEndReason.BaseDestroyed,
                decodedResult.EndReason);
        }

        [Test]
        public void DedicatedFlow_OrdersAllocationReadyAndShutdown()
        {
            var platform = new FakeDedicatedPlatform();
            var network = new FakeServerNetwork();
            var flow = new DedicatedServerApplicationFlow(
                platform,
                network);

            flow.BootAsync().GetAwaiter().GetResult();
            Assert.AreEqual(
                DedicatedServerApplicationState.AwaitAssignedPlayers,
                flow.State);
            CollectionAssert.AreEqual(
                new[] { "a", "b" },
                flow.Allocation.AccountIds);
            flow.EnterLobby();
            flow.BeginLoadingBarrier();
            flow.StartGameplay();
            flow.BeginResultDelivery();
            flow.BeginSettlement();
            flow.ShutdownAsync().GetAwaiter().GetResult();

            Assert.AreEqual(
                DedicatedServerApplicationState.Shutdown,
                flow.State);
            Assert.IsTrue(platform.ReadBeforeReady);
            Assert.IsTrue(platform.ShutdownCalled);
            Assert.IsFalse(network.IsListening);
        }

        private static void Ready(
            LobbySessionFlowNetwork lobby,
            int playerSlot,
            int heroConfigId)
        {
            string account = playerSlot == 0 ? "a" : "b";
            ulong client = (ulong)(10 + playerSlot);
            lobby.MarkConnected(playerSlot);
            lobby.VerifyIdentity(playerSlot, account, client);
            lobby.SelectHero(playerSlot, heroConfigId);
            lobby.LockHero(playerSlot);
            lobby.MarkGameplaySceneLoaded(playerSlot);
            lobby.MarkReady(playerSlot);
        }

        private sealed class MemoryPersistence :
            ITestAccountPersistence
        {
            public string Value;
            public MemoryPersistence(string value) => Value = value;
            public bool TryLoad(out string testAccountId)
            {
                testAccountId = Value;
                return !string.IsNullOrEmpty(Value);
            }
            public void Save(string testAccountId) =>
                Value = testAccountId;
        }

        private sealed class FakeClientSession :
            IUosClientSession
        {
            public Task InitializeAsync(string testAccountId) =>
                Task.CompletedTask;
        }

        private sealed class FakeMatchmaking :
            IMatchmakingApplicationClient
        {
            public Task<string> CreateTicketAsync(
                string accountId) =>
                Task.FromResult("ticket");

            public Task<GameServerAssignment?>
                PollAssignmentAsync(string ticketId) =>
                Task.FromResult<GameServerAssignment?>(
                    new GameServerAssignment(
                        "127.0.0.1",
                        7777,
                        "room"));

            public Task CancelTicketAsync(string ticketId) =>
                Task.CompletedTask;
        }

        private sealed class FakeConnection :
            IGameServerConnectionService
        {
            public bool Connected;
            public bool IsConnected => Connected;
            public void BeginConnect(
                in GameServerAssignment assignment)
            {
            }
            public void Disconnect() => Connected = false;
        }

        private sealed class FakeDedicatedPlatform :
            IDedicatedServerPlatform
        {
            public bool ReadBeforeReady;
            public bool ShutdownCalled;

            public Task<DedicatedServerAllocation>
                ReadAllocationAsync()
            {
                ReadBeforeReady = true;
                return Task.FromResult(
                    new DedicatedServerAllocation(
                        "match",
                        new[] { "b", "a" }));
            }

            public Task NotifyReadyAsync()
            {
                Assert.IsTrue(ReadBeforeReady);
                return Task.CompletedTask;
            }

            public Task ShutdownAsync()
            {
                ShutdownCalled = true;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeServerNetwork :
            IServerNetworkListener
        {
            public bool IsListening { get; private set; }
            public void StartServer() => IsListening = true;
            public void StopServer() => IsListening = false;
        }
    }
}
