using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class UnitSpawner : MonoSingleton<UnitSpawner>, IGameFlowManaged, IGlobalCommandHandler
{
    [SerializeField, LabelText("单位预制体表")]
    private SerializedDictionary<int, UnitCore> spawnableDict;

    // 已生成单位的查找表
    private Dictionary<UnitUID, UnitCore> spawnedUnitTable = new();
    public IReadOnlyDictionary<UnitUID, UnitCore> Spawns => spawnedUnitTable;

    // 对象池表
    public Dictionary<int, UnityEngine.Pool.ObjectPool<UnitCore>> unitPoolTable = new();

    // 每帧的序列号计数器（键为目标帧号）
    private Dictionary<uint, byte> tickSequenceMap = new();

    public IEnumerator Init()
    {
        spawnedUnitTable ??= new Dictionary<UnitUID, UnitCore>();
        unitPoolTable ??= new Dictionary<int, UnityEngine.Pool.ObjectPool<UnitCore>>();
        tickSequenceMap ??= new Dictionary<uint, byte>();
        FrameSyncCoreSystem.Instance?.RegisterGlobalHandler(this);
        yield break;
    }

    public IEnumerator Begin()
    {
        spawnedUnitTable.Clear();
        unitPoolTable.Clear();
        tickSequenceMap.Clear();
        yield break;
    }

    public IEnumerator Clean()
    {
        foreach (var unit in spawnedUnitTable.Values)
        {
            if (unit != null)
                Destroy(unit.gameObject);
        }
        spawnedUnitTable.Clear();
        foreach (var pool in unitPoolTable.Values)
            pool.Clear();
        unitPoolTable.Clear();
        tickSequenceMap.Clear();
        yield break;
    }

    public void Tick(ulong currentTick)
    {
        // 每帧开始时清理旧帧的序列号记录
        var keysToRemove = new List<uint>();
        foreach (var tick in tickSequenceMap.Keys)
        {
            if (tick < currentTick)
                keysToRemove.Add(tick);
        }
        foreach (var tick in keysToRemove)
            tickSequenceMap.Remove(tick);
    }

    // 处理生成指令
    public void HandleSpawnCommand(SpawnUnitCommand cmd)
    {
        if (!spawnableDict.TryGetValue(cmd.PrefabId, out var prefab))
        {
            Debug.LogError($"Spawn failed: prefabId {cmd.PrefabId} not found.");
            return;
        }

        // 获取或创建该目标帧的序列号
        if (!tickSequenceMap.TryGetValue(cmd.TargetTick, out byte seq))
            seq = 0;

        UnitCore core = null;
        switch (cmd.Mode)
        {
            case SpawnableMode.Default:
                core = Instantiate(prefab);
                break;
            case SpawnableMode.Pool:
                if (!unitPoolTable.TryGetValue(cmd.PrefabId, out var pool))
                {
                    pool = CreateNewUnitPool(prefab);
                    unitPoolTable.Add(cmd.PrefabId, pool);
                }
                core = pool.Get();
                break;
        }

        if (core == null)
        {
            Debug.LogError($"Failed to instantiate unit of prefabId {cmd.PrefabId}");
            return;
        }

        core.transform.position = cmd.SpawnPosition;
        core.transform.rotation = cmd.SpawnRotation;

        // 生成唯一ID：帧号使用指令的目标帧，序列号使用当前计数
        var uid = new UnitUID(cmd.PrefabId, cmd.TargetTick, cmd.TeamId, seq);
        core.OnSpawn(uid, cmd.StartLevel);

        // 注册指令接收器（如果单位实现了ICommandReceiver）
        if (core is ICommandReceiver receiver)
            FrameSyncCoreSystem.Instance.RegisterReceiver(receiver);

        // 存入查找表
        spawnedUnitTable[uid] = core;

        // 更新该帧的序列号
        tickSequenceMap[cmd.TargetTick] = (byte)(seq + 1);
    }

    // 处理销毁指令
    public void HandleDespawnCommand(DespawnUnitCommand cmd)
    {
        if (!spawnedUnitTable.TryGetValue(cmd.UnitId, out var core))
            return;

        // 注销指令接收器
        if (core is ICommandReceiver)
            FrameSyncCoreSystem.Instance.UnregisterReceiver(cmd.UnitId);

        core.OnDespawn();

        switch (cmd.Mode)
        {
            case SpawnableMode.Default:
                Destroy(core.gameObject);
                break;
            case SpawnableMode.Pool:
                if (unitPoolTable.TryGetValue(core.PrefabId, out var pool))
                    pool.Release(core);
                else
                    Destroy(core.gameObject);
                break;
        }

        spawnedUnitTable.Remove(cmd.UnitId);
    }

    private UnityEngine.Pool.ObjectPool<UnitCore> CreateNewUnitPool(UnitCore prefab)
    {
        return new UnityEngine.Pool.ObjectPool<UnitCore>(
            createFunc: () => Instantiate(prefab),
            actionOnGet: unit => unit.gameObject.SetActive(true),
            actionOnRelease: unit => unit.gameObject.SetActive(false),
            actionOnDestroy: unit => Destroy(unit.gameObject),
            collectionCheck: false,
            defaultCapacity: 32,
            maxSize: 1024
        );
    }

    public bool CanHandle(CommandType type) =>
        type == CommandType.SpawnUnit || type == CommandType.DespawnUnit;

    public void HandleCommand(ICommand command)
    {
        if (command.Type == CommandType.SpawnUnit)
            HandleSpawnCommand((SpawnUnitCommand)command);
        else if (command.Type == CommandType.DespawnUnit)
            HandleDespawnCommand((DespawnUnitCommand)command);
    }
}

public enum SpawnableMode
{
    Default,
    Pool,
}