using System.Collections.Generic;

public class PredictionSystem : MonoSingleton<PredictionSystem>
{
    // 本地预测指令历史
    private Dictionary<uint, List<ICommand>> predictedCommands = new();

    public void AddLocalCommand(ICommand cmd)
    {
        if (!predictedCommands.ContainsKey(cmd.TargetTick))
            predictedCommands[cmd.TargetTick] = new List<ICommand>();

        predictedCommands[cmd.TargetTick].Add(cmd);

        // 立即预测执行
        ExecuteCommand(cmd);
    }

    // 供 RollbackSystem 重放使用
    public void ExecuteCommand(ICommand cmd)
    {
        FrameSyncCoreSystem.Instance.ExecuteSingleCommand(cmd);
    }

    public bool GetPredictedCommands(uint tick, out List<ICommand> cmds)
    {
        return predictedCommands.TryGetValue(tick, out cmds);
    }

    public void ClearHistoryBeforeTick(uint tick)
    {
        // 清理过旧的预测记录
        var keys = new List<uint>(predictedCommands.Keys);
        foreach (var k in keys)
        {
            if (k < tick) predictedCommands.Remove(k);
        }
    }
}

