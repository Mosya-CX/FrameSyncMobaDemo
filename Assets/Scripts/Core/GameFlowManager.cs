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

    [SerializeField, LabelText("每秒逻辑帧")] 
    private ushort tickPerSecond = 30;

    [SerializeField, LabelText("正式开始延迟时长")]
    private float startDelay = 3;

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
    public NetworkVariable<uint> AuthoritativeTick = new(0, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public uint CurrentLocalTick => localTick;
    public float ServerTickTimer => serverTickTimer;
    public bool IsRunning => isRunning;
    public float TickInterval => tickInterval;
    public fp TickIntervalFP => (fp)tickInterval;

    #region 管理器列表
    public UnitManager UnitManager => UnitManager.Instance;
    public DamageManager DamageManager => DamageManager.Instance;
    public RVOGenerator RVOGenerator => RVOGenerator.Instance;
    public MissleManager MissleManager => MissleManager.Instance;
    public FrameSyncCoreSystem FrameSyncCoreSystem => FrameSyncCoreSystem.Instance;
    public DeterministicRandom DeterministicRandom => DeterministicRandom.Instance;
    public RollbackSystem RollbackSystem => RollbackSystem.Instance;
    public PredictionSystem PredictionSystem => PredictionSystem.Instance;
    public EntitiesSimulation EntitiesSimulation => EntitiesSimulation.Instance;
    #endregion

    protected override void Awake()
    {
        base.Awake();
        tickInterval = 1f / tickPerSecond;
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

        yield return RVOGenerator.Init();
        yield return UnitManager.Init();
        yield return MissleManager.Init();
        yield return EntitiesSimulation.Init();
        yield return DamageManager.Init();

        yield return null; // 等待一帧确保初始化完成

        if (IsServer)
        {
            readyClients.Add(NetworkManager.Singleton.LocalClientId);
            currentState.Value = GameFlowState.WaitingForClientsReady;
            CheckAllClientsReady();
        }
        else
            NotifyInitCompleteServerRpc();
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
            if (!readyClients.Contains(clientId))
                return;

        StartCoroutine(BeginPhaseRoutine(0));
        BoardcastGameStartClientRpc((float)NetworkManager.ServerTime.Time);
    }

    [ClientRpc]
    private void BoardcastGameStartClientRpc(float serverSendTime)
    {
        var recevieDelay = ((float)NetworkManager.LocalTime.Time) - serverSendTime;
        StartCoroutine(BeginPhaseRoutine(recevieDelay));
    }

    private IEnumerator BeginPhaseRoutine(float beginDelay)
    {
        currentState.Value = GameFlowState.PreGame;

        var delay = startDelay - beginDelay;
        yield return new WaitForSecondsRealtime(delay);

        RVOGenerator.Begin();
        UnitManager.Begin();
        MissleManager.Begin();
        EntitiesSimulation.Begin();
        DamageManager.Begin();

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
            TickServer();
        }

        if (IsClient)
        {
            TickClient();
        }
    }

    private void TickServer()
    {
        serverTickTimer += Time.deltaTime;
        while (serverTickTimer >= tickInterval)
        {
            serverTickTimer -= tickInterval;
            AuthoritativeTick.Value++;
            ExecuteTick(AuthoritativeTick.Value); // 服务器执行该帧
        }
    }

    private void TickClient()
    {
        // 优化时钟同步算法
        int tickDelta = (int)AuthoritativeTick.Value - (int)localTick;

        // 如果落后超过阈值，瞬移逻辑帧追赶
        if (tickDelta > 10)
        {
            Debug.Log($"Client lagging behind. Jumping from {localTick} to {AuthoritativeTick.Value}");
            while (localTick < AuthoritativeTick.Value)
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
        FrameSyncCoreSystem.Tick(tick);
        GameTick(tick);
    }

    public void GameTick(uint tick)
    {
        UnitManager.UpdateLocalTick(tick);
        MissleManager.UpdateLocalTick(tick);

        UnitManager.TickSpawnUnit();
        MissleManager.TickSpawnMissle();

        RVOGenerator.Tick(tick);
        UnitManager.TickUpdateUnitTransform();
        MissleManager.TickUpdateMissTransform();

        EntitiesSimulation.Tick(tick);
        UnitManager.TickUpdateUnitState();
        MissleManager.TickUpdateMissleState();

        DamageManager.Tick(tick);
        UnitManager.TickDeathDecision();
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
        RVOGenerator.Clean();
        UnitManager.Clean();
        MissleManager.Clean();
        EntitiesSimulation.Clean();
        DamageManager.Clean();
    }

    #endregion
}