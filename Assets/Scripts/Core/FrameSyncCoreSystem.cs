using Sirenix.OdinInspector;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class FrameSyncCoreSystem : NetworkSingleton<FrameSyncCoreSystem>
{
    [SerializeField, LabelText("最小目标Tick偏移")]
    private int minTargetTickOffset = 2;
    [SerializeField, LabelText("最大目标Tick偏移")]
    private int maxTargetTickOffset = 6;
    [SerializeField, LabelText("额外安全毫秒")]
    private float targetTickSafetyMs = 40f;
    [SerializeField, LabelText("RTT平滑系数")]
    private float rttSmoothFactor = 0.15f;
    [ShowInInspector, ReadOnly, LabelText("当前平均RTT(ms)")]
    private float avgRttMs = 120f;
    [ShowInInspector, ReadOnly, LabelText("当前目标Tick偏移")]
    private int debugCurrentTargetTickOffset = 3;

    // 待调度指令队列
    private PriorityQueue<CommandBase> pendingSchedulingCommands = new(Comparer<CommandBase>.Create((a, b)=>b.TargetTick.CompareTo(a.TargetTick)));

    // 权威帧数据表
    private Dictionary<uint, FrameData> authoritativeCommands = new();
    public IReadOnlyDictionary<uint, FrameData> AuthoritativeCommands => authoritativeCommands;

    // 指令接收者字典
    public readonly Dictionary<UnitUID, ICommandReceiver> commandReceivers = new();
    // 指令缓存
    public readonly List<CommandBase> commandCache = new();

    #region 快捷访问
    private uint LocalTick => GameFlowManager.Instance.CurrentLocalTick;
    private uint ServerTick => GameFlowManager.Instance.AuthoritativeTick.Value;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        pendingSchedulingCommands.Clear();
        authoritativeCommands.Clear();
    }

    public override void OnDestroy()
    {
        pendingSchedulingCommands.Clear();
        authoritativeCommands.Clear();
        base.OnDestroy();
    }

    public void Tick(uint tick)
    {
        if (IsServer)
            TickServer(tick);
        if (IsClient)
            TickClient(tick);
    }

    private void TickServer(uint tick)
    {
        commandCache.Clear();

        while (pendingSchedulingCommands.Count > 0 && pendingSchedulingCommands.Peek().TargetTick <= tick)
        {
            var excuteCommand = pendingSchedulingCommands.Dequeue();
            excuteCommand.TargetTick = tick;
            commandCache.Add(excuteCommand);
        }

        var currentTickFrameData = new FrameData(tick, commandCache);
        authoritativeCommands.Add(tick, currentTickFrameData);
        BroadcastFrameDataClientRpc(currentTickFrameData);

        for (int i = 0; i < commandCache.Count; i++)
            ExecuteCommand(commandCache[i]);

        commandCache.Clear();
    }

    private void TickClient(uint localTick)
    {
        commandCache.Clear();
        if (pendingSchedulingCommands.Count > 0)
        {
            while (pendingSchedulingCommands.Count > 0)
                commandCache.Add(pendingSchedulingCommands.Dequeue());

            byte seq = 0;
            for (int i = 0;i < commandCache.Count;i++)
            {
                var commands = PredictionSystem.Instance.GetPredictedCommandList(commandCache[i].TargetTick);
                commandCache[i].CommandId = new CommandId(NetworkManager.LocalClientId, localTick, seq);
                commands.Add(commandCache[i]);
                seq++;
            }

            var frameData = new FrameData(localTick, commandCache);
            SubmitFrameDataServerRpc(frameData);

            commandCache.Clear();
        }

        // 检查是否需要回滚重建
        RollbackSystem.Instance.CheckRollback(localTick);

        // 存储当前Tick快照
        RollbackSystem.Instance.TakeSnapshot(localTick);

        // 执行当前帧的预测指令
        PredictionSystem.Instance.ExcutePredicte(localTick);
    }

    [ClientRpc(Delivery = RpcDelivery.Reliable)]
    private void BroadcastFrameDataClientRpc(FrameData data)
    {
        if (!authoritativeCommands.TryAdd(data.ExcuteTick, data))
            return;

        if (LocalTick < data.ExcuteTick)
            return;

        if (PredictionSystem.Instance.CheckPredicteSuccess(data.ExcuteTick))
        {
            RollbackSystem.Instance.EraseTickSnapshot(data.ExcuteTick);
            return;
        }

        RollbackSystem.Instance.CreateNewRollbackRequest(data.ExcuteTick);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitFrameDataServerRpc(FrameData data)
    {
        for (int i = 0; i < data.Commands.Count; i++)
            pendingSchedulingCommands.Enqueue(data.Commands[i]);
    }

    // 客户端本地添加待处理命令队列
    public void AddPendingCommand(CommandBase command)
    {
        command.TargetTick = LocalTick + (uint)GetTargetTickOffset();
        pendingSchedulingCommands.Enqueue(command);
    }

    public int GetTargetTickOffset()
    {
        float tickIntervalMs = GameFlowManager.Instance.TickInterval * 1000f;
        if (tickIntervalMs <= 0.001f)
            return minTargetTickOffset;

        // 单向延迟近似 = RTT / 2
        float oneWayDelayMs = avgRttMs * 0.5f;

        // 加安全边际，避免边缘帧刚好赶不上
        float requiredMs = oneWayDelayMs + targetTickSafetyMs;

        int offset = Mathf.CeilToInt(requiredMs / tickIntervalMs);
        offset = Mathf.Clamp(offset, minTargetTickOffset, maxTargetTickOffset);

        debugCurrentTargetTickOffset = offset;
        return offset;
    }

    public void ReportRttSample(float rttMs)
    {
        if (rttMs <= 0)
            return;

        if (avgRttMs <= 0)
        {
            avgRttMs = rttMs;
            return;
        }

        avgRttMs = Mathf.Lerp(avgRttMs, rttMs, rttSmoothFactor);
    }

    public void ExecuteCommand(CommandBase cmd)
    {
        if (commandReceivers.TryGetValue(cmd.ReceiverUnitId, out var receiver))
            receiver.ReceiveCommand(cmd);
    }

    #region 注册指令同步对象

    public void RegisterReceiver(ICommandReceiver receiver)
    {
        if (receiver == null) return;
        commandReceivers[receiver.ReceiverID] = receiver;
    }

    public void UnregisterReceiver(UnitUID unitId)
    {
        commandReceivers.Remove(unitId);
    }

    public bool TryGetReceiver(UnitUID uid, out ICommandReceiver receiver)
    {
        return commandReceivers.TryGetValue(uid, out receiver);
    }

    #endregion
}

public interface ICommandReceiver
{
    void ReceiveCommand(CommandBase command);
    UnitUID ReceiverID { get; }
}

public struct FrameData : INetworkSerializable
{
    private uint excuteTick;
    private List<CommandBase> commands;

    public uint ExcuteTick => excuteTick;
    public IReadOnlyList<CommandBase> Commands => commands;

    public FrameData(in uint excuteTick, in List<CommandBase> commands = null)
    {
        this.excuteTick = excuteTick;
        this.commands = commands != null ? new(commands) : null;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref excuteTick);

        if (serializer.IsReader)
        {
            var reader = serializer.GetFastBufferReader();
            commands = CommandSerializer.Deserialize(reader);
        }
        else
        {
            var writer = serializer.GetFastBufferWriter();
            CommandSerializer.Serialize(writer, commands);
        }
    }
}