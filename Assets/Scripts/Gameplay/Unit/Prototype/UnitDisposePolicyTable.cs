using System;
using System.Collections.Generic;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    [CreateAssetMenu(menuName = "MOBA/Unit Dispose Policy Table")]
    public sealed class UnitDisposePolicyTable : ScriptableObject
    {
        public List<UnitDisposePolicyEntry> Entries = new List<UnitDisposePolicyEntry>();
        public bool TryGet(ushort id, out UnitDisposePolicyEntry entry)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Id == id) { entry = Entries[i]; return true; }
            entry = default; return false;
        }
        public bool Contains(ushort id)
        {
            for (int i = 0; i < Entries.Count; i++)
                if (Entries[i].Id == id) return true;
            return false;
        }

        public void BakeTime(int tickRate)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                UnitDisposePolicyEntry entry = Entries[i];
                entry.DeathPresentationTicks =
                    entry.DeathPresentationDuration.IsAuthored
                        ? entry.DeathPresentationDuration
                            .BakeTicks(tickRate)
                        : DeterministicTimeConversion
                            .Legacy30HzTicksToTicks(
                                entry.DeathPresentationTicks,
                                tickRate);
                Entries[i] = entry;
            }
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            var seen = new HashSet<ushort>();
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (!seen.Add(e.Id)) Debug.LogError($"Duplicate UnitDisposePolicy Id {e.Id} at index {i}.");
                if (e.Kind == UnitDisposePolicyKind.SpawnRuin && e.RuinUnitPrototypeId <= 0)
                    Debug.LogError($"UnitDisposePolicy {e.Id} is SpawnRuin but RuinUnitPrototypeId is invalid.");
            }
        }
#endif
    }
    [Serializable]
    public struct UnitDisposePolicyEntry
    {
        [Min(0)] public ushort Id;
        public UnitDisposePolicyKind Kind;
        public DurationAuthoring DeathPresentationDuration;
        [HideInInspector]
        [Min(0)] public int DeathPresentationTicks;
        [Min(0)] public int RuinUnitPrototypeId;
    }
}
