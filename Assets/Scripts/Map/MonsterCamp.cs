using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

public class MonsterCamp : MonoBehaviour, IStateful
{
    [SerializeField, LabelText("野怪刷新时间")]
    private float monsterFreshDuration = 90f;

    [SerializeField, LabelText("主野怪索引"), ValueDropdown(nameof(GetItemIndices))]
    private int selectedMainMonsterIndex;

    [SerializeField, LabelText("野怪点位")]
    private CampSpawn[] campSpawns;

    [Serializable]
    public struct CampSpawn
    {
        [LabelText("生成点位")]
        public Transform spawnPoint;

        [LabelText("野怪预制体")]
        public MonsterUnit monsterPrefab;
    }

    [Serializable]
    public struct MonsterCampSnapshot
    {
        public bool ShouldRefresh;
        public fp RefreshTimer;
        public UnitUID[] SpawnedMonsterIds;
    }

    protected readonly List<MonsterUnit> spawnedMonster = new();

    protected bool shouldRefresh;
    protected fp refreshTimer;

    public IReadOnlyList<MonsterUnit> SpawnedMonster => spawnedMonster;
    public bool ShouldRefresh => shouldRefresh;
    public fp RefreshTimer => refreshTimer;

    private IEnumerable<ValueDropdownItem<int>> GetItemIndices()
    {
        for (int i = 0; i < campSpawns.Length; i++)
        {
            string name = campSpawns[i].monsterPrefab != null ? campSpawns[i].monsterPrefab.name : "空";
            yield return new ValueDropdownItem<int>($"[{i}] {name}", i);
        }
    }

    public virtual void Tick(fp dt)
    {
        CleanupNullMonsters();

        if (!shouldRefresh)
            return;

        refreshTimer -= dt;
        if (refreshTimer > 0)
            return;

        RefreshMonster();
        shouldRefresh = false;
        refreshTimer = 0;
    }

    public virtual void SetRefresh(fp delay)
    {
        shouldRefresh = true;
        refreshTimer = delay;
    }

    public virtual void RefreshMonster()
    {
        // 先清掉现有野怪
        for (int i = 0; i < spawnedMonster.Count; i++)
        {
            var monster = spawnedMonster[i];
            if (monster == null)
                continue;

            UnbindMainMonsterIfNeeded(monster, i);
            UnitManager.Instance.DespawnNow(monster);
        }

        spawnedMonster.Clear();

        // 重新生成
        for (int i = 0; i < campSpawns.Length; i++)
        {
            int index = i;
            var config = campSpawns[index];

            if (config.monsterPrefab == null || config.spawnPoint == null)
                continue;

            var spawnPos = config.spawnPoint.position;
            var spawnRot = config.spawnPoint.rotation;

            var monster = UnitManager.Instance.SpawnNow<MonsterUnit>(config.monsterPrefab.PrefabId, 1, 1, spawned =>
            {
                spawned.SetBelongTo(
                    this,
                    new fp3((fp)spawnPos.x, (fp)spawnPos.y, (fp)spawnPos.z),
                    new fp2((fp)spawnRot.y, (fp)spawnRot.w));
            });

            if (monster == null)
                continue;

            spawnedMonster.Add(monster);

            if (index == selectedMainMonsterIndex)
                BindMainMonsterDeath(monster);
        }
    }

    protected virtual void OnMainMonsterDeath(DeathEvent evt)
    {
        if (evt.Victim is MonsterUnit monster)
            monster.Death -= OnMainMonsterDeath;

        SetRefresh((fp)monsterFreshDuration);
    }

    protected void BindMainMonsterDeath(MonsterUnit monster)
    {
        if (monster == null)
            return;

        monster.Death -= OnMainMonsterDeath;
        monster.Death += OnMainMonsterDeath;
    }

    protected void UnbindMainMonsterIfNeeded(MonsterUnit monster, int index)
    {
        if (monster == null)
            return;

        if (index == selectedMainMonsterIndex)
            monster.Death -= OnMainMonsterDeath;
    }

    protected void CleanupNullMonsters()
    {
        for (int i = spawnedMonster.Count - 1; i >= 0; i--)
        {
            if (spawnedMonster[i] == null)
                spawnedMonster.RemoveAt(i);
        }
    }

    public object CaptureState()
    {
        CleanupNullMonsters();

        var ids = new List<UnitUID>(spawnedMonster.Count);
        for (int i = 0; i < spawnedMonster.Count; i++)
        {
            if (spawnedMonster[i] != null)
                ids.Add(spawnedMonster[i].UnitID);
        }

        return new MonsterCampSnapshot
        {
            ShouldRefresh = shouldRefresh,
            RefreshTimer = refreshTimer,
            SpawnedMonsterIds = ids.ToArray(),
        };
    }

    public void RestoreState(object state)
    {
        var snap = (MonsterCampSnapshot)state;

        shouldRefresh = snap.ShouldRefresh;
        refreshTimer = snap.RefreshTimer;

        spawnedMonster.Clear();

        if (snap.SpawnedMonsterIds == null)
            return;

        for (int i = 0; i < snap.SpawnedMonsterIds.Length; i++)
        {
            if (UnitManager.Instance.Spawns.TryGetValue(snap.SpawnedMonsterIds[i], out var unit) &&
                unit is MonsterUnit monster)
            {
                spawnedMonster.Add(monster);

                if (i == selectedMainMonsterIndex)
                    BindMainMonsterDeath(monster);
            }
        }
    }
}
