using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;

public class UnitManager : MonoSingleton<UnitManager>, IStateful
{
    [SerializeField, LabelText("单位预制体表")]
    private SerializedDictionary<int, UnitCore> spawnablePrefabDict;

    [SerializeField, LabelText("A*路径更新间隔")]
    private float astarPathUpdateInterval = 0.1f;

    private readonly Dictionary<UnitUID, UnitCore> spawnedUnits = new();
    public IReadOnlyDictionary<UnitUID, UnitCore> Spawns => spawnedUnits;

    private readonly Dictionary<int, UnityEngine.Pool.ObjectPool<UnitCore>> pools = new();

    private byte frameSpawnSequence;
    private fp astarPathUpdateTimer;
    private uint localTick;

    private fp AstarPathUpdateIntervalFP => (fp)astarPathUpdateInterval;
    public fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;
    public uint CurrentTick => localTick;

    public void Begin()
    {
        spawnedUnits.Clear();
        frameSpawnSequence = 0;
        astarPathUpdateTimer = 0;
        localTick = 0;
    }

    public void Clean()
    {
        foreach (var unit in spawnedUnits.Values)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        spawnedUnits.Clear();

        foreach (var pool in pools.Values)
            pool.Clear();

        pools.Clear();
    }

    public void UpdateLocalTick(uint currentTick)
    {
        localTick = currentTick;
        frameSpawnSequence = 0;
    }

    #region Spawn / Despawn

    public UnitCore SpawnNow(int prefabId, byte teamId, int startLevel = 1, Action<UnitCore> init = null)
    {
        if (!TryGetPool(prefabId, out var pool))
        {
            Debug.LogError($"[{nameof(UnitManager)}] 未找到 PrefabId={prefabId} 的对象池。");
            return null;
        }

        var uid = new UnitUID(prefabId, localTick, teamId, frameSpawnSequence++);
        var unit = pool.Get();

        unit.OnSpawn(uid, startLevel);
        init?.Invoke(unit);
        unit.SyncTransform();

        spawnedUnits[uid] = unit;
        return unit;
    }

    public T SpawnNow<T>(int prefabId, byte teamId, int startLevel = 1, Action<T> init = null) where T : UnitCore
    {
        var unit = SpawnNow(prefabId, teamId, startLevel, u =>
        {
            if (u is T typed)
                init?.Invoke(typed);
        });

        return unit as T;
    }

    public void DespawnNow(UnitCore unit)
    {
        if (unit == null)
            return;

        if (spawnedUnits.Remove(unit.UnitID))
        {
            unit.OnDespawn();

            if (TryGetPool(unit.PrefabId, out var pool))
                pool.Release(unit);
            else
                Destroy(unit.gameObject);
        }
        else
        {
            unit.gameObject.SetActive(false);
            Destroy(unit.gameObject);
        }
    }

    public UnitCore GetActiveUnit(UnitUID uid)
    {
        return spawnedUnits.ContainsKey(uid) ? spawnedUnits[uid] : null;
    }

    #endregion

    #region Tick

    public void TickUpdateUnitTransform()
    {
        astarPathUpdateTimer += DeltaTime;

        if (astarPathUpdateTimer > AstarPathUpdateIntervalFP)
        {
            foreach (var unit in spawnedUnits.Values)
                unit.UpdateAStarPath();

            astarPathUpdateTimer -= AstarPathUpdateIntervalFP;
        }

        foreach (var unit in spawnedUnits.Values)
            unit.UpdateMoveDirection();

        foreach (var unit in spawnedUnits.Values)
        {
            fp3 rvoCorrection = RVOGenerator.Instance.GetModifiedDirection(unit);
            unit.ApplyMove(DeltaTime, rvoCorrection);
        }
    }

    public void TickUpdateUnitState()
    {
        foreach (var unit in spawnedUnits.Values)
            unit.Tick(DeltaTime, localTick);
    }

    public void TickDeathDecision()
    {
        foreach (var unit in spawnedUnits.Values)
            unit.CheckDead();
    }

    #endregion

    #region Pool

    private bool TryGetPool(int prefabId, out UnityEngine.Pool.ObjectPool<UnitCore> pool)
    {
        if (!pools.TryGetValue(prefabId, out pool))
        {
            pool = CreatePool(prefabId);
            if (pool != null)
                pools[prefabId] = pool;
        }

        return pool != null;
    }

    private UnityEngine.Pool.ObjectPool<UnitCore> CreatePool(int prefabId)
    {
        if (!spawnablePrefabDict.TryGetValue(prefabId, out var prefab) || prefab == null)
            return null;

        return new UnityEngine.Pool.ObjectPool<UnitCore>(
            createFunc: () =>
            {
                var instance = Instantiate(prefab);
                instance.PrefabId = prefabId;
                return instance;
            },
            actionOnGet: unit => unit.gameObject.SetActive(true),
            actionOnRelease: unit => unit.gameObject.SetActive(false),
            actionOnDestroy: unit => Destroy(unit.gameObject),
            collectionCheck: false,
            defaultCapacity: 32,
            maxSize: 1024
        );
    }

    #endregion

    #region Snapshot

    [Serializable]
    public class GlobalUnitSnapshot
    {
        public uint Tick;
        public byte FrameSpawnSequence;
        public Dictionary<UnitUID, object> UnitSnapshots = new();
    }

    public object CaptureState()
    {
        var snapshot = new GlobalUnitSnapshot
        {
            Tick = localTick,
            FrameSpawnSequence = frameSpawnSequence,
        };

        foreach (var pair in spawnedUnits)
            snapshot.UnitSnapshots.Add(pair.Key, pair.Value.CaptureState());

        return snapshot;
    }

    public void RestoreState(object state)
    {
        if (state is not GlobalUnitSnapshot snapshot)
            return;

        localTick = snapshot.Tick;
        frameSpawnSequence = snapshot.FrameSpawnSequence;

        var existingKeys = new List<UnitUID>(spawnedUnits.Keys);
        var unitsToRemove = new Queue<UnitUID>();

        for (int i = 0; i < existingKeys.Count; i++)
        {
            var uid = existingKeys[i];

            if (snapshot.UnitSnapshots.TryGetValue(uid, out var unitSnapshot))
            {
                spawnedUnits[uid].RestoreState(unitSnapshot);
                snapshot.UnitSnapshots.Remove(uid);
            }
            else
            {
                unitsToRemove.Enqueue(uid);
            }
        }

        foreach (var restorePair in snapshot.UnitSnapshots)
        {
            var uid = restorePair.Key;

            if (!TryGetPool(uid.PrefabId, out var pool))
            {
                Debug.LogError($"[{nameof(UnitManager)}] 恢复时找不到 PrefabId={uid.PrefabId} 的对象池。");
                continue;
            }

            var unit = pool.Get();
            unit.OnSpawn(uid, 1);
            unit.RestoreState(restorePair.Value);

            spawnedUnits[uid] = unit;
        }

        while (unitsToRemove.Count > 0)
        {
            var uid = unitsToRemove.Dequeue();

            if (!spawnedUnits.Remove(uid, out var unit))
                continue;

            unit.OnDespawn();

            if (TryGetPool(unit.PrefabId, out var pool))
                pool.Release(unit);
            else
                Destroy(unit.gameObject);
        }
    }

    #endregion
}