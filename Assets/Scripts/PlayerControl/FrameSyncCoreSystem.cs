using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using System.Collections;

public sealed class FrameSyncCoreSystem : NetworkSingleton<FrameSyncCoreSystem>, IGameFlowManaged
{
    // 服务器：待调度指令队列
    private Dictionary<uint, List<ICommand>> pendingCommands = new(); // key = target tick

    // 客户端：已接收的权威指令
    private Dictionary<uint, List<ICommand>> authoritativeCommands = new();

    // 本地指令历史
    private Dictionary<uint, List<ICommand>> localPredictedCommands = new();

    [SerializeField] private uint stateHashInterval = 30;

    private uint currentServerTick; // 服务器当前帧
    private uint currentLocalTick;   // 客户端本地帧（由GameFlowManager驱动）

    public uint LocalTick => currentLocalTick;

    public IEnumerator Init()
    {
        pendingCommands.Clear();
        authoritativeCommands.Clear();
        localPredictedCommands.Clear();
        currentServerTick = 0;
        currentLocalTick = 0;
        yield break;
    }

    public IEnumerator Begin() { yield break; }

    public IEnumerator Clean()
    {
        pendingCommands.Clear();
        authoritativeCommands.Clear();
        localPredictedCommands.Clear();
        yield break;
    }

    // 由GameFlowManager每逻辑帧调用
    public void Tick(ulong tick)
    {
        if (IsServer)
        {
            ServerTick((uint)tick);
        }
        else if (IsClient)
        {
            ClientTick((uint)tick);
        }

        // 执行已就绪的指令
        ExecuteTickCommands((uint)tick);
    }

    #region Server

    private void ServerTick(uint tick)
    {
        currentServerTick = tick;

        if (pendingCommands.TryGetValue(tick, out var commands))
        {
            BroadcastCommandsForTick(tick, commands);
            pendingCommands.Remove(tick);
        }
    }

    private void BroadcastCommandsForTick(uint tick, List<ICommand> commands)
    {
        // 可以在这里计算状态 Hash 并下发，防止客户端作弊

        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        CommandSerializer.Serialize(writer, commands);
        BroadcastCommandsClientRpc(writer.ToArray(), tick);
    }

    [ClientRpc]
    private void BroadcastCommandsClientRpc(byte[] commandData, uint executeTick)
    {
        var authoritativeCmds = CommandSerializer.Deserialize(commandData);

        // 核心逻辑：对比与回滚
        if (PredictionSystem.Instance.GetPredictedCommands(executeTick, out var predCmds))
        {
            if (!AreCommandsEqual(authoritativeCmds, predCmds))
            {
                // 预测失败，触发回滚
                RollbackSystem.Instance.PerformRollback(executeTick, authoritativeCmds);
            }
            // 预测正确：无需额外操作，状态已经是对的
        }
        else
        {
            // 没有预测记录，直接执行
            foreach (var cmd in authoritativeCmds)
                ExecuteSingleCommand(cmd);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCommandsServerRpc(byte[] commandData, ServerRpcParams rpcParams = default)
    {
        if (commandData == null || commandData.Length == 0)
            return;

        var commands = CommandSerializer.Deserialize(commandData);
        ulong clientId = rpcParams.Receive.SenderClientId;

        foreach (var cmd in commands)
        {
            // 检查目标帧
            if (cmd.TargetTick <= currentServerTick)
            {
                // 来晚了，强制改到下一帧
                cmd.TargetTick = currentServerTick + 1;
                Debug.LogWarning($"Client {clientId} command arrived late, rescheduled to tick {cmd.TargetTick}");
            }

            if (!pendingCommands.ContainsKey(cmd.TargetTick))
                pendingCommands[cmd.TargetTick] = new List<ICommand>();
            pendingCommands[cmd.TargetTick].Add(cmd);
        }
    }

    #endregion

    #region Client

    private void ClientTick(uint localTick)
    {
        currentLocalTick = localTick;

        // 收集本地预测指令
        if (LocalController.Local != null)
        {
            var outgoing = LocalController.Local.FlushOutgoingCommands();
            if (outgoing.Count > 0)
            {
                // 保存到本地历史
                localPredictedCommands[localTick] = outgoing;

                // 发送到服务器
                using var writer = new FastBufferWriter(2048, Allocator.Temp);
                CommandSerializer.Serialize(writer, outgoing);
                SubmitCommandsServerRpc(writer.ToArray());
            }
        }

        // 2. 检查是否需要回滚
        CheckAndRollback(localTick);
    }

    private void CheckAndRollback(uint localTick)
    {
        // 每帧检查当前帧的权威指令是否已到达，并与本地预测对比
        if (authoritativeCommands.TryGetValue(localTick, out var authCmds))
        {
            // 获取本地预测指令
            localPredictedCommands.TryGetValue(localTick, out var predCmds);

            // 比较两个列表是否一致（顺序、内容）
            if (!AreCommandsEqual(authCmds, predCmds))
            {
                // 触发回滚
                RollbackSystem.Instance.RollbackToTick(localTick - 1, authCmds, predCmds);
            }

            // 清除已处理的权威指令
            authoritativeCommands.Remove(localTick);
            localPredictedCommands.Remove(localTick);
        }
    }

    private bool AreCommandsEqual(List<ICommand> a, List<ICommand> b)
    {
        // 简单实现：比较长度和序列化后数据（需要序列化比较）
        // 实际应比较每个指令的关键字段
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i])) return false; // 需要重写ICommand的Equals
        }
        return true;
    }

    #endregion

    private void ExecuteTickCommands(uint tick)
    {
        if (authoritativeCommands.TryGetValue(tick, out var cmds))
        {
            foreach (var cmd in cmds)
            {
                ExecuteSingleCommand(cmd);
            }
        }
    }

    public void ExecuteSingleCommand(ICommand cmd)
    {
        if (globalHandlers.Exists(h => h.CanHandle(cmd.Type)))
        {
            globalHandlers.Find(h => h.CanHandle(cmd.Type)).HandleCommand(cmd);
        }
        else if (commandReceivers.TryGetValue(cmd.ControlledUnitId, out var receiver))
        {
            receiver.ReceiveCommand(cmd);
        }
    }

    #region 注册指令同步对象

    public readonly Dictionary<UnitUID, ICommandReceiver> commandReceivers = new();

    public void RegisterReceiver(ICommandReceiver receiver)
    {
        if (receiver == null) return;
        commandReceivers[receiver.ReceiverID] = receiver;
    }

    public void UnregisterReceiver(UnitUID unitId)
    {
        commandReceivers.Remove(unitId);
    }

    private List<IGlobalCommandHandler> globalHandlers = new();

    public void RegisterGlobalHandler(IGlobalCommandHandler handler)
    {
        if (!globalHandlers.Contains(handler))
            globalHandlers.Add(handler);
    }

    public void UnregisterGlobalHandler(IGlobalCommandHandler handler)
    {
        globalHandlers.Remove(handler);
    }

    public bool TryGetReceiver(UnitUID uid, out ICommandReceiver receiver)
    {
        return commandReceivers.TryGetValue(uid, out receiver);
    }

    #endregion
}

public interface IGlobalCommandHandler// 管理器用
{
    bool CanHandle(CommandType type);
    void HandleCommand(ICommand command);
}

public interface ICommandReceiver// 单位用
{
    void ReceiveCommand(ICommand command);
    UnitUID ReceiverID { get; }
}