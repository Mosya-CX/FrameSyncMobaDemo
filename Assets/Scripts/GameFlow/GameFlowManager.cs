using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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

    [SerializeField] private GameObject[] managedObjects;
    private IGameFlowManaged[] manageds;

    [SerializeField] private ushort tickPerSecond = 30;
    private float tickInterval;

    private NetworkVariable<GameFlowState> currentState =
        new(GameFlowState.None,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private NetworkVariable<ulong> currentTick =
        new(0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> isSpawnHero = 
        new(false, 
            NetworkVariableReadPermission.Everyone, 
            NetworkVariableWritePermission.Owner);

    private HashSet<ulong> readyClients = new();

    private float serverTickTimer;
    private ulong localExecutedTick;
    private bool isRunning;

    public float ServerTickTimer => serverTickTimer;
    public ulong LocalExecutedTick => localExecutedTick;
    public bool IsRunning => isRunning;

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

    #region Init 

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

    #endregion

    #region Begin

    private IEnumerator BeginPhaseRoutine()
    {
        currentState.Value = GameFlowState.PreGame;

        foreach (var m in manageds)
            yield return m.Begin();

        yield return null;

        currentTick.Value = 0;
        localExecutedTick = 0;

        currentState.Value = GameFlowState.Running;
        isRunning = true;

        if (IsClient)
        {
            // 关闭加载界面
        }
    }

    #endregion

    #region Tick

    private void Update()
    {
        if (!isRunning) return;

        if (IsServer)
            ServerTick();

        ClientTickSync();
    }

    private void ServerTick()
    {
        if (currentState.Value != GameFlowState.Running)
            return;

        serverTickTimer += Time.deltaTime;

        while (serverTickTimer >= tickInterval)
        {
            serverTickTimer -= tickInterval;
            currentTick.Value++;
            ExecuteTick(currentTick.Value);
        }
    }

    private void ClientTickSync()
    {
        if (currentState.Value != GameFlowState.Running)
            return;

        while (localExecutedTick < currentTick.Value)
        {
            localExecutedTick++;
            ExecuteTick(localExecutedTick);
        }
    }

    private void ExecuteTick(ulong tick)
    {
        foreach (var m in manageds)
            m.Tick(tick);
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
    int GetStateHash();
}