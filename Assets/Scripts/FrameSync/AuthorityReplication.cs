using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public readonly struct GameplayCommandBundle
    {
        private readonly byte[] canonicalCommandBytes;

        public readonly ulong ClientId;
        public readonly uint BundleSequence;
        public readonly int SendLocalTick;
        public readonly int MinTargetTick;
        public readonly int MaxTargetTick;
        public readonly int CommandCount;

        public byte[] CanonicalCommandBytes =>
            canonicalCommandBytes == null
                ? Array.Empty<byte>()
                : (byte[])canonicalCommandBytes.Clone();

        internal byte[] CanonicalCommandBytesUnsafe =>
            canonicalCommandBytes ?? Array.Empty<byte>();

        public GameplayCommandBundle(
            ulong clientId,
            uint bundleSequence,
            int sendLocalTick,
            int minTargetTick,
            int maxTargetTick,
            int commandCount,
            byte[] canonicalCommandBytes)
        {
            if (bundleSequence == 0)
                throw new ArgumentOutOfRangeException(nameof(bundleSequence));
            if (sendLocalTick < 0)
                throw new ArgumentOutOfRangeException(nameof(sendLocalTick));
            if (commandCount < 0)
                throw new ArgumentOutOfRangeException(nameof(commandCount));
            if (commandCount == 0)
            {
                if (minTargetTick != -1 || maxTargetTick != -1)
                    throw new ArgumentException(
                        "An empty bundle must use -1 target Tick bounds.");
            }
            else if (minTargetTick < 0 || maxTargetTick < minTargetTick)
            {
                throw new ArgumentException(
                    "A non-empty bundle requires valid target Tick bounds.");
            }

            ClientId = clientId;
            BundleSequence = bundleSequence;
            SendLocalTick = sendLocalTick;
            MinTargetTick = minTargetTick;
            MaxTargetTick = maxTargetTick;
            CommandCount = commandCount;
            this.canonicalCommandBytes = canonicalCommandBytes == null
                ? throw new ArgumentNullException(nameof(canonicalCommandBytes))
                : (byte[])canonicalCommandBytes.Clone();

            GameplayCommand[] decoded =
                CanonicalCommandCodec.DecodeBundle(this.canonicalCommandBytes);
            ValidateDecoded(decoded);
        }

        public static GameplayCommandBundle Create(
            ulong clientId,
            uint bundleSequence,
            int sendLocalTick,
            IReadOnlyList<GameplayCommand> commands)
        {
            int sourceCount = commands?.Count ?? 0;
            for (int i = 0; i < sourceCount; i++)
            {
                if (commands[i].Header.ClientId != clientId)
                {
                    LogBundleFailure(
                        clientId,
                        commands,
                        $"ClientId mismatch at index {i}: " +
                        $"command client={commands[i].Header.ClientId} " +
                        $"bundle client={clientId}");
                    throw new ArgumentException(
                        "Every bundled Command must belong to the bundle ClientId.",
                        nameof(commands));
                }
            }

            byte[] canonicalBytes =
                CanonicalCommandCodec.Encode(commands);
            GameplayCommand[] canonicalCommands =
                TryDecodeBundleOrLogFailure(
                    canonicalBytes,
                    commands,
                    clientId);
            int count = canonicalCommands.Length;
            int minTick = -1;
            int maxTick = -1;
            if (count > 0)
            {
                minTick = canonicalCommands[0].TargetTick;
                maxTick = minTick;
                for (int i = 0; i < count; i++)
                {
                    GameplayCommand command = canonicalCommands[i];
                    if (command.TargetTick < minTick) minTick = command.TargetTick;
                    if (command.TargetTick > maxTick) maxTick = command.TargetTick;
                }
            }

            return new GameplayCommandBundle(
                clientId,
                bundleSequence,
                sendLocalTick,
                minTick,
                maxTick,
                count,
                canonicalBytes);
        }

        private static GameplayCommand[] TryDecodeBundleOrLogFailure(
            byte[] canonicalBytes,
            IReadOnlyList<GameplayCommand> sourceCommands,
            ulong bundleClientId)
        {
            try
            {
                return CanonicalCommandCodec.DecodeBundle(
                    canonicalBytes);
            }
            catch (System.Exception exception)
            {
                LogBundleFailure(
                    bundleClientId,
                    sourceCommands,
                    "canonical round-trip failed: " +
                    exception.Message);
                throw;
            }
        }

        private static void LogBundleFailure(
            ulong bundleClientId,
            IReadOnlyList<GameplayCommand> commands,
            string reason)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append(
                $"[BundleDiag] {reason}; bundleClient={bundleClientId} " +
                $"count={commands?.Count ?? 0}");
            if (commands != null)
            {
                var seenKeys =
                    new HashSet<(int, int, UnitUid, uint)>();
                for (int i = 0;
                     i < commands.Count;
                     i++)
                {
                    GameplayCommand command = commands[i];
                    var key = (
                        command.TargetTick,
                        command.PlayerSlot,
                        command.ControlledUnitUid,
                        command.CommandSeq);
                    builder.Append(
                        $"\n  [{i}] kind={command.Kind} " +
                        $"seq={command.CommandSeq} client={command.Header.ClientId} " +
                        $"slot={command.PlayerSlot} uid={command.ControlledUnitUid} " +
                        $"tick={command.TargetTick} build={command.Header.BuildLocalTick} " +
                        $"payload={command.Header.PayloadByteLength} " +
                        $"dupKey={seenKeys.Contains(key)}");
                    seenKeys.Add(key);
                }
            }
            UnityEngine.Debug.LogError(builder.ToString());
        }

        public GameplayCommand[] DecodeCommands()
        {
            GameplayCommand[] commands =
                CanonicalCommandCodec.DecodeBundle(canonicalCommandBytes);
            ValidateDecoded(commands);
            return commands;
        }

        private void ValidateDecoded(GameplayCommand[] commands)
        {
            if (commands.Length != CommandCount)
                throw new DeterministicSimulationException(
                    "GameplayCommandBundle CommandCount does not match its canonical bytes.");
            int minTick = -1;
            int maxTick = -1;
            for (int i = 0; i < commands.Length; i++)
            {
                GameplayCommand command = commands[i];
                if (command.Header.ClientId != ClientId)
                    throw new DeterministicSimulationException(
                        "GameplayCommandBundle contains a Command for another ClientId.");
                if (i == 0)
                {
                    minTick = command.TargetTick;
                    maxTick = command.TargetTick;
                }
                else
                {
                    if (command.TargetTick < minTick) minTick = command.TargetTick;
                    if (command.TargetTick > maxTick) maxTick = command.TargetTick;
                }
            }
            if (minTick != MinTargetTick || maxTick != MaxTargetTick)
                throw new DeterministicSimulationException(
                    "GameplayCommandBundle target Tick bounds do not match its canonical bytes.");
        }
    }

    public readonly struct AcceptedCommandRelay
    {
        private readonly byte[] canonicalCommandBytesForTick;

        public readonly int TargetTick;
        public readonly uint RelayRevision;

        public byte[] CanonicalCommandBytesForTick =>
            canonicalCommandBytesForTick == null
                ? Array.Empty<byte>()
                : (byte[])canonicalCommandBytesForTick.Clone();

        internal byte[] CanonicalCommandBytesUnsafe =>
            canonicalCommandBytesForTick ?? Array.Empty<byte>();

        public AcceptedCommandRelay(
            int targetTick,
            uint relayRevision,
            byte[] canonicalCommandBytesForTick)
        {
            if (targetTick < 0)
                throw new ArgumentOutOfRangeException(nameof(targetTick));
            TargetTick = targetTick;
            RelayRevision = relayRevision;
            this.canonicalCommandBytesForTick =
                canonicalCommandBytesForTick == null
                    ? throw new ArgumentNullException(
                        nameof(canonicalCommandBytesForTick))
                    : (byte[])canonicalCommandBytesForTick.Clone();
            CanonicalCommandCodec.Decode(
                this.canonicalCommandBytesForTick,
                targetTick);
        }

        public GameplayCommand[] DecodeCommands() =>
            CanonicalCommandCodec.Decode(
                canonicalCommandBytesForTick,
                TargetTick);
    }

    /// <summary>
    /// Server-side, transport-independent canonical replacement buffer.
    /// Network identity checks remain supplied by the application boundary.
    /// </summary>
    public sealed class CommandRelayBuffer
    {
        private readonly SortedDictionary<int, TickRelayState> states =
            new SortedDictionary<int, TickRelayState>();
        private readonly Dictionary<ulong, uint> latestBundleSequenceByClient =
            new Dictionary<ulong, uint>();
        private readonly HashSet<GameplayCommandIdentity>
            acceptedCommandIdentities =
                new HashSet<GameplayCommandIdentity>();

        public AcceptedCommandRelay[] AcceptBundle(
            in GameplayCommandBundle bundle,
            int serverTick,
            int maxFutureCommandTicks,
            Func<GameplayCommand, bool> authorizeCommand)
        {
            if (serverTick < 0)
                throw new ArgumentOutOfRangeException(nameof(serverTick));
            if (maxFutureCommandTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(maxFutureCommandTicks));
            if (latestBundleSequenceByClient.TryGetValue(
                    bundle.ClientId,
                    out uint latestSequence) &&
                bundle.BundleSequence <= latestSequence)
                return Array.Empty<AcceptedCommandRelay>();

            GameplayCommand[] commands = bundle.DecodeCommands();
            int lastAllowedTick = checked(serverTick + maxFutureCommandTicks);
            var touchedTicks = new List<int>();
            for (int i = 0; i < commands.Length; i++)
            {
                GameplayCommand command = commands[i];
                GameplayCommandIdentity identity =
                    GameplayCommandIdentity.From(command);
                if (acceptedCommandIdentities.Contains(identity))
                    continue;

                if (command.TargetTick < serverTick)
                {
                    // Late Command: its targeted Tick has already executed.
                    // Re-target it to the current server Tick so the player's
                    // input survives; the client's ordinary rollback replay
                    // picks it up from the relay/authority frame.
                    command = command.WithTargetTick(serverTick);
                }
                else if (command.TargetTick > lastAllowedTick)
                {
                    throw new DeterministicSimulationException(
                        $"Command Tick {command.TargetTick} is outside server acceptance window [{serverTick}, {lastAllowedTick}].");
                }
                if (authorizeCommand != null && !authorizeCommand(command))
                    throw new DeterministicSimulationException(
                        $"Command {command.CommandSeq} failed its client/unit binding check.");

                if (!states.TryGetValue(
                        command.TargetTick,
                        out TickRelayState state))
                {
                    state = new TickRelayState(command.TargetTick);
                    states.Add(command.TargetTick, state);
                }
                state.Collect(command);
                acceptedCommandIdentities.Add(identity);
                InsertUniqueSorted(touchedTicks, command.TargetTick);
            }

            latestBundleSequenceByClient[bundle.ClientId] =
                bundle.BundleSequence;
            var relays = new AcceptedCommandRelay[touchedTicks.Count];
            for (int i = 0; i < touchedTicks.Count; i++)
                relays[i] = states[touchedTicks[i]].CommitRevisionIfChanged();
            return relays;
        }

        public AcceptedCommandRelay GetCurrentRelay(int targetTick)
        {
            if (states.TryGetValue(targetTick, out TickRelayState state))
                return state.CurrentRelay;
            return new AcceptedCommandRelay(
                targetTick,
                0,
                CanonicalCommandCodec.Encode(Array.Empty<GameplayCommand>()));
        }

        public AcceptedCommandRelay FreezeTick(int targetTick)
        {
            AcceptedCommandRelay relay = GetCurrentRelay(targetTick);
            states.Remove(targetTick);
            return relay;
        }

        private static void InsertUniqueSorted(List<int> ticks, int tick)
        {
            int index = ticks.BinarySearch(tick);
            if (index >= 0) return;
            ticks.Insert(~index, tick);
        }

        private sealed class TickRelayState
        {
            private readonly CommandCollector collector = new CommandCollector();
            private readonly int targetTick;
            private byte[] committedBytes;
            private uint revision;

            public TickRelayState(int targetTick)
            {
                this.targetTick = targetTick;
                collector.BeginTick(targetTick);
                committedBytes =
                    CanonicalCommandCodec.Encode(Array.Empty<GameplayCommand>());
            }

            public AcceptedCommandRelay CurrentRelay =>
                new AcceptedCommandRelay(
                    targetTick,
                    revision,
                    committedBytes);

            public void Collect(GameplayCommand command)
            {
                collector.Collect(command);
            }

            public AcceptedCommandRelay CommitRevisionIfChanged()
            {
                byte[] current =
                    CanonicalCommandCodec.Encode(
                        collector.GetCanonicalCommands());
                if (!CanonicalCommandCodec.ByteArrayEquals(
                        current,
                        committedBytes))
                {
                    if (revision == uint.MaxValue)
                        throw new DeterministicSimulationException(
                            $"Relay revision exhausted for Tick {targetTick}.");
                    revision++;
                    committedBytes = current;
                }
                return CurrentRelay;
            }
        }

    }

    public sealed class AuthorityRecoveryArchive
    {
        private readonly SortedDictionary<int, AuthorityFrame> frames =
            new SortedDictionary<int, AuthorityFrame>();
        private readonly int capacity;

        public AuthorityRecoveryArchive(int capacity)
        {
            if (capacity < 2)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }

        public int EarliestRetainedTick
        {
            get
            {
                foreach (int tick in frames.Keys) return tick;
                return -1;
            }
        }

        public int LatestRetainedTick
        {
            get
            {
                int latest = -1;
                foreach (int tick in frames.Keys) latest = tick;
                return latest;
            }
        }

        public void Add(in AuthorityFrame frame)
        {
            if (frames.ContainsKey(frame.Tick))
                throw new DeterministicSimulationException(
                    $"AuthorityFrame {frame.Tick} was already archived.");
            if (LatestRetainedTick >= frame.Tick)
                throw new DeterministicSimulationException(
                    "AuthorityFrames must be archived in ascending Tick order.");
            frames.Add(frame.Tick, frame);
            while (frames.Count > capacity)
            {
                int earliest = EarliestRetainedTick;
                frames.Remove(earliest);
            }
        }

        public AuthorityRecoveryResponse BuildResponse(
            in AuthorityRecoveryRequest request)
        {
            MissingAuthorityFrameRange[] ranges = request.MissingRanges;
            if (request.RequestSequence == 0 || ranges.Length == 0)
                throw new DeterministicSimulationException(
                    "AuthorityRecovery requires a nonzero sequence and at least one range.");
            var result = new List<AuthorityFrame>();
            int previousToTick = -1;
            for (int rangeIndex = 0; rangeIndex < ranges.Length; rangeIndex++)
            {
                MissingAuthorityFrameRange range = ranges[rangeIndex];
                if (range.FromTick < 0 ||
                    range.ToTick < range.FromTick ||
                    range.FromTick <= previousToTick)
                    throw new DeterministicSimulationException(
                        "AuthorityRecovery ranges must be valid, disjoint and ascending.");
                previousToTick = range.ToTick;
                for (int tick = range.FromTick; tick <= range.ToTick; tick++)
                {
                    if (!frames.TryGetValue(tick, out AuthorityFrame frame))
                        throw new AuthorityRecoveryUnavailableException(tick);
                    result.Add(frame);
                    if (tick == int.MaxValue) break;
                }
            }
            return new AuthorityRecoveryResponse(
                request.RequestSequence,
                result.ToArray());
        }
    }

    public sealed class AuthorityRecoveryUnavailableException : Exception
    {
        public int MissingTick { get; }

        public AuthorityRecoveryUnavailableException(int missingTick)
            : base(
                $"AuthorityFrame {missingTick} is no longer available; the client match connection must terminate.")
        {
            MissingTick = missingTick;
        }
    }

    public sealed class AuthorityFrameReplicator
    {
        private readonly SimulationTickPipeline pipeline;
        private readonly SimulationTickContextController tickController;
        private readonly CommandRelayBuffer relayBuffer;
        private readonly AuthorityRecoveryArchive recoveryArchive;
        private uint nextFrameSequence = 1;

        public int ServerTick => pipeline.LocalSimulationTick;
        public event Action<AuthorityFrame> AuthorityFrameBuilt;

        public AuthorityFrameReplicator(
            SimulationTickPipeline pipeline,
            SimulationTickContextController tickController,
            CommandRelayBuffer relayBuffer,
            AuthorityRecoveryArchive recoveryArchive)
        {
            this.pipeline = pipeline ??
                throw new ArgumentNullException(nameof(pipeline));
            this.tickController = tickController ??
                throw new ArgumentNullException(nameof(tickController));
            this.relayBuffer = relayBuffer ??
                throw new ArgumentNullException(nameof(relayBuffer));
            this.recoveryArchive = recoveryArchive ??
                throw new ArgumentNullException(nameof(recoveryArchive));
        }

        public AuthorityFrame ExecuteNextTick()
        {
            int tick = ServerTick;
            AcceptedCommandRelay relay = relayBuffer.FreezeTick(tick);
            GameplayCommand[] commands = relay.DecodeCommands();
            pipeline.ReplaceCommandsForNextTick(commands);
            pipeline.ExecuteTick(
                tickController,
                ExecutionMode.ServerAuthority);

            AuthorityFrameFlags flags =
                pipeline.MatchRule != null &&
                pipeline.MatchRule.GameOverTick == tick
                    ? AuthorityFrameFlags.MatchEndCandidate
                    : AuthorityFrameFlags.None;
            if (nextFrameSequence == uint.MaxValue)
                throw new DeterministicSimulationException(
                    "AuthorityFrame sequence exhausted.");
            AuthorityFrame frame = AuthorityFrame.Create(
                tick,
                nextFrameSequence++,
                relay.RelayRevision,
                commands,
                flags,
                pipeline.LastChecksum);
            // D-032: full Snapshot/unit diagnostics are explicitly opt-in.
            // They synchronously capture and format the whole world, so never
            // place them on the healthy server Tick path by default.
            if (SharedGameplayChecksum.DetailedLoggingEnabled)
            {
                PrintDetailedChecksum(tick, pipeline);
            }
            pipeline.GoldIncome?.ConfirmThroughTick(tick);
            recoveryArchive.Add(frame);
            AuthorityFrameBuilt?.Invoke(frame);
            return frame;
        }

        private static void PrintDetailedChecksum(
            int tick,
            SimulationTickPipeline pipeline)
        {
            GameplaySnapshot snapshot =
                pipeline.CaptureAggregateSnapshot();
            var lines = new System.Collections.Generic.List<string>
            {
                $"[ChecksumDetail] Tick {tick} server segments:",
            };
            GoldIncomeBatchDigest digest =
                pipeline.GoldIncome?.GetBatchDigest(tick) ??
                new GoldIncomeBatchDigest(0);
            SharedGameplayChecksum.ChecksumSegment[] segments =
                SharedGameplayChecksum.ComputeSegmentHashes(
                    snapshot,
                    digest);
            for (int i = 0; i < segments.Length; i++)
            {
                lines.Add(
                    $"  {segments[i].Label}={segments[i].Hash}");
            }
            ChecksumDiagnosticFormatter.AppendWorldState(
                lines,
                snapshot);
            UnitSnapshot[] units =
                snapshot.UnitWorldState.Units ??
                System.Array.Empty<UnitSnapshot>();
            for (int u = 0; u < units.Length; u++)
            {
                lines.Add(
                    $"  Unit {units[u].UnitUid} " +
                    $"(kind={units[u].UnitKind}):");
                ChecksumDiagnosticFormatter.AppendUnitState(
                    lines,
                    units[u]);
                SharedGameplayChecksum.ChecksumSegment[] handlers =
                    SharedGameplayChecksum
                        .ComputeUnitHandlerHashes(units[u]);
                for (int h = 0;
                     h < handlers.Length;
                     h++)
                {
                    lines.Add(
                        $"    {handlers[h].Label}=" +
                        $"{handlers[h].Hash}");
                }
                ChecksumDiagnosticFormatter.AppendEquipmentSlots(
                    lines,
                    units[u]);
                var ccInstances =
                    units[u].CCState.Instances;
                lines.Add(
                    $"    ccPendingSignals={units[u].CCState.PendingSignals} " +
                    $"ccNextInstance={units[u].CCState.NextInstanceId} " +
                    $"ccForcedMove={units[u].CCState.ActiveForcedMoveHandle.InstanceId}");
                if (ccInstances != null &&
                    ccInstances.Count > 0)
                {
                    for (int c = 0;
                         c < ccInstances.Count;
                         c++)
                    {
                        var inst = ccInstances[c];
                        lines.Add(
                            $"      CC id={inst.ControlId.Value} " +
                            $"inst={inst.InstanceId} " +
                            $"start={inst.StartTick} " +
                            $"expire={inst.ExpireTick}");
                    }
                }
            }
            ChecksumDiagnosticFormatter.AppendShopState(
                lines,
                snapshot.EquipmentShopState);
            UnityEngine.Debug.Log(
                string.Join(
                    System.Environment.NewLine,
                    lines));
        }
    }

    /// <summary>
    /// Client-side retry policy. Time is expressed only as local logic/control
    /// Ticks supplied by the caller; it never reads Unity time.
    /// </summary>
    public sealed class AuthorityRecoveryCoordinator
    {
        private readonly PredictionRollbackCoordinator rollback;
        private readonly int retryTicks;
        private readonly int maximumAttempts;
        private int lastRequestControlTick = -1;
        private int attempts;

        public int AttemptCount => attempts;
        public bool ConnectionMustTerminate { get; private set; }

        public AuthorityRecoveryCoordinator(
            PredictionRollbackCoordinator rollback,
            int retryTicks,
            int maximumAttempts)
        {
            this.rollback = rollback ??
                throw new ArgumentNullException(nameof(rollback));
            if (retryTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(retryTicks));
            if (maximumAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
            this.retryTicks = retryTicks;
            this.maximumAttempts = maximumAttempts;
        }

        public bool TryCreateRequest(
            int controlTick,
            out AuthorityRecoveryRequest request)
        {
            if (controlTick < 0)
                throw new ArgumentOutOfRangeException(nameof(controlTick));
            request = default;
            if (ConnectionMustTerminate) return false;
            if (!rollback.HasMissingAuthorityFrames)
            {
                ResetAttempts();
                return false;
            }
            if (lastRequestControlTick >= 0 &&
                controlTick - lastRequestControlTick < retryTicks)
                return false;
            if (attempts >= maximumAttempts)
            {
                ConnectionMustTerminate = true;
                return false;
            }

            request = rollback.BuildRecoveryRequest();
            if (request.RequestSequence == 0) return false;
            attempts++;
            lastRequestControlTick = controlTick;
            return true;
        }

        public bool ApplyResponse(in AuthorityRecoveryResponse response)
        {
            if (ConnectionMustTerminate) return false;
            bool applied = rollback.ApplyRecoveryResponse(response);
            if (applied && !rollback.HasMissingAuthorityFrames)
                ResetAttempts();
            return applied;
        }

        public void MarkRecoveryUnavailable()
        {
            ConnectionMustTerminate = true;
        }

        private void ResetAttempts()
        {
            attempts = 0;
            lastRequestControlTick = -1;
        }
    }
}
