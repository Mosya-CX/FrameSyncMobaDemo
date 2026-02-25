using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Mathematics.FixedPoint;
using Sirenix.OdinInspector;

public sealed class GameFlowManager : NetworkSingleton<GameFlowManager>
{
    public enum GameFlowState
    {
        None,
        Initializing,
        WaitingForClientsReady,
        PreGame,
        Running,
        GameOver
    }

    [SerializeField, LabelText("受控管理器")] 
    private GameObject[] managedObjects;
    private IGameFlowManaged[] manageds;

    [SerializeField, LabelText("每秒逻辑帧")] 
    private ushort tickPerSecond = 30;

    [ReadOnly]
    public NetworkVariable<GameFlowState> currentState =new(GameFlowState.None,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [ReadOnly]
    public NetworkVariable<bool> isSpawnHero = new(false, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private HashSet<ulong> readyClients = new();

    private uint localTick;
    private float localTickTimer;
    private bool isRunning;
    private float serverTickTimer;
    private float tickInterval;

    [ReadOnly]// 服务器权威帧号
    public NetworkVariable<uint> authoritativeTick = new(0, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public uint CurrentLocalTick => localTick;
    public float ServerTickTimer => serverTickTimer;
    public bool IsRunning => isRunning;
    public float TickInterval => tickInterval;
    public fp TickIntervalFP => (fp)tickInterval;

    protected override void Awake()
    {
        base.Awake();
        tickInterval = 1f / tickPerSecond;
    }

    private void Start()
    {
        List<IGameFlowManaged> list = new();
        foreach (var go in managedObjects)
        {
            if (go.TryGetComponent(out IGameFlowManaged m))
                list.Add(m);
        }
        manageds = list.ToArray();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        StartCoroutine(LocalInitRoutine());
    }

    private IEnumerator LocalInitRoutine()
    {
        if (IsServer)
            currentState.Value = GameFlowState.Initializing;

        foreach (var m in manageds)
            yield return m.Init();

        yield return null; // 等待一帧确保初始化完成

        if (IsServer)
        {
            readyClients.Add(NetworkManager.Singleton.LocalClientId);
            currentState.Value = GameFlowState.WaitingForClientsReady;
            CheckAllClientsReady();
        }
        else
        {
            NotifyInitCompleteServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyInitCompleteServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        readyClients.Add(clientId);
        CheckAllClientsReady();
    }

    private void CheckAllClientsReady()
    {
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!readyClients.Contains(clientId))
                return;
        }

        StartCoroutine(BeginPhaseRoutine());
    }

    private IEnumerator BeginPhaseRoutine()
    {
        currentState.Value = GameFlowState.PreGame;

        foreach (var m in manageds)
            yield return m.Begin();

        yield return null;

        currentState.Value = GameFlowState.Running;
        isRunning = true;

        if (IsClient)
        {
            // 关闭加载界面
        }
    }

    #region Tick

    private void Update()
    {
        if (!isRunning) return;

        if (IsServer)
        {
            ServerTick();
        }

        if (IsClient)
        {
            ClientTick();
        }
    }

    private void ServerTick()
    {
        serverTickTimer += Time.deltaTime;
        while (serverTickTimer >= tickInterval)
        {
            serverTickTimer -= tickInterval;
            authoritativeTick.Value++;
            ExecuteTick(authoritativeTick.Value); // 服务器执行该帧
        }
    }

    private void ClientTick()
    {
        // 优化时钟同步算法
        int tickDelta = (int)authoritativeTick.Value - (int)localTick;

        // 如果落后超过阈值，瞬移逻辑帧追赶
        if (tickDelta > 10)
        {
            Debug.Log($"Client lagging behind. Jumping from {localTick} to {authoritativeTick.Value}");
            while (localTick < authoritativeTick.Value)
            {
                localTick++;
                ExecuteTick(localTick);
            }
            localTickTimer = 0;
        }
        else
        {
            // 正常推进
            localTickTimer += Time.deltaTime;
            if (localTickTimer >= tickInterval)
            {
                localTickTimer -= tickInterval;
                localTick++;
                ExecuteTick(localTick);
            }
        }
    }

    private void ExecuteTick(uint tick)
    {
        // 处理网络指令分发
        FrameSyncCoreSystem.Instance?.Tick(tick);

        // 驱动所有单位逻辑
        foreach (var m in manageds)
            m.Tick(tick);

        // 客户端保存快照
        if (IsClient)
        {
            RollbackSystem.Instance?.TakeSnapshot(tick);

            // 收集并发送本地输入
            LocalController.Local?.GenerateCommandsForTick(tick);

            // 发送指令
            var cmds = LocalController.Local?.FlushOutgoingCommands();
            if (cmds != null && cmds.Count > 0)
            {
                // 通过 FrameSyncCoreSystem 发送 (原逻辑在 LocalController 里，建议移到这里统一管理)
                // FrameSyncCoreSystem.Instance.SendCommands(cmds); 
                // 注：需在 FrameSyncCoreSystem 增加公开的发送方法
            }
        }
    }

    #endregion

    #region GameOver

    public void GameOver(int winner)
    {
        if (!IsServer) return;

        currentState.Value = GameFlowState.GameOver;
        isRunning = false;

        CleanClientRpc();
    }

    [ClientRpc]
    private void CleanClientRpc()
    {
        foreach (var m in manageds)
            m.Clean();
    }

    #endregion
}

public interface IGameFlowManaged
{
    IEnumerator Init();
    IEnumerator Begin();
    void Tick(ulong currentTick);
    IEnumerator Clean();
}