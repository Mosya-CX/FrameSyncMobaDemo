using System.Collections.Generic;
using UnityEngine;
using System;
using static RollbackSystem;
using static EntitiesSimulation;
using static MissleManager;

public class RollbackSystem : MonoSingleton<RollbackSystem>
{
    [Serializable]
    public class WorldSnapshot
    {
        public uint Tick;
        public UnitManager.GlobalUnitSnapshot GlobalUnitSnapshot;// 单位快照
        public MissleManager.GlobalMissleSnapshot GlobalMissleSnapshot;// 投掷物快照
        public EntitiesSimulation.SimulationSnapshot SimulationSnapshot;
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
            RandomState = (uint)DeterministicRandom.Instance.CaptureState(),

        };
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

            worldSnapshots.Remove(rollbackTick);
        }
    }

    public void Restore(in WorldSnapshot worldSnapshot)
    {
        UnitManager.Instance.RestoreState(worldSnapshot.GlobalUnitSnapshot);
        MissleManager.Instance.RestoreState(worldSnapshot.GlobalMissleSnapshot);
        EntitiesSimulation.Instance.RestoreState(worldSnapshot.SimulationSnapshot);
        DeterministicRandom.Instance.RestoreState(worldSnapshot.RandomState);
    }
}

// 状态快照接口
public interface IStateful
{
    object CaptureState();
    void RestoreState(object state);
}