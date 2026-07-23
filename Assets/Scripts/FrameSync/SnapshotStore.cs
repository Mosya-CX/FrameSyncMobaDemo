using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Ring-buffer store of per-Tick deterministic snapshots
    /// (Snapshot Appendix v7.2 section 13).
    ///
    /// Maintains snapshots from the rollback anchor Tick forward.
    /// Snapshots older than the anchor may be discarded.
    /// Capacity exhaustion suspends prediction (no silent eviction).
    /// </summary>
    public sealed class SnapshotStore
    {
        public const int CurrentSnapshotSchemaVersion = 1;
        private RollbackFrameSnapshot[] _buffer;
        private int _head;
        private int _count;
        private int _baseTick;

        /// <summary>Maximum number of snapshots this store can hold.</summary>
        public int Capacity { get; }

        /// <summary>Current number of stored snapshots.</summary>
        public int Count => _count;

        /// <summary>The earliest Tick for which a snapshot exists.</summary>
        public int EarliestTick => _count > 0 ? _baseTick : -1;

        /// <summary>The latest Tick for which a snapshot exists.</summary>
        public int LatestTick => _count > 0 ? _baseTick + _count - 1 : -1;

        public SnapshotStore(int capacity = 512)
        {
            Capacity = capacity;
            _buffer = new RollbackFrameSnapshot[capacity];
            _head = 0;
            _count = 0;
            _baseTick = 0;
        }

        /// <summary>
        /// Store a snapshot for the given Tick.
        /// If the store is full, throws (no silent eviction).
        /// </summary>
        public void Store(int tick, in GameplaySnapshot snapshot)
        {
            if (_count == Capacity)
            {
                throw new DeterministicSimulationException(
                    "Snapshot store capacity exhausted. Suspend prediction.");
            }

            // If empty, set base
            if (_count == 0)
            {
                _baseTick = tick;
                _head = 0;
            }

            // Calculate insertion index
            int offset = tick - _baseTick;
            if (offset < 0)
            {
                throw new DeterministicSimulationException(
                    $"Cannot store snapshot at Tick {tick}: earlier than base Tick {_baseTick}.");
            }

            int index = (_head + offset) % Capacity;
            _buffer[index] = new RollbackFrameSnapshot
            {
                SnapshotTick = tick + 1,
                SnapshotSchemaVersion = CurrentSnapshotSchemaVersion,
                Gameplay = snapshot,
            };

            // Update count
            int newCount = offset + 1;
            if (newCount > _count) _count = newCount;
        }

        /// <summary>
        /// Retrieve the snapshot for the given Tick.
        /// Returns true if found, false otherwise.
        /// </summary>
        public bool TryGet(int tick, out RollbackFrameSnapshot snapshot)
        {
            snapshot = default;
            if (_count == 0) return false;
            if (tick < _baseTick || tick >= _baseTick + _count) return false;

            int index = (_head + (tick - _baseTick)) % Capacity;
            snapshot = _buffer[index];
            return snapshot.SnapshotSchemaVersion == CurrentSnapshotSchemaVersion;
        }

        /// <summary>
        /// Discard all snapshots at or after the given Tick.
        /// Used before rollback: discard unconfirmed snapshots,
        /// then restore from the anchor.
        /// </summary>
        public void DiscardFromTick(int tick)
        {
            if (_count == 0) return;

            if (tick <= _baseTick)
            {
                _count = 0;
                _baseTick = 0;
                return;
            }

            int newCount = tick - _baseTick;
            if (newCount < _count)
            {
                _count = newCount;
            }
        }

        /// <summary>
        /// Advance the base Tick, discarding snapshots older than the new base.
        /// Used when authority confirms frames and old snapshots are no longer needed.
        /// </summary>
        public void AdvanceBase(int newBaseTick)
        {
            if (_count == 0) return;

            if (newBaseTick <= _baseTick) return;

            int discardCount = newBaseTick - _baseTick;
            if (discardCount >= _count)
            {
                _count = 0;
                _baseTick = 0;
                return;
            }

            _head = (_head + discardCount) % Capacity;
            _baseTick = newBaseTick;
            _count -= discardCount;
        }
    }
}
