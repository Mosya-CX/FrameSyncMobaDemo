using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;

public class MissleManager : MonoSingleton<MissleManager>, IStateful
{
    [SerializeField, LabelText("投掷物预制体表")]
    private SerializedDictionary<short, BaseMissle> spawnablePrefabDict;

    private readonly Dictionary<short, UnityEngine.Pool.ObjectPool<BaseMissle>> missleFactory = new();
    private readonly Dictionary<MissleUID, BaseMissle> spawnedMissles = new();

    public IReadOnlyDictionary<MissleUID, BaseMissle> Spawns => spawnedMissles;

    private uint localTick;
    private byte frameSequence;

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;
    public uint CurrentTick => localTick;

    public void Begin()
    {
        missleFactory.Clear();
        spawnedMissles.Clear();
        localTick = 0;
        frameSequence = 0;
    }

    public void Clean()
    {
        foreach (var missle in spawnedMissles.Values)
        {
            if (missle != null)
                Destroy(missle.gameObject);
        }

        spawnedMissles.Clear();

        foreach (var pool in missleFactory.Values)
            pool.Clear();

        missleFactory.Clear();
    }

    public void UpdateLocalTick(uint currentTick)
    {
        localTick = currentTick;
        frameSequence = 0;
    }

    #region Tick

    public void TickUpdateMissTransform()
    {
        foreach (var missle in spawnedMissles.Values)
        {
            if (missle != null && missle.gameObject.activeSelf)
                missle.UpdateTransform(DeltaTime, localTick);
        }
    }

    public void TickUpdateMissleState()
    {
        var recycleList = ListPool<BaseMissle>.Get();

        foreach (var missle in spawnedMissles.Values)
        {
            if (missle == null || !missle.gameObject.activeSelf)
                continue;

            missle.Tick(DeltaTime, localTick);

            if (missle.ShouldRecycleNow)
                recycleList.Add(missle);
        }

        for (int i = 0; i < recycleList.Count; i++)
            RecycleNow(recycleList[i]);

        ListPool<BaseMissle>.Release(recycleList);
    }

    #endregion

    #region Spawn / Recycle

    public T SpawnNow<T>(short prefabId, IMissleInitialData initialData) where T : BaseMissle
    {
        if (!TryGetMisslePool(prefabId, out var pool))
        {
            Debug.LogError($"[{nameof(MissleManager)}] 未找到投掷物池，PrefabId={prefabId}");
            return null;
        }

        var missle = pool.Get();
        missle.MissleUid = new MissleUID(prefabId, localTick, frameSequence++);
        missle.OnSpawn(initialData);
        missle.SyncTransform();

        spawnedMissles[missle.MissleUid] = missle;
        return missle as T;
    }

    public BaseMissle SpawnNow(short prefabId, IMissleInitialData initialData)
    {
        return SpawnNow<BaseMissle>(prefabId, initialData);
    }

    public void RecycleNow(BaseMissle missle)
    {
        if (missle == null)
            return;

        if (!spawnedMissles.Remove(missle.MissleUid))
        {
            Destroy(missle.gameObject);
            return;
        }

        missle.OnDespawn();

        if (TryGetMisslePool(missle.PrefabID, out var pool))
            pool.Release(missle);
        else
            Destroy(missle.gameObject);
    }

    private bool TryGetMisslePool(short prefabId, out UnityEngine.Pool.ObjectPool<BaseMissle> pool)
    {
        if (missleFactory.TryGetValue(prefabId, out pool))
            return true;

        if (!spawnablePrefabDict.TryGetValue(prefabId, out var misslePrefab) || misslePrefab == null)
        {
            pool = null;
            return false;
        }

        pool = new UnityEngine.Pool.ObjectPool<BaseMissle>(
            createFunc: () =>
            {
                var missle = Instantiate(misslePrefab);
                return missle;
            },
            actionOnGet: missle => missle.gameObject.SetActive(true),
            actionOnRelease: missle => missle.gameObject.SetActive(false),
            actionOnDestroy: missle => Destroy(missle.gameObject),
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: 256);

        missleFactory[prefabId] = pool;
        return true;
    }

    #endregion

    #region Snapshot

    [Serializable]
    public class GlobalMissleSnapshot
    {
        public uint Tick;
        public byte FrameSequence;
        public Dictionary<MissleUID, object> MissleSnapshots = new();
    }

    public object CaptureState()
    {
        var snapshot = new GlobalMissleSnapshot
        {
            Tick = localTick,
            FrameSequence = frameSequence,
        };

        foreach (var pair in spawnedMissles)
            snapshot.MissleSnapshots.Add(pair.Key, pair.Value.CaptureState());

        return snapshot;
    }

    public void RestoreState(object state)
    {
        if (state is not GlobalMissleSnapshot snapshot)
            return;

        localTick = snapshot.Tick;
        frameSequence = snapshot.FrameSequence;

        var existingKeys = new List<MissleUID>(spawnedMissles.Keys);
        var redundant = new Queue<MissleUID>();

        for (int i = 0; i < existingKeys.Count; i++)
        {
            var id = existingKeys[i];
            if (snapshot.MissleSnapshots.TryGetValue(id, out var missleState))
            {
                spawnedMissles[id].RestoreState(missleState);
                spawnedMissles[id].SyncTransform();
                snapshot.MissleSnapshots.Remove(id);
            }
            else
            {
                redundant.Enqueue(id);
            }
        }

        foreach (var remain in snapshot.MissleSnapshots)
        {
            if (!TryGetMisslePool(remain.Key.PrefabId, out var pool))
                continue;

            var missle = pool.Get();
            missle.MissleUid = remain.Key;
            missle.RestoreState(remain.Value);
            missle.SyncTransform();

            spawnedMissles[remain.Key] = missle;
        }

        while (redundant.Count > 0)
        {
            var id = redundant.Dequeue();

            if (!spawnedMissles.Remove(id, out var missle))
                continue;

            if (TryGetMisslePool(id.PrefabId, out var pool))
                pool.Release(missle);
            else
                Destroy(missle.gameObject);
        }
    }

    #endregion
}

public static class ListPool<T>
{
    private static readonly Stack<List<T>> pool = new();

    public static List<T> Get()
    {
        return pool.Count > 0 ? pool.Pop() : new List<T>();
    }

    public static void Release(List<T> list)
    {
        list.Clear();
        pool.Push(list);
    }
}

public readonly struct MissleUID : IEquatable<MissleUID>, IComparable<MissleUID>
{
    public readonly short PrefabId;
    public readonly uint Frame;
    public readonly byte Sequence;

    public MissleUID(short prefabId, uint frame, byte sequence)
    {
        PrefabId = prefabId;
        Frame = frame;
        Sequence = sequence;
    }

    public bool Equals(MissleUID other) =>
        PrefabId == other.PrefabId &&
        Frame == other.Frame &&
        Sequence == other.Sequence;

    public override bool Equals(object obj) => obj is MissleUID other && Equals(other);

    public static bool operator ==(MissleUID left, MissleUID right) => left.Equals(right);
    public static bool operator !=(MissleUID left, MissleUID right) => !left.Equals(right);

    public override int GetHashCode()
    {
        return HashCode.Combine(PrefabId, Frame, Sequence);
    }

    public int CompareTo(MissleUID other)
    {
        int cmp = PrefabId.CompareTo(other.PrefabId);
        if (cmp != 0) return cmp;
        cmp = Frame.CompareTo(other.Frame);
        if (cmp != 0) return cmp;
        return Sequence.CompareTo(other.Sequence);
    }

    public override string ToString() => $"{PrefabId}:{Frame}:{Sequence}";
}
