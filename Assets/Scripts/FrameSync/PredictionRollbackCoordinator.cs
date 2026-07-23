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
        { FromTick = fromTick; ToTick = toTick; }
    }

    public readonly struct AuthorityRecoveryRequest
    {
        public readonly uint RequestSequence;
        public readonly MissingAuthorityFrameRange[] MissingRanges;
        public AuthorityRecoveryRequest(uint sequence, MissingAuthorityFrameRange[] ranges)
        { RequestSequence = sequence; MissingRanges = ranges ?? Array.Empty<MissingAuthorityFrameRange>(); }
    }

    public readonly struct AuthorityRecoveryResponse
    {
        public readonly uint RequestSequence;
        public readonly AuthorityFrame[] AuthorityFrames;
        public AuthorityRecoveryResponse(uint sequence, AuthorityFrame[] frames)
        { RequestSequence = sequence; AuthorityFrames = frames ?? Array.Empty<AuthorityFrame>(); }
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
        private bool replaying;

        public int LatestAuthorityFrameTick { get; private set; } = -1;
        public int LocalSimulationTick => pipeline.LocalSimulationTick;
        public int SnapshotTick { get; private set; } = -1;
        public PredictionPauseReason PauseReasons { get; private set; }
        public NonHeroRestoreHelper NonHeroHelper { get; set; }
        public IReadOnlyDictionary<int, LocalFrameVerificationRecord> LocalFrameVerificationRecordByTick =>
            verificationByTick;

        public PredictionRollbackCoordinator(
            SnapshotStore store,
            SimulationTickPipeline pipeline,
            SimulationTickContextController tickController = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            this.tickController = tickController ?? new SimulationTickContextController();
            pipeline.TickCompleted += OnLocalTickCompleted;
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

        public void ExecutePredictionTick()
        {
            EnsureRollbackAnchor();
            int tick = pipeline.LocalSimulationTick;
            if (commandHistory.TryGetValue(tick, out CommandHistoryRecord record))
                pipeline.ReplaceCommandsForNextTick(record.Commands);
            pipeline.ExecuteTick(tickController, ExecutionMode.ClientPrediction);
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

        public void ApplyRecoveryResponse(in AuthorityRecoveryResponse response)
        {
            AuthorityFrame[] frames = response.AuthorityFrames ?? Array.Empty<AuthorityFrame>();
            Array.Sort(frames, (left, right) => left.Tick.CompareTo(right.Tick));
            for (int i = 0; i < frames.Length; i++) OnAuthorityFrameReceived(frames[i]);
        }

        public AuthorityRecoveryRequest BuildRecoveryRequest()
        {
            MissingAuthorityFrameRange[] ranges = BuildMissingRanges();
            if (ranges.Length == 0)
                return new AuthorityRecoveryRequest(0, ranges);
            if (nextRecoveryRequestSequence == uint.MaxValue)
                throw new DeterministicSimulationException("AuthorityRecovery request sequence exhausted.");
            return new AuthorityRecoveryRequest(nextRecoveryRequestSequence++, ranges);
        }

        public void DiscardConfirmedSnapshots(int newBaseTick) => store.AdvanceBase(newBaseTick);

        private void ProcessAuthorityFramesSequentially()
        {
            while (authorityBuffer.TryGetValue(LatestAuthorityFrameTick + 1, out AuthorityFrame frame))
            {
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
                    throw new DeterministicSimulationException(
                        $"Authority replay checksum mismatch remains at Tick {frame.Tick}.");

                pipeline.GoldIncome?.ConfirmThroughTick(frame.Tick);
                LatestAuthorityFrameTick = frame.Tick;
                latestFrameSequence = frame.FrameSequence;
                hasAcceptedFrameSequence = true;
                verificationByTick.Remove(frame.Tick);
                authorityBuffer.Remove(frame.Tick);
                store.AdvanceBase(frame.Tick);
            }
            RefreshMissingFramePause();
        }

        private void CorrectAndReplay(in AuthorityFrame frame)
        {
            int replayEndTick = pipeline.LocalSimulationTick;
            if (frame.Tick >= replayEndTick)
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
                (byte[])frame.CanonicalCommandBytes.Clone());

            replaying = true;
            pipeline.AuthorityReplayTick = frame.Tick;
            try
            {
                for (int tick = frame.Tick; tick < replayEndTick; tick++)
                {
                    IReadOnlyList<GameplayCommand> commands = GetReplayCommands(tick, frame);
                    pipeline.ReplaceCommandsForNextTick(commands);
                    pipeline.ExecuteTick(tickController, ExecutionMode.ClientReplay);
                }
            }
            finally
            {
                replaying = false;
                pipeline.AuthorityReplayTick = -1;
            }
        }

        private IReadOnlyList<GameplayCommand> GetReplayCommands(int tick, in AuthorityFrame currentFrame)
        {
            if (tick == currentFrame.Tick) return commandHistory[tick].Commands;
            if (authorityBuffer.TryGetValue(tick, out AuthorityFrame authority))
            {
                GameplayCommand[] commands = authority.DecodeCommands();
                commandHistory[tick] = new CommandHistoryRecord(
                    authority.FinalCommandRevision, commands,
                    (byte[])authority.CanonicalCommandBytes.Clone());
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
