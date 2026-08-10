using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    [Flags]
    public enum PredictionPauseReason : byte
    {
        None = 0,
        MissingAuthorityFrame = 1 << 0,
        PredictionLeadLimit = 1 << 1,
        MatchEndCandidate = 1 << 2,
    }

    public readonly struct LocalFrameVerificationRecord
    {
        public readonly int LogicTick;
        public readonly uint SharedGameplayChecksum;
        public LocalFrameVerificationRecord(int logicTick, uint checksum)
        { LogicTick = logicTick; SharedGameplayChecksum = checksum; }
    }

    public readonly struct MissingAuthorityFrameRange
    {
        public readonly int FromTick;
        public readonly int ToTick;
        public MissingAuthorityFrameRange(int fromTick, int toTick)
        {
            if (fromTick < 0 || toTick < fromTick)
                throw new ArgumentOutOfRangeException(nameof(fromTick));
            FromTick = fromTick;
            ToTick = toTick;
        }
    }

    public readonly struct AuthorityRecoveryRequest
    {
        private readonly MissingAuthorityFrameRange[] missingRanges;
        public readonly uint RequestSequence;
        public MissingAuthorityFrameRange[] MissingRanges =>
            missingRanges == null
                ? Array.Empty<MissingAuthorityFrameRange>()
                : (MissingAuthorityFrameRange[])missingRanges.Clone();
        public AuthorityRecoveryRequest(uint sequence, MissingAuthorityFrameRange[] ranges)
        {
            RequestSequence = sequence;
            missingRanges = ranges == null
                ? Array.Empty<MissingAuthorityFrameRange>()
                : (MissingAuthorityFrameRange[])ranges.Clone();
        }
    }

    public readonly struct AuthorityRecoveryResponse
    {
        private readonly AuthorityFrame[] authorityFrames;
        public readonly uint RequestSequence;
        public AuthorityFrame[] AuthorityFrames =>
            authorityFrames == null
                ? Array.Empty<AuthorityFrame>()
                : (AuthorityFrame[])authorityFrames.Clone();
        public AuthorityRecoveryResponse(uint sequence, AuthorityFrame[] frames)
        {
            RequestSequence = sequence;
            authorityFrames = frames == null
                ? Array.Empty<AuthorityFrame>()
                : (AuthorityFrame[])frames.Clone();
        }
    }

    /// <summary>
    /// Owns continuous AuthorityFrame acceptance, local verification history,
    /// rollback anchors and deterministic correction replay.
    /// </summary>
    public sealed class PredictionRollbackCoordinator
    {
        private readonly SnapshotStore store;
        private readonly SimulationTickPipeline pipeline;
        private readonly SimulationTickContextController tickController;
        private readonly SortedDictionary<int, AuthorityFrame> authorityBuffer =
            new SortedDictionary<int, AuthorityFrame>();
        private readonly Dictionary<int, CommandHistoryRecord> commandHistory =
            new Dictionary<int, CommandHistoryRecord>();
        private readonly Dictionary<int, LocalFrameVerificationRecord> verificationByTick =
            new Dictionary<int, LocalFrameVerificationRecord>();
        private readonly List<Action<RollbackContext>> resolveRegistrations = new List<Action<RollbackContext>>();
        private readonly List<Action<RollbackContext>> rebuildRegistrations = new List<Action<RollbackContext>>();
        private readonly List<Action<GameplaySnapshot>> captureRegistrations = new List<Action<GameplaySnapshot>>();
        private readonly List<Action<GameplaySnapshot>> restoreRegistrations = new List<Action<GameplaySnapshot>>();
        private uint latestFrameSequence;
        private bool hasAcceptedFrameSequence;
        private uint nextRecoveryRequestSequence = 1;
        private uint latestRecoveryRequestSequence;
        private MissingAuthorityFrameRange[] latestRecoveryRanges =
            Array.Empty<MissingAuthorityFrameRange>();
        private bool replaying;
        private readonly int maxPredictionLeadTicks;

        public int LatestAuthorityFrameTick { get; private set; } = -1;
        public int LocalSimulationTick => pipeline.LocalSimulationTick;
        public int SnapshotTick { get; private set; } = -1;
        public int PredictedMatchEndCandidateTick { get; private set; } = -1;
        public int PredictedTickCount =>
            pipeline.LocalSimulationTick - (LatestAuthorityFrameTick + 1);
        public bool HasMissingAuthorityFrames =>
            (PauseReasons & PredictionPauseReason.MissingAuthorityFrame) != 0;
        public PredictionPauseReason PauseReasons { get; private set; }
        public NonHeroRestoreHelper NonHeroHelper { get; set; }
        public IReadOnlyDictionary<int, LocalFrameVerificationRecord> LocalFrameVerificationRecordByTick =>
            verificationByTick;

        public PredictionRollbackCoordinator(
            SnapshotStore store,
            SimulationTickPipeline pipeline,
            SimulationTickContextController tickController = null,
            int maxPredictionLeadTicks = int.MaxValue)
        {
            if (maxPredictionLeadTicks < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(maxPredictionLeadTicks));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            this.tickController = tickController ?? new SimulationTickContextController();
            this.maxPredictionLeadTicks = maxPredictionLeadTicks;
            pipeline.TickCompleted += OnLocalTickCompleted;
            RefreshPredictionLeadPause();
        }

        public void RegisterResolve(Action<RollbackContext> resolve) => resolveRegistrations.Add(resolve);
        public void RegisterRebuild(Action<RollbackContext> rebuild) => rebuildRegistrations.Add(rebuild);
        public void RegisterCapture(Action<GameplaySnapshot> capture) => captureRegistrations.Add(capture);
        public void RegisterRestore(Action<GameplaySnapshot> restore) => restoreRegistrations.Add(restore);

        public void EnsureRollbackAnchor()
        {
            int completedTick = pipeline.LocalSimulationTick - 1;
            if (store.TryGet(completedTick, out _)) return;
            CaptureAndStore(completedTick);
        }

        public void CaptureAndStore(int completedTick)
        {
            GameplaySnapshot snapshot = pipeline.CaptureAggregateSnapshot();
            for (int i = 0; i < captureRegistrations.Count; i++) captureRegistrations[i](snapshot);
            store.Store(completedTick, snapshot);
            SnapshotTick = completedTick + 1;
        }

        public void SetPredictedCommandFrame(
            int tick,
            uint relayRevision,
            IReadOnlyList<GameplayCommand> commands)
        {
            if (tick < LatestAuthorityFrameTick + 1)
                throw new DeterministicSimulationException("Cannot replace Commands for a confirmed Tick.");
            GameplayCommand[] copy = CopyAndValidateCommands(tick, commands);
            commandHistory[tick] = new CommandHistoryRecord(
                relayRevision, copy, CanonicalCommandCodec.Encode(copy));
        }

        public bool ExecutePredictionTick()
        {
            RefreshPredictionLeadPause();
            if (PauseReasons != PredictionPauseReason.None)
                return false;
            EnsureRollbackAnchor();
            int tick = pipeline.LocalSimulationTick;
            if (commandHistory.TryGetValue(tick, out CommandHistoryRecord record))
                pipeline.ReplaceCommandsForNextTick(record.Commands);
            pipeline.ExecuteTick(tickController, ExecutionMode.ClientPrediction);
            ProcessAuthorityFramesSequentially();
            if (pipeline.HasPredictedMatchEndCandidate())
            {
                PredictedMatchEndCandidateTick = tick;
                PauseReasons |= PredictionPauseReason.MatchEndCandidate;
            }
            RefreshPredictionLeadPause();
            return true;
        }

        public void OnAuthorityFrameReceived(in AuthorityFrame frame)
        {
            if (frame.Tick <= LatestAuthorityFrameTick)
            {
                throw new DeterministicSimulationException(
                    $"AuthorityFrame {frame.Tick} is already confirmed through {LatestAuthorityFrameTick}.");
            }
            frame.DecodeCommands();
            if (authorityBuffer.TryGetValue(frame.Tick, out AuthorityFrame existing))
            {
                if (!FramesEqual(existing, frame))
                    throw new DeterministicSimulationException(
                        $"Conflicting AuthorityFrame payloads for Tick {frame.Tick}.");
                return;
            }

            authorityBuffer.Add(frame.Tick, frame);
            RefreshMissingFramePause();
            ProcessAuthorityFramesSequentially();
        }

        public bool ApplyRecoveryResponse(in AuthorityRecoveryResponse response)
        {
            if (response.RequestSequence == 0 ||
                response.RequestSequence != latestRecoveryRequestSequence)
                return false;
            AuthorityFrame[] frames = response.AuthorityFrames;
            for (int i = 0; i < frames.Length; i++)
            {
                if (!IsTickInLatestRecoveryRanges(frames[i].Tick))
                    throw new DeterministicSimulationException(
                        $"AuthorityRecovery response {response.RequestSequence} contains unrequested Tick {frames[i].Tick}.");
            }
            Array.Sort(frames, (left, right) => left.Tick.CompareTo(right.Tick));
            for (int i = 0; i < frames.Length; i++) OnAuthorityFrameReceived(frames[i]);
            if (!HasMissingAuthorityFrames)
            {
                latestRecoveryRequestSequence = 0;
                latestRecoveryRanges = Array.Empty<MissingAuthorityFrameRange>();
            }
            return true;
        }

        public AuthorityRecoveryRequest BuildRecoveryRequest()
        {
            MissingAuthorityFrameRange[] ranges = BuildMissingRanges();
            if (ranges.Length == 0)
                return new AuthorityRecoveryRequest(0, ranges);
            if (nextRecoveryRequestSequence == uint.MaxValue)
                throw new DeterministicSimulationException("AuthorityRecovery request sequence exhausted.");
            uint sequence = nextRecoveryRequestSequence++;
            latestRecoveryRequestSequence = sequence;
            latestRecoveryRanges =
                (MissingAuthorityFrameRange[])ranges.Clone();
            return new AuthorityRecoveryRequest(sequence, ranges);
        }

        public void DiscardConfirmedSnapshots(int newBaseTick) => store.AdvanceBase(newBaseTick);

        internal void InitializeAuthorityBaseline(
            int snapshotTick)
        {
            if (snapshotTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(snapshotTick));
            if (pipeline.LocalSimulationTick != snapshotTick ||
                store.Count != 0 ||
                authorityBuffer.Count != 0 ||
                commandHistory.Count != 0 ||
                verificationByTick.Count != 0 ||
                hasAcceptedFrameSequence)
                throw new DeterministicSimulationException(
                    "Initial authority baseline requires a pristine coordinator restored to SnapshotTick.");

            LatestAuthorityFrameTick =
                snapshotTick - 1;
            SnapshotTick = snapshotTick;
            RefreshMissingFramePause();
            RefreshPredictionLeadPause();
        }

        internal void ReleaseServerAuthorityHistory(int confirmedTick)
        {
            commandHistory.Remove(confirmedTick);
            verificationByTick.Remove(confirmedTick);
            store.AdvanceBase(checked(confirmedTick + 1));
        }

        private void ProcessAuthorityFramesSequentially()
        {
            while (authorityBuffer.TryGetValue(LatestAuthorityFrameTick + 1, out AuthorityFrame frame))
            {
                if (frame.Tick >=
                    pipeline.LocalSimulationTick)
                    break;
                if (hasAcceptedFrameSequence && frame.FrameSequence <= latestFrameSequence)
                    throw new DeterministicSimulationException(
                        $"AuthorityFrame sequence {frame.FrameSequence} is not newer than {latestFrameSequence}.");

                bool commandsMatch = commandHistory.TryGetValue(frame.Tick, out CommandHistoryRecord commands) &&
                    commands.Revision == frame.FinalCommandRevision &&
                    CanonicalCommandCodec.ByteArrayEquals(
                        commands.CanonicalBytes, frame.CanonicalCommandBytes);
                bool checksumMatches = verificationByTick.TryGetValue(
                    frame.Tick, out LocalFrameVerificationRecord verification) &&
                    verification.SharedGameplayChecksum == frame.SharedGameplayChecksum;

                if (!commandsMatch || !checksumMatches)
                    CorrectAndReplay(frame);

                if (!verificationByTick.TryGetValue(frame.Tick, out verification) ||
                    verification.SharedGameplayChecksum != frame.SharedGameplayChecksum)
                {
                    UnityEngine.Debug.LogError(
                        $"[Checksum] Tick {frame.Tick} " +
                        $"local={pipeline.LocalSimulationTick} " +
                        $"sync={LatestAuthorityFrameTick} " +
                        $"expected(server)={frame.SharedGameplayChecksum} " +
                        $"actual(client)=" +
                        $"{(verificationByTick.TryGetValue(frame.Tick, out var v) ? v.SharedGameplayChecksum : 0u)} " +
                        $"commandsMatch={commandsMatch} " +
                        $"commands={frame.CanonicalCommandBytes?.Length ?? 0}");
                    PrintDetailedChecksumDiagnostics(
                        frame.Tick,
                        pipeline);
                    throw new DeterministicSimulationException(
                        $"Authority replay checksum mismatch remains at Tick {frame.Tick}.");
                }

                pipeline.GoldIncome?.ConfirmThroughTick(frame.Tick);
                LatestAuthorityFrameTick = frame.Tick;
                latestFrameSequence = frame.FrameSequence;
                hasAcceptedFrameSequence = true;
                verificationByTick.Remove(frame.Tick);
                authorityBuffer.Remove(frame.Tick);
                store.AdvanceBase(frame.Tick);
                if (PredictedMatchEndCandidateTick == frame.Tick)
                {
                    if ((frame.FrameFlags &
                            AuthorityFrameFlags.MatchEndCandidate) == 0)
                    {
                        PredictedMatchEndCandidateTick = -1;
                        PauseReasons &=
                            ~PredictionPauseReason.MatchEndCandidate;
                    }
                }
                else if ((frame.FrameFlags &
                        AuthorityFrameFlags.MatchEndCandidate) != 0)
                {
                    PredictedMatchEndCandidateTick = frame.Tick;
                    PauseReasons |=
                        PredictionPauseReason.MatchEndCandidate;
                }
            }
            RefreshMissingFramePause();
            RefreshPredictionLeadPause();
        }

        private void CorrectAndReplay(in AuthorityFrame frame)
        {
            int predictedEndTick = pipeline.LocalSimulationTick;
            if (frame.Tick >= predictedEndTick)
                throw new DeterministicSimulationException(
                    $"No local execution record exists for AuthorityFrame Tick {frame.Tick}.");
            if (frame.Tick < LatestAuthorityFrameTick + 1)
                throw new DeterministicSimulationException(
                    "Ordinary rollback cannot cross LatestAuthorityFrameTick + 1.");
            if (!store.TryGet(frame.Tick - 1, out RollbackFrameSnapshot anchor))
                throw new DeterministicSimulationException(
                    $"Missing local rollback anchor at SnapshotTick {frame.Tick}.");

            RemoveVerificationFrom(frame.Tick);
            pipeline.GoldIncome?.DiscardUnconfirmedFromTick(frame.Tick);
            store.DiscardFromTick(frame.Tick);
            bool authorityEndsMatch =
                (frame.FrameFlags &
                    AuthorityFrameFlags.MatchEndCandidate) != 0;
            int replayEndTick = authorityEndsMatch
                ? checked(frame.Tick + 1)
                : predictedEndTick;

            // Ordinary rollback must not drop the player's already-created
            // Commands that target ticks beyond the replay window:
            // ReplaceCommandsForNextTick clears the pipeline collector during
            // the replay, and losing them would permanently desync the
            // client's future prediction against the server's accepted
            // Commands.
            var pendingCommands = new List<GameplayCommand>();
            if (!authorityEndsMatch)
            {
                List<GameplayCommand> current =
                    pipeline.CommandCollector
                        .GetCanonicalCommands();
                for (int i = 0;
                     i < current.Count;
                     i++)
                {
                    if (current[i].TargetTick >=
                        replayEndTick)
                    {
                        pendingCommands.Add(
                            current[i]);
                    }
                }
            }
            UnityEngine.Debug.Log(
                $"[Rollback] tick={frame.Tick} " +
                $"anchorUnits={anchor.Gameplay.UnitWorldState.Units?.Length ?? -1}");
            UnityEngine.Debug.Log(
                $"[Rollback] tick={frame.Tick} " +
                $"anchor={anchor.SnapshotTick} " +
                $"replayEnd={replayEndTick} " +
                $"predictedEnd={predictedEndTick} " +
                $"pendingPreserved={pendingCommands.Count} " +
                $"authorityEndsMatch={authorityEndsMatch}");

            if (authorityEndsMatch)
                RemoveCommandHistoryAfter(frame.Tick);
            pipeline.RestoreFromSnapshot(
                anchor.Gameplay, anchor.SnapshotTick, ExecutionMode.ClientReplay);
            for (int i = 0; i < restoreRegistrations.Count; i++) restoreRegistrations[i](anchor.Gameplay);
            var context = new RollbackContext(frame.Tick, ExecutionMode.ClientReplay);
            for (int i = 0; i < resolveRegistrations.Count; i++) resolveRegistrations[i](context);
            NonHeroHelper?.ResolveNonHero(context);
            for (int i = 0; i < rebuildRegistrations.Count; i++) rebuildRegistrations[i](context);
            NonHeroHelper?.RebuildNonHero(context);

            GameplayCommand[] authoritativeCommands = frame.DecodeCommands();
            commandHistory[frame.Tick] = new CommandHistoryRecord(
                frame.FinalCommandRevision,
                authoritativeCommands,
                (byte[])frame.CanonicalCommandBytesUnsafe.Clone());

            replaying = true;
            pipeline.AuthorityReplayTick = frame.Tick;
            try
            {
                for (int tick = frame.Tick; tick < replayEndTick; tick++)
                {
                    IReadOnlyList<GameplayCommand> commands = GetReplayCommands(tick, frame);
                    pipeline.ReplaceCommandsForNextTick(commands);
                    pipeline.ExecuteTick(tickController, ExecutionMode.ClientReplay);
                    if (tick == frame.Tick)
                    {
                        // The verification compares the state AT frame.Tick,
                        // not the post-replay end state. Log positions at the
                        // first replayed tick so they are comparable to the
                        // server's frame.Tick detail.
                        var firstTickSnapshot =
                            pipeline.CaptureAggregateSnapshot();
                        var firstDigest =
                            pipeline.GoldIncome?.GetBatchDigest(
                                frame.Tick) ??
                            new GoldIncomeBatchDigest(0);
                        var firstSegments =
                            SharedGameplayChecksum
                                .ComputeSegmentHashes(
                                    firstTickSnapshot,
                                    firstDigest);
                        var firstLines =
                            new System.Collections.Generic.List<string>
                            {
                                $"[ReplaySegs] tick={frame.Tick} " +
                                $"server={frame.SharedGameplayChecksum} " +
                                $"local={pipeline.LastChecksum}",
                            };
                        for (int si = 0;
                             si < firstSegments.Length;
                             si++)
                        {
                            firstLines.Add(
                                $"  {firstSegments[si].Label}=" +
                                $"{firstSegments[si].Hash}");
                        }
                        UnityEngine.Debug.Log(
                            string.Join(
                                System.Environment.NewLine,
                                firstLines));
                        var firstUnits =
                            firstTickSnapshot.UnitWorldState.Units;
                        for (int ui = 0;
                             ui < firstUnits.Length;
                             ui++)
                        {
                            var pu = firstUnits[ui].PhysicsTransform.Position;
                            UnityEngine.Debug.Log(
                                $"[ReplayFirst] tick={frame.Tick} " +
                                $"unit={firstUnits[ui].UnitUid} " +
                                $"pos=({pu.x},{pu.y})");
                        }
                    }
                }
            }
            finally
            {
                replaying = false;
                pipeline.AuthorityReplayTick = -1;
            }
            if (pendingCommands.Count > 0)
            {
                pipeline.ReplaceCommandsForNextTick(
                    pendingCommands);
            }
            uint replayedTickChecksum =
                verificationByTick.TryGetValue(
                    frame.Tick,
                    out LocalFrameVerificationRecord
                        replayedRecord)
                    ? replayedRecord.SharedGameplayChecksum
                    : 0u;
            UnityEngine.Debug.Log(
                $"[Rollback] replay done tick={frame.Tick} " +
                $"local={pipeline.LocalSimulationTick} " +
                $"tickChecksum={replayedTickChecksum} " +
                $"server={frame.SharedGameplayChecksum} " +
                $"match={replayedTickChecksum == frame.SharedGameplayChecksum}");
        }

        private static void PrintDetailedChecksumDiagnostics(
            int tick,
            SimulationTickPipeline pipeline)
        {
            GameplaySnapshot predicted =
                pipeline.CaptureAggregateSnapshot();
            var lines = new System.Collections.Generic.List<string>
            {
                $"[ChecksumDetail] Tick {tick} predicted(client) segments:",
            };
            GoldIncomeBatchDigest digest =
                pipeline.GoldIncome?.GetBatchDigest(tick) ??
                new GoldIncomeBatchDigest(0);
            SharedGameplayChecksum.ChecksumSegment[] segments =
                SharedGameplayChecksum.ComputeSegmentHashes(
                    predicted,
                    digest);
            for (int i = 0; i < segments.Length; i++)
            {
                lines.Add(
                    $"  {segments[i].Label}={segments[i].Hash}");
            }
            UnitSnapshot[] units =
                predicted.UnitWorldState.Units ??
                System.Array.Empty<UnitSnapshot>();
            for (int u = 0; u < units.Length; u++)
            {
                lines.Add(
                    $"  Unit {units[u].UnitUid} " +
                    $"(kind={units[u].UnitKind}):");
                var pos = units[u].PhysicsTransform.Position;
                lines.Add(
                    $"    pos=({pos.x},{pos.y})");
                var loco = units[u].LocomotionState;
                lines.Add(
                    $"    locoActive={loco.HasActiveTask} " +
                    $"purpose={loco.Task.Purpose} " +
                    $"state={loco.Task.State} " +
                    $"cursor={loco.FollowerState.PathCursor} " +
                    $"routeFinished={loco.FollowerState.RouteFinished} " +
                    $"needRepath={loco.Route.NeedRepath}");
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
            UnityEngine.Debug.LogError(
                string.Join(
                    System.Environment.NewLine,
                    lines));
        }

        private IReadOnlyList<GameplayCommand> GetReplayCommands(int tick, in AuthorityFrame currentFrame)
        {
            if (tick == currentFrame.Tick) return commandHistory[tick].Commands;
            if (authorityBuffer.TryGetValue(tick, out AuthorityFrame authority))
            {
                GameplayCommand[] commands = authority.DecodeCommands();
                commandHistory[tick] = new CommandHistoryRecord(
                    authority.FinalCommandRevision, commands,
                    (byte[])authority.CanonicalCommandBytesUnsafe.Clone());
                return commands;
            }
            return commandHistory.TryGetValue(tick, out CommandHistoryRecord predicted)
                ? predicted.Commands
                : Array.Empty<GameplayCommand>();
        }

        private void OnLocalTickCompleted(
            int tick,
            IReadOnlyList<GameplayCommand> commands,
            uint checksum)
        {
            if (tick <= 5)
                UnityEngine.Debug.Log(
                    $"[Checksum] Client local Tick {tick} " +
                    $"checksum={checksum} commands={commands.Count}");
            if (!commandHistory.TryGetValue(tick, out CommandHistoryRecord record) || replaying)
            {
                GameplayCommand[] copy = CopyAndValidateCommands(tick, commands);
                uint revision = commandHistory.TryGetValue(tick, out record) ? record.Revision : 0;
                commandHistory[tick] = new CommandHistoryRecord(
                    revision, copy, CanonicalCommandCodec.Encode(copy));
            }
            verificationByTick[tick] = new LocalFrameVerificationRecord(tick, checksum);
            CaptureAndStore(tick);
        }

        private void RemoveVerificationFrom(int tick)
        {
            var keys = new List<int>();
            foreach (int key in verificationByTick.Keys)
                if (key >= tick) keys.Add(key);
            for (int i = 0; i < keys.Count; i++) verificationByTick.Remove(keys[i]);
        }

        private void RemoveCommandHistoryAfter(int tick)
        {
            var keys = new List<int>();
            foreach (int key in commandHistory.Keys)
                if (key > tick) keys.Add(key);
            for (int i = 0; i < keys.Count; i++)
                commandHistory.Remove(keys[i]);
        }

        private MissingAuthorityFrameRange[] BuildMissingRanges()
        {
            if (authorityBuffer.Count == 0) return Array.Empty<MissingAuthorityFrameRange>();
            int expected = LatestAuthorityFrameTick + 1;
            var ranges = new List<MissingAuthorityFrameRange>();
            foreach (int bufferedTick in authorityBuffer.Keys)
            {
                if (bufferedTick > expected)
                    ranges.Add(new MissingAuthorityFrameRange(expected, bufferedTick - 1));
                if (bufferedTick >= expected) expected = bufferedTick + 1;
            }
            return ranges.ToArray();
        }

        private void RefreshMissingFramePause()
        {
            if (BuildMissingRanges().Length != 0)
                PauseReasons |= PredictionPauseReason.MissingAuthorityFrame;
            else
                PauseReasons &= ~PredictionPauseReason.MissingAuthorityFrame;
        }

        private void RefreshPredictionLeadPause()
        {
            if (PredictedTickCount >= maxPredictionLeadTicks)
                PauseReasons |= PredictionPauseReason.PredictionLeadLimit;
            else
                PauseReasons &= ~PredictionPauseReason.PredictionLeadLimit;
        }

        private bool IsTickInLatestRecoveryRanges(int tick)
        {
            for (int i = 0; i < latestRecoveryRanges.Length; i++)
            {
                MissingAuthorityFrameRange range = latestRecoveryRanges[i];
                if (tick >= range.FromTick && tick <= range.ToTick)
                    return true;
            }
            return false;
        }

        private static GameplayCommand[] CopyAndValidateCommands(
            int tick,
            IReadOnlyList<GameplayCommand> commands)
        {
            if (commands == null || commands.Count == 0) return Array.Empty<GameplayCommand>();
            var copy = new GameplayCommand[commands.Count];
            for (int i = 0; i < commands.Count; i++)
            {
                if (commands[i].TargetTick != tick)
                    throw new DeterministicSimulationException(
                        $"Command history Tick {tick} contains Command for {commands[i].TargetTick}.");
                copy[i] = commands[i];
            }
            return copy;
        }

        private static bool FramesEqual(in AuthorityFrame left, in AuthorityFrame right) =>
            left.Tick == right.Tick &&
            left.FrameSequence == right.FrameSequence &&
            left.FinalCommandRevision == right.FinalCommandRevision &&
            left.FrameFlags == right.FrameFlags &&
            left.SharedGameplayChecksum == right.SharedGameplayChecksum &&
            CanonicalCommandCodec.ByteArrayEquals(
                left.CanonicalCommandBytes, right.CanonicalCommandBytes);

        private readonly struct CommandHistoryRecord
        {
            public readonly uint Revision;
            public readonly GameplayCommand[] Commands;
            public readonly byte[] CanonicalBytes;
            public CommandHistoryRecord(uint revision, GameplayCommand[] commands, byte[] bytes)
            { Revision = revision; Commands = commands; CanonicalBytes = bytes; }
        }
    }
}
