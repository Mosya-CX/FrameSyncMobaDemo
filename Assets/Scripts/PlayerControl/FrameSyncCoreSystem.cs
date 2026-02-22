using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using System.Linq;
using Unity.Collections;
using System.Collections;

public interface ICommandReceiver
{
    void ReceiveCommand(ICommand command);
    UnitUID ReceiverID { get; }
}

public sealed class FrameSyncCoreSystem : NetworkSingleton<FrameSyncCoreSystem>, IGameFlowManaged
{
    private readonly Queue<ICommand> localCommandQueue = new();
    private readonly Dictionary<ulong, List<ICommand>> pendingCommands = new();
    private readonly List<ICommand> serverCurrentTickCommands = new();
    private readonly Dictionary<UnitUID, ICommandReceiver> commandReceivers = new();

    [SerializeField] private bool enableCommandFilter = true;
    [SerializeField] private ulong stateHashInterval = 30;

    private ulong currentServerTick;

    #region Register

    public void RegisterReceiver(ICommandReceiver receiver)
    {
        if (receiver == null) return;
        commandReceivers[receiver.ReceiverID] = receiver;
    }

    public void UnregisterReceiver(UnitUID unitId)
    {
        commandReceivers.Remove(unitId);
    }

    #endregion

    #region Local Command

    public void AddCommand(ICommand command)
    {
        localCommandQueue.Enqueue(command);
    }

    #endregion

    #region Lifecycle

    public IEnumerator Init()
    {
        pendingCommands.Clear();
        localCommandQueue.Clear();
        serverCurrentTickCommands.Clear();
        commandReceivers.Clear();
        currentServerTick = 0;
        yield break;
    }

    public IEnumerator Begin() { yield break; }

    public IEnumerator Clean()
    {
        pendingCommands.Clear();
        localCommandQueue.Clear();
        serverCurrentTickCommands.Clear();
        yield break;
    }

    #endregion

    #region Tick

    public void Tick(ulong tick)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            ServerTick(tick);
        }

        if (NetworkManager.Singleton.IsClient)
        {
            ClientTick();
        }

        ExecuteTickCommands(tick);

        if (tick % stateHashInterval == 0)
        {
            RequestStateHash(tick);
        }
    }

    #endregion

    #region Server

    private void ServerTick(ulong tick)
    {
        if (tick != currentServerTick)
        {
            // 广播上一 Tick 指令
            if (serverCurrentTickCommands.Count > 0)
            {
                BroadcastCommandsForTick(currentServerTick, serverCurrentTickCommands);
                serverCurrentTickCommands.Clear();
            }

            currentServerTick = tick;
        }
    }

    private void BroadcastCommandsForTick(ulong tick, List<ICommand> commands)
    {
        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        CommandSerializer.Serialize(writer, commands);
        BroadcastCommandsClientRpc(writer.ToArray(), tick);
    }

    [ClientRpc]
    private void BroadcastCommandsClientRpc(byte[] commandData, ulong executeTick)
    {
        var commands = CommandSerializer.Deserialize(commandData);

        if (!pendingCommands.TryGetValue(executeTick, out var list))
        {
            list = new List<ICommand>();
            pendingCommands[executeTick] = list;
        }

        list.AddRange(commands);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitCommandsServerRpc(byte[] commandData)
    {
        if (commandData == null || commandData.Length == 0)
            return;

        var commands = CommandSerializer.Deserialize(commandData);
        serverCurrentTickCommands.AddRange(commands);
    }

    #endregion

    #region Client

    private void ClientTick()
    {
        if (localCommandQueue.Count == 0)
            return;

        var commandsToSend = localCommandQueue.ToList();
        localCommandQueue.Clear();

        if (enableCommandFilter)
            commandsToSend = FilterCommands(commandsToSend);

        using var writer = new FastBufferWriter(2048, Allocator.Temp);
        CommandSerializer.Serialize(writer, commandsToSend);
        SubmitCommandsServerRpc(writer.ToArray());
    }

    #endregion

    #region Filter

    private List<ICommand> FilterCommands(List<ICommand> rawCommands)
    {
        var result = new List<ICommand>();
        var groupedByUnit = rawCommands.GroupBy(c => c.ControlledUnitId);

        foreach (var unitGroup in groupedByUnit)
        {
            var typeGroups = unitGroup.GroupBy(c => c.Type);

            foreach (var typeGroup in typeGroups)
            {
                switch (typeGroup.Key)
                {
                    case CommandType.Move:
                        result.Add(typeGroup.Last());
                        break;

                    case CommandType.Attack:
                        result.Add(typeGroup.Last());
                        break;

                    case CommandType.AbilityPress:
                        result.Add(typeGroup.Last());
                        break;

                    case CommandType.AbilityRelease:
                        result.Add(typeGroup.Last());
                        break;

                    case CommandType.AbilityCancel:
                        result.Add(typeGroup.Last());
                        break;

                    default:
                        result.AddRange(typeGroup);
                        break;
                }
            }
        }

        return result;
    }

    #endregion

    #region Execute

    private void ExecuteTickCommands(ulong tick)
    {
        if (!pendingCommands.TryGetValue(tick, out var commands))
            return;

        foreach (var cmd in commands)
        {
            if (commandReceivers.TryGetValue(cmd.ControlledUnitId, out var receiver))
            {
                receiver.ReceiveCommand(cmd);
            }
        }

        pendingCommands.Remove(tick);
    }

    #endregion

    #region StateHash

    private void RequestStateHash(ulong tick)
    {
        // 预留：帧校验
    }

    public int GetStateHash()
    {
        return 0;
    }

    #endregion
}
