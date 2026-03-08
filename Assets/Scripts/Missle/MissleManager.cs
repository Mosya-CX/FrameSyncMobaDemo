using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine.Rendering;
using UnityEngine;
using System;

public class MissleManager : MonoSingleton<MissleManager>, IStateful
{
    [SerializeField, LabelText("投掷物预制体表")]
    private SerializedDictionary<short, BaseMissle> spawnablePrefabDict;

    private readonly Dictionary<short, UnityEngine.Pool.ObjectPool<BaseMissle>> missleFactory = new();

    private readonly Dictionary<MissleUID, BaseMissle> spawnedMissles = new();
    public IReadOnlyDictionary<MissleUID, BaseMissle> Spawns => spawnedMissles;

    private Queue<MissleSpawnRequest> missleSpawnRequests = new();
    private Queue<MissleDespawnRequest> missleDespawnRequests = new();

    private uint localTick;
    private byte frameSequence;

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;
    
    public IEnumerator Init()
    {

        yield break;
    }

    public void Begin()
    {
        missleFactory.Clear();
        spawnedMissles.Clear();
        missleSpawnRequests.Clear();
        missleDespawnRequests.Clear();
    }

    public void Clean()
    {
        missleFactory.Clear();
        spawnedMissles.Clear();
        missleSpawnRequests.Clear();
        missleDespawnRequests.Clear();
    }

    #region Tick
    public void UpdateLocalTick(uint currentTick) => localTick = currentTick;

    public void TickSpawnMissle()
    {
        frameSequence = 0;

        while (missleDespawnRequests.Count > 0)
        {
            var request = missleDespawnRequests.Dequeue();
            if (spawnedMissles.TryGetValue(request.missleInstanceId, out var missle))
            {
                missle.OnDespawn();
                spawnedMissles.Remove(missle.MissleUid);
                request.pool.Release(missle);
            }
        }

        while (missleSpawnRequests.Count > 0)
        {
            var request = missleSpawnRequests.Dequeue();
            var missle = request.pool.Get();
            missle.MissleUid = request.missleInstanceId;
            missle.OnSpawn(request.initialData);
            spawnedMissles.Add(missle.MissleUid, missle);
        }
    }

    public void TickUpdateMissTransform()
    {
        foreach (var missle in spawnedMissles.Values)
            if (missle.gameObject.activeSelf)
                missle.UpdateTransform(DeltaTime);
    }

    public void TickUpdateMissleState()
    {
        foreach (var missle in spawnedMissles.Values)
            if (missle.gameObject.activeSelf)
                missle.Tick(DeltaTime);
    }

    #endregion

    #region 生成与销毁

    public void Recycle(BaseMissle missle)
    {
        if (missle == null)
            return;
        if (!spawnedMissles.ContainsKey(missle.MissleUid))
        {
            Destroy(missle.gameObject);
            return;
        }
        if (!TryGetMisslePool(missle.PrefabID, out var pool))
        {
            Destroy(missle.gameObject);
            return;
        }

        missle.gameObject.SetActive(false);
        missleDespawnRequests.Enqueue(new MissleDespawnRequest
        {
            missleInstanceId = missle.MissleUid,
            pool = pool,
        });
    }

    public MissleSpawnHandle CreateNewMissleRequest<T>(short prefabId, T initialData = default(T)) where T : IMissleInitialData
    {
        if (!TryGetMisslePool(prefabId, out var pool))
            return default;

        var request = new MissleSpawnRequest
        {
            missleInstanceId = new MissleUID(prefabId, localTick + 1, frameSequence),
            initialData = initialData,
            pool = pool,
        };

        frameSequence++;
        return new MissleSpawnHandle(request);
    }

    private bool TryGetMisslePool(short prefabId, out UnityEngine.Pool.ObjectPool<BaseMissle> pool)
    {
        if (missleFactory.TryGetValue(prefabId, out pool))
            return true;

        if (spawnablePrefabDict.TryGetValue(prefabId, out var misslePrefab))
        {
            pool = new UnityEngine.Pool.ObjectPool<BaseMissle>(
                () =>
                {
                    var missle = Instantiate(misslePrefab);
                    return missle;
                },
                (missle) => missle.gameObject.SetActive(true),
                (missle) => missle.gameObject.SetActive(false),
                (missle) => Destroy(missle.gameObject),
                false, 4, 32);
            missleFactory[prefabId] = pool;
            return true;
        }

        return false;
    }

    public class MissleSpawnRequest : ICloneable
    {
        public MissleUID missleInstanceId;
        public UnityEngine.Pool.ObjectPool<BaseMissle> pool;
        public IMissleInitialData initialData;

        public object Clone() => MemberwiseClone();
    }

    public class MissleDespawnRequest : ICloneable
    {
        public MissleUID missleInstanceId;
        public UnityEngine.Pool.ObjectPool<BaseMissle> pool;

        public object Clone() => MemberwiseClone();
    }

    #endregion

    #region 快照和回滚

    [System.Serializable]
    public class GlobalMissleSnapshot
    {
        public uint tick;
        public List<MissleSpawnRequest> missleSpawnRequests = new();
        public List<MissleDespawnRequest> missleDespawnRequests = new();
        public Dictionary<MissleUID, object> missleSnapshots = new(); 
    }

    public object CaptureState()
    {
        var snapshot = new GlobalMissleSnapshot();
        snapshot.tick = localTick;

        foreach (var request in missleSpawnRequests)
            snapshot.missleSpawnRequests.Add((MissleSpawnRequest)request.Clone());
        foreach (var request in missleDespawnRequests)
            snapshot.missleDespawnRequests.Add((MissleDespawnRequest)request.Clone());

        foreach (var missleInfo in spawnedMissles)
            snapshot.missleSnapshots.Add(missleInfo.Key, missleInfo.Value.CaptureState());

        return snapshot;
    }

    public void RestoreState(object state)
    {
        if (state is GlobalMissleSnapshot snapshot)
        {
            localTick = snapshot.tick;

            missleDespawnRequests.Clear();
            missleSpawnRequests.Clear();

            for (int i = 0; i < snapshot.missleSpawnRequests.Count; i++) 
                missleSpawnRequests.Enqueue((MissleSpawnRequest)snapshot.missleSpawnRequests[i].Clone());
            for (int i = 0; i < snapshot.missleDespawnRequests.Count; i++)
                missleDespawnRequests.Enqueue((MissleDespawnRequest)snapshot.missleDespawnRequests[i].Clone());

            Queue<MissleUID> redundant = new();
            foreach (var presentMissleInfo in spawnedMissles)
            {
                if (snapshot.missleSnapshots.TryGetValue(presentMissleInfo.Key, out var missleStateSnapshot))
                {
                    presentMissleInfo.Value.RestoreState(missleStateSnapshot);
                    snapshot.missleSnapshots.Remove(presentMissleInfo.Key);
                }
                else
                {
                    redundant.Enqueue(presentMissleInfo.Key);
                }
            }

            foreach (var remainSpawnMissleInfo in snapshot.missleSnapshots)
            {
                if (TryGetMisslePool(remainSpawnMissleInfo.Key.PrefabId, out var pool))
                {
                    var missle = pool.Get();
                    missle.MissleUid = remainSpawnMissleInfo.Key;
                    missle.RestoreState(remainSpawnMissleInfo.Value);
                    spawnedMissles.Add(remainSpawnMissleInfo.Key, missle);
                }
            }

            while (redundant.Count > 0)
            {
                var redundantMissleId = redundant.Dequeue();
                if (spawnedMissles.TryGetValue(redundantMissleId, out var missle))
                {
                    if (TryGetMisslePool(redundantMissleId.PrefabId, out var pool))
                        pool.Release(missle);
                    else
                        Destroy(missle);

                    spawnedMissles.Remove(redundantMissleId);
                }
            }
        }
    }

    #endregion
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

public struct MissleSpawnHandle
{
    private MissleManager.MissleSpawnRequest request;
    public bool IsRequestSuccess => request != null;
    public bool IsSpawned => MissleManager.Instance.Spawns.ContainsKey(request.missleInstanceId);
    public T GetMissle<T>() where T : BaseMissle
    {
        if (MissleManager.Instance.Spawns.TryGetValue(request.missleInstanceId, out var missle))
            return missle as T;
        return null;
    }


    public MissleSpawnHandle(MissleManager.MissleSpawnRequest request)
    {
        this.request = request;
    }
}