using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.Rendering;

public class UnitManager : MonoSingleton<UnitManager>
{
    [SerializeField, LabelText("单位预制体表")]
    private SerializedDictionary<int, UnitCore> spawnablePrefabDict;
    [SerializeField, LabelText("A*路径更新间隔")]
    private float astarPathUpdateInterval = 0.1f;

    // 已生成单位的查找表
    private Dictionary<UnitUID, UnitCore> spawnedUnitTable = new();
    public IReadOnlyDictionary<UnitUID, UnitCore> Spawns => spawnedUnitTable;
    // 对象池
    private Dictionary<int, UnityEngine.Pool.ObjectPool<UnitCore>> unitFactory = new();
    // 请求队列
    private List<SpawnRequest> spawnRequests = new();
    private List<DespawnRequest> despawnRequests = new();

    private byte frameSpawnSequence;
    private fp astarPathUpdateTimer;
    private uint localTick;

    private fp AstarPathUpdateIntervalFP => (fp)astarPathUpdateInterval;
    public fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    public void Begin()
    {
        spawnedUnitTable.Clear();
        unitFactory.Clear();
    }

    public void Clean()
    {
        foreach (var unit in spawnedUnitTable.Values)
            if (unit != null)
                Destroy(unit.gameObject);
        spawnedUnitTable.Clear();

        foreach (var pool in unitFactory.Values)
            pool.Clear();

        unitFactory.Clear();   
    }

    public IEnumerator Init()
    {
        spawnedUnitTable ??= new();
        unitFactory ??= new();
        yield break;
    }

    #region Tick
    public void UpdateLocalTick(uint currentTick) => localTick = currentTick;

    public void TickSpawnUnit()
    {
        frameSpawnSequence = 0;

        for (int i = despawnRequests.Count - 1; i >= 0; i--)
        {
            var request = despawnRequests[i];
            request.despawnTimer -= DeltaTime;

            if (request.despawnTimer < 0)
            {
                var despawnTarget = spawnedUnitTable[request.despawnTargetUid];
                despawnTarget.OnDespawn();
                spawnedUnitTable.Remove(request.despawnTargetUid);
                request.pool.Release(despawnTarget);
                despawnRequests.RemoveAt(i);
            }
        }
        for (int i = spawnRequests.Count - 1; i >= 0; i--)
        {
            var request = spawnRequests[i];
            request.spawnTimer -= DeltaTime;

            if (request.spawnTimer < 0)
            {
                var spawnedUnit = request.pool.Get();
                spawnedUnit.OnSpawn(request.spawnedUnitUid, request.startLevel);
                request.spawnedAction?.Invoke(spawnedUnit);
                spawnedUnitTable.Add(request.spawnedUnitUid, spawnedUnit);
                spawnRequests.RemoveAt(i);
            }
        }
    }

    public void TickUpdateUnitTransform()
    {
        
        astarPathUpdateTimer += DeltaTime;

        // 周期性更新路径
        if (astarPathUpdateTimer > AstarPathUpdateIntervalFP)
        {
            foreach (var unit in spawnedUnitTable.Values)
                unit.UpdateAStarPath();
            astarPathUpdateTimer -= AstarPathUpdateIntervalFP;
        }

        // 更新移动方向
        foreach (var unit in spawnedUnitTable.Values)
        {
            unit.UpdateMoveDirection();
        }

        // 获取 RVO 修正并应用移动
        foreach (var unit in spawnedUnitTable.Values)
        {
            fp3 rvoCorrection = RVOGenerator.Instance.GetModifiedDirection(unit);
            unit.ApplyMove(DeltaTime, rvoCorrection);
        }
        
    }

    public void TickUpdateUnitState()
    {
        // 执行每个单位Tick
        foreach (var unit in spawnedUnitTable.Values)
            unit.Tick(DeltaTime);
    }

    public void TickDeathDecision()
    {
        foreach (var unit in spawnedUnitTable.Values)
            unit.CheckDead();
    }

    #endregion

    #region 生成和销毁单位
    public UnitSpawnHandle CreateSpawnRequest(int prefabId, byte teamId, fp spawnDelay, Action<UnitCore> spawnedAction = null, int startLevel = 1)
    {
        if (!unitFactory.TryGetValue(prefabId, out var pool))
            return default;

        var spawnRequest = new SpawnRequest
        {
            spawnedUnitUid = new UnitUID(prefabId, localTick + 1, teamId, frameSpawnSequence),
            pool = pool,
            startLevel = startLevel,
            spawnedAction = spawnedAction,
            spawnTimer = spawnDelay,
        };
        frameSpawnSequence++;

        spawnRequests.Add(spawnRequest);
        return new UnitSpawnHandle(spawnRequest);
    }

    public void CreateDespawnRequest(UnitCore despawnTarget, fp despawnDelay)
    {
        if (spawnedUnitTable.ContainsKey(despawnTarget.UnitID) &&
            unitFactory.TryGetValue(despawnTarget.PrefabId, out var pool))
        {
            despawnRequests.Add(new DespawnRequest
            {
                despawnTargetUid = despawnTarget.UnitID,
                pool = pool,
                despawnTimer = despawnDelay,
            });
        }
        else
        {
            despawnTarget.gameObject.SetActive(false);
            Destroy(despawnTarget.gameObject);
        }
    }

    private bool TryGetTargetPrefabPool(int prefabId, out UnityEngine.Pool.ObjectPool<UnitCore> pool)
    {
        if (!unitFactory.TryGetValue(prefabId, out pool))
            pool = CreateNewUnitPool(prefabId);

        if (pool == null)
            return false;

        return true;
    }

    private UnityEngine.Pool.ObjectPool<UnitCore> CreateNewUnitPool(int prefabId)
    {
        if (!spawnablePrefabDict.TryGetValue(prefabId, out var prefab))
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

    public class SpawnRequest : ICloneable
    {
        public int startLevel;
        public UnitUID spawnedUnitUid;
        public UnityEngine.Pool.ObjectPool<UnitCore> pool;
        public Action<UnitCore> spawnedAction;
        public fp spawnTimer;

        public object Clone() => MemberwiseClone();
    }

    public class DespawnRequest : ICloneable
    {
        public UnitUID despawnTargetUid;
        public UnityEngine.Pool.ObjectPool<UnitCore> pool;
        public fp despawnTimer;

        public object Clone() => MemberwiseClone();
    }
    #endregion

    #region 快照和恢复
    [System.Serializable]
    public class GlobalUnitSnapshot
    {
        public uint tick;
        public List<SpawnRequest> spawnRequests = new();
        public List<DespawnRequest> despawnRequests = new();
        public Dictionary<UnitUID, object> unitSnapshots = new();
    }

    public object CaptureState()
    {
        GlobalUnitSnapshot snapshot = new GlobalUnitSnapshot();
        snapshot.tick = localTick;

        foreach (var request in spawnRequests) 
            snapshot.spawnRequests.Add((SpawnRequest)request.Clone());
        foreach (var request in despawnRequests)
            snapshot.despawnRequests.Add((DespawnRequest)request.Clone());

        foreach (var unitInfo in spawnedUnitTable)
            snapshot.unitSnapshots.Add(unitInfo.Key, unitInfo.Value.CaptureState());
        return snapshot;
    }

    public void RestoreState(object state)
    {
        if (state is GlobalUnitSnapshot snapshot)
        {
            localTick = snapshot.tick;

            spawnRequests.Clear();
            despawnRequests.Clear();
            for (int i = 0; i < snapshot.spawnRequests.Count; i++)
                spawnRequests.Add((SpawnRequest)snapshot.spawnRequests[i].Clone());
            for (int i = 0; i <= snapshot.despawnRequests.Count; i++)
                despawnRequests.Add((DespawnRequest)snapshot.despawnRequests[i].Clone());

            Queue<UnitUID> eraseUnits = new Queue<UnitUID>();
            foreach (var spawnedUnitId in spawnedUnitTable.Keys)
            {
                if (snapshot.unitSnapshots.TryGetValue(spawnedUnitId, out var unitSnapshot))
                {
                    spawnedUnitTable[spawnedUnitId].RestoreState(unitSnapshot);
                    snapshot.unitSnapshots.Remove(spawnedUnitId);
                }
                else
                    eraseUnits.Enqueue(spawnedUnitId);
            }

            foreach (var restoreUnitInfo in snapshot.unitSnapshots)
            {
                if (TryGetTargetPrefabPool(restoreUnitInfo.Key.PrefabId, out var pool))
                {
                    var restoreUnit = pool.Get();
                    restoreUnit.RestoreState(restoreUnitInfo.Value);
                }
            }

            while (eraseUnits.Count > 0)
            {
                if (spawnedUnitTable.Remove(eraseUnits.Dequeue(), out var eraseUnit))
                {
                    if (TryGetTargetPrefabPool(eraseUnit.PrefabId, out var pool))
                        pool.Release(eraseUnit);
                    else
                        Destroy(eraseUnit.gameObject);
                }
            }
        }
    }
    #endregion
}

public struct UnitSpawnHandle
{
    private UnitManager.SpawnRequest result;
    public UnitCore GetSpawnedUnit => IsSpawned ? UnitManager.Instance.Spawns[result.spawnedUnitUid] : null;
    public bool IsSpawned => UnitManager.Instance.Spawns.ContainsKey(result.spawnedUnitUid);

    public UnitSpawnHandle(UnitManager.SpawnRequest result)
    {
        this.result = result;
    }
}