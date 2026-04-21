using System.Collections.Generic;
using UnityEngine;
using System;

public class RollbackSystem : MonoSingleton<RollbackSystem>
{
    //[SerializeField]
    //public uint maxSnapshotCount = 120;

    [Serializable]
    public class WorldSnapshot
    {
        public uint Tick;
        public UnitManager.GlobalUnitSnapshot GlobalUnitSnapshot;// 单位快照
        public MissleManager.GlobalMissleSnapshot GlobalMissleSnapshot;// 投掷物快照
        public EntitiesSimulation.SimulationSnapshot SimulationSnapshot;
        public DamageManager.DamageManagerSnapshot DamageRequestsSnapshot;
        public HealManager.HealManagerSnapshot HealRequestsSnapshot;
        public uint RandomState;
    }

    private Dictionary<uint, WorldSnapshot> worldSnapshots;

    private PriorityQueue<uint> rollbackRequest = new(Comparer<uint>.Create((a, b)=>b.CompareTo(a)));

    public void TakeSnapshot(uint tick)
    {
        var snapshot = new WorldSnapshot
        {
            Tick = tick,
            GlobalUnitSnapshot = UnitManager.Instance.CaptureState() as UnitManager.GlobalUnitSnapshot,
            GlobalMissleSnapshot = MissleManager.Instance.CaptureState() as MissleManager.GlobalMissleSnapshot,
            SimulationSnapshot = EntitiesSimulation.Instance.CaptureState() as EntitiesSimulation.SimulationSnapshot,
            DamageRequestsSnapshot = DamageManager.Instance.CaptureState() as DamageManager.DamageManagerSnapshot,
            HealRequestsSnapshot = HealManager.Instance.CaptureState() as HealManager.HealManagerSnapshot,
            RandomState = (uint)DeterministicRandom.Instance.CaptureState(),
        };

        worldSnapshots[tick] = snapshot;
    }

    public void EraseTickSnapshot(uint targetTick)
    {
        worldSnapshots.Remove(targetTick);
    }

    public void CreateNewRollbackRequest(uint rollbackTick)
    {
        rollbackRequest.Enqueue(rollbackTick);
    }

    public void CheckRollback(uint localTick)
    {
        if (rollbackRequest.Count > 0)
        {
            var rollbackTick = rollbackRequest.Dequeue();
            while (rollbackRequest.Count > 0)
                EraseTickSnapshot(rollbackRequest.Dequeue());
            Rollback(rollbackTick, localTick);
        }
    }

    // 回滚到指定帧，然后应用权威指令重新模拟
    public void Rollback(uint rollbackTick, uint currentTick)
    {
        Debug.Log($"Rolling back to tick {rollbackTick}");
        if (worldSnapshots.TryGetValue(rollbackTick, out var worldSnapshot))
        {
            // 复原状态
            Restore(worldSnapshot);

            foreach (var unit in UnitManager.Instance.Spawns.Values)
                if (unit is CombatUnitBase combatUnit)
                    combatUnit.AnimationController?.RemoveRecordsAfter(rollbackTick);

            ParticleManager.Instance.Rollback(rollbackTick);
            AudioManager.Instance.Rollback(rollbackTick);

            // 重建
            for (uint rebuildTick = rollbackTick; rebuildTick < currentTick; rebuildTick++)
            {
                if (FrameSyncCoreSystem.Instance.AuthoritativeCommands.TryGetValue(rebuildTick, out var frameData))
                    for (int i = 0; i < frameData.Commands.Count; i++)
                        FrameSyncCoreSystem.Instance.ExecuteCommand(frameData.Commands[i]);
                else
                    PredictionSystem.Instance.ExcutePredicte(rebuildTick);

                GameFlowManager.Instance.GameTick(rebuildTick);
            }

            ParticleManager.Instance.Correct(currentTick);
            AudioManager.Instance.Correct(currentTick);

            foreach (var unit in UnitManager.Instance.Spawns.Values)
                if (unit is CombatUnitBase combatUnit)
                    combatUnit.AnimationController?.RebuildOverlayToTick(currentTick, GameFlowManager.Instance.TickInterval);

            worldSnapshots.Remove(rollbackTick);
        }
    }

    public void Restore(in WorldSnapshot worldSnapshot)
    {
        UnitManager.Instance.RestoreState(worldSnapshot.GlobalUnitSnapshot);
        MissleManager.Instance.RestoreState(worldSnapshot.GlobalMissleSnapshot);
        EntitiesSimulation.Instance.RestoreState(worldSnapshot.SimulationSnapshot);
        DamageManager.Instance.RestoreState(worldSnapshot.DamageRequestsSnapshot);
        HealManager.Instance.RestoreState(worldSnapshot.HealRequestsSnapshot);
        DeterministicRandom.Instance.RestoreState(worldSnapshot.RandomState);
    }
}

// 状态快照接口
public interface IStateful
{
    object CaptureState();
    void RestoreState(object state);
}