using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.UOS.Auth;
using Unity.UOS.Matchmaking;
using Unity.UOS.Matchmaking.Server;
using Unity.UOS.Multiverse;
using UnityEngine;
using UosClientPlayer =
    Unity.UOS.Matchmaking.Model.Player;
using UosClientTicket =
    Unity.UOS.Matchmaking.Model.Ticket;
using UosServerMatch =
    Unity.UOS.Matchmaking.Server.Model.Match;
using UosServerTeam =
    Unity.UOS.Matchmaking.Server.Model.Team;

namespace FrameSyncMoba.Bootstrap
{
    public sealed class PlayerPrefsTestAccountPersistence :
        ITestAccountPersistence
    {
        private const string Key =
            "FrameSyncMoba.TestAccountId";

        public bool TryLoad(out string testAccountId)
        {
            testAccountId = PlayerPrefs.GetString(Key, null);
            return !string.IsNullOrWhiteSpace(testAccountId);
        }

        public void Save(string testAccountId)
        {
            if (string.IsNullOrWhiteSpace(testAccountId))
                throw new ArgumentException(
                    "TestAccountId is required.",
                    nameof(testAccountId));
            PlayerPrefs.SetString(Key, testAccountId);
            PlayerPrefs.Save();
        }
    }

    public sealed class UosClientSession : IUosClientSession
    {
        public async Task InitializeAsync(string testAccountId)
        {
            if (string.IsNullOrWhiteSpace(testAccountId))
                throw new ArgumentException(
                    "TestAccountId is required.",
                    nameof(testAccountId));
            MatchmakingSDK.Initialize();
            await AuthTokenManager.ExternalLogin(testAccountId);
        }
    }

    public sealed class UosMatchmakingApplicationClient :
        IMatchmakingApplicationClient
    {
        private readonly string matchmakingConfigId;
        private readonly string regionId;

        public UosMatchmakingApplicationClient(
            string matchmakingConfigId,
            string regionId = null)
        {
            if (string.IsNullOrWhiteSpace(matchmakingConfigId))
                throw new ArgumentException(
                    "A UOS Matchmaking config ID is required.",
                    nameof(matchmakingConfigId));
            this.matchmakingConfigId = matchmakingConfigId;
            this.regionId = regionId;
        }

        public Task<string> CreateTicketAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException(
                    "AccountId is required.",
                    nameof(accountId));
            var players = new List<UosClientPlayer>(1)
            {
                new UosClientPlayer { id = accountId },
            };
            return MatchmakingSDK.Instance.CreateTicketAsync(
                matchmakingConfigId,
                players,
                regionId);
        }

        public async Task<GameServerAssignment?>
            PollAssignmentAsync(string ticketId)
        {
            if (string.IsNullOrWhiteSpace(ticketId))
                throw new ArgumentException(
                    "TicketId is required.",
                    nameof(ticketId));
            UosClientTicket ticket =
                await MatchmakingSDK.Instance.GetTicketAsync(ticketId);
            if (ticket == null)
                throw new InvalidOperationException(
                    "UOS returned a null Matchmaking ticket.");
            if (ticket.status == MatchmakingSDK.TicketStatusCreated ||
                ticket.status ==
                    MatchmakingSDK.TicketStatusAwaitingAssignment)
                return null;
            if (ticket.status == MatchmakingSDK.TicketStatusError)
                throw new InvalidOperationException(
                    $"UOS Matchmaking failed: {ticket.assignment?.msg}");
            if (ticket.status != MatchmakingSDK.TicketStatusMatched ||
                ticket.assignment == null)
                throw new InvalidOperationException(
                    $"Unsupported UOS ticket status '{ticket.status}'.");

            ushort port = ParseFirstGamePort(
                ticket.assignment.gamePorts);
            return new GameServerAssignment(
                ticket.assignment.ip,
                port,
                ticket.assignment.roomId);
        }

        public Task CancelTicketAsync(string ticketId)
        {
            if (string.IsNullOrWhiteSpace(ticketId))
                throw new ArgumentException(
                    "TicketId is required.",
                    nameof(ticketId));
            return MatchmakingSDK.Instance.DeleteTicketAsync(ticketId);
        }

        internal static ushort ParseFirstGamePort(
            string gamePorts)
        {
            if (string.IsNullOrWhiteSpace(gamePorts))
                throw new FormatException(
                    "UOS assignment contains no game port.");
            string[] entries = gamePorts.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                int separator = entry.LastIndexOf('/');
                string value = separator >= 0
                    ? entry.Substring(separator + 1)
                    : entry;
                if (ushort.TryParse(value, out ushort port) &&
                    port > 0)
                    return port;
            }
            throw new FormatException(
                $"UOS game port list '{gamePorts}' is invalid.");
        }
    }

    public sealed class NgoConnectionService :
        IGameServerConnectionService,
        IServerNetworkListener
    {
        private readonly NetworkManager networkManager;

        public bool IsConnected =>
            networkManager != null &&
            networkManager.IsConnectedClient;

        public bool IsListening =>
            networkManager != null &&
            networkManager.IsListening;

        public NgoConnectionService(NetworkManager networkManager)
        {
            this.networkManager = networkManager ??
                throw new ArgumentNullException(nameof(networkManager));
        }

        public void BeginConnect(
            in GameServerAssignment assignment)
        {
            if (!assignment.IsValid)
                throw new ArgumentException(
                    "A valid server assignment is required.",
                    nameof(assignment));
            if (networkManager.IsListening)
                throw new InvalidOperationException(
                    "NetworkManager is already listening.");
            UnityTransport transport = RequireTransport();
            transport.SetConnectionData(
                assignment.IpAddress,
                assignment.Port);
            if (!networkManager.StartClient())
                throw new InvalidOperationException(
                    "NGO failed to start the client.");
        }

        public void Disconnect()
        {
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }

        public void StartServer()
        {
            if (networkManager.IsListening)
                throw new InvalidOperationException(
                    "NetworkManager is already listening.");
            RequireTransport();
            if (!networkManager.StartServer())
                throw new InvalidOperationException(
                    "NGO failed to start the Dedicated Server.");
        }

        public void StopServer()
        {
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }

        private UnityTransport RequireTransport()
        {
            if (!(networkManager.NetworkConfig.NetworkTransport
                    is UnityTransport transport))
                throw new InvalidOperationException(
                    "FrameSyncMoba NGO flow requires UnityTransport.");
            return transport;
        }
    }

    public sealed class UosDedicatedServerPlatform :
        IDedicatedServerPlatform
    {
        private bool initialized;

        public async Task<DedicatedServerAllocation>
            ReadAllocationAsync()
        {
            if (!initialized)
            {
                await MultiverseSDK.Initialize();
                await MatchmakingServerSDK.Initialize();
                initialized = true;
            }
            UosServerMatch match =
                await MatchmakingServerSDK.Instance.GetMatchInfo();
            if (match == null)
                throw new InvalidOperationException(
                    "UOS returned no Dedicated Server match allocation.");
            var accountIds = new List<string>();
            List<UosServerTeam> teams = match.Teams == null
                ? null
                : new List<UosServerTeam>(match.Teams);
            if (teams != null)
            {
                teams.Sort(CompareTeams);
                for (int teamIndex = 0;
                     teamIndex < teams.Count;
                     teamIndex++)
                {
                    List<Unity.UOS.Matchmaking.Server.Model.Player>
                        players = CollectPlayers(teams[teamIndex]);
                    players.Sort((left, right) =>
                        string.CompareOrdinal(left.id, right.id));
                    for (int playerIndex = 0;
                         playerIndex < players.Count;
                         playerIndex++)
                    {
                        string accountId = players[playerIndex].id;
                        if (string.IsNullOrWhiteSpace(accountId) ||
                            accountIds.Contains(accountId))
                            throw new InvalidOperationException(
                                "UOS allocation contains an empty or duplicate player ID.");
                        accountIds.Add(accountId);
                    }
                }
            }
            return new DedicatedServerAllocation(
                match.RoomId,
                accountIds.ToArray());
        }

        public Task NotifyReadyAsync()
        {
            if (!initialized)
                throw new InvalidOperationException(
                    "Read the UOS allocation before notifying Ready.");
            return MultiverseSDK.Instance.ReadyAsync();
        }

        public Task ShutdownAsync()
        {
            if (!initialized)
                throw new InvalidOperationException(
                    "The UOS Dedicated Server was not initialized.");
            return MultiverseSDK.Instance.ShutdownAsync();
        }

        private static int CompareTeams(
            UosServerTeam left,
            UosServerTeam right)
        {
            int comparison = string.CompareOrdinal(
                left?.teamDefinitionName,
                right?.teamDefinitionName);
            if (comparison != 0) return comparison;
            return string.CompareOrdinal(
                left?.teamName,
                right?.teamName);
        }

        private static List<
            Unity.UOS.Matchmaking.Server.Model.Player>
            CollectPlayers(UosServerTeam team)
        {
            var result = new List<
                Unity.UOS.Matchmaking.Server.Model.Player>();
            if (team?.tickets == null) return result;
            for (int ticketIndex = 0;
                 ticketIndex < team.tickets.Count;
                 ticketIndex++)
            {
                Unity.UOS.Matchmaking.Server.Model.Ticket ticket =
                    team.tickets[ticketIndex];
                if (ticket?.players == null) continue;
                for (int playerIndex = 0;
                     playerIndex < ticket.players.Count;
                     playerIndex++)
                    result.Add(ticket.players[playerIndex]);
            }
            return result;
        }
    }
}
