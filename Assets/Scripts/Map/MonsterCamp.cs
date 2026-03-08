using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MonsterCamp : MonoBehaviour, IStateful
{
    [SerializeField, LabelText("野怪刷新时间")]
    public float monsterFreshDuration;

    [SerializeField, LabelText("选择主野怪"), ValueDropdown(nameof(GetItemIndices))]
    private int selectedMainMonsterIndex;

    [SerializeField, LabelText("野怪点位")]
    private CampSpawn[] campSpawns;

    [System.Serializable]
    public struct CampSpawn
    {
        [LabelText("生成点位")]
        public Transform spanwPoint;
        [LabelText("预制体")]
        public MonsterUnit monsterPrefab;
    }

    private IEnumerable<ValueDropdownItem<int>> GetItemIndices()
    {
        for (int i = 0; i < campSpawns.Length; i++)
        {
            yield return new ValueDropdownItem<int>
            {
                Text = $"【{i}/{campSpawns[i]}】",  // 下拉显示的文本
                Value = i                    // 实际存储的下标
            };
        }
    }

    protected List<MonsterUnit> spawnedMonster = new();

    protected bool shouldRefresh;
    protected fp refreshTimer;

    public virtual void RefreshMonster()
    {
        // 1销毁所有已存在的怪物（需确保 spawnedMonster 准确记录所有生成的怪物）
        for (int i = 0; i < spawnedMonster.Count; i++)
            UnitManager.Instance.CreateDespawnRequest(spawnedMonster[i], 0);
        spawnedMonster.Clear(); // 清空列表，因为这些怪物即将销毁

        // 生成新怪物
        for (int i = 0; i < campSpawns.Length; i++)
        {
            // 保存当前索引的副本，避免闭包问题
            int index = i;
            var spawnPos = campSpawns[i].spanwPoint.position;
            var spawnRot = campSpawns[i].spanwPoint.rotation;

            UnitManager.Instance.CreateSpawnRequest(campSpawns[i].monsterPrefab.PrefabId, 1, 0, (spawned) =>
            {
                // 安全类型转换
                MonsterUnit monster = spawned as MonsterUnit;
                if (monster == null)
                {
                    Debug.LogError($"生成的单位不是 MonsterUnit 类型: {spawned}");
                    return;
                }

                // 设置所属关系
                monster.SetBelongTo(this,
                    new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z),
                    new fp2((fp)spawnRot.y, (fp)spawnRot.w));

                // 将野怪加入管理列表
                spawnedMonster.Add(monster);

                // 如果是主野怪，注册死亡回调
                if (index == selectedMainMonsterIndex)
                    monster.RegisterDamageCallback(UnitDamageCallbackType.OnDeath, OnMainMonsterDeath);
            });
        }
    }

    protected void OnMainMonsterDeath(in DamageInfo _)
    {
        _.Target.UnregisterDamageCallback(UnitDamageCallbackType.OnDeath, OnMainMonsterDeath);
        SetRefresh((fp)monsterFreshDuration);
    }

    public void Tick(fp dt)
    {
        if (shouldRefresh)
        {
            if (refreshTimer <= 0)
            {
                RefreshMonster();
                shouldRefresh = false;
            }
        }
    }

    public void SetRefresh(fp delay)
    {
        shouldRefresh = true;
        refreshTimer = delay;
    }

    public object CaptureState()
    {
        throw new System.NotImplementedException();
    }

    public void RestoreState(object state)
    {
        throw new System.NotImplementedException();
    }
}
