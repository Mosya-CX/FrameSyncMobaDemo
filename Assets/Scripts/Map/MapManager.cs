using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using Unity.VisualScripting;
using UnityEngine;

public class MapManager : MonoSingleton<MapManager>
{

    [SerializeField, LabelText("蓝方初始势力配置")]
    private TeamOriginPower blueTeamOriginPowerConfig;
    [SerializeField, LabelText("红初初始势力配置")]
    private TeamOriginPower redTeamOriginPowerConfig;
    [SerializeField, LabelText("野怪营地")]
    private MonsterCamp[] monsterCamps;

    [System.Serializable]
    public struct TeamOriginPower
    {
        [SerializeField, LabelText("防御塔预制体")]
        private Turret TurretPrefab;
        [SerializeField, LabelText("水晶预制体")]
        private Turret NexusPrefab;
        [SerializeField, LabelText("泉水")]
        public Fountain Fountain;
        [SerializeField, LabelText("水晶位置")]
        public Transform NexusTransform;
        [SerializeField, LabelText("上路小兵生产点")]
        public Transform MobTopSpawn;
        [SerializeField, LabelText("中路路小兵生产点")]
        public Transform MobMiddleSpawn;
        [SerializeField, LabelText("下路小兵生产点")]
        public Transform MobButtomSpawn;
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
    private float monsterStartSpawnDelay = 15;

    [SerializeField, LabelText("出兵间隔")]
    private float minionSpawnInterval = 30;
    [SerializeField, LabelText("每批次生成间隔")]
    private float batchSpawnInterval = 0.5f;
    [SerializeField, LabelText("每批次生成单位")]
    private List<MobUnit> batchSpawnMobs = new();

    private uint localTick;
    private fp minionStartSpawnDelayTimer;
    private bool isStartMinionSpawn;
    private fp mobSpawnIntervalTimer;

    private fp DeltaTime => GameFlowManager.Instance.TickIntervalFP;

    public void Begin()
    {
        minionStartSpawnDelayTimer = (fp)monsterStartSpawnDelay;
        for (int i = 0; i < monsterCamps.Length; i++)
            monsterCamps[i].SetRefresh((fp)monsterStartSpawnDelay);
    }

    public void Tick(uint currentTick)
    {
        localTick = currentTick;
        if (isStartMinionSpawn)
        {
            if (mobSpawnIntervalTimer <= 0)
            {
                SpawnMob();
                mobSpawnIntervalTimer = (fp)minionSpawnInterval;
            }
            else
                mobSpawnIntervalTimer -= DeltaTime;
        }
        else
        {
            minionStartSpawnDelayTimer -= DeltaTime;
            if (minionStartSpawnDelayTimer < 0)
                isStartMinionSpawn = true;
        }
    }

    private void SpawnMob()
    {
        for (int i = 0; i < batchSpawnMobs.Count; i++)
        {
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit)=>
            {
                var spawnPos = blueTeamOriginPowerConfig.MobTopSpawn.position;
                var spawnRot = blueTeamOriginPowerConfig.MobTopSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit) =>
            {
                var spawnPos = blueTeamOriginPowerConfig.MobMiddleSpawn.position;
                var spawnRot = blueTeamOriginPowerConfig.MobMiddleSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit) =>
            {
                var spawnPos = blueTeamOriginPowerConfig.MobButtomSpawn.position;
                var spawnRot = blueTeamOriginPowerConfig.MobButtomSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
        }

        for (int i = 0; i < batchSpawnMobs.Count; i++)
        {
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit) =>
            {
                var spawnPos = redTeamOriginPowerConfig.MobTopSpawn.position;
                var spawnRot = redTeamOriginPowerConfig.MobTopSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit) =>
            {
                var spawnPos = redTeamOriginPowerConfig.MobMiddleSpawn.position;
                var spawnRot = redTeamOriginPowerConfig.MobMiddleSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
            UnitManager.Instance.CreateSpawnRequest(batchSpawnMobs[i].PrefabId, 2, i * (fp)batchSpawnInterval, (unit) =>
            {
                var spawnPos = redTeamOriginPowerConfig.MobButtomSpawn.position;
                var spawnRot = redTeamOriginPowerConfig.MobButtomSpawn.rotation;
                unit.LogicPosition = new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z);
                unit.LogicRotation = new fp2((fp)spawnRot.y, (fp)spawnRot.w);
                unit.SyncTransform();
            });
        }
    }
}
