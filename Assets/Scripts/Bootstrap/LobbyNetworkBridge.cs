using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [Serializable]
    public struct LocalLobbySlotDefinition
    {
        [Min(0)] public int PlayerSlot;
        public string AccountId;
        [Range(1, byte.MaxValue)]
        public int TeamId;
        [Min(0)] public int SpawnPointId;
        [Min(1)] public int HeroConfigId;
    }

    [DisallowMultipleComponent]
    public sealed class LobbyNetworkBridge :
        MonoBehaviour
    {
        private const string IdentityMessage =
            "FrameSyncMoba.Lobby.Identity.v1";
        private const string SelectMessage =
            "FrameSyncMoba.Lobby.Select.v1";
        private const string IdentityAcceptedMessage =
            "FrameSyncMoba.Lobby.IdentityAccepted.v1";
        private const string LockMessage =
            "FrameSyncMoba.Lobby.Lock.v1";
        private const string LoadedMessage =
            "FrameSyncMoba.Lobby.Loaded.v1";
        private const string ReadyMessage =
            "FrameSyncMoba.Lobby.Ready.v1";
        private const string BootstrapMessage =
            "FrameSyncMoba.GameBootstrapPayload.v1";

        [SerializeField] private NetworkManager
            networkManager;

        private readonly Dictionary<ulong, int>
            clientSlots =
                new Dictionary<ulong, int>();
        private GameBootstrap bootstrap;
        private LobbySessionFlowNetwork lobby;
        private LocalLobbySlotDefinition[]
            serverSlots =
                Array.Empty<LocalLobbySlotDefinition>();
        private bool registered;
        private bool isServerOwner;
        private bool isClientOwner;
        private bool localReadySent;
        private int localPlayerSlot = -1;
        private string localAccountId;
        private string matchId;
        private int startLeadTicks;
        private int gameModeId;
        private int mapConfigId;
        private int teamCount;
        private uint initialRandomSeed;
        private ClientUiActionRouter uiActions;

        public bool IsBound =>
            isServerOwner || isClientOwner;
        public bool HasAppliedBootstrap =>
            bootstrap != null &&
            bootstrap.IsMatchReady;
        public event Action<GameBootstrapPayload>
            BootstrapApplied;
        public event Action IdentityAccepted;

        public void BindServer(
            GameBootstrap gameBootstrap,
            LocalLobbySlotDefinition[] slots,
            string localMatchId,
            int localStartLeadTicks,
            int localGameModeId,
            int localMapConfigId,
            uint randomSeed)
        {
            RequireUnbound();
            bootstrap = gameBootstrap ??
                throw new ArgumentNullException(
                    nameof(gameBootstrap));
            if (networkManager == null)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires NetworkManager.");
            serverSlots =
                ValidateAndCopySlots(slots);
            matchId =
                string.IsNullOrWhiteSpace(localMatchId)
                    ? throw new ArgumentException(
                        "MatchId is required.",
                        nameof(localMatchId))
                    : localMatchId;
            if (localStartLeadTicks < 0 ||
                localGameModeId <= 0 ||
                localMapConfigId <= 0 ||
                randomSeed == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(localStartLeadTicks));
            startLeadTicks =
                localStartLeadTicks;
            gameModeId = localGameModeId;
            mapConfigId = localMapConfigId;
            initialRandomSeed = randomSeed;
            teamCount =
                CountTeams(serverSlots);
            lobby =
                new LobbySessionFlowNetwork(
                    serverSlots.Length);
            isServerOwner = true;
            RegisterHandlers();
        }

        public void BindClient(
            GameBootstrap gameBootstrap,
            int playerSlot,
            string accountId,
            ClientUiActionRouter actionRouter)
        {
            RequireUnbound();
            bootstrap = gameBootstrap ??
                throw new ArgumentNullException(
                    nameof(gameBootstrap));
            if (networkManager == null)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires NetworkManager.");
            if (playerSlot < 0 ||
                playerSlot >= 10)
                throw new ArgumentOutOfRangeException(
                    nameof(playerSlot));
            if (string.IsNullOrWhiteSpace(
                    accountId))
                throw new ArgumentException(
                    "AccountId is required.",
                    nameof(accountId));
            localPlayerSlot = playerSlot;
            localAccountId = accountId;
            uiActions = actionRouter ??
                throw new ArgumentNullException(
                    nameof(actionRouter));
            uiActions.Bind(
                SendSelectHero,
                SendLockHero,
                SendReady,
                ReturnToMainMenu);
            isClientOwner = true;
            RegisterHandlers();
        }

        public void NotifyClientConnected()
        {
            RequireConnectedClient();
            SendToServer(
                IdentityMessage,
                LobbyWireCodec.WriteIdentity(
                    localPlayerSlot,
                    localAccountId,
                    bootstrap.LocalVersions));
        }

        public void SubmitAutomaticReady(
            int heroConfigId)
        {
            SendSelectHero(heroConfigId);
            SendLockHero(heroConfigId);
            SendLoaded();
            SendReady(true);
        }

        public void SendSelectHero(
            int heroConfigId)
        {
            RequireConnectedClient();
            SendToServer(
                SelectMessage,
                LobbyWireCodec.WritePositiveInt(
                    heroConfigId));
        }

        public void SendLockHero(
            int heroConfigId)
        {
            RequireConnectedClient();
            SendToServer(
                LockMessage,
                LobbyWireCodec.WritePositiveInt(
                    heroConfigId));
        }

        public void SendLoaded()
        {
            RequireConnectedClient();
            SendToServer(
                LoadedMessage,
                LobbyWireCodec.WriteMarker());
        }

        public void SendReady(bool isReady)
        {
            RequireConnectedClient();
            if (!isReady)
            {
                if (localReadySent)
                    throw new InvalidOperationException(
                        "Formal Ready cannot be withdrawn after submission.");
                return;
            }
            if (localReadySent)
                return;
            SendToServer(
                ReadyMessage,
                LobbyWireCodec.WriteMarker());
            localReadySent = true;
        }

        private void RegisterHandlers()
        {
            if (registered)
                return;
            CustomMessagingManager messages =
                networkManager
                    .CustomMessagingManager;
            messages.RegisterNamedMessageHandler(
                IdentityMessage,
                ReceiveIdentity);
            messages.RegisterNamedMessageHandler(
                IdentityAcceptedMessage,
                ReceiveIdentityAccepted);
            messages.RegisterNamedMessageHandler(
                SelectMessage,
                ReceiveSelect);
            messages.RegisterNamedMessageHandler(
                LockMessage,
                ReceiveLock);
            messages.RegisterNamedMessageHandler(
                LoadedMessage,
                ReceiveLoaded);
            messages.RegisterNamedMessageHandler(
                ReadyMessage,
                ReceiveReady);
            messages.RegisterNamedMessageHandler(
                BootstrapMessage,
                ReceiveBootstrap);
            registered = true;
        }

        private void ReceiveIdentity(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            LobbyIdentity identity =
                LobbyWireCodec.ReadIdentity(
                    ReadPayload(reader));
            bootstrap.LocalVersions
                .RequireExactMatch(
                    identity.Versions);
            LocalLobbySlotDefinition definition =
                GetServerSlot(
                    identity.PlayerSlot);
            if (!string.Equals(
                    definition.AccountId,
                    identity.AccountId,
                    StringComparison.Ordinal))
                throw new DeterministicSimulationException(
                    "Lobby account does not match its assigned local slot.");
            if (clientSlots.ContainsKey(
                    senderClientId))
                throw new DeterministicSimulationException(
                    "The client already owns a Lobby slot.");
            lobby.Assign(
                definition.PlayerSlot,
                definition.AccountId,
                senderClientId,
                new TeamId(
                    (byte)definition.TeamId),
                definition.SpawnPointId);
            lobby.MarkConnected(
                definition.PlayerSlot);
            lobby.VerifyIdentity(
                definition.PlayerSlot,
                definition.AccountId,
                senderClientId);
            clientSlots.Add(
                senderClientId,
                definition.PlayerSlot);
            SendToClient(
                IdentityAcceptedMessage,
                senderClientId,
                LobbyWireCodec.WriteMarker());
        }

        private void ReceiveIdentityAccepted(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!isClientOwner ||
                senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "Lobby identity acceptance must come from the server.");
            LobbyWireCodec.ReadMarker(
                ReadPayload(reader));
            IdentityAccepted?.Invoke();
        }

        private void ReceiveSelect(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            int slot = GetClientSlot(
                senderClientId);
            int hero =
                LobbyWireCodec.ReadPositiveInt(
                    ReadPayload(reader));
            LocalLobbySlotDefinition definition =
                GetServerSlot(slot);
            if (hero !=
                definition.HeroConfigId)
                throw new DeterministicSimulationException(
                    "The local fixture selected a HeroConfigId not present in its frozen composition.");
            lobby.SelectHero(slot, hero);
        }

        private void ReceiveLock(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            int slot = GetClientSlot(
                senderClientId);
            int hero =
                LobbyWireCodec.ReadPositiveInt(
                    ReadPayload(reader));
            if (hero !=
                GetServerSlot(slot)
                    .HeroConfigId)
                throw new DeterministicSimulationException(
                    "Locked HeroConfigId disagrees with the selected fixture.");
            lobby.LockHero(slot);
        }

        private void ReceiveLoaded(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            LobbyWireCodec.ReadMarker(
                ReadPayload(reader));
            lobby.MarkGameplaySceneLoaded(
                GetClientSlot(
                    senderClientId));
        }

        private void ReceiveReady(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            LobbyWireCodec.ReadMarker(
                ReadPayload(reader));
            lobby.MarkReady(
                GetClientSlot(
                    senderClientId));
            TryScheduleStart();
        }

        private void TryScheduleStart()
        {
            if (!lobby.CanScheduleStart())
                return;
            GameStartConfig config =
                lobby.ScheduleStart(
                    matchId,
                    gameModeId,
                    mapConfigId,
                    teamCount,
                    bootstrap.Runtime
                        .CurrentTick,
                    startLeadTicks,
                    initialRandomSeed,
                    bootstrap.LocalVersions
                        .GameplayDataVersion);
            GameBootstrapPayload payload =
                bootstrap
                    .BuildAuthoritativeBootstrapPayload(
                        config);
            byte[] bytes =
                BootstrapPayloadWireCodec.Write(
                    payload);
            bootstrap.ApplyGameBootstrapPayload(
                payload);
            Broadcast(
                BootstrapMessage,
                bytes);
            Debug.Log(
                $"[LocalNGO] Server applied and broadcast bootstrap " +
                $"for match '{payload.GameStartConfig.MatchId}' at " +
                $"StartTick {payload.StartTick}.");
        }

        private void ReceiveBootstrap(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!isClientOwner ||
                senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "GameBootstrapPayload must come from the server.");
            GameBootstrapPayload payload =
                BootstrapPayloadWireCodec.Read(
                    ReadPayload(reader));
            bootstrap.ApplyGameBootstrapPayload(
                payload);
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} applied " +
                $"bootstrap for match '{payload.GameStartConfig.MatchId}' " +
                $"at StartTick {payload.StartTick}.");
            BootstrapApplied?.Invoke(payload);
        }

        private void SendToServer(
            string message,
            byte[] payload)
        {
            using (var writer =
                   new FastBufferWriter(
                       payload.Length,
                       Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager
                    .CustomMessagingManager
                    .SendNamedMessage(
                        message,
                        NetworkManager
                            .ServerClientId,
                        writer,
                        NetworkDelivery
                            .ReliableSequenced);
            }
        }

        private void SendToClient(
            string message,
            ulong clientId,
            byte[] payload)
        {
            using (var writer =
                   new FastBufferWriter(
                       payload.Length,
                       Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager
                    .CustomMessagingManager
                    .SendNamedMessage(
                        message,
                        clientId,
                        writer,
                        NetworkDelivery
                            .ReliableSequenced);
            }
        }

        private void Broadcast(
            string message,
            byte[] payload)
        {
            var clients = new List<ulong>(
                networkManager
                    .ConnectedClientsIds);
            clients.Sort();
            clients.Remove(
                NetworkManager.ServerClientId);
            if (clients.Count == 0)
                return;
            using (var writer =
                   new FastBufferWriter(
                       payload.Length,
                       Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager
                    .CustomMessagingManager
                    .SendNamedMessage(
                        message,
                        clients,
                        writer,
                        NetworkDelivery
                            .ReliableFragmentedSequenced);
            }
        }

        private static byte[] ReadPayload(
            FastBufferReader reader)
        {
            int count =
                reader.Length -
                reader.Position;
            if (count <= 0 ||
                count >
                FrameSyncWireCodec
                    .MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "Lobby payload length is invalid.");
            var bytes = new byte[count];
            reader.ReadBytesSafe(
                ref bytes,
                count);
            return bytes;
        }

        private int GetClientSlot(
            ulong clientId)
        {
            if (!clientSlots.TryGetValue(
                    clientId,
                    out int slot))
                throw new DeterministicSimulationException(
                    "Lobby message sender has no verified slot.");
            return slot;
        }

        private LocalLobbySlotDefinition
            GetServerSlot(int slot)
        {
            if ((uint)slot >=
                    (uint)serverSlots.Length ||
                serverSlots[slot]
                    .PlayerSlot != slot)
                throw new DeterministicSimulationException(
                    "Lobby PlayerSlot is invalid.");
            return serverSlots[slot];
        }

        private void ReturnToMainMenu()
        {
            if (networkManager != null &&
                networkManager.IsListening)
                networkManager.Shutdown();
        }

        private void RequireConnectedClient()
        {
            if (!isClientOwner ||
                networkManager == null ||
                !networkManager.IsClient ||
                networkManager.IsServer ||
                !networkManager.IsConnectedClient)
                throw new InvalidOperationException(
                    "Lobby action requires a connected NGO client.");
        }

        private void RequireServerOwner()
        {
            if (!isServerOwner ||
                networkManager == null ||
                !networkManager.IsServer)
                throw new InvalidOperationException(
                    "Lobby message requires the server owner.");
        }

        private void RequireUnbound()
        {
            if (IsBound)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge is already bound.");
        }

        private void UnregisterHandlers()
        {
            if (!registered ||
                networkManager == null)
                return;
            CustomMessagingManager messages =
                networkManager
                    .CustomMessagingManager;
            messages.UnregisterNamedMessageHandler(
                IdentityMessage);
            messages.UnregisterNamedMessageHandler(
                IdentityAcceptedMessage);
            messages.UnregisterNamedMessageHandler(
                SelectMessage);
            messages.UnregisterNamedMessageHandler(
                LockMessage);
            messages.UnregisterNamedMessageHandler(
                LoadedMessage);
            messages.UnregisterNamedMessageHandler(
                ReadyMessage);
            messages.UnregisterNamedMessageHandler(
                BootstrapMessage);
            registered = false;
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private static LocalLobbySlotDefinition[]
            ValidateAndCopySlots(
                LocalLobbySlotDefinition[] slots)
        {
            if (slots == null ||
                slots.Length == 0 ||
                slots.Length > 10)
                throw new ArgumentException(
                    "Local Lobby requires 1-10 slots.",
                    nameof(slots));
            var copy =
                (LocalLobbySlotDefinition[])
                slots.Clone();
            Array.Sort(
                copy,
                (left, right) =>
                    left.PlayerSlot.CompareTo(
                        right.PlayerSlot));
            for (int i = 0;
                 i < copy.Length;
                 i++)
            {
                LocalLobbySlotDefinition slot =
                    copy[i];
                if (slot.PlayerSlot != i ||
                    string.IsNullOrWhiteSpace(
                        slot.AccountId) ||
                    slot.TeamId <= 0 ||
                    slot.TeamId >
                    byte.MaxValue ||
                    slot.SpawnPointId < 0 ||
                    slot.HeroConfigId <= 0)
                    throw new ArgumentException(
                        $"Local Lobby slot {i} is invalid.",
                        nameof(slots));
                for (int j = 0;
                     j < i;
                     j++)
                    if (string.Equals(
                            copy[j].AccountId,
                            slot.AccountId,
                            StringComparison.Ordinal))
                        throw new ArgumentException(
                            "Local Lobby AccountIds must be unique.",
                            nameof(slots));
            }
            return copy;
        }

        private static int CountTeams(
            LocalLobbySlotDefinition[] slots)
        {
            var teams = new List<int>();
            for (int i = 0;
                 i < slots.Length;
                 i++)
                if (!teams.Contains(
                        slots[i].TeamId))
                    teams.Add(slots[i].TeamId);
            return teams.Count;
        }
    }

    internal readonly struct LobbyIdentity
    {
        public readonly int PlayerSlot;
        public readonly string AccountId;
        public readonly FrameSyncVersionHandshake
            Versions;

        public LobbyIdentity(
            int playerSlot,
            string accountId,
            in FrameSyncVersionHandshake versions)
        {
            PlayerSlot = playerSlot;
            AccountId = accountId;
            Versions = versions;
        }
    }

    internal static class LobbyWireCodec
    {
        private const byte Marker = 0x5A;

        public static byte[] WriteIdentity(
            int playerSlot,
            string accountId,
            in FrameSyncVersionHandshake versions)
        {
            FrameSyncVersionHandshake value =
                versions;
            return Write(
                writer =>
                {
                    writer.Write(playerSlot);
                    BootstrapPayloadWireCodec
                        .WriteString(
                            writer,
                            accountId);
                    writer.Write(
                        value
                            .GameplayDataVersion);
                    writer.Write(
                        value.MapDataVersion);
                    writer.Write(
                        value
                            .GlobalPrefabTableVersion);
                    writer.Write(
                        value
                            .CommandSchemaVersion);
                    writer.Write(
                        value
                            .SnapshotSchemaVersion);
                });
        }

        public static LobbyIdentity ReadIdentity(
            byte[] bytes)
        {
            return Read(
                bytes,
                reader =>
                    new LobbyIdentity(
                        reader.ReadInt32(),
                        BootstrapPayloadWireCodec
                            .ReadString(reader),
                        new FrameSyncVersionHandshake(
                            reader.ReadUInt32(),
                            reader.ReadUInt32(),
                            reader.ReadUInt32(),
                            reader.ReadUInt32(),
                            reader.ReadUInt32())));
        }

        public static byte[] WritePositiveInt(
            int value)
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(value));
            return Write(
                writer =>
                    writer.Write(value));
        }

        public static int ReadPositiveInt(
            byte[] bytes)
        {
            int value = Read(
                bytes,
                reader =>
                    reader.ReadInt32());
            if (value <= 0)
                throw new DeterministicSimulationException(
                    "Lobby positive integer is invalid.");
            return value;
        }

        public static byte[] WriteMarker() =>
            new[] { Marker };

        public static void ReadMarker(
            byte[] bytes)
        {
            if (bytes == null ||
                bytes.Length != 1 ||
                bytes[0] != Marker)
                throw new DeterministicSimulationException(
                    "Lobby marker is invalid.");
        }

        private static byte[] Write(
            Action<BinaryWriter> action)
        {
            using (var stream =
                   new MemoryStream())
            using (var writer =
                   new BinaryWriter(
                       stream,
                       Encoding.UTF8,
                       true))
            {
                action(writer);
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static T Read<T>(
            byte[] bytes,
            Func<BinaryReader, T> action)
        {
            if (bytes == null ||
                bytes.Length == 0 ||
                bytes.Length > 4096)
                throw new DeterministicSimulationException(
                    "Lobby wire payload length is invalid.");
            try
            {
                using (var stream =
                       new MemoryStream(
                           bytes,
                           false))
                using (var reader =
                       new BinaryReader(
                           stream,
                           Encoding.UTF8,
                           true))
                {
                    T value = action(reader);
                    if (stream.Position !=
                        stream.Length)
                        throw new DeterministicSimulationException(
                            "Lobby wire payload contains trailing bytes.");
                    return value;
                }
            }
            catch (EndOfStreamException exception)
            {
                throw new DeterministicSimulationException(
                    "Lobby wire payload is truncated.",
                    exception);
            }
        }
    }
}
