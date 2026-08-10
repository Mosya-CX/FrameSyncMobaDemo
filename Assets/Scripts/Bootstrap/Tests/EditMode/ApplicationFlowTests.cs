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
        public void ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode()
        {
            Assert.IsTrue(
                LocalNgoEndpointDriver
                    .OwnsClientConnectionLifecycle(
                        FrameFlowMode.LocalDirect),
                "Local direct clients are owned by LocalNgoEndpointDriver.");
            Assert.IsFalse(
                LocalNgoEndpointDriver
                    .OwnsClientConnectionLifecycle(
                        FrameFlowMode.UosOnline),
                "UOS clients are owned exclusively by LobbyFlowController.");
        }

        [Test]
        public void
            ClientFlow_CancelWaitingAssignment_DeletesTicketAndReturnsMain()
        {
            var matchmaking = new FakeMatchmaking();
            var connection = new FakeConnection
            {
                Connected = true,
            };
            var flow = CreateClientFlow(
                matchmaking,
                connection);

            flow.InitializeAccountAsync(Array.Empty<string>())
                .GetAwaiter().GetResult();
            flow.BeginMatchmakingAsync()
                .GetAwaiter().GetResult();
            flow.CancelMatchmakingAsync()
                .GetAwaiter().GetResult();

            Assert.AreEqual(
                ClientApplicationState.MainMenu,
                flow.State);
            Assert.AreEqual(
                "ticket",
                matchmaking.CancelledTicketId);
            Assert.IsFalse(connection.IsConnected);
        }

        [Test]
        public void
            ClientFlow_CancelWhileTicketCreationIsPending_DeletesLateTicket()
        {
            var matchmaking = new FakeMatchmaking
            {
                PendingTicket =
                    new TaskCompletionSource<string>(),
            };
            var flow = CreateClientFlow(
                matchmaking,
                new FakeConnection());

            flow.InitializeAccountAsync(Array.Empty<string>())
                .GetAwaiter().GetResult();
            Task beginTask = flow.BeginMatchmakingAsync();
            flow.CancelMatchmakingAsync()
                .GetAwaiter().GetResult();
            matchmaking.PendingTicket.SetResult(
                "late-ticket");
            beginTask.GetAwaiter().GetResult();

            Assert.AreEqual(
                ClientApplicationState.MainMenu,
                flow.State);
            Assert.AreEqual(
                "late-ticket",
                matchmaking.CancelledTicketId);
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

        [Test]
        public void Lobby_SameTeamDuplicateHeroIsBlocked()
        {
            var lobby = new LobbySessionFlowNetwork(3);
            lobby.Assign(0, "a", 10, new TeamId(1), 0);
            lobby.Assign(1, "b", 11, new TeamId(1), 1);
            lobby.Assign(2, "c", 12, new TeamId(2), 2);
            lobby.SelectHero(0, 1001);

            Assert.IsTrue(
                lobby.IsHeroBlockedInTeam(1, 1001),
                "A teammate's selected hero must be blocked.");
            Assert.IsFalse(
                lobby.IsHeroBlockedInTeam(1, 1002),
                "A different hero stays selectable.");
            Assert.IsFalse(
                lobby.IsHeroBlockedInTeam(2, 1001),
                "An opposing team may pick the same hero.");
            Assert.IsFalse(
                lobby.IsHeroBlockedInTeam(0, 1001),
                "The selecting player itself is not blocked.");
        }

        [Test]
        public void Lobby_SelectionSnapshot_ReflectsHeroAndFlags()
        {
            var lobby = new LobbySessionFlowNetwork(2);
            lobby.Assign(0, "a", 10, new TeamId(1), 0);
            lobby.Assign(1, "b", 11, new TeamId(2), 1);
            lobby.SelectHero(0, 1001);

            LobbySelectionSnapshot before =
                lobby.GetSelectionSnapshot(0);
            Assert.AreEqual(1001, before.HeroConfigId);
            Assert.IsFalse(before.IsLocked);
            Assert.IsFalse(before.IsReady);
            Assert.AreEqual(1, before.TeamId);
            Assert.AreEqual("a", before.AccountId);

            lobby.LockHero(0);
            LobbySelectionSnapshot after =
                lobby.GetSelectionSnapshot(0);
            Assert.IsTrue(after.IsLocked);
        }

        [Test]
        public void LobbyState_WireCodec_RoundTripsFullSelection()
        {
            var snapshots = new[]
            {
                new LobbySelectionSnapshot(
                    0,
                    "alpha",
                    1,
                    1001,
                    true,
                    false),
                new LobbySelectionSnapshot(
                    1,
                    "beta",
                    2,
                    0,
                    false,
                    false),
            };

            LobbySelectionSnapshot[] decoded =
                LobbyWireCodec.ReadLobbyState(
                    LobbyWireCodec.WriteLobbyState(
                        snapshots));

            Assert.AreEqual(2, decoded.Length);
            Assert.AreEqual(0, decoded[0].PlayerSlot);
            Assert.AreEqual(
                "alpha",
                decoded[0].AccountId);
            Assert.AreEqual(1, decoded[0].TeamId);
            Assert.AreEqual(1001, decoded[0].HeroConfigId);
            Assert.IsTrue(decoded[0].IsLocked);
            Assert.IsFalse(decoded[0].IsReady);
            Assert.AreEqual(1, decoded[1].PlayerSlot);
            Assert.AreEqual("beta", decoded[1].AccountId);
            Assert.AreEqual(0, decoded[1].HeroConfigId);
            Assert.IsFalse(decoded[1].IsLocked);
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

        private static ClientApplicationFlow
            CreateClientFlow(
                FakeMatchmaking matchmaking,
                FakeConnection connection)
        {
            return new ClientApplicationFlow(
                new TestAccountBootstrapService(
                    new MemoryPersistence(null),
                    () => "account"),
                new FakeClientSession(),
                matchmaking,
                connection);
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
            public TaskCompletionSource<string>
                PendingTicket;
            public string CancelledTicketId;

            public Task<string> CreateTicketAsync(
                string accountId) =>
                PendingTicket?.Task ??
                Task.FromResult("ticket");

            public Task<GameServerAssignment?>
                PollAssignmentAsync(string ticketId) =>
                Task.FromResult<GameServerAssignment?>(
                    new GameServerAssignment(
                        "127.0.0.1",
                        7777,
                        "room"));

            public Task CancelTicketAsync(string ticketId)
            {
                CancelledTicketId = ticketId;
                return Task.CompletedTask;
            }
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
