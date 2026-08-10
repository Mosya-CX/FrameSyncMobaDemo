using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public readonly struct AbilityBlackboardKey<T>
    {
        public readonly int Id;
        public AbilityBlackboardKey(int id)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            Id = id;
        }
    }

    public enum AbilityBlackboardValueKind : byte
    {
        Number = 1,
        UnitUid = 2,
        Vector2 = 3,
        ProjectileUid = 4,
        StatModifierHandle = 5,
        CrowdControlHandle = 6,
    }

    public struct AbilityBlackboardEntrySnapshot
    {
        public int KeyId;
        public AbilityBlackboardValueKind Kind;
        public fp Number;
        public UnitUid UnitUid;
        public fp2 Vector;
        public ProjectileUid ProjectileUid;
        public StatModifierHandle StatModifierHandle;
        public CrowdControlHandle CrowdControlHandle;
    }

    public struct AbilityBlackboardSnapshot
    {
        public System.Collections.Generic.List<AbilityBlackboardEntrySnapshot> Entries;
    }

    public sealed class AbilityBlackboard
    {
        private readonly Dictionary<int, fp> numbers = new Dictionary<int, fp>();
        private readonly Dictionary<int, UnitUid> units = new Dictionary<int, UnitUid>();
        private readonly Dictionary<int, fp2> vectors = new Dictionary<int, fp2>();
        private readonly Dictionary<int, ProjectileUid> projectiles =
            new Dictionary<int, ProjectileUid>();
        private readonly Dictionary<int, StatModifierHandle> statHandles =
            new Dictionary<int, StatModifierHandle>();
        private readonly Dictionary<int, CrowdControlHandle> crowdControlHandles =
            new Dictionary<int, CrowdControlHandle>();

        public void Set(AbilityBlackboardKey<fp> key, fp value) => numbers[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<fp> key, out fp value) => numbers.TryGetValue(key.Id, out value);
        public void Set(AbilityBlackboardKey<UnitUid> key, UnitUid value) => units[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<UnitUid> key, out UnitUid value) => units.TryGetValue(key.Id, out value);
        public void Set(AbilityBlackboardKey<fp2> key, fp2 value) => vectors[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<fp2> key, out fp2 value) => vectors.TryGetValue(key.Id, out value);
        public void Set(AbilityBlackboardKey<ProjectileUid> key, ProjectileUid value) => projectiles[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<ProjectileUid> key, out ProjectileUid value) => projectiles.TryGetValue(key.Id, out value);
        public void Set(AbilityBlackboardKey<StatModifierHandle> key, StatModifierHandle value) => statHandles[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<StatModifierHandle> key, out StatModifierHandle value) => statHandles.TryGetValue(key.Id, out value);
        public void Set(AbilityBlackboardKey<CrowdControlHandle> key, CrowdControlHandle value) => crowdControlHandles[key.Id] = value;
        public bool TryGet(AbilityBlackboardKey<CrowdControlHandle> key, out CrowdControlHandle value) => crowdControlHandles.TryGetValue(key.Id, out value);

        public AbilityBlackboardSnapshot Capture()
        {
            var entries = new List<AbilityBlackboardEntrySnapshot>(
                numbers.Count + units.Count + vectors.Count + projectiles.Count + statHandles.Count + crowdControlHandles.Count);
            foreach (var pair in numbers)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.Number, Number = pair.Value });
            foreach (var pair in units)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.UnitUid, UnitUid = pair.Value });
            foreach (var pair in vectors)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.Vector2, Vector = pair.Value });
            foreach (var pair in projectiles)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.ProjectileUid, ProjectileUid = pair.Value });
            foreach (var pair in statHandles)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.StatModifierHandle, StatModifierHandle = pair.Value });
            foreach (var pair in crowdControlHandles)
                entries.Add(new AbilityBlackboardEntrySnapshot { KeyId = pair.Key, Kind = AbilityBlackboardValueKind.CrowdControlHandle, CrowdControlHandle = pair.Value });
            entries.Sort((a, b) =>
            {
                int comparison = a.KeyId.CompareTo(b.KeyId);
                return comparison != 0 ? comparison : a.Kind.CompareTo(b.Kind);
            });
            return new AbilityBlackboardSnapshot { Entries = new System.Collections.Generic.List<AbilityBlackboardEntrySnapshot>(entries) };
        }

        public void Restore(in AbilityBlackboardSnapshot snapshot)
        {
            Clear();
            var entries = snapshot.Entries ?? new System.Collections.Generic.List<AbilityBlackboardEntrySnapshot>();
            int previousKey = -1;
            AbilityBlackboardValueKind previousKind = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                AbilityBlackboardEntrySnapshot entry = entries[i];
                if (entry.KeyId <= 0 ||
                    (i > 0 && (entry.KeyId < previousKey ||
                     (entry.KeyId == previousKey && entry.Kind <= previousKind))))
                    throw new Deterministic.DeterministicSimulationException(
                        "Ability Blackboard snapshot entries are not in canonical order.");
                previousKey = entry.KeyId;
                previousKind = entry.Kind;
                switch (entry.Kind)
                {
                    case AbilityBlackboardValueKind.Number: numbers.Add(entry.KeyId, entry.Number); break;
                    case AbilityBlackboardValueKind.UnitUid: units.Add(entry.KeyId, entry.UnitUid); break;
                    case AbilityBlackboardValueKind.Vector2: vectors.Add(entry.KeyId, entry.Vector); break;
                    case AbilityBlackboardValueKind.ProjectileUid: projectiles.Add(entry.KeyId, entry.ProjectileUid); break;
                    case AbilityBlackboardValueKind.StatModifierHandle: statHandles.Add(entry.KeyId, entry.StatModifierHandle); break;
                    case AbilityBlackboardValueKind.CrowdControlHandle: crowdControlHandles.Add(entry.KeyId, entry.CrowdControlHandle); break;
                    default: throw new Deterministic.DeterministicSimulationException("Invalid Ability Blackboard value kind.");
                }
            }
        }

        public void ValidateUnitReferences(UnitWorld world)
        {
            foreach (UnitUid uid in units.Values)
                if (uid.IsValid() && !world.TryGetUnit(uid, out _))
                    throw new Deterministic.DeterministicSimulationException(
                        $"Ability Blackboard references missing UnitUid {uid}.");
            foreach (StatModifierHandle handle in statHandles.Values)
                if (handle.IsValid &&
                    handle.OwnerUnitUid.IsValid() &&
                    !world.TryGetUnit(handle.OwnerUnitUid, out _))
                    throw new Deterministic.DeterministicSimulationException(
                        $"Ability Blackboard stat modifier references missing owner {handle.OwnerUnitUid}.");
            foreach (CrowdControlHandle handle in crowdControlHandles.Values)
                if (handle.IsValid &&
                    !world.TryGetUnit(handle.TargetUnitUid, out _))
                    throw new Deterministic.DeterministicSimulationException(
                        $"Ability Blackboard crowd control handle references missing target {handle.TargetUnitUid}.");
        }

        public void Clear()
        {
            numbers.Clear();
            units.Clear();
            vectors.Clear();
            projectiles.Clear();
            statHandles.Clear();
            crowdControlHandles.Clear();
        }
    }
}
