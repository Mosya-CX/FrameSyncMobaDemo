using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    public readonly struct GoldIncomeBatchDigest
    {
        public readonly ulong Value;

        public GoldIncomeBatchDigest(ulong value) => Value = value;

        public static GoldIncomeBatchDigest Compute(
            in GoldIncomeRecordBatch batch,
            CanonicalByteWriter reusableWriter)
        {
            reusableWriter.Reset();
            reusableWriter.WriteInt32(batch.LogicTick);
            GoldIncomeRecord[] records = batch.Records ?? Array.Empty<GoldIncomeRecord>();
            reusableWriter.WriteInt32(records.Length);
            for (int i = 0; i < records.Length; i++)
            {
                GoldIncomeRecord record = records[i];
                reusableWriter.WriteInt32(record.PlayerSlot);
                reusableWriter.WriteInt32(record.Amount);
                reusableWriter.WriteByte((byte)record.Reason);
                reusableWriter.WriteInt32(record.IncomeSequenceInTick);
            }

            ArraySegment<byte> bytes = reusableWriter.GetWrittenSegment();
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (int i = 0; i < bytes.Count; i++)
            {
                hash ^= bytes.Array[bytes.Offset + i];
                hash *= prime;
            }
            return new GoldIncomeBatchDigest(hash);
        }
    }

    public enum GoldIncomeReason : byte
    {
        NaturalIncome = 0,
        UnitKill = 1,
        StructureDestruction = 2,
        MapObjective = 3,
    }

    public readonly struct GoldIncomeRecord
    {
        public readonly int PlayerSlot;
        public readonly int Amount;
        public readonly GoldIncomeReason Reason;
        public readonly int IncomeSequenceInTick;

        public GoldIncomeRecord(
            int playerSlot,
            int amount,
            GoldIncomeReason reason,
            int incomeSequenceInTick)
        {
            PlayerSlot = playerSlot;
            Amount = amount;
            Reason = reason;
            IncomeSequenceInTick = incomeSequenceInTick;
        }
    }

    public struct GoldIncomeRecordBatch
    {
        public int LogicTick;
        public GoldIncomeRecord[] Records;
        public GoldIncomeBatchDigest Digest;
    }

    public struct GoldIncomeSnapshot
    {
        public int ConfirmedIncomeThroughTick;
        public System.Collections.Generic.List<int> ConfirmedEarnedGoldTotals;
        public System.Collections.Generic.List<GoldIncomeRecordBatch> UnconfirmedBatches;
        public static readonly GoldIncomeSnapshot Default = default;
    }

    public sealed class GoldIncomeRuntime
    {
        private enum BuildState : byte
        {
            Idle = 0,
            AcceptingRequests = 1,
        }

        private readonly List<int> confirmedEarnedGoldTotals = new List<int>();
        private readonly List<GoldIncomeRecordBatch> unconfirmedBatches =
            new List<GoldIncomeRecordBatch>();
        private readonly List<GoldIncomeRecord> currentRecords =
            new List<GoldIncomeRecord>();
        private readonly CanonicalByteWriter digestWriter =
            new CanonicalByteWriter(new byte[8192]);
        private int confirmedIncomeThroughTick = -1;
        private int currentBuildingTick = -1;
        private int nextIncomeSequenceInTick;
        private BuildState buildState;

        public void Initialize(int maxPlayers, int initialEarnedGold)
        {
            if (maxPlayers < 0) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            if (initialEarnedGold < 0)
                throw new ArgumentOutOfRangeException(nameof(initialEarnedGold));
            int[] values = new int[maxPlayers];
            for (int i = 0; i < values.Length; i++) values[i] = initialEarnedGold;
            Initialize(0, values);
        }

        public void Initialize(
            int matchStartTick,
            IReadOnlyList<int> initialEarnedGoldByPlayer)
        {
            if (matchStartTick < 0)
                throw new ArgumentOutOfRangeException(nameof(matchStartTick));
            if (initialEarnedGoldByPlayer == null)
                throw new ArgumentNullException(nameof(initialEarnedGoldByPlayer));

            confirmedEarnedGoldTotals.Clear();
            for (int i = 0; i < initialEarnedGoldByPlayer.Count; i++)
            {
                int value = initialEarnedGoldByPlayer[i];
                if (value < 0)
                    throw new ArgumentOutOfRangeException(
                        nameof(initialEarnedGoldByPlayer));
                confirmedEarnedGoldTotals.Add(value);
            }
            confirmedIncomeThroughTick = matchStartTick - 1;
            unconfirmedBatches.Clear();
            currentRecords.Clear();
            currentBuildingTick = -1;
            nextIncomeSequenceInTick = 0;
            buildState = BuildState.Idle;
        }

        public void BeginTick(int logicTick)
        {
            if (buildState != BuildState.Idle)
                throw new DeterministicSimulationException(
                    $"Gold Tick {currentBuildingTick} has not been sealed.");
            if (logicTick <= confirmedIncomeThroughTick)
                throw new DeterministicSimulationException(
                    $"Gold Tick {logicTick} is already authority-confirmed.");
            if (FindBatch(logicTick) >= 0)
                throw new DeterministicSimulationException(
                    $"Gold Tick {logicTick} is already sealed.");
            currentBuildingTick = logicTick;
            nextIncomeSequenceInTick = 0;
            currentRecords.Clear();
            buildState = BuildState.AcceptingRequests;
        }

        public void RequestGoldIncome(
            int playerSlot,
            int amount,
            GoldIncomeReason reason)
        {
            if (buildState != BuildState.AcceptingRequests)
                throw new DeterministicSimulationException(
                    "Gold income requests are only legal between BeginTick and SealTick.");
            if (playerSlot < 0 || playerSlot >= confirmedEarnedGoldTotals.Count)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsValidReason(reason)) throw new ArgumentOutOfRangeException(nameof(reason));

            currentRecords.Add(new GoldIncomeRecord(
                playerSlot, amount, reason, nextIncomeSequenceInTick++));
        }

        public GoldIncomeRecordBatch SealTick(int logicTick)
        {
            if (buildState != BuildState.AcceptingRequests || logicTick != currentBuildingTick)
                throw new DeterministicSimulationException(
                    $"Cannot seal Gold Tick {logicTick}; current Tick is {currentBuildingTick}.");
            if (unconfirmedBatches.Count > 0 &&
                unconfirmedBatches[unconfirmedBatches.Count - 1].LogicTick >= logicTick)
                throw new DeterministicSimulationException(
                    "Gold batch history must be appended in strictly increasing Tick order.");

            var batch = new GoldIncomeRecordBatch
            {
                LogicTick = logicTick,
                Records = currentRecords.ToArray(),
            };
            batch.Digest = GoldIncomeBatchDigest.Compute(batch, digestWriter);
            unconfirmedBatches.Add(batch);
            currentRecords.Clear();
            currentBuildingTick = -1;
            nextIncomeSequenceInTick = 0;
            buildState = BuildState.Idle;
            return CloneBatch(batch);
        }

        public bool TryGetSealedBatch(int logicTick, out GoldIncomeRecordBatch batch)
        {
            int index = FindBatch(logicTick);
            if (index < 0)
            {
                batch = default;
                return false;
            }
            batch = CloneBatch(unconfirmedBatches[index]);
            return true;
        }

        public bool TryGetBatchDigest(int logicTick, out GoldIncomeBatchDigest digest)
        {
            int index = FindBatch(logicTick);
            if (index < 0)
            {
                digest = default;
                return false;
            }
            digest = unconfirmedBatches[index].Digest;
            return true;
        }

        public GoldIncomeBatchDigest GetBatchDigest(int logicTick) =>
            TryGetBatchDigest(logicTick, out GoldIncomeBatchDigest digest)
                ? digest
                : default;

        public void ConfirmAcceptedTick(int logicTick)
        {
            if (logicTick != confirmedIncomeThroughTick + 1)
                throw new DeterministicSimulationException(
                    $"Gold confirmation must be continuous; expected " +
                    $"{confirmedIncomeThroughTick + 1}, got {logicTick}.");
            int batchIndex = FindBatch(logicTick);
            if (batchIndex < 0)
                throw new DeterministicSimulationException(
                    $"Gold Tick {logicTick} has no sealed batch.");

            GoldIncomeRecord[] records =
                unconfirmedBatches[batchIndex].Records ?? Array.Empty<GoldIncomeRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                GoldIncomeRecord record = records[i];
                confirmedEarnedGoldTotals[record.PlayerSlot] = checked(
                    confirmedEarnedGoldTotals[record.PlayerSlot] + record.Amount);
            }
            unconfirmedBatches.RemoveAt(batchIndex);
            confirmedIncomeThroughTick = logicTick;
        }

        public void ConfirmThroughTick(int authorityTick) => ConfirmAcceptedTick(authorityTick);

        public void DiscardUnconfirmedFromTick(int firstDiscardedTick)
        {
            if (buildState == BuildState.AcceptingRequests &&
                currentBuildingTick >= firstDiscardedTick)
            {
                currentRecords.Clear();
                currentBuildingTick = -1;
                nextIncomeSequenceInTick = 0;
                buildState = BuildState.Idle;
            }
            for (int i = unconfirmedBatches.Count - 1; i >= 0; i--)
                if (unconfirmedBatches[i].LogicTick >= firstDiscardedTick)
                    unconfirmedBatches.RemoveAt(i);
        }

        public int GetConfirmedAvailableGold(int playerSlot)
        {
            if (playerSlot < 0 || playerSlot >= confirmedEarnedGoldTotals.Count)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));
            return confirmedEarnedGoldTotals[playerSlot];
        }

        public int GetCurrentAvailableGold(int playerSlot, int effectiveShopGoldDelta) =>
            checked(GetConfirmedAvailableGold(playerSlot) + effectiveShopGoldDelta);

        public bool HasUnconfirmedIncome(int playerSlot)
        {
            if (playerSlot < 0 || playerSlot >= confirmedEarnedGoldTotals.Count)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));
            for (int i = 0; i < unconfirmedBatches.Count; i++)
            {
                GoldIncomeRecord[] records =
                    unconfirmedBatches[i].Records ?? Array.Empty<GoldIncomeRecord>();
                for (int j = 0; j < records.Length; j++)
                    if (records[j].PlayerSlot == playerSlot) return true;
            }
            return false;
        }

        public void Capture(ref GoldIncomeSnapshot state)
        {
            if (buildState != BuildState.Idle)
                throw new DeterministicSimulationException(
                    "Gold runtime cannot be captured while accepting requests.");
            state.ConfirmedIncomeThroughTick = confirmedIncomeThroughTick;
            state.ConfirmedEarnedGoldTotals = new System.Collections.Generic.List<int>(confirmedEarnedGoldTotals);
            state.UnconfirmedBatches = new System.Collections.Generic.List<GoldIncomeRecordBatch>(unconfirmedBatches.Count);
            for (int i = 0; i < unconfirmedBatches.Count; i++)
                state.UnconfirmedBatches.Add(CloneBatch(unconfirmedBatches[i]));
        }

        public void Restore(in GoldIncomeSnapshot state)
        {
            var totals = state.ConfirmedEarnedGoldTotals ?? new System.Collections.Generic.List<int>();
            var batches =
                state.UnconfirmedBatches ?? new System.Collections.Generic.List<GoldIncomeRecordBatch>();
            for (int i = 0; i < totals.Count; i++)
                if (totals[i] < 0)
                    throw new DeterministicSimulationException(
                        "Confirmed earned Gold totals must not be negative.");
            for (int i = 0; i < batches.Count; i++)
            {
                if (batches[i].LogicTick <= state.ConfirmedIncomeThroughTick ||
                    (i > 0 && batches[i - 1].LogicTick >= batches[i].LogicTick))
                    throw new DeterministicSimulationException(
                        "Unconfirmed Gold batch Ticks are not canonical.");
                ValidateBatch(batches[i], totals.Count, digestWriter);
            }

            confirmedIncomeThroughTick = state.ConfirmedIncomeThroughTick;
            confirmedEarnedGoldTotals.Clear();
            confirmedEarnedGoldTotals.AddRange(totals);
            unconfirmedBatches.Clear();
            for (int i = 0; i < batches.Count; i++)
                unconfirmedBatches.Add(CloneBatch(batches[i]));
            currentRecords.Clear();
            currentBuildingTick = -1;
            nextIncomeSequenceInTick = 0;
            buildState = BuildState.Idle;
        }

        public int ConfirmedIncomeThroughTick => confirmedIncomeThroughTick;
        public IReadOnlyList<int> ConfirmedEarnedGoldTotals => confirmedEarnedGoldTotals;
        public IReadOnlyList<GoldIncomeRecordBatch> UnconfirmedBatches => unconfirmedBatches;

        private int FindBatch(int logicTick)
        {
            int low = 0;
            int high = unconfirmedBatches.Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (unconfirmedBatches[middle].LogicTick < logicTick) low = middle + 1;
                else high = middle;
            }
            return low < unconfirmedBatches.Count &&
                unconfirmedBatches[low].LogicTick == logicTick ? low : -1;
        }

        private static void ValidateBatch(
            in GoldIncomeRecordBatch batch,
            int playerCount,
            CanonicalByteWriter writer)
        {
            GoldIncomeRecord[] records = batch.Records ?? Array.Empty<GoldIncomeRecord>();
            for (int i = 0; i < records.Length; i++)
            {
                GoldIncomeRecord record = records[i];
                if (record.PlayerSlot < 0 || record.PlayerSlot >= playerCount ||
                    record.Amount <= 0 || !IsValidReason(record.Reason) ||
                    record.IncomeSequenceInTick != i)
                    throw new DeterministicSimulationException(
                        $"Gold batch {batch.LogicTick} record {i} is invalid.");
            }
            GoldIncomeBatchDigest expected = GoldIncomeBatchDigest.Compute(batch, writer);
            if (expected.Value != batch.Digest.Value)
                throw new DeterministicSimulationException(
                    $"Gold batch {batch.LogicTick} digest mismatch.");
        }

        private static GoldIncomeRecordBatch CloneBatch(in GoldIncomeRecordBatch source) =>
            new GoldIncomeRecordBatch
            {
                LogicTick = source.LogicTick,
                Records = source.Records == null
                    ? Array.Empty<GoldIncomeRecord>()
                    : (GoldIncomeRecord[])source.Records.Clone(),
                Digest = source.Digest,
            };

        private static bool IsValidReason(GoldIncomeReason reason) =>
            reason == GoldIncomeReason.NaturalIncome ||
            reason == GoldIncomeReason.UnitKill ||
            reason == GoldIncomeReason.StructureDestruction ||
            reason == GoldIncomeReason.MapObjective;
    }
}
