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
        private const string LobbyStateMessage =
            "FrameSyncMoba.Lobby.State.v1";
        private const string LoadSceneMessage =
            "FrameSyncMoba.Lobby.LoadScene.v1";
        private const string LoadedMessage =
            "FrameSyncMoba.Lobby.Loaded.v1";
        private const string ReadyMessage =
            "FrameSyncMoba.Lobby.Ready.v1";
        private const string BootstrapMessage =
            "FrameSyncMoba.GameBootstrapPayload.v1";
        private const string BootstrapAppliedMessage =
            "FrameSyncMoba.BootstrapApplied.v1";
        private const string LaunchCommitMessage =
            "FrameSyncMoba.LaunchCommit.v2";

        [SerializeField] private NetworkManager
            networkManager;

        private readonly Dictionary<ulong, int>
            clientSlots =
                new Dictionary<ulong, int>();
        private readonly BootstrapAppliedBarrier
            bootstrapAppliedBarrier =
                new BootstrapAppliedBarrier();
        private LobbySessionFlowNetwork lobby;
        private LocalLobbySlotDefinition[]
            serverSlots =
                Array.Empty<LocalLobbySlotDefinition>();
        private bool registered;
        private bool isServerOwner;
        private bool isClientOwner;
        private bool localReadySent;
        private bool localBootstrapAppliedSent;
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
        /// <summary>
        /// True when the persistent NetworkManager is reachable. NGO's
        /// CustomMessagingManager only exists after StartServer/StartClient,
        /// so this check deliberately does not require it; the driver must
        /// start the network before binding the bridge.
        /// </summary>
        public bool IsNetworkReady
        {
            get
            {
                if (networkManager == null)
                    networkManager =
                        FindObjectOfType<NetworkManager>(true);
                return networkManager != null;
            }
        }
        public bool HasAppliedBootstrap =>
            GameSessionContext.Bootstrap != null &&
            GameSessionContext.Bootstrap.IsMatchReady;
        public event Action<GameBootstrapPayload>
            BootstrapApplied;
        public event Action AllClientsBootstrapApplied;
        public event Action IdentityAccepted;
        public event Action<GameStartConfig> StartScheduled;
        public event Action AllHeroesLocked;
        public event Action HeroLocked;
        public event Action<int> ConfirmedCountChanged;
        public event Action LoadSceneRequested;

        public void BindServer(
            LocalLobbySlotDefinition[] slots,
            string localMatchId,
            int localStartLeadTicks,
            int localGameModeId,
            int localMapConfigId,
            uint randomSeed)
        {
            RequireUnbound();
            if (networkManager == null)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires NetworkManager.");
            RequireVersions();
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
            int playerSlot,
            string accountId,
            ClientUiActionRouter actionRouter)
        {
            RequireUnbound();
            if (networkManager == null)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires NetworkManager.");
            RequireVersions();
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
                    GameSessionContext.Versions.Value));
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
            HeroLocked?.Invoke();
        }

        public void SubmitLoadedAndReady()
        {
            SendLoaded();
            SendReady(true);
        }

        /// <summary>
        /// Sent after the client restored the bootstrap snapshot and completed
        /// local player binding. Duplicate local submissions are suppressed.
        /// </summary>
        public void SubmitBootstrapApplied(
            in GameBootstrapPayload payload)
        {
            if (localBootstrapAppliedSent)
                return;
            RequireConnectedClient();
            if (!HasAppliedBootstrap)
                throw new InvalidOperationException(
                    "BootstrapApplied requires an applied bootstrap.");
            var confirmation =
                new BootstrapAppliedConfirmation(
                    payload.GameStartConfig.MatchId,
                    payload.StartTick);
            SendToServer(
                BootstrapAppliedMessage,
                MatchLaunchWireCodec
                    .WriteBootstrapApplied(
                        confirmation));
            localBootstrapAppliedSent = true;
            Debug.Log(
                $"[BootstrapApplied] Client slot {localPlayerSlot} " +
                $"confirmed match '{confirmation.MatchId}' " +
                $"StartTick {confirmation.StartTick}.");
        }

        public void SendLoaded()
        {
            RequireConnectedClient();
            Debug.Log(
                $"[Lobby] Client slot {localPlayerSlot} sending Loaded.");
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
            Debug.Log(
                $"[Lobby] Client slot {localPlayerSlot} sending Ready.");
            SendToServer(
                ReadyMessage,
                LobbyWireCodec.WriteMarker());
            localReadySent = true;
        }

        private void RegisterHandlers()
        {
            if (registered)
                return;
            if (!IsNetworkReady)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires a live NetworkManager.");
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
                LobbyStateMessage,
                ReceiveLobbyState);
            messages.RegisterNamedMessageHandler(
                LoadSceneMessage,
                ReceiveLoadScene);
            messages.RegisterNamedMessageHandler(
                LoadedMessage,
                ReceiveLoaded);
            messages.RegisterNamedMessageHandler(
                ReadyMessage,
                ReceiveReady);
            messages.RegisterNamedMessageHandler(
                BootstrapMessage,
                ReceiveBootstrap);
            messages.RegisterNamedMessageHandler(
                BootstrapAppliedMessage,
                ReceiveBootstrapApplied);
            messages.RegisterNamedMessageHandler(
                LaunchCommitMessage,
                ReceiveLaunchCommit);
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
            GameSessionContext.Versions.Value
                .RequireExactMatch(
                    identity.Versions);
            LocalLobbySlotDefinition definition =
                FindSlotByAccountId(
                    identity.AccountId);
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
            // Hero select opens only after every assigned player has joined
            // matchmaking (identity verified), so the party enters together.
            if (AreAllSlotsVerified())
            {
                Broadcast(
                    IdentityAcceptedMessage,
                    LobbyWireCodec.WriteMarker());
                Debug.Log(
                    "[Lobby] All players verified; broadcasting identity " +
                    "acceptance.");
            }
        }

        private LocalLobbySlotDefinition
            FindSlotByAccountId(
                string accountId)
        {
            for (int i = 0;
                 i < serverSlots.Length;
                 i++)
                if (string.Equals(
                        serverSlots[i].AccountId,
                        accountId,
                        StringComparison.Ordinal))
                    return serverSlots[i];
            throw new DeterministicSimulationException(
                "Lobby account is not part of the allocated match.");
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
            // Same-team duplicate rule: a hero already selected by another
            // slot on the same team cannot be picked again.
            if (lobby.IsHeroBlockedInTeam(
                    slot,
                    hero))
            {
                Debug.LogWarning(
                    $"[Lobby] Rejected duplicate hero {hero} for slot " +
                    $"{slot} (same team already selected it).");
                BroadcastLobbyState();
                return;
            }
            lobby.SelectHero(slot, hero);
            BroadcastLobbyState();
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
            lobby.LockHero(slot);
            BroadcastLobbyState();
            CheckAllHeroesLocked();
        }

        private void ReceiveLobbyState(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!isClientOwner ||
                senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "Lobby state must come from the server.");
            LobbySelectionSnapshot[] snapshots =
                LobbyWireCodec.ReadLobbyState(
                    ReadPayload(reader));
            GameFlowLuaBridge.ApplyLobbySelection(
                snapshots,
                localPlayerSlot);
            int confirmed = 0;
            for (int i = 0;
                 i < snapshots.Length;
                 i++)
            {
                if (snapshots[i].IsLocked)
                {
                    confirmed++;
                }
            }
            GameFlowLuaBridge.ConfirmedHeroCount =
                confirmed;
            ConfirmedCountChanged?.Invoke(
                confirmed);
            GameFlowLuaBridge.UiManager?
                .RefreshLuaHost(
                    UIPageId.Select);
        }

        private void ReceiveLoadScene(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!isClientOwner ||
                senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "Load scene request must come from the server.");
            LobbyWireCodec.ReadMarker(
                ReadPayload(reader));
            LoadSceneRequested?.Invoke();
        }

        private void ReceiveLoaded(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            LobbyWireCodec.ReadMarker(
                ReadPayload(reader));
            Debug.Log(
                $"[Lobby] Server received Loaded from client {senderClientId}.");
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
            Debug.Log(
                $"[Lobby] Server received Ready from client {senderClientId}; " +
                $"canSchedule={lobby.CanScheduleStart()}");
            lobby.MarkReady(
                GetClientSlot(
                    senderClientId));
            BroadcastLobbyState();
            TryScheduleStart();
        }

        private void TryScheduleStart()
        {
            Debug.Log(
                $"[Lobby] TryScheduleStart canSchedule=" +
                $"{lobby.CanScheduleStart()} " +
                $"scheduled={lobby.IsStartScheduled}");
            if (!lobby.CanScheduleStart())
                return;
            FrameSyncVersionHandshake versions =
                GameSessionContext.Versions.HasValue
                    ? GameSessionContext.Versions.Value
                    : GameSessionContext.Bootstrap != null
                        ? GameSessionContext.Bootstrap.LocalVersions
                        : default;
            if (versions.GameplayDataVersion == 0)
                throw new DeterministicSimulationException(
                    "Lobby start requires a deterministic version handshake.");
            GameStartConfig config =
                lobby.ScheduleStart(
                    matchId,
                    gameModeId,
                    mapConfigId,
                    teamCount,
                    GameSessionContext.Bootstrap != null
                        ? GameSessionContext.Bootstrap.Runtime.CurrentTick
                        : 0,
                    startLeadTicks,
                    initialRandomSeed,
                    versions.GameplayDataVersion);
            GameSessionContext.PendingServerStart =
                config;
            Debug.Log(
                $"[Lobby] Start scheduled for match '{config.MatchId}' " +
                $"at StartTick {config.StartTick}; " +
                "GameScene will build and broadcast the authoritative payload.");
            StartScheduled?.Invoke(config);
        }

        /// <summary>
        /// Server-side payload broadcast. Called by GameScene's GameBootstrap
        /// after it builds the authoritative payload from
        /// <see cref="GameSessionContext.PendingServerStart"/>.
        /// </summary>
        public void BroadcastBootstrap(
            in GameBootstrapPayload payload)
        {
            RequireServerOwner();
            bootstrapAppliedBarrier.Initialize(
                payload.GameStartConfig);
            byte[] bytes =
                BootstrapPayloadWireCodec.Write(
                    payload);
            Broadcast(
                BootstrapMessage,
                bytes);
            Debug.Log(
                $"[Lobby] Server broadcast bootstrap for match " +
                $"'{payload.GameStartConfig.MatchId}' at " +
                $"StartTick {payload.StartTick}; waiting for " +
                $"{bootstrapAppliedBarrier.ExpectedCount} " +
                "BootstrapApplied confirmations.");
        }

        public void BroadcastLaunchCommit(
            in MatchLaunchCommit commit)
        {
            RequireServerOwner();
            if (!bootstrapAppliedBarrier.IsComplete)
                throw new InvalidOperationException(
                    "LaunchCommit requires every client BootstrapApplied confirmation.");
            Broadcast(
                LaunchCommitMessage,
                MatchLaunchWireCodec
                    .WriteLaunchCommit(commit));
            Debug.Log(
                $"[LaunchCommit] Server broadcast match " +
                $"'{commit.MatchId}' StartTick {commit.StartTick} " +
                $"LaunchServerTimeMs " +
                $"{commit.LaunchServerTimeMilliseconds}.");
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
            if (GameSessionContext.Bootstrap == null ||
                GameSessionContext.Bootstrap.IsMatchReady)
            {
                GameSessionContext.ReceivedClientPayload =
                    payload;
            }
            else
            {
                GameSessionContext.Bootstrap
                    .ApplyGameBootstrapPayload(
                        payload);
                BootstrapApplied?.Invoke(payload);
            }
            Debug.Log(
                $"[LocalNGO] Client slot {localPlayerSlot} received " +
                $"bootstrap for match '{payload.GameStartConfig.MatchId}' " +
                $"at StartTick {payload.StartTick}.");
        }

        private void ReceiveBootstrapApplied(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServerOwner();
            BootstrapAppliedConfirmation confirmation =
                MatchLaunchWireCodec
                    .ReadBootstrapApplied(
                        ReadPayload(reader));
            bool completed =
                bootstrapAppliedBarrier.MarkApplied(
                    senderClientId,
                    confirmation);
            Debug.Log(
                $"[BootstrapApplied] Server accepted client " +
                $"{senderClientId}; " +
                $"{bootstrapAppliedBarrier.AppliedCount}/" +
                $"{bootstrapAppliedBarrier.ExpectedCount} ready.");
            if (completed)
                AllClientsBootstrapApplied?.Invoke();
        }

        private void ReceiveLaunchCommit(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!isClientOwner ||
                senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "LaunchCommit must come from the server.");
            MatchLaunchCommit commit =
                MatchLaunchWireCodec.ReadLaunchCommit(
                    ReadPayload(reader));
            if (GameSessionContext.Bootstrap == null ||
                !GameSessionContext.Bootstrap.IsMatchReady)
            {
                GameSessionContext.ReceivedClientLaunchCommit =
                    commit;
                return;
            }
            GameSessionContext.Bootstrap
                .ApplyMatchLaunchCommit(commit);
        }

        private void CheckAllHeroesLocked()
        {
            if (lobby == null)
                return;
            for (int i = 0;
                 i < serverSlots.Length;
                 i++)
                if ((lobby.GetState(i) &
                        LobbyPlayerSlotState.HeroLocked) ==
                    0)
                    return;
            Broadcast(
                LoadSceneMessage,
                LobbyWireCodec.WriteMarker());
            Debug.Log(
                "[Lobby] All heroes locked; broadcasting load scene " +
                "to all clients.");
            AllHeroesLocked?.Invoke();
        }

        private bool AreAllSlotsVerified()
        {
            if (lobby == null)
                return false;
            for (int i = 0;
                 i < serverSlots.Length;
                 i++)
                if ((lobby.GetState(i) &
                        LobbyPlayerSlotState
                            .IdentityVerified) ==
                    0)
                    return false;
            return true;
        }

        private void BroadcastLobbyState()
        {
            if (lobby == null)
            {
                return;
            }
            var snapshots =
                new LobbySelectionSnapshot[
                    lobby.SlotCount];
            for (int i = 0;
                 i < snapshots.Length;
                 i++)
            {
                snapshots[i] =
                    lobby.GetSelectionSnapshot(
                        i);
            }
            Broadcast(
                LobbyStateMessage,
                LobbyWireCodec.WriteLobbyState(
                    snapshots));
        }

        private static void RequireVersions()
        {
            if (!GameSessionContext.Versions.HasValue)
                throw new InvalidOperationException(
                    "LobbyNetworkBridge requires GameSessionContext.Versions " +
                    "before binding. The Lobby driver must compute the " +
                    "deterministic version handshake first.");
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
            Shutdown();
        }

        /// <summary>
        /// Tears down the NGO session when returning to the Lobby/Main menu.
        /// </summary>
        public void Shutdown()
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
            if (messages == null)
            {
                registered = false;
                return;
            }
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
            messages.UnregisterNamedMessageHandler(
                BootstrapAppliedMessage);
            messages.UnregisterNamedMessageHandler(
                LaunchCommitMessage);
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

        public static byte[] WriteLobbyState(
            LobbySelectionSnapshot[] snapshots)
        {
            if (snapshots == null ||
                snapshots.Length == 0 ||
                snapshots.Length > 10)
            {
                throw new ArgumentException(
                    "Lobby state requires 1-10 slots.",
                    nameof(snapshots));
            }
            return Write(
                writer =>
                {
                    writer.Write(
                        snapshots.Length);
                    for (int i = 0;
                         i < snapshots.Length;
                         i++)
                    {
                        LobbySelectionSnapshot
                            snapshot =
                                snapshots[i];
                        writer.Write(
                            snapshot.PlayerSlot);
                        BootstrapPayloadWireCodec
                            .WriteString(
                                writer,
                                snapshot.AccountId);
                        writer.Write(
                            snapshot.TeamId);
                        writer.Write(
                            snapshot.HeroConfigId);
                        writer.Write(
                            snapshot.IsLocked);
                        writer.Write(
                            snapshot.IsReady);
                    }
                });
        }

        public static LobbySelectionSnapshot[]
            ReadLobbyState(byte[] bytes)
        {
            return Read(
                bytes,
                reader =>
                {
                    int count =
                        reader.ReadInt32();
                    if (count < 1 ||
                        count > 10)
                    {
                        throw new
                            DeterministicSimulationException(
                                "Lobby state slot count is invalid.");
                    }
                    var snapshots =
                        new LobbySelectionSnapshot[
                            count];
                    for (int i = 0;
                         i < count;
                         i++)
                    {
                        int slot =
                            reader.ReadInt32();
                        string accountId =
                            BootstrapPayloadWireCodec
                                .ReadString(reader);
                        int teamId =
                            reader.ReadInt32();
                        int heroId =
                            reader.ReadInt32();
                        bool locked =
                            reader.ReadBoolean();
                        bool ready =
                            reader.ReadBoolean();
                        if (slot != i ||
                            string.IsNullOrWhiteSpace(
                                accountId) ||
                            teamId <= 0)
                        {
                            throw new
                                DeterministicSimulationException(
                                    "Lobby state slot is invalid.");
                        }
                        snapshots[i] =
                            new LobbySelectionSnapshot(
                                slot,
                                accountId,
                                teamId,
                                heroId,
                                locked,
                                ready);
                    }
                    return snapshots;
                });
        }

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
