using System.Collections.Generic;
using UnityEngine;
using System;

public class RollbackSystem : MonoSingleton<RollbackSystem>
{
    // 快照数据结构
    [Serializable]
    public class WorldSnapshot
    {
        public uint tick;
        public Dictionary<UnitUID, object> unitStates = new(); // 每个单位的状态快照
    }

    // 环形缓冲区，存储最近N帧的快照（N可配置）
    private List<WorldSnapshot> snapshots = new List<WorldSnapshot>();
    [SerializeField] private int maxSnapshotCount = 100; // 对应约3秒（30fps）

    // 快照间隔（每多少帧存一次快照）
    [SerializeField] private int snapshotInterval = 10;

    private uint lastSnapshotTick = 0;

    // 由GameFlowManager在每帧Tick后调用，保存当前帧快照
    public void TakeSnapshot(uint tick)
    {
        if (tick - lastSnapshotTick < snapshotInterval) return;

        var snap = new WorldSnapshot { tick = tick };
        // 收集所有需要回滚的单元的状态
        foreach (var kv in FrameSyncCoreSystem.Instance.commandReceivers) // 注意：commandReceivers现在是private，需要公开访问或另寻途径
        {
            if (kv.Value is IStateful stateful) // 定义接口IStateful
            {
                snap.unitStates[kv.Key] = stateful.CaptureState();
            }
        }
        snapshots.Add(snap);
        if (snapshots.Count > maxSnapshotCount)
            snapshots.RemoveAt(0);
        lastSnapshotTick = tick;
    }

    // 回滚到指定帧（不包括该帧，即到前一帧），然后应用权威指令重新模拟
    public void RollbackToTick(uint targetTick, List<ICommand> authCmds, List<ICommand> predCmds)
    {
        Debug.Log($"Rolling back to tick {targetTick}");

        // 找到最近的快照，其tick ≤ targetTick
        WorldSnapshot snapshot = null;
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            if (snapshots[i].tick <= targetTick)
            {
                snapshot = snapshots[i];
                break;
            }
        }
        if (snapshot == null)
        {
            Debug.LogError("No snapshot available for rollback!");
            return;
        }

        // 恢复所有单位状态到快照时刻
        foreach (var kv in snapshot.unitStates)
        {
            if (FrameSyncCoreSystem.Instance.commandReceivers.TryGetValue(kv.Key, out var receiver) && receiver is IStateful stateful)
            {
                stateful.RestoreState(kv.Value);
            }
        }

        // 从 snapshot.tick+1 开始到 targetTick，重新模拟权威指令
        for (uint t = snapshot.tick + 1; t <= targetTick; t++)
        {
            // 这里需要获取tick t的权威指令，可能来自 authoritativeCommands 或者本地预测（如果尚未收到）
            // 实际实现时，应在 FrameSyncCoreSystem 中存储所有接收到的权威指令，并在此重新应用
            // 简化起见，我们可以让 FrameSyncCoreSystem 提供一个方法 ReplayTick(tick)
        }

        // 然后继续正常模拟直到当前帧
        // 这部分需要在 GameFlowManager 中驱动重新模拟
    }

    public void PerformRollback(uint mismatchedTick, List<ICommand> authoritativeCmds)
    {
        Debug.Log($"[Rollback] Detected mismatch at tick {mismatchedTick}");

        // 寻找最近的有效快照
        WorldSnapshot snapshot = GetClosestSnapshot(mismatchedTick);
        if (snapshot == null) return; // 无法回滚

        // 恢复全局状态
        foreach (var kv in snapshot.unitStates)
        {
            if (FrameSyncCoreSystem.Instance.TryGetReceiver(kv.Key, out var receiver) && receiver is IStateful stateful)
            {
                stateful.RestoreState(kv.Value);
            }
        }

        // TODO 恢复随机数状态
 

        // 重新模拟
        uint currentLocalTick = GameFlowManager.Instance.CurrentLocalTick;

        for (uint t = snapshot.tick + 1; t <= currentLocalTick; t++)
        {
            // 优先使用权威指令
            if (authoritativeCmds != null && t == mismatchedTick)
            {
                foreach (var cmd in authoritativeCmds)
                    PredictionSystem.Instance.ExecuteCommand(cmd);
            }
            else
            {
                // 如果是未来的帧，尝试使用本地预测 (如果是本地玩家输入)
                // 注意：纯服务端权威的游戏通常只回滚到收到权威帧为止，
                // 但这里我们要修正当前的状态，所以需要重放预测
                if (PredictionSystem.Instance.GetPredictedCommands(t, out var predCmds))
                {
                    foreach (var cmd in predCmds)
                        PredictionSystem.Instance.ExecuteCommand(cmd);
                }
            }

            // 模拟这一帧的逻辑
            // 注意：这里需要手动调用单位的 Tick，或者由 GameFlowManager 提供接口
            // GameFlowManager.Instance.SimulateSingleTick(t); 
        }
    }

    // TODO
    private WorldSnapshot GetClosestSnapshot(uint tick)
    {
        
        return null;
    }
}

// 状态快照接口
public interface IStateful
{
    object CaptureState();
    void RestoreState(object state);
}

public interface IHandlerStateful
{
    object CaptureHandlerState();
    void RestoreHandlerState(object state);
}