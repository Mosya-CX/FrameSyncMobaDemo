using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class EntitiesSimulation : MonoSingleton<EntitiesSimulation>, IStateful
{
    public IReadOnlyDictionary<UnitUID, UnitCore> SimulableUnits => UnitManager.Instance.Spawns;
    public IReadOnlyDictionary<MissleUID, BaseMissle> SimulationMissles => MissleManager.Instance.Spawns;

    private readonly Queue<MissleTriggerEvent> missleTriggerEventQueue = new();
    private readonly Queue<UnitContactEvent> unitContactEventQueue = new();

    private readonly HashSet<UnitPairKey> lastUnitContactPairs = new();
    private readonly HashSet<UnitPairKey> currentUnitContactPairs = new();

    private uint localTick;

    public void Begin()
    {
        localTick = 0;
        missleTriggerEventQueue.Clear();
        unitContactEventQueue.Clear();
        lastUnitContactPairs.Clear();
        currentUnitContactPairs.Clear();
    }

    public void Clean()
    {
        missleTriggerEventQueue.Clear();
        unitContactEventQueue.Clear();
        lastUnitContactPairs.Clear();
        currentUnitContactPairs.Clear();
    }

    public void Tick(uint currentTick)
    {
        localTick = currentTick;

        DetectMissleUnitTriggers();
        DetectUnitUnitContacts();

        FlushMissleTriggerEvents();
        FlushUnitContactEvents();
    }

    #region Detect

    private void DetectMissleUnitTriggers()
    {
        foreach (var misslePair in SimulationMissles)
        {
            var missle = misslePair.Value;
            if (missle == null || missle.ShouldRecycleNow)
                continue;

            foreach (var unitPair in SimulableUnits)
            {
                var unit = unitPair.Value;
                if (unit == null || unit.IsDead)
                    continue;

                if (SpatialQueryUtility.MissleIntersectsUnit(missle, unit))
                {
                    missleTriggerEventQueue.Enqueue(new MissleTriggerEvent
                    {
                        TriggeredMissleId = misslePair.Key,
                        TriggeredUnitUid = unitPair.Key,
                    });
                }
            }
        }
    }

    private void DetectUnitUnitContacts()
    {
        currentUnitContactPairs.Clear();

        var units = ListPool<UnitCore>.Get();
        foreach (var pair in SimulableUnits)
        {
            if (pair.Value != null && !pair.Value.IsDead)
                units.Add(pair.Value);
        }

        for (int i = 0; i < units.Count; i++)
        {
            for (int j = i + 1; j < units.Count; j++)
            {
                var a = units[i];
                var b = units[j];

                if (!SpatialQueryUtility.UnitIntersectsUnit(a, b))
                    continue;

                var key = UnitPairKey.Create(a.UnitID, b.UnitID);
                currentUnitContactPairs.Add(key);

                if (!lastUnitContactPairs.Contains(key))
                {
                    unitContactEventQueue.Enqueue(new UnitContactEvent
                    {
                        EventType = UnitContactEventType.Enter,
                        UnitA = a.UnitID,
                        UnitB = b.UnitID,
                    });
                }
                else
                {
                    unitContactEventQueue.Enqueue(new UnitContactEvent
                    {
                        EventType = UnitContactEventType.Stay,
                        UnitA = a.UnitID,
                        UnitB = b.UnitID,
                    });
                }
            }
        }

        foreach (var oldPair in lastUnitContactPairs)
        {
            if (!currentUnitContactPairs.Contains(oldPair))
            {
                unitContactEventQueue.Enqueue(new UnitContactEvent
                {
                    EventType = UnitContactEventType.Exit,
                    UnitA = oldPair.A,
                    UnitB = oldPair.B,
                });
            }
        }

        lastUnitContactPairs.Clear();
        foreach (var pair in currentUnitContactPairs)
            lastUnitContactPairs.Add(pair);

        ListPool<UnitCore>.Release(units);
    }

    #endregion

    #region Flush

    private void FlushMissleTriggerEvents()
    {
        while (missleTriggerEventQueue.Count > 0)
        {
            var triggerEvent = missleTriggerEventQueue.Dequeue();

            if (MissleManager.Instance.Spawns.TryGetValue(triggerEvent.TriggeredMissleId, out var missle) &&
                UnitManager.Instance.Spawns.TryGetValue(triggerEvent.TriggeredUnitUid, out var unit))
            {
                missle.OnMissleTrigger(unit);
            }
        }
    }

    private void FlushUnitContactEvents()
    {
        while (unitContactEventQueue.Count > 0)
        {
            var evt = unitContactEventQueue.Dequeue();

            if (!UnitManager.Instance.Spawns.TryGetValue(evt.UnitA, out var a) ||
                !UnitManager.Instance.Spawns.TryGetValue(evt.UnitB, out var b))
                continue;

            if (a is IUnitContactListener listenerA)
                listenerA.OnUnitContact(evt.EventType, b);

            if (b is IUnitContactListener listenerB)
                listenerB.OnUnitContact(evt.EventType, a);
        }
    }

    #endregion

    #region Query

    public IReadOnlyList<UnitCore> SearchRectRangeUnits(fp3 origin, fp3 toward, fp length, fp width,
        SimulationFilter filter = default)
    {
        return SpatialQueryUtility.SearchRectRangeUnits(SimulableUnits.Values, origin, toward, length, width,
            filter.Equals(default(SimulationFilter)) ? SimulationFilter.Default : filter);
    }

    public IReadOnlyList<UnitCore> SearchLadderRangeUnits(fp3 origin, fp3 toward, fp bottomLength, fp topLength, fp height,
        SimulationFilter filter = default)
    {
        return SpatialQueryUtility.SearchLadderRangeUnits(SimulableUnits.Values, origin, toward, bottomLength, topLength, height,
            filter.Equals(default(SimulationFilter)) ? SimulationFilter.Default : filter);
    }

    public IReadOnlyList<UnitCore> SearchRoundRangeUnits(fp3 origin, fp radius, SimulationFilter filter = default)
    {
        return SpatialQueryUtility.SearchRoundRangeUnits(SimulableUnits.Values, origin, radius,
            filter.Equals(default(SimulationFilter)) ? SimulationFilter.Default : filter);
    }

    public IReadOnlyList<UnitCore> SearchFanShapedRangeUnits(fp3 origin, fp3 toward, fp radius, fp angle, SimulationFilter filter = default)
    {
        return SpatialQueryUtility.SearchFanShapedRangeUnits(SimulableUnits.Values, origin, toward, radius, angle,
            filter.Equals(default(SimulationFilter)) ? SimulationFilter.Default : filter);
    }

    #endregion

    #region Snapshot

    [Serializable]
    public class SimulationSnapshot
    {
        public uint Tick;
        public List<MissleTriggerEvent> MissleTriggerEvents = new();
        public List<UnitContactEvent> UnitContactEvents = new();
        public List<UnitPairKey> LastContactPairs = new();
    }

    public object CaptureState()
    {
        return new SimulationSnapshot
        {
            Tick = localTick,
            MissleTriggerEvents = new List<MissleTriggerEvent>(missleTriggerEventQueue),
            UnitContactEvents = new List<UnitContactEvent>(unitContactEventQueue),
            LastContactPairs = new List<UnitPairKey>(lastUnitContactPairs),
        };
    }

    public void RestoreState(object state)
    {
        if (state is not SimulationSnapshot snapshot)
            return;

        localTick = snapshot.Tick;

        missleTriggerEventQueue.Clear();
        for (int i = 0; i < snapshot.MissleTriggerEvents.Count; i++)
            missleTriggerEventQueue.Enqueue(snapshot.MissleTriggerEvents[i]);

        unitContactEventQueue.Clear();
        for (int i = 0; i < snapshot.UnitContactEvents.Count; i++)
            unitContactEventQueue.Enqueue(snapshot.UnitContactEvents[i]);

        lastUnitContactPairs.Clear();
        for (int i = 0; i < snapshot.LastContactPairs.Count; i++)
            lastUnitContactPairs.Add(snapshot.LastContactPairs[i]);

        currentUnitContactPairs.Clear();
    }

    #endregion
}

public struct MissleTriggerEvent
{
    public UnitUID TriggeredUnitUid;
    public MissleUID TriggeredMissleId;
}

public enum UnitContactEventType : byte
{
    Enter,
    Stay,
    Exit,
}

public struct UnitContactEvent
{
    public UnitContactEventType EventType;
    public UnitUID UnitA;
    public UnitUID UnitB;
}

public readonly struct UnitPairKey : IEquatable<UnitPairKey>
{
    public readonly UnitUID A;
    public readonly UnitUID B;

    public UnitPairKey(UnitUID a, UnitUID b)
    {
        A = a;
        B = b;
    }

    public static UnitPairKey Create(UnitUID a, UnitUID b)
    {
        return a.CompareTo(b) <= 0 ? new UnitPairKey(a, b) : new UnitPairKey(b, a);
    }

    public bool Equals(UnitPairKey other) => A.Equals(other.A) && B.Equals(other.B);
    public override bool Equals(object obj) => obj is UnitPairKey other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(A, B);
}