using System.Collections.Generic;

public class PredictionSystem : MonoSingleton<PredictionSystem>
{
    // 客户端本地预测指令表
    private Dictionary<uint, List<CommandBase>> localPredictedCommands = new();
    public IReadOnlyDictionary<uint, List<CommandBase>> LocalPredictedCommands => localPredictedCommands;

    protected override void Awake()
    {
        base.Awake();
        localPredictedCommands.Clear();
    }

    protected override void OnDestroy()
    {
        localPredictedCommands.Clear();
        base.OnDestroy();
    }

    public void ExcutePredicte(uint tick)
    {
        if (localPredictedCommands.TryGetValue(tick, out var _commands))
            for (int i = 0; i < _commands.Count; i++)
                FrameSyncCoreSystem.Instance.ExecuteCommand(_commands[i]);
    }

    public bool CheckPredicteSuccess(uint tick)
    {
        var predictedCommands = localPredictedCommands[tick];
        var authoritativeCommands = FrameSyncCoreSystem.Instance.AuthoritativeCommands[tick].Commands;

        if ((authoritativeCommands == null || authoritativeCommands.Count == 0) &&
            (predictedCommands == null || predictedCommands.Count == 0))
            return true;

        if (authoritativeCommands.Count != predictedCommands.Count)
            return false;

        for (int i = 0; i < authoritativeCommands.Count; i++)
            if (authoritativeCommands[i].CommandId != predictedCommands[i].CommandId)
                return false;
        return true;
    }

    public List<CommandBase> GetPredictedCommandList(uint tick)
    {
        if (localPredictedCommands.TryGetValue(tick, out var commands))
        {
            commands = new();
            localPredictedCommands.Add(tick, commands);
        }

        return commands;
    }
}