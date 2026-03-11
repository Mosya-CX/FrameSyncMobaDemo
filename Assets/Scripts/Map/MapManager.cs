using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MapManager : MonoSingleton<MapManager>, IStateful
{
    [SerializeField, LabelText("蓝方初始势力配置")]
    private TeamOriginPower blueTeamOriginPowerConfig;

    [SerializeField, LabelText("红方初始势力配置")]
    private TeamOriginPower redTeamOriginPowerConfig;

    [SerializeField, LabelText("野怪营地")]
    private MonsterCamp[] monsterCamps;

    [SerializeField, LabelText("蓝方阵营ID")]
    private byte blueTeamId = 2;

    [SerializeField, LabelText("红方阵营ID")]
    private byte redTeamId = 3;

    [SerializeField, LabelText("中立阵营ID")]
    private byte neutralTeamId = 1;

    [Serializable]
    public struct TeamOriginPower
    {
        [SerializeField, LabelText("外塔预制体")]
        public Turret TurretPrefab;

        [SerializeField, LabelText("水晶预制体")]
        public Turret NexusPrefab;

        [SerializeField, LabelText("泉水")]
        public Fountain Fountain;

        [SerializeField, LabelText("水晶位置")]
        public Transform NexusTransform;

        [SerializeField, LabelText("上路小兵生产点")]
        public Transform MobTopSpawn;

        [SerializeField, LabelText("中路小兵生产点")]
        public Transform MobMiddleSpawn;

        [SerializeField, LabelText("下路小兵生产点")]
        public Transform MobBottomSpawn;

        [SerializeField, LabelText("上路外塔位置"), FoldoutGroup("防御塔配置")]
        public Transform TopOuterTurretTransform;

        [SerializeField, LabelText("上路内塔位置"), FoldoutGroup("防御塔配置")]
        public Transform TopInnerTurretTransform;

        [SerializeField, LabelText("中路外塔位置"), FoldoutGroup("防御塔配置")]
        public Transform MiddleOuterTurretTransform;

        [SerializeField, LabelText("中路内塔位置"), FoldoutGroup("防御塔配置")]
        public Transform MiddleInnerTurretTransform;

        [SerializeField, LabelText("下路外塔位置"), FoldoutGroup("防御塔配置")]
        public Transform BottomOuterTurretTransform;

        [SerializeField, LabelText("下路内塔位置"), FoldoutGroup("防御塔配置")]
        public Transform BottomInnerTurretTransform;
    }

    [SerializeField, LabelText("开局出兵延迟")]
    private float startMinionSpawnDelay = 10f;

    [SerializeField, LabelText("野怪初始刷新延迟")]
    private float monsterStartSpawnDelay = 15f;

    [SerializeField, LabelText("出兵间隔")]
    private float minionSpawnInterval = 30f;

    [SerializeField, LabelText("每批次生成间隔")]
    private float batchSpawnInterval = 0.5f;

    [SerializeField, LabelText("每路每波生成的小兵列表")]
    private List<MinionUnit> batchSpawnMinions = new();

    private bool hasBuiltStaticStructures;
    private bool isStartMinionSpawn;

    private fp minionStartSpawnDelayTimer;
    private fp minionSpawnIntervalTimer;

    /// <summary>
    /// 待生成小兵调度表（方案A核心）
    /// </summary>
    private readonly List<ScheduledMinionSpawn> scheduledMinionSpawns = new();

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    [Serializable]
    public struct ScheduledMinionSpawn
    {
        public int PrefabId;
        public byte TeamId;
        public LaneId Lane;
        public fp RemainingDelay;
        public fp3 SpawnPosition;
        public fp2 SpawnRotation;
    }

    [Serializable]
    public struct MapManagerSnapshot
    {
        public bool HasBuiltStaticStructures;
        public bool IsStartMinionSpawn;
        public fp MinionStartSpawnDelayTimer;
        public fp MinionSpawnIntervalTimer;
        public ScheduledMinionSpawn[] PendingMinionSpawns;
        public object[] CampStates;
    }

    public void Begin()
    {
        if (!hasBuiltStaticStructures)
        {
            BuildStaticStructures();
            hasBuiltStaticStructures = true;
        }

        isStartMinionSpawn = false;
        minionStartSpawnDelayTimer = (fp)startMinionSpawnDelay;
        minionSpawnIntervalTimer = 0;
        scheduledMinionSpawns.Clear();

        for (int i = 0; i < monsterCamps.Length; i++)
            monsterCamps[i].SetRefresh((fp)monsterStartSpawnDelay);
    }

    public void Tick(uint currentTick)
    {
        TickMonsterCamps();
        TickMinionWaveSchedule();
        TickPendingMinionSpawns();
    }

    private void TickMonsterCamps()
    {
        for (int i = 0; i < monsterCamps.Length; i++)
            monsterCamps[i].Tick(DeltaTime);
    }

    private void TickMinionWaveSchedule()
    {
        if (!isStartMinionSpawn)
        {
            minionStartSpawnDelayTimer -= DeltaTime;
            if (minionStartSpawnDelayTimer <= 0)
            {
                isStartMinionSpawn = true;
                minionSpawnIntervalTimer = 0;
            }
            return;
        }

        minionSpawnIntervalTimer -= DeltaTime;
        if (minionSpawnIntervalTimer > 0)
            return;

        ScheduleMinionWave();
        minionSpawnIntervalTimer = (fp)minionSpawnInterval;
    }

    private void TickPendingMinionSpawns()
    {
        for (int i = scheduledMinionSpawns.Count - 1; i >= 0; i--)
        {
            var task = scheduledMinionSpawns[i];
            task.RemainingDelay -= DeltaTime;

            if (task.RemainingDelay > 0)
            {
                scheduledMinionSpawns[i] = task;
                continue;
            }

            SpawnScheduledMinion(task);
            scheduledMinionSpawns.RemoveAt(i);
        }
    }

    private void ScheduleMinionWave()
    {
        ScheduleLaneWaveForTeam(blueTeamOriginPowerConfig, blueTeamId);
        ScheduleLaneWaveForTeam(redTeamOriginPowerConfig, redTeamId);
    }

    private void ScheduleLaneWaveForTeam(TeamOriginPower config, byte teamId)
    {
        ScheduleLane(config.MobTopSpawn, teamId, LaneId.Top);
        ScheduleLane(config.MobMiddleSpawn, teamId, LaneId.Middle);
        ScheduleLane(config.MobBottomSpawn, teamId, LaneId.Bottom);
    }

    private void ScheduleLane(Transform laneSpawn, byte teamId, LaneId laneId)
    {
        if (laneSpawn == null)
            return;

        var spawnPos = laneSpawn.position;
        var spawnRot = laneSpawn.rotation;

        for (int i = 0; i < batchSpawnMinions.Count; i++)
        {
            var prefab = batchSpawnMinions[i];
            if (prefab == null)
                continue;

            scheduledMinionSpawns.Add(new ScheduledMinionSpawn
            {
                PrefabId = prefab.PrefabId,
                TeamId = teamId,
                Lane = laneId,
                RemainingDelay = i * (fp)batchSpawnInterval,
                SpawnPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z),
                SpawnRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w),
            });
        }
    }

    private void SpawnScheduledMinion(ScheduledMinionSpawn task)
    {
        var unit = UnitManager.Instance.SpawnNow<MinionUnit>(task.PrefabId, task.TeamId, 1, minion =>
        {
            minion.LogicPosition = task.SpawnPosition;
            minion.LogicRotation = task.SpawnRotation;
            minion.SetLane(task.Lane);
            minion.SetSide(task.TeamId);
            minion.SyncTransform();
        });

        if (unit == null)
            Debug.LogError($"[{nameof(MapManager)}] 生成小兵失败，PrefabId={task.PrefabId}");
    }

    private void BuildStaticStructures()
    {
        BuildTeamStructures(blueTeamOriginPowerConfig, blueTeamId);
        BuildTeamStructures(redTeamOriginPowerConfig, redTeamId);
    }

    private void BuildTeamStructures(TeamOriginPower config, byte teamId)
    {
        SpawnTurret(config.TurretPrefab, config.TopOuterTurretTransform, teamId);
        SpawnTurret(config.TurretPrefab, config.TopInnerTurretTransform, teamId);
        SpawnTurret(config.TurretPrefab, config.MiddleOuterTurretTransform, teamId);
        SpawnTurret(config.TurretPrefab, config.MiddleInnerTurretTransform, teamId);
        SpawnTurret(config.TurretPrefab, config.BottomOuterTurretTransform, teamId);
        SpawnTurret(config.TurretPrefab, config.BottomInnerTurretTransform, teamId);
        SpawnTurret(config.NexusPrefab, config.NexusTransform, teamId);
    }

    private void SpawnTurret(Turret prefab, Transform point, byte teamId)
    {
        if (prefab == null || point == null)
            return;

        var spawnPos = point.position;
        var spawnRot = point.rotation;

        var unit = UnitManager.Instance.SpawnNow<Turret>(prefab.PrefabId, teamId, 1, turret =>
        {
            turret.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
            turret.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
            turret.SyncTransform();
        });

        if (unit == null)
            Debug.LogError($"[{nameof(MapManager)}] 生成防御塔失败，PrefabId={prefab.PrefabId}");
    }

    public object CaptureState()
    {
        var campStates = new object[monsterCamps.Length];
        for (int i = 0; i < monsterCamps.Length; i++)
            campStates[i] = monsterCamps[i].CaptureState();

        return new MapManagerSnapshot
        {
            HasBuiltStaticStructures = hasBuiltStaticStructures,
            IsStartMinionSpawn = isStartMinionSpawn,
            MinionStartSpawnDelayTimer = minionStartSpawnDelayTimer,
            MinionSpawnIntervalTimer = minionSpawnIntervalTimer,
            PendingMinionSpawns = scheduledMinionSpawns.ToArray(),
            CampStates = campStates,
        };
    }

    public void RestoreState(object state)
    {
        var snap = (MapManagerSnapshot)state;

        hasBuiltStaticStructures = snap.HasBuiltStaticStructures;
        isStartMinionSpawn = snap.IsStartMinionSpawn;
        minionStartSpawnDelayTimer = snap.MinionStartSpawnDelayTimer;
        minionSpawnIntervalTimer = snap.MinionSpawnIntervalTimer;

        scheduledMinionSpawns.Clear();
        if (snap.PendingMinionSpawns != null)
            scheduledMinionSpawns.AddRange(snap.PendingMinionSpawns);

        if (snap.CampStates == null)
            return;

        for (int i = 0; i < monsterCamps.Length && i < snap.CampStates.Length; i++)
            monsterCamps[i].RestoreState(snap.CampStates[i]);
    }
}

public enum LaneId : byte
{
    Top,
    Middle,
    Bottom,
}
