using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using Unity.UOS.Matchmaking.Server.Model;
using UnityEngine;
using UnityEngine.Rendering;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

public class UnitSpawner : MonoSingleton<UnitSpawner>, IGameFlowManaged
{
    [SerializeField, LabelText("单位预制体表")]
    private SerializedDictionary<int, UnitCore> spawnableDict;

    private Dictionary<UnitUID, UnitCore> spawnedUnitTable = new();

    private Queue<UnitSpawnRequest> spawnRequestQueue = new ();
    private Queue<UnitDespawnRequest> despawnRequestQueue = new ();

    public Dictionary<int, UnityEngine.Pool.ObjectPool<UnitCore>> unitPoolTabel = new();

    public IEnumerator Init()
    {
        if (spawnedUnitTable == null)
            spawnedUnitTable = new();
        if (spawnRequestQueue == null)
            spawnRequestQueue = new();
        if (despawnRequestQueue == null)
            despawnRequestQueue = new();
        if (unitPoolTabel == null)
            unitPoolTabel = new();

        yield break;
    }

    public IEnumerator Begin()
    {
        spawnedUnitTable.Clear();
        spawnRequestQueue.Clear();
        despawnRequestQueue.Clear();
        foreach (var pool in unitPoolTabel.Values)
            pool.Clear();
        unitPoolTabel.Clear();

        yield break;
    }

    public IEnumerator Clean()
    {
        spawnedUnitTable.Clear();
        spawnRequestQueue.Clear();
        despawnRequestQueue.Clear();
        foreach (var pool in unitPoolTabel.Values)
            pool.Clear();
        unitPoolTabel.Clear();
        spawnedUnitTable = null;
        spawnRequestQueue = null;
        despawnRequestQueue = null;
        unitPoolTabel = null;

        yield break;
    }

    public void Tick(ulong currentTick)
    {
        if (spawnRequestQueue.Count > 0)
        {
            byte spawnSequence = 0;
            while (spawnRequestQueue.Count > 0)
            {
                var request = spawnRequestQueue.Dequeue();
                if (spawnableDict.TryGetValue(request.spawnableId, out var unitPrefab))
                {
                    UnitCore core = null;
                    switch (request.mode)
                    {
                        case SpawnableMode.Default:
                            core = Instantiate(unitPrefab);
                            break;
                        case SpawnableMode.Pool:
                            if (!unitPoolTabel.TryGetValue(request.spawnableId, out var pool))
                            {
                                pool = CreateNewUnitPool(unitPrefab);
                                unitPoolTabel.Add(request.spawnableId, pool);
                            }
                            core = pool.Get();
                            break;
                    }

                    if (!core)
                        continue;

                    core.transform.position = request.spawnPos;
                    core.transform.rotation = request.spawnRot;

                    core.OnSpawn(new UnitUID(
                        request.spawnableId, 
                        currentTick, 
                        request.assignedTeamId, 
                        spawnSequence),  
                        request.startLevel);

                    spawnSequence++;

                    spawnedUnitTable.Add(core.UnitID, core);
                }
            }
        }

        if (despawnRequestQueue.Count > 0)
        {
            while (despawnRequestQueue.Count > 0)
            {
                var request = despawnRequestQueue.Dequeue();
                if (spawnedUnitTable.TryGetValue(request.despawnableId, out var unit))
                {
                    unit?.OnDespawn();
                    switch (request.mode)
                    {
                        case SpawnableMode.Default:
                            Destroy(unit.gameObject);
                            break;
                        case SpawnableMode.Pool:
                            if (!unitPoolTabel.TryGetValue(unit.PrefabId, out var pool))
                            {
                                Destroy(unit.gameObject);
                                break;
                            }
                            pool.Release(unit);
                            break;
                    }
                }
                spawnedUnitTable.Remove(request.despawnableId);
            }
        }
    }

    private UnityEngine.Pool.ObjectPool<UnitCore> CreateNewUnitPool(UnitCore prefab)
    {
        return new(
            createFunc: () => Instantiate(prefab),
            actionOnGet: unit => unit.gameObject.SetActive(true),
            actionOnRelease: unit => unit.gameObject.SetActive(false),
            actionOnDestroy: unit => Destroy(unit.gameObject),
            collectionCheck: false,
            defaultCapacity: 32,
            maxSize: 1024
        );
    }

    public void SendUnitSpawnRequest(
        byte spawnableId, Vector3 spawnPos, Quaternion spawnRot,
        byte assignedTeamId, int startLevel = 1, SpawnableMode mode = SpawnableMode.Default)
    {
        spawnRequestQueue.Enqueue(new UnitSpawnRequest(
            spawnableId, spawnPos, spawnRot, assignedTeamId, startLevel, mode));
    }

    public void SendUnitDespawnRequest(UnitUID despawnableId, SpawnableMode mode = SpawnableMode.Default)
    {
        despawnRequestQueue.Enqueue(new UnitDespawnRequest(despawnableId, mode));
    }

    private struct UnitSpawnRequest
    {
        public readonly byte spawnableId;
        public readonly Vector3 spawnPos;
        public readonly Quaternion spawnRot;
        public readonly int startLevel;
        public readonly SpawnableMode mode;
        public readonly byte assignedTeamId;

        public UnitSpawnRequest(byte spawnableId, Vector3 spawnPos, Quaternion spawnRot,
            byte assignedTeamId, int startLevel = 1, SpawnableMode mode = SpawnableMode.Default)
        {
            this.spawnableId = spawnableId;
            this.spawnPos = spawnPos;
            this.spawnRot = spawnRot;
            this.startLevel = startLevel;
            this.mode = mode;
            this.assignedTeamId = assignedTeamId;
        }
    }

    private struct UnitDespawnRequest
    {
        public readonly UnitUID despawnableId;
        public readonly SpawnableMode mode;

        public UnitDespawnRequest(UnitUID despawnableId, SpawnableMode mode = SpawnableMode.Default)
        {
            this.despawnableId = despawnableId;
            this.mode = mode;
        }
    }

    public int GetStateHash()
    {
        return 0;
    }
}

public enum SpawnableMode
{
    Default,
    Pool,
}