using System;
using System.Collections.Generic;
using System.IO;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class FrameSyncNetworkBridge : MonoBehaviour
    {
        private const string BundleMessage =
            "FrameSyncMoba.GameplayCommandBundle.v1";
        private const string RelayMessage =
            "FrameSyncMoba.AcceptedCommandRelay.v1";
        private const string AuthorityMessage =
            "FrameSyncMoba.AuthorityFrame.v1";
        private const string RecoveryRequestMessage =
            "FrameSyncMoba.AuthorityRecoveryRequest.v1";
        private const string RecoveryResponseMessage =
            "FrameSyncMoba.AuthorityRecoveryResponse.v1";
        private const string MatchResultMessage =
            "FrameSyncMoba.MatchResultState.v1";
        private const string PingRequestMessage =
            "FrameSyncMoba.PresentationPingRequest.v1";
        private const string PingResponseMessage =
            "FrameSyncMoba.PresentationPingResponse.v1";

        [SerializeField] private NetworkManager networkManager;
        [SerializeField, Min(1)]
        private int pingRefreshIntervalMilliseconds;
        [FormerlySerializedAs("pingRefreshIntervalSeconds")]
        [SerializeField, HideInInspector]
        private float legacyPingRefreshIntervalSeconds;

        private FrameSyncGameRuntime runtime;
        private Func<ulong, GameplayCommand, bool>
            authorizeCommand;
        private uint nextBundleSequence = 1;
        private uint nextResultRevision = 1;
        private bool registered;
        private string matchId;
        private MatchResultState? pendingMatchResult;
        private PresentationPingTracker pingTracker;
        private readonly GameplayCommandSendLedger commandSendLedger =
            new GameplayCommandSendLedger();

        public bool IsBound => runtime != null;
        internal bool IsConnectedClient =>
            networkManager != null &&
            networkManager.IsClient &&
            !networkManager.IsServer &&
            networkManager.IsConnectedClient;
        public event Action<MatchResultState> MatchResultReady;
        public event Action<IReadOnlyList<GameplayCommand>>
            AcceptedCommandsReceived;
        public int LatestPingMilliseconds =>
            pingTracker?.LatestRoundTripMilliseconds ?? -1;

        public void SetMatchId(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException(
                    "MatchId is required.",
                    nameof(matchId));
            this.matchId = matchId;
        }

        public void Bind(
            FrameSyncGameRuntime runtime,
            Func<ulong, GameplayCommand, bool>
                authorizeCommand = null)
        {
            if (registered)
                throw new InvalidOperationException(
                    "FrameSyncNetworkBridge is already bound.");
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            if (networkManager == null)
                throw new InvalidOperationException(
                    "FrameSyncNetworkBridge requires NetworkManager.");
            this.authorizeCommand = authorizeCommand;
            commandSendLedger.Reset();
            pingTracker = new PresentationPingTracker(
                pingRefreshIntervalMilliseconds > 0
                    ? pingRefreshIntervalMilliseconds
                    : legacyPingRefreshIntervalSeconds > 0f
                        ? (int)Math.Round(
                            legacyPingRefreshIntervalSeconds * 1000f)
                        : 500);
            TryRegisterHandlers();
            runtime.AuthorityFrames.AuthorityFrameBuilt +=
                OnAuthorityFrameBuilt;
        }

        public void ActivateTransportHandlers()
        {
            if (runtime == null)
                throw new InvalidOperationException(
                    "FrameSyncNetworkBridge must be bound before transport activation.");
            if (!TryRegisterHandlers())
                throw new InvalidOperationException(
                    "NGO CustomMessagingManager is not available after network start.");
        }

        public void SendLocalCommands()
        {
            RequireClient();
            if (!commandSendLedger.TryBuildUnsentCommands(
                    runtime.CommandCollector,
                    out ulong contentRevision,
                    out List<GameplayCommand> commands))
            {
                return;
            }
            if (nextBundleSequence == uint.MaxValue)
                throw new DeterministicSimulationException(
                    "GameplayCommandBundle sequence exhausted.");
            int minTick = int.MaxValue;
            int maxTick = int.MinValue;
            for (int i = 0; i < commands.Count; i++)
            {
                int t = commands[i].TargetTick;
                if (t < minTick) minTick = t;
                if (t > maxTick) maxTick = t;
            }
            UnityEngine.Debug.Log(
                $"[CmdSend] local={runtime.CurrentTick} " +
                $"sync={runtime.LatestSynchronizedServerTick} " +
                $"count={commands.Count} min={minTick} max={maxTick} " +
                $"client={networkManager.LocalClientId}");
            LogCastAbilityCommands(
                "BundleSend",
                commands,
                $"sendTick={runtime.CurrentTick} " +
                $"bundleSeq={nextBundleSequence}");
            GameplayCommandBundle bundle =
                GameplayCommandBundle.Create(
                    networkManager.LocalClientId,
                    nextBundleSequence++,
                    runtime.CurrentTick,
                    commands);
            Send(
                BundleMessage,
                NetworkManager.ServerClientId,
                FrameSyncWireCodec.WriteBundle(bundle));
            commandSendLedger.CommitSuccessfulSend(
                contentRevision,
                commands);
        }

        public void TickRecovery(int controlTick)
        {
            RequireClient();
            if (!runtime.AuthorityRecovery.TryCreateRequest(
                    controlTick,
                    out AuthorityRecoveryRequest request))
            {
                if (runtime.AuthorityRecovery
                    .ConnectionMustTerminate)
                    networkManager.Shutdown();
                return;
            }
            Send(
                RecoveryRequestMessage,
                NetworkManager.ServerClientId,
                FrameSyncWireCodec.WriteRecoveryRequest(
                    request));
        }

        public void TickPresentationPing(long realtimeMilliseconds)
        {
            if (!IsConnectedClient ||
                pingTracker == null ||
                !pingTracker.TryBegin(
                    realtimeMilliseconds,
                    out uint sequence))
                return;
            using (var writer = new FastBufferWriter(
                sizeof(uint),
                Allocator.Temp))
            {
                writer.WriteValueSafe(sequence);
                networkManager.CustomMessagingManager
                    .SendNamedMessage(
                        PingRequestMessage,
                        NetworkManager.ServerClientId,
                        writer,
                        NetworkDelivery.Unreliable);
            }
        }

        private bool TryRegisterHandlers()
        {
            if (registered)
                return true;
            CustomMessagingManager messages =
                networkManager.CustomMessagingManager;
            if (messages == null)
                return false;
            messages.RegisterNamedMessageHandler(
                BundleMessage,
                ReceiveBundle);
            messages.RegisterNamedMessageHandler(
                RelayMessage,
                ReceiveRelay);
            messages.RegisterNamedMessageHandler(
                AuthorityMessage,
                ReceiveAuthority);
            messages.RegisterNamedMessageHandler(
                RecoveryRequestMessage,
                ReceiveRecoveryRequest);
            messages.RegisterNamedMessageHandler(
                RecoveryResponseMessage,
                ReceiveRecoveryResponse);
            messages.RegisterNamedMessageHandler(
                MatchResultMessage,
                ReceiveMatchResult);
            messages.RegisterNamedMessageHandler(
                PingRequestMessage,
                ReceivePingRequest);
            messages.RegisterNamedMessageHandler(
                PingResponseMessage,
                ReceivePingResponse);
            registered = true;
            return true;
        }

        private void UnregisterHandlers()
        {
            if (!registered || networkManager == null) return;
            CustomMessagingManager messages =
                networkManager.CustomMessagingManager;
            if (messages == null)
            {
                registered = false;
                return;
            }
            messages.UnregisterNamedMessageHandler(BundleMessage);
            messages.UnregisterNamedMessageHandler(RelayMessage);
            messages.UnregisterNamedMessageHandler(
                AuthorityMessage);
            messages.UnregisterNamedMessageHandler(
                RecoveryRequestMessage);
            messages.UnregisterNamedMessageHandler(
                RecoveryResponseMessage);
            messages.UnregisterNamedMessageHandler(
                MatchResultMessage);
            messages.UnregisterNamedMessageHandler(
                PingRequestMessage);
            messages.UnregisterNamedMessageHandler(
                PingResponseMessage);
            registered = false;
        }

        private void ReceiveBundle(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServer();
            GameplayCommandBundle bundle =
                FrameSyncWireCodec.ReadBundle(ReadPayload(reader));
            if (bundle.ClientId != senderClientId)
                throw new DeterministicSimulationException(
                    "GameplayCommandBundle sender does not match ClientId.");
            GameplayCommand[] bundledCommands =
                bundle.DecodeCommands();
            LogCastAbilityCommands(
                "BundleReceive",
                bundledCommands,
                $"client={bundle.ClientId} bundleSeq={bundle.BundleSequence} " +
                $"sendTick={bundle.SendLocalTick}");
            AcceptedCommandRelay[] relays =
                runtime.AcceptCommandBundle(
                    bundle,
                    command =>
                        authorizeCommand != null &&
                        authorizeCommand(
                            senderClientId,
                            command));
            for (int i = 0; i < relays.Length; i++)
            {
                LogCastAbilityCommands(
                    "RelayBuild",
                    relays[i].DecodeCommands(),
                    $"targetTick={relays[i].TargetTick} " +
                    $"revision={relays[i].RelayRevision}");
                Broadcast(
                    RelayMessage,
                    FrameSyncWireCodec.WriteRelay(relays[i]));
            }
        }

        private void ReceivePingRequest(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServer();
            reader.ReadValueSafe(out uint sequence);
            using (var writer = new FastBufferWriter(
                sizeof(uint),
                Allocator.Temp))
            {
                writer.WriteValueSafe(sequence);
                networkManager.CustomMessagingManager
                    .SendNamedMessage(
                        PingResponseMessage,
                        senderClientId,
                        writer,
                        NetworkDelivery.Unreliable);
            }
        }

        private void ReceivePingResponse(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireClientServerSender(senderClientId);
            reader.ReadValueSafe(out uint sequence);
            pingTracker?.TryComplete(
                sequence,
                FrameSyncLaunchSchedule.SecondsToMilliseconds(
                    Time.realtimeSinceStartupAsDouble));
        }

        private void ReceiveRelay(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireClientServerSender(senderClientId);
            AcceptedCommandRelay relay =
                FrameSyncWireCodec.ReadRelay(
                    ReadPayload(reader));
            GameplayCommand[] acceptedCommands =
                relay.DecodeCommands();
            LogCastAbilityCommands(
                "RelayReceive",
                acceptedCommands,
                $"targetTick={relay.TargetTick} " +
                $"revision={relay.RelayRevision}");
            runtime.ApplyAcceptedCommandRelay(relay);
            AcceptedCommandsReceived?.Invoke(acceptedCommands);
        }

        private void ReceiveAuthority(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireClientServerSender(senderClientId);
            AuthorityFrame frame =
                FrameSyncWireCodec.ReadAuthorityFrame(
                    ReadPayload(reader));
            LogCastAbilityCommands(
                "AuthorityReceive",
                frame.DecodeCommands(),
                $"tick={frame.Tick} frameSeq={frame.FrameSequence} " +
                $"revision={frame.FinalCommandRevision} " +
                $"checksum=0x{frame.SharedGameplayChecksum:X8}");
            runtime.ReceiveAuthorityFrame(frame);
            TryDispatchPendingMatchResult();
        }

        private void ReceiveRecoveryRequest(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireServer();
            AuthorityRecoveryRequest request =
                FrameSyncWireCodec.ReadRecoveryRequest(
                    ReadPayload(reader));
            try
            {
                AuthorityRecoveryResponse response =
                    runtime.BuildRecoveryResponse(request);
                Send(
                    RecoveryResponseMessage,
                    senderClientId,
                    FrameSyncWireCodec
                        .WriteRecoveryResponse(response));
            }
            catch (AuthorityRecoveryUnavailableException)
            {
                networkManager.DisconnectClient(senderClientId);
            }
        }

        private void ReceiveRecoveryResponse(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireClientServerSender(senderClientId);
            runtime.AuthorityRecovery.ApplyResponse(
                FrameSyncWireCodec.ReadRecoveryResponse(
                    ReadPayload(reader)));
        }

        private void ReceiveMatchResult(
            ulong senderClientId,
            FastBufferReader reader)
        {
            RequireClientServerSender(senderClientId);
            MatchResultState result =
                FrameSyncWireCodec.ReadMatchResult(
                    ReadPayload(reader));
            if (pendingMatchResult.HasValue &&
                result.ResultRevision <=
                    pendingMatchResult.Value.ResultRevision)
                return;
            pendingMatchResult = result;
            TryDispatchPendingMatchResult();
        }

        private void OnAuthorityFrameBuilt(
            AuthorityFrame frame)
        {
            if (networkManager != null &&
                networkManager.IsServer &&
                networkManager.IsListening)
                Broadcast(
                    AuthorityMessage,
                    FrameSyncWireCodec.WriteAuthorityFrame(
                        frame));
            if ((frame.FrameFlags &
                    AuthorityFrameFlags.MatchEndCandidate) != 0)
            {
                if (string.IsNullOrWhiteSpace(matchId))
                    throw new InvalidOperationException(
                        "MatchId must be bound before the game-over AuthorityFrame.");
                if (nextResultRevision == uint.MaxValue)
                    throw new DeterministicSimulationException(
                        "MatchResult revision exhausted.");
                MatchRuleRuntime rule = runtime.MatchRule;
                var result = new MatchResultState(
                    matchId,
                    nextResultRevision++,
                    frame.Tick,
                    rule.WinningTeamId,
                    rule.EndReason);
                Broadcast(
                    MatchResultMessage,
                    FrameSyncWireCodec.WriteMatchResult(result));
            }
        }

        private void TryDispatchPendingMatchResult()
        {
            if (!pendingMatchResult.HasValue ||
                runtime.Prediction.LatestAuthorityFrameTick <
                    pendingMatchResult.Value.GameOverTick)
                return;
            MatchResultState result =
                pendingMatchResult.Value;
            pendingMatchResult = null;
            MatchResultReady?.Invoke(result);
        }

        private void Send(
            string messageName,
            ulong clientId,
            byte[] payload)
        {
            using (var writer = new FastBufferWriter(
                payload.Length,
                Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager.CustomMessagingManager
                    .SendNamedMessage(
                        messageName,
                        clientId,
                        writer,
                        NetworkDelivery.ReliableSequenced);
            }
        }

        private void Broadcast(
            string messageName,
            byte[] payload)
        {
            var clients = new List<ulong>(
                networkManager.ConnectedClientsIds);
            clients.Sort();
            if (clients.Count == 0) return;
            using (var writer = new FastBufferWriter(
                payload.Length,
                Allocator.Temp))
            {
                writer.WriteBytesSafe(payload);
                networkManager.CustomMessagingManager
                    .SendNamedMessage(
                        messageName,
                        clients,
                        writer,
                        NetworkDelivery.ReliableSequenced);
            }
        }

        private static void LogCastAbilityCommands(
            string boundary,
            IReadOnlyList<GameplayCommand> commands,
            string metadata)
        {
            if (commands == null)
                return;
            for (int i = 0; i < commands.Count; i++)
            {
                GameplayCommand command = commands[i];
                if (command.Kind != GameplayCommandKind.CastAbility)
                    continue;
                UnityEngine.Debug.Log(
                    $"[AbilityTransport] boundary={boundary} {metadata} " +
                    $"unit={command.ControlledUnitUid} " +
                    $"seq={command.CommandSeq} slot={command.AbilitySlot} " +
                    $"verb={command.AbilityVerb} targetTick={command.TargetTick} " +
                    $"buildTick={command.Header.BuildLocalTick}");
            }
        }

        private static byte[] ReadPayload(
            FastBufferReader reader)
        {
            int count = reader.Length - reader.Position;
            if (count <= 0 ||
                count > FrameSyncWireCodec.MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "FrameSync network payload length is invalid.");
            var payload = new byte[count];
            reader.ReadBytesSafe(ref payload, count);
            return payload;
        }

        private void RequireServer()
        {
            if (networkManager == null ||
                !networkManager.IsServer)
                throw new InvalidOperationException(
                    "This FrameSync message is server-only.");
        }

        private void RequireClient()
        {
            if (runtime == null ||
                networkManager == null ||
                !networkManager.IsClient ||
                networkManager.IsServer)
                throw new InvalidOperationException(
                    "This FrameSync operation requires a connected client.");
        }

        private void RequireClientServerSender(
            ulong senderClientId)
        {
            RequireClient();
            if (senderClientId !=
                NetworkManager.ServerClientId)
                throw new DeterministicSimulationException(
                    "A server-owned FrameSync message came from a client.");
        }

        private void OnDestroy()
        {
            if (runtime != null)
                runtime.AuthorityFrames.AuthorityFrameBuilt -=
                    OnAuthorityFrameBuilt;
            UnregisterHandlers();
        }
    }

    /// <summary>
    /// Presentation-only ping cadence and round-trip measurement. It never
    /// enters Gameplay commands, snapshots, or checksums.
    /// </summary>
    public sealed class PresentationPingTracker
    {
        private readonly int intervalMilliseconds;
        private long nextSendRealtimeMilliseconds;
        private long pendingSendRealtimeMilliseconds;
        private uint nextSequence = 1;
        private uint pendingSequence;

        public int LatestRoundTripMilliseconds { get; private set; } = -1;

        public PresentationPingTracker(int intervalMilliseconds)
        {
            if (intervalMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(intervalMilliseconds));
            this.intervalMilliseconds = intervalMilliseconds;
        }

        public bool TryBegin(
            long realtimeMilliseconds,
            out uint sequence)
        {
            sequence = 0;
            if (realtimeMilliseconds <
                nextSendRealtimeMilliseconds)
                return false;
            sequence = nextSequence;
            nextSequence = nextSequence == uint.MaxValue
                ? 1u
                : nextSequence + 1u;
            pendingSequence = sequence;
            pendingSendRealtimeMilliseconds =
                realtimeMilliseconds;
            nextSendRealtimeMilliseconds = checked(
                realtimeMilliseconds + intervalMilliseconds);
            return true;
        }

        public bool TryComplete(
            uint sequence,
            long realtimeMilliseconds)
        {
            if (sequence == 0 ||
                sequence != pendingSequence ||
                realtimeMilliseconds <
                    pendingSendRealtimeMilliseconds)
                return false;
            long milliseconds =
                realtimeMilliseconds -
                pendingSendRealtimeMilliseconds;
            LatestRoundTripMilliseconds =
                milliseconds >= int.MaxValue
                    ? int.MaxValue
                    : (int)milliseconds;
            pendingSequence = 0;
            return true;
        }
    }

    internal static class FrameSyncWireCodec
    {
        public const int MaximumPayloadBytes = 4 * 1024 * 1024;
        private const int MaximumFrameCount = 4096;
        private const int MaximumRangeCount = 256;

        public static byte[] WriteBundle(
            GameplayCommandBundle bundle)
        {
            return Write(writer =>
            {
                writer.Write(bundle.ClientId);
                writer.Write(bundle.BundleSequence);
                writer.Write(bundle.SendLocalTick);
                writer.Write(bundle.MinTargetTick);
                writer.Write(bundle.MaxTargetTick);
                writer.Write(bundle.CommandCount);
                WriteBytes(
                    writer,
                    bundle.CanonicalCommandBytes);
            });
        }

        public static GameplayCommandBundle ReadBundle(
            byte[] payload)
        {
            return Read(payload, reader =>
                new GameplayCommandBundle(
                    reader.ReadUInt64(),
                    reader.ReadUInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    reader.ReadInt32(),
                    ReadBytes(reader)));
        }

        public static byte[] WriteRelay(
            AcceptedCommandRelay relay)
        {
            return Write(writer =>
            {
                writer.Write(relay.TargetTick);
                writer.Write(relay.RelayRevision);
                WriteBytes(
                    writer,
                    relay.CanonicalCommandBytesForTick);
            });
        }

        public static AcceptedCommandRelay ReadRelay(
            byte[] payload)
        {
            return Read(payload, reader =>
                new AcceptedCommandRelay(
                    reader.ReadInt32(),
                    reader.ReadUInt32(),
                    ReadBytes(reader)));
        }

        public static byte[] WriteAuthorityFrame(
            AuthorityFrame frame)
        {
            return Write(writer =>
                WriteAuthorityFrame(writer, frame));
        }

        public static AuthorityFrame ReadAuthorityFrame(
            byte[] payload)
        {
            return Read(payload, ReadAuthorityFrame);
        }

        public static byte[] WriteRecoveryRequest(
            AuthorityRecoveryRequest request)
        {
            return Write(writer =>
            {
                writer.Write(request.RequestSequence);
                MissingAuthorityFrameRange[] ranges =
                    request.MissingRanges;
                writer.Write(ranges.Length);
                for (int i = 0; i < ranges.Length; i++)
                {
                    writer.Write(ranges[i].FromTick);
                    writer.Write(ranges[i].ToTick);
                }
            });
        }

        public static AuthorityRecoveryRequest
            ReadRecoveryRequest(byte[] payload)
        {
            return Read(payload, reader =>
            {
                uint sequence = reader.ReadUInt32();
                int count = ReadBoundedCount(
                    reader,
                    MaximumRangeCount,
                    "recovery range");
                var ranges =
                    new MissingAuthorityFrameRange[count];
                for (int i = 0; i < count; i++)
                    ranges[i] =
                        new MissingAuthorityFrameRange(
                            reader.ReadInt32(),
                            reader.ReadInt32());
                return new AuthorityRecoveryRequest(
                    sequence,
                    ranges);
            });
        }

        public static byte[] WriteRecoveryResponse(
            AuthorityRecoveryResponse response)
        {
            return Write(writer =>
            {
                writer.Write(response.RequestSequence);
                AuthorityFrame[] frames =
                    response.AuthorityFrames;
                writer.Write(frames.Length);
                for (int i = 0; i < frames.Length; i++)
                    WriteAuthorityFrame(writer, frames[i]);
            });
        }

        public static AuthorityRecoveryResponse
            ReadRecoveryResponse(byte[] payload)
        {
            return Read(payload, reader =>
            {
                uint sequence = reader.ReadUInt32();
                int count = ReadBoundedCount(
                    reader,
                    MaximumFrameCount,
                    "AuthorityFrame");
                var frames = new AuthorityFrame[count];
                for (int i = 0; i < count; i++)
                    frames[i] = ReadAuthorityFrame(reader);
                return new AuthorityRecoveryResponse(
                    sequence,
                    frames);
            });
        }

        public static byte[] WriteMatchResult(
            MatchResultState result)
        {
            return Write(writer =>
            {
                writer.Write(result.MatchId);
                writer.Write(result.ResultRevision);
                writer.Write(result.GameOverTick);
                writer.Write(result.WinningTeamId.Value);
                writer.Write((byte)result.EndReason);
            });
        }

        public static MatchResultState ReadMatchResult(
            byte[] payload)
        {
            return Read(payload, reader =>
                new MatchResultState(
                    reader.ReadString(),
                    reader.ReadUInt32(),
                    reader.ReadInt32(),
                    new TeamId(reader.ReadByte()),
                    (MatchEndReason)reader.ReadByte()));
        }

        private static void WriteAuthorityFrame(
            BinaryWriter writer,
            in AuthorityFrame frame)
        {
            writer.Write(frame.Tick);
            writer.Write(frame.FrameSequence);
            writer.Write(frame.FinalCommandRevision);
            WriteBytes(
                writer,
                frame.CanonicalCommandBytes);
            writer.Write((byte)frame.FrameFlags);
            writer.Write(frame.SharedGameplayChecksum);
        }

        private static AuthorityFrame ReadAuthorityFrame(
            BinaryReader reader)
        {
            return new AuthorityFrame(
                reader.ReadInt32(),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                ReadBytes(reader),
                (AuthorityFrameFlags)reader.ReadByte(),
                reader.ReadUInt32());
        }

        private static void WriteBytes(
            BinaryWriter writer,
            byte[] bytes)
        {
            bytes ??= Array.Empty<byte>();
            if (bytes.Length > MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "FrameSync byte field exceeds the protocol limit.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static byte[] ReadBytes(
            BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "FrameSync byte field length is invalid.");
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException(
                    "FrameSync byte field is truncated.");
            return bytes;
        }

        private static int ReadBoundedCount(
            BinaryReader reader,
            int maximum,
            string label)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximum)
                throw new DeterministicSimulationException(
                    $"FrameSync {label} count is invalid.");
            return count;
        }

        private static byte[] Write(
            Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                write(writer);
                writer.Flush();
                if (stream.Length > MaximumPayloadBytes)
                    throw new DeterministicSimulationException(
                        "FrameSync network envelope exceeds the protocol limit.");
                return stream.ToArray();
            }
        }

        private static T Read<T>(
            byte[] payload,
            Func<BinaryReader, T> read)
        {
            if (payload == null ||
                payload.Length == 0 ||
                payload.Length > MaximumPayloadBytes)
                throw new DeterministicSimulationException(
                    "FrameSync network envelope length is invalid.");
            using (var stream = new MemoryStream(
                payload,
                false))
            using (var reader = new BinaryReader(stream))
            {
                T result = read(reader);
                if (stream.Position != stream.Length)
                    throw new DeterministicSimulationException(
                        "FrameSync network envelope contains trailing bytes.");
                return result;
            }
        }
    }
}
