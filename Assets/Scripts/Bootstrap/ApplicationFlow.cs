using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.Bootstrap
{
    public enum ClientApplicationState : byte
    {
        Boot = 0,
        AutoAccountInitializing = 1,
        AccountInitializeFailed = 2,
        MainMenu = 3,
        Matchmaking = 4,
        WaitingAssignment = 5,
        ConnectingServer = 6,
        Lobby = 7,
        LoadingGame = 8,
        InGame = 9,
        Ending = 10,
        Result = 11,
    }

    public enum DedicatedServerApplicationState : byte
    {
        ServerBoot = 0,
        ReadAllocation = 1,
        StartNetwork = 2,
        NotifyUosReady = 3,
        AwaitAssignedPlayers = 4,
        Lobby = 5,
        LoadingBarrier = 6,
        Gameplay = 7,
        ResultDelivery = 8,
        Settlement = 9,
        Shutdown = 10,
    }

    [Flags]
    public enum LobbyPlayerSlotState : byte
    {
        None = 0,
        Assigned = 1 << 0,
        Connected = 1 << 1,
        IdentityVerified = 1 << 2,
        HeroSelected = 1 << 3,
        HeroLocked = 1 << 4,
        GameplaySceneLoaded = 1 << 5,
        Ready = 1 << 6,
    }

    public readonly struct ClientAccountSession
    {
        public readonly string TestAccountId;

        public ClientAccountSession(string testAccountId)
        {
            if (string.IsNullOrWhiteSpace(testAccountId))
                throw new ArgumentException(
                    "TestAccountId is required.",
                    nameof(testAccountId));
            TestAccountId = testAccountId;
        }
    }

    /// <summary>
    /// Immutable per-player hero-select view broadcast to every endpoint so
    /// the Select page renders all players' choices identically.
    /// Presentation-only; the lobby slot state machine remains authoritative.
    /// </summary>
    public readonly struct LobbySelectionSnapshot
    {
        public readonly int PlayerSlot;
        public readonly string AccountId;
        public readonly int TeamId;
        public readonly int HeroConfigId;
        public readonly bool IsLocked;
        public readonly bool IsReady;

        public LobbySelectionSnapshot(
            int playerSlot,
            string accountId,
            int teamId,
            int heroConfigId,
            bool isLocked,
            bool isReady)
        {
            PlayerSlot = playerSlot;
            AccountId = accountId;
            TeamId = teamId;
            HeroConfigId = heroConfigId;
            IsLocked = isLocked;
            IsReady = isReady;
        }
    }

    public readonly struct GameServerAssignment
    {
        public readonly string IpAddress;
        public readonly ushort Port;
        public readonly string RoomId;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(IpAddress) &&
            Port > 0 &&
            !string.IsNullOrWhiteSpace(RoomId);

        public GameServerAssignment(
            string ipAddress,
            ushort port,
            string roomId)
        {
            IpAddress = ipAddress;
            Port = port;
            RoomId = roomId;
            if (!IsValid)
                throw new ArgumentException(
                    "A complete server assignment is required.");
        }
    }

    public interface ITestAccountPersistence
    {
        bool TryLoad(out string testAccountId);
        void Save(string testAccountId);
    }

    public interface IUosClientSession
    {
        Task InitializeAsync(string testAccountId);
    }

    public interface IMatchmakingApplicationClient
    {
        Task<string> CreateTicketAsync(string accountId);
        Task<GameServerAssignment?> PollAssignmentAsync(string ticketId);
        Task CancelTicketAsync(string ticketId);
    }

    public interface IGameServerConnectionService
    {
        bool IsConnected { get; }
        void BeginConnect(in GameServerAssignment assignment);
        void Disconnect();
    }

    public sealed class TestAccountBootstrapService
    {
        private const string ArgumentName = "TestAccountId";
        private readonly ITestAccountPersistence persistence;
        private readonly Func<string> generateId;

        public TestAccountBootstrapService(
            ITestAccountPersistence persistence,
            Func<string> generateId = null)
        {
            this.persistence = persistence ??
                throw new ArgumentNullException(nameof(persistence));
            this.generateId = generateId ??
                (() => Guid.NewGuid().ToString("N"));
        }

        public ClientAccountSession Resolve(string[] arguments)
        {
            string commandLine = ReadCommandLineId(arguments);
            if (!string.IsNullOrWhiteSpace(commandLine))
            {
                persistence.Save(commandLine);
                return new ClientAccountSession(commandLine);
            }
            if (persistence.TryLoad(out string persisted) &&
                !string.IsNullOrWhiteSpace(persisted))
                return new ClientAccountSession(persisted);

            string generated = generateId();
            if (string.IsNullOrWhiteSpace(generated))
                throw new InvalidOperationException(
                    "Test account generation returned an empty ID.");
            persistence.Save(generated);
            return new ClientAccountSession(generated);
        }

        private static string ReadCommandLineId(string[] arguments)
        {
            if (arguments == null) return null;
            for (int i = 0; i < arguments.Length; i++)
            {
                string argument = arguments[i];
                if (string.IsNullOrEmpty(argument)) continue;
                string prefix = "--" + ArgumentName + "=";
                if (argument.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                    return argument.Substring(prefix.Length);
                if ((string.Equals(
                         argument,
                         "-" + ArgumentName,
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(
                         argument,
                         "--" + ArgumentName,
                         StringComparison.OrdinalIgnoreCase)) &&
                    i + 1 < arguments.Length)
                    return arguments[i + 1];
            }
            return null;
        }
    }

    public sealed class ClientApplicationFlow
    {
        private readonly TestAccountBootstrapService accountBootstrap;
        private readonly IUosClientSession uosSession;
        private readonly IMatchmakingApplicationClient matchmaking;
        private readonly IGameServerConnectionService connection;
        private string ticketId;
        private bool cancelMatchmakingRequested;

        public ClientApplicationState State { get; private set; } =
            ClientApplicationState.Boot;
        public ClientAccountSession AccountSession { get; private set; }
        public GameServerAssignment Assignment { get; private set; }
        public MatchResultState? Result { get; private set; }

        public ClientApplicationFlow(
            TestAccountBootstrapService accountBootstrap,
            IUosClientSession uosSession,
            IMatchmakingApplicationClient matchmaking,
            IGameServerConnectionService connection)
        {
            this.accountBootstrap = accountBootstrap ??
                throw new ArgumentNullException(nameof(accountBootstrap));
            this.uosSession = uosSession ??
                throw new ArgumentNullException(nameof(uosSession));
            this.matchmaking = matchmaking ??
                throw new ArgumentNullException(nameof(matchmaking));
            this.connection = connection ??
                throw new ArgumentNullException(nameof(connection));
        }

        public async Task InitializeAccountAsync(string[] arguments)
        {
            RequireState(
                ClientApplicationState.Boot,
                ClientApplicationState.AccountInitializeFailed);
            State = ClientApplicationState.AutoAccountInitializing;
            try
            {
                AccountSession = accountBootstrap.Resolve(arguments);
                await uosSession.InitializeAsync(
                    AccountSession.TestAccountId);
                State = ClientApplicationState.MainMenu;
            }
            catch
            {
                State = ClientApplicationState.AccountInitializeFailed;
                throw;
            }
        }

        public async Task BeginMatchmakingAsync()
        {
            RequireState(ClientApplicationState.MainMenu);
            cancelMatchmakingRequested = false;
            ticketId = null;
            State = ClientApplicationState.Matchmaking;
            try
            {
                string createdTicketId =
                    await matchmaking.CreateTicketAsync(
                    AccountSession.TestAccountId);
                if (string.IsNullOrWhiteSpace(createdTicketId))
                    throw new InvalidOperationException(
                        "Matchmaking returned an empty ticket ID.");
                if (cancelMatchmakingRequested)
                {
                    await matchmaking.CancelTicketAsync(
                        createdTicketId);
                    State = ClientApplicationState.MainMenu;
                    return;
                }
                ticketId = createdTicketId;
                State = ClientApplicationState.WaitingAssignment;
            }
            catch
            {
                ticketId = null;
                State = ClientApplicationState.MainMenu;
                throw;
            }
        }

        public async Task<bool> PollAssignmentAsync()
        {
            RequireState(ClientApplicationState.WaitingAssignment);
            GameServerAssignment? assignment =
                await matchmaking.PollAssignmentAsync(ticketId);
            if (cancelMatchmakingRequested ||
                State != ClientApplicationState.WaitingAssignment)
                return false;
            if (!assignment.HasValue) return false;
            Assignment = assignment.Value;
            State = ClientApplicationState.ConnectingServer;
            connection.BeginConnect(Assignment);
            return true;
        }

        public async Task CancelMatchmakingAsync()
        {
            RequireState(
                ClientApplicationState.Matchmaking,
                ClientApplicationState.WaitingAssignment);
            cancelMatchmakingRequested = true;
            string activeTicketId = ticketId;
            ticketId = null;
            Assignment = default;
            connection.Disconnect();
            State = ClientApplicationState.MainMenu;

            if (!string.IsNullOrWhiteSpace(activeTicketId))
                await matchmaking.CancelTicketAsync(activeTicketId);
        }

        public bool PollConnection()
        {
            RequireState(ClientApplicationState.ConnectingServer);
            if (!connection.IsConnected) return false;
            State = ClientApplicationState.Lobby;
            return true;
        }

        public void BeginLoadingGame()
        {
            RequireState(ClientApplicationState.Lobby);
            State = ClientApplicationState.LoadingGame;
        }

        public void EnterGame()
        {
            RequireState(ClientApplicationState.LoadingGame);
            State = ClientApplicationState.InGame;
        }

        public void ConfirmAuthorityMatchEnd()
        {
            RequireState(ClientApplicationState.InGame);
            State = ClientApplicationState.Ending;
        }

        public void ApplyMatchResult(
            in MatchResultState result,
            MatchRuleRuntime matchRule,
            int latestAuthorityFrameTick)
        {
            RequireState(ClientApplicationState.Ending);
            result.ValidateAgainst(
                matchRule,
                latestAuthorityFrameTick);
            Result = result;
            State = ClientApplicationState.Result;
        }

        public void ReturnToMainMenu()
        {
            RequireState(ClientApplicationState.Result);
            connection.Disconnect();
            ticketId = null;
            Assignment = default;
            Result = null;
            State = ClientApplicationState.MainMenu;
        }

        private void RequireState(
            ClientApplicationState first,
            ClientApplicationState? second = null)
        {
            if (State != first &&
                (!second.HasValue || State != second.Value))
                throw new InvalidOperationException(
                    $"Client application transition is invalid from {State}.");
        }
    }

    public sealed class LobbySessionFlowNetwork
    {
        private readonly LobbySlot[] slots;

        public int GameStartPlayerCount => slots.Length;
        public bool IsStartScheduled { get; private set; }
        public int ScheduledStartTick { get; private set; } = -1;

        public LobbySessionFlowNetwork(int gameStartPlayerCount)
        {
            if (gameStartPlayerCount < 1 ||
                gameStartPlayerCount > 10)
                throw new ArgumentOutOfRangeException(
                    nameof(gameStartPlayerCount));
            slots = new LobbySlot[gameStartPlayerCount];
        }

        public void Assign(
            int playerSlot,
            string accountId,
            ulong controllerClientId,
            TeamId teamId,
            int spawnPointId)
        {
            RequireSlot(playerSlot);
            if ((slots[playerSlot].State &
                    LobbyPlayerSlotState.Assigned) != 0)
                throw new InvalidOperationException(
                    $"PlayerSlot {playerSlot} is already assigned.");
            for (int i = 0; i < slots.Length; i++)
            {
                if ((slots[i].State &
                        LobbyPlayerSlotState.Assigned) == 0)
                    continue;
                if (slots[i].AccountId == accountId ||
                    slots[i].ControllerClientId ==
                        controllerClientId)
                    throw new InvalidOperationException(
                        "Lobby account and controller bindings must be unique.");
            }
            slots[playerSlot] = new LobbySlot
            {
                State = LobbyPlayerSlotState.Assigned,
                AccountId = accountId,
                ControllerClientId = controllerClientId,
                TeamId = teamId,
                SpawnPointId = spawnPointId,
            };
        }

        public void MarkConnected(int playerSlot) =>
            AddState(playerSlot, LobbyPlayerSlotState.Connected);

        public void VerifyIdentity(
            int playerSlot,
            string accountId,
            ulong controllerClientId)
        {
            LobbySlot slot = GetAssigned(playerSlot);
            if (slot.AccountId != accountId ||
                slot.ControllerClientId != controllerClientId)
                throw new InvalidOperationException(
                    "Lobby identity does not match its assigned PlayerSlot.");
            AddState(
                playerSlot,
                LobbyPlayerSlotState.IdentityVerified);
        }

        public void SelectHero(int playerSlot, int heroConfigId)
        {
            if (heroConfigId <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(heroConfigId));
            LobbySlot slot = GetAssigned(playerSlot);
            if ((slot.State & LobbyPlayerSlotState.HeroLocked) != 0)
                throw new InvalidOperationException(
                    "A locked hero selection cannot change.");
            slot.HeroConfigId = heroConfigId;
            slot.State |= LobbyPlayerSlotState.HeroSelected;
            slots[playerSlot] = slot;
        }

        public void LockHero(int playerSlot)
        {
            LobbySlot slot = GetAssigned(playerSlot);
            RequireFlag(slot, LobbyPlayerSlotState.HeroSelected);
            AddState(playerSlot, LobbyPlayerSlotState.HeroLocked);
        }

        public void MarkGameplaySceneLoaded(int playerSlot) =>
            AddState(
                playerSlot,
                LobbyPlayerSlotState.GameplaySceneLoaded);

        public void MarkReady(int playerSlot)
        {
            LobbySlot slot = GetAssigned(playerSlot);
            RequireFlag(slot, LobbyPlayerSlotState.Connected);
            RequireFlag(slot, LobbyPlayerSlotState.IdentityVerified);
            RequireFlag(slot, LobbyPlayerSlotState.HeroLocked);
            RequireFlag(
                slot,
                LobbyPlayerSlotState.GameplaySceneLoaded);
            AddState(playerSlot, LobbyPlayerSlotState.Ready);
        }

        public bool CanScheduleStart()
        {
            const LobbyPlayerSlotState required =
                LobbyPlayerSlotState.Assigned |
                LobbyPlayerSlotState.Connected |
                LobbyPlayerSlotState.IdentityVerified |
                LobbyPlayerSlotState.HeroSelected |
                LobbyPlayerSlotState.HeroLocked |
                LobbyPlayerSlotState.GameplaySceneLoaded |
                LobbyPlayerSlotState.Ready;
            for (int i = 0; i < slots.Length; i++)
                if ((slots[i].State & required) != required)
                    return false;
            return true;
        }

        public GameStartConfig ScheduleStart(
            string matchId,
            int gameModeId,
            int mapConfigId,
            int teamCount,
            int serverTick,
            int startLeadTicks,
            uint initialRandomSeed,
            uint gameplayDataVersion)
        {
            if (IsStartScheduled)
                throw new InvalidOperationException(
                    "Lobby start is already scheduled.");
            if (!CanScheduleStart())
                throw new InvalidOperationException(
                    "Every assigned player must pass the full Ready barrier.");
            if (serverTick < 0 || startLeadTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(serverTick));
            int startTick = checked(serverTick + startLeadTicks);
            var playerSlots =
                new PlayerSlotConfig[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                LobbySlot slot = slots[i];
                playerSlots[i] = new PlayerSlotConfig(
                    i,
                    slot.AccountId,
                    slot.ControllerClientId,
                    slot.TeamId,
                    slot.HeroConfigId,
                    slot.SpawnPointId);
            }
            IsStartScheduled = true;
            ScheduledStartTick = startTick;
            return new GameStartConfig(
                matchId,
                gameModeId,
                mapConfigId,
                slots.Length,
                teamCount,
                playerSlots,
                startTick,
                initialRandomSeed,
                gameplayDataVersion);
        }

        public LobbyPlayerSlotState GetState(int playerSlot)
        {
            RequireSlot(playerSlot);
            return slots[playerSlot].State;
        }

        public int SlotCount => slots.Length;

        /// <summary>
        /// Read-only per-slot view used to broadcast the complete hero-select
        /// state to every endpoint.
        /// </summary>
        public LobbySelectionSnapshot GetSelectionSnapshot(
            int playerSlot)
        {
            RequireSlot(playerSlot);
            LobbySlot slot = slots[playerSlot];
            return new LobbySelectionSnapshot(
                playerSlot,
                slot.AccountId,
                slot.TeamId.Value,
                slot.HeroConfigId,
                (slot.State &
                    LobbyPlayerSlotState.HeroLocked) != 0,
                (slot.State &
                    LobbyPlayerSlotState.Ready) != 0);
        }

        /// <summary>
        /// Same-team duplicate rule: a hero already selected by another slot
        /// on the same team cannot be picked again.
        /// </summary>
        public bool IsHeroBlockedInTeam(
            int playerSlot,
            int heroConfigId)
        {
            if (heroConfigId <= 0)
            {
                return false;
            }
            LobbySlot self = GetAssigned(playerSlot);
            for (int i = 0;
                 i < slots.Length;
                 i++)
            {
                if (i == playerSlot)
                {
                    continue;
                }
                LobbySlot other = slots[i];
                if ((other.State &
                        LobbyPlayerSlotState.Assigned) == 0 ||
                    other.TeamId != self.TeamId)
                {
                    continue;
                }
                if (other.HeroConfigId == heroConfigId)
                {
                    return true;
                }
            }
            return false;
        }

        private void AddState(
            int playerSlot,
            LobbyPlayerSlotState state)
        {
            LobbySlot slot = GetAssigned(playerSlot);
            slot.State |= state;
            slots[playerSlot] = slot;
        }

        private LobbySlot GetAssigned(int playerSlot)
        {
            RequireSlot(playerSlot);
            LobbySlot slot = slots[playerSlot];
            RequireFlag(slot, LobbyPlayerSlotState.Assigned);
            return slot;
        }

        private void RequireSlot(int playerSlot)
        {
            if ((uint)playerSlot >= (uint)slots.Length)
                throw new ArgumentOutOfRangeException(
                    nameof(playerSlot));
        }

        private static void RequireFlag(
            in LobbySlot slot,
            LobbyPlayerSlotState flag)
        {
            if ((slot.State & flag) == 0)
                throw new InvalidOperationException(
                    $"Lobby state requires {flag}.");
        }

        private struct LobbySlot
        {
            public LobbyPlayerSlotState State;
            public string AccountId;
            public ulong ControllerClientId;
            public TeamId TeamId;
            public int HeroConfigId;
            public int SpawnPointId;
        }
    }

    public readonly struct DedicatedServerAllocation
    {
        private readonly string[] accountIds;
        public readonly string MatchId;
        public string[] AccountIds =>
            accountIds == null
                ? Array.Empty<string>()
                : (string[])accountIds.Clone();

        public DedicatedServerAllocation(
            string matchId,
            string[] accountIds)
        {
            if (string.IsNullOrWhiteSpace(matchId) ||
                accountIds == null ||
                accountIds.Length == 0)
                throw new ArgumentException(
                    "Dedicated Server allocation is incomplete.");
            MatchId = matchId;
            this.accountIds = (string[])accountIds.Clone();
            Array.Sort(this.accountIds, StringComparer.Ordinal);
            for (int i = 0; i < this.accountIds.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(
                        this.accountIds[i]) ||
                    (i > 0 &&
                     this.accountIds[i - 1] ==
                        this.accountIds[i]))
                    throw new ArgumentException(
                        "Allocation account IDs must be nonempty and unique.",
                        nameof(accountIds));
            }
        }
    }

    public interface IDedicatedServerPlatform
    {
        Task<DedicatedServerAllocation> ReadAllocationAsync();
        Task NotifyReadyAsync();
        Task ShutdownAsync();
    }

    public interface IServerNetworkListener
    {
        bool IsListening { get; }
        void StartServer();
        void StopServer();
    }

    public sealed class DedicatedServerApplicationFlow
    {
        private readonly IDedicatedServerPlatform platform;
        private readonly IServerNetworkListener network;

        public DedicatedServerApplicationState State { get; private set; } =
            DedicatedServerApplicationState.ServerBoot;
        public DedicatedServerAllocation Allocation { get; private set; }

        public DedicatedServerApplicationFlow(
            IDedicatedServerPlatform platform,
            IServerNetworkListener network)
        {
            this.platform = platform ??
                throw new ArgumentNullException(nameof(platform));
            this.network = network ??
                throw new ArgumentNullException(nameof(network));
        }

        public async Task BootAsync()
        {
            RequireState(
                DedicatedServerApplicationState.ServerBoot);
            State =
                DedicatedServerApplicationState.ReadAllocation;
            Allocation = await platform.ReadAllocationAsync();
            State =
                DedicatedServerApplicationState.StartNetwork;
            network.StartServer();
            if (!network.IsListening)
                throw new InvalidOperationException(
                    "NGO server failed to start listening.");
            State =
                DedicatedServerApplicationState.NotifyUosReady;
            await platform.NotifyReadyAsync();
            State =
                DedicatedServerApplicationState.AwaitAssignedPlayers;
        }

        public void EnterLobby()
        {
            RequireState(
                DedicatedServerApplicationState.AwaitAssignedPlayers);
            State = DedicatedServerApplicationState.Lobby;
        }

        public void BeginLoadingBarrier()
        {
            RequireState(DedicatedServerApplicationState.Lobby);
            State =
                DedicatedServerApplicationState.LoadingBarrier;
        }

        public void StartGameplay()
        {
            RequireState(
                DedicatedServerApplicationState.LoadingBarrier);
            State = DedicatedServerApplicationState.Gameplay;
        }

        public void BeginResultDelivery()
        {
            RequireState(DedicatedServerApplicationState.Gameplay);
            State =
                DedicatedServerApplicationState.ResultDelivery;
        }

        public void BeginSettlement()
        {
            RequireState(
                DedicatedServerApplicationState.ResultDelivery);
            State = DedicatedServerApplicationState.Settlement;
        }

        public async Task ShutdownAsync()
        {
            RequireState(
                DedicatedServerApplicationState.Settlement);
            await platform.ShutdownAsync();
            network.StopServer();
            State = DedicatedServerApplicationState.Shutdown;
        }

        private void RequireState(
            DedicatedServerApplicationState expected)
        {
            if (State != expected)
                throw new InvalidOperationException(
                    $"Dedicated Server transition is invalid from {State}.");
        }
    }

    public sealed class GameApplicationFlowManager
    {
        public ClientApplicationFlow Client { get; }
        public DedicatedServerApplicationFlow DedicatedServer { get; }

        public GameApplicationFlowManager(
            ClientApplicationFlow client)
        {
            Client = client ??
                throw new ArgumentNullException(nameof(client));
        }

        public GameApplicationFlowManager(
            DedicatedServerApplicationFlow dedicatedServer)
        {
            DedicatedServer = dedicatedServer ??
                throw new ArgumentNullException(
                    nameof(dedicatedServer));
        }
    }
}
