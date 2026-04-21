using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.UOS.Matchmaking;
using Unity.UOS.Matchmaking.Model;
using Unity.UOS.Matchmaking.Server;
using Unity.UOS.Multiverse;
using Unity.UOS.Matchmaking.Internal.Utility;
using Sirenix.OdinInspector;

public sealed class GameManager : MonoSingleton<GameManager>
{
    public GlobalDatabase GlobalDatabase;
    public GameObject UIManagerGO;

    public enum LaunchMode
    {
        Client,
        DedicatedServer
    }

    public enum ClientFlowState
    {
        None,
        Initializing,
        LobbyIdle,
        Matchmaking,
        Connecting,
        HeroSelecting,
        WaitingGameStart,
        LoadingGameScene,
        InGame,
        Error
    }

    [SerializeField, LabelText("启动端")]
    private LaunchMode launchMode = LaunchMode.Client;

    [SerializeField, LabelText("大厅场景名字")]
    private string lobbySceneName = "Lobby";

    [SerializeField, LabelText("正式游戏场景名字")]
    private string gameSceneName = "GameScene";

    [SerializeField, LabelText("英雄选择面板名")]
    private string selectPanelName = "SelectPanel";

    [SerializeField, LabelText("大厅面板名")]
    private string lobbyPanelName = "LobbyPanel";

    [SerializeField, LabelText("加载面板名")]
    private string loadingPanelName = "LoadingPanel";

    [SerializeField, LabelText("游戏 HUD 面板名")]
    private string gameplayHUDPanelName = "GameplayHUD";

    [SerializeField, LabelText("匹配轮询间隔(秒)")]
    private int matchmakingPollIntervalMs = 2000;

    private string matchConfigId;
    private string currentRoomId;
    private string currentTicketId;
    private bool cancelMatchRequested;
    private bool localClientConnected;

    public ClientFlowState CurrentClientState { get; private set; } = ClientFlowState.None;
    public string LastError { get; private set; }

    public bool IsClientBuild => launchMode == LaunchMode.Client;
    public bool IsDedicatedServerBuild => launchMode == LaunchMode.DedicatedServer;
    public string CurrentRoomId => currentRoomId;

    protected override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        SceneManager.sceneLoaded += OnUnitySceneLoaded;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        if (launchMode == LaunchMode.Client)
            await StartClientBootstrapAsync();
        else
            await StartDedicatedServerBootstrapAsync();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        SceneManager.sceneLoaded -= OnUnitySceneLoaded;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    #region Bootstrap

    private async Task StartClientBootstrapAsync()
    {
        SetClientState(ClientFlowState.Initializing);
        SetLoadingVisible(true);

        try
        {
            LuaManager.Instance.Init();
            MatchmakingSDK.Initialize();
            matchConfigId = await QuickStartConfig.Create();

            Debug.Log($"[GameManager] Match Config ID = {matchConfigId}");
        }
        catch (Exception e)
        {
            SetError($"客户端初始化失败: {e.Message}");
            return;
        }

        SceneManager.LoadScene(lobbySceneName);
    }

    private async Task StartDedicatedServerBootstrapAsync()
    {
        try
        {
            Debug.Log("[GameManager] DedicatedServer 启动中...");

            await MultiverseSDK.Initialize();
            await MatchmakingServerSDK.Initialize();

            var match = await MatchmakingServerSDK.Instance.GetMatchInfo();
            currentRoomId = match.RoomId;

            Debug.Log($"[GameManager] 房间 {currentRoomId} 已分配，队伍数: {match.Teams.Count}");

            if (!NetworkManager.Singleton.IsServer)
                NetworkManager.Singleton.StartServer();

            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);

            await MultiverseSDK.Instance.ReadyAsync();
            Debug.Log("[GameManager] DedicatedServer 已就绪，正在大厅等待玩家");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] 服务器启动失败: {e.Message}");
        }
    }

    #endregion

    #region Scene / Network

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsClientBuild)
            return;

        if (scene.name == lobbySceneName)
        {
            CloseGameplayHUD();

            if (!localClientConnected)
            {
                SetClientState(ClientFlowState.LobbyIdle);
                SetLoadingVisible(false);
                OpenLobbyPanel();
                CloseSelectPanel();
            }
            else
            {
                SetClientState(ClientFlowState.HeroSelecting);
                SetLoadingVisible(false);
                CloseLobbyPanel();
                OpenSelectPanel();
            }
        }
        else if (scene.name == gameSceneName)
        {
            SetClientState(ClientFlowState.InGame);
            SetLoadingVisible(false);
            CloseLobbyPanel();
            CloseSelectPanel();
            OpenGameplayHUD();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsClientBuild)
            return;

        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        localClientConnected = true;

        if (SceneManager.GetActiveScene().name == lobbySceneName)
        {
            SetClientState(ClientFlowState.HeroSelecting);
            SetLoadingVisible(false);
            CloseLobbyPanel();
            OpenSelectPanel();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsClientBuild)
            return;

        if (NetworkManager.Singleton == null || clientId != NetworkManager.Singleton.LocalClientId)
            return;

        localClientConnected = false;

        if (CurrentClientState != ClientFlowState.InGame)
        {
            SetClientState(ClientFlowState.LobbyIdle);
            SetLoadingVisible(false);
            SceneManager.LoadScene(lobbySceneName);
        }
    }

    #endregion

    #region Client Flow

    public async void StartMatchmakingFromLua()
    {
        await StartMatchmakingAsync();
    }

    public async Task StartMatchmakingAsync()
    {
        if (!IsClientBuild)
            return;

        if (CurrentClientState == ClientFlowState.Matchmaking ||
            CurrentClientState == ClientFlowState.Connecting ||
            CurrentClientState == ClientFlowState.HeroSelecting ||
            CurrentClientState == ClientFlowState.WaitingGameStart)
            return;

        if (string.IsNullOrEmpty(matchConfigId))
        {
            SetError("未设置匹配配置ID");
            return;
        }

        cancelMatchRequested = false;
        SetClientState(ClientFlowState.Matchmaking);
        SetLoadingVisible(true);

        try
        {
            var players = new List<Player>
            {
                new Player { id = SystemInfo.deviceUniqueIdentifier }
            };

            currentTicketId = await MatchmakingSDK.Instance.CreateTicketAsync(
                configId: matchConfigId,
                players: players,
                regionId: null,
                roomId: null
            );

            Ticket ticket = null;

            while (!cancelMatchRequested)
            {
                await Task.Delay(matchmakingPollIntervalMs);
                ticket = await MatchmakingSDK.Instance.GetTicketAsync(currentTicketId);

                if (ticket == null)
                    continue;

                if (ticket.status == MatchmakingSDK.TicketStatusMatched)
                    break;

                if (ticket.status == MatchmakingSDK.TicketStatusError)
                {
                    SetError($"匹配失败: {ticket.assignment?.msg ?? "未知错误"}");
                    return;
                }
            }

            if (cancelMatchRequested)
            {
                await TryCancelTicketBestEffortAsync();
                currentTicketId = null;

                SetClientState(ClientFlowState.LobbyIdle);
                SetLoadingVisible(false);
                return;
            }

            if (ticket == null || ticket.assignment == null)
            {
                SetError("匹配完成但未拿到分配信息");
                return;
            }

            var assignment = ticket.assignment;
            string ip = assignment.ip;
            string portStr = ParsePort(assignment.gamePorts, "http");

            if (!ushort.TryParse(portStr, out ushort port))
            {
                SetError("无法解析分配端口");
                return;
            }

            currentRoomId = assignment.roomId;

            SetClientState(ClientFlowState.Connecting);
            ConnectToServer(ip, port);
        }
        catch (Exception e)
        {
            SetError($"匹配异常: {e.Message}");
        }
    }

    public async void CancelMatchmakingFromLua()
    {
        await CancelMatchmakingAsync();
    }

    public async Task CancelMatchmakingAsync()
    {
        if (!IsClientBuild)
            return;

        cancelMatchRequested = true;

        if (CurrentClientState == ClientFlowState.Matchmaking)
        {
            await TryCancelTicketBestEffortAsync();
            currentTicketId = null;
            SetClientState(ClientFlowState.LobbyIdle);
            SetLoadingVisible(false);
        }
    }

    public void ConfirmHeroSelectionFromLua(int heroPrefabId)
    {
        var localPlayer = GamePlayer.Local;
        if (localPlayer == null)
        {
            SetError("本地玩家不存在，无法确认英雄");
            return;
        }

        localPlayer.SelectHero(heroPrefabId);
        localPlayer.LockHeroSelection();

        SetClientState(ClientFlowState.WaitingGameStart);
        SetLoadingVisible(true);
    }

    private string ParsePort(string gamePorts, string protocol)
    {
        if (string.IsNullOrEmpty(gamePorts))
            return null;

        foreach (var part in gamePorts.Split(','))
        {
            var kv = part.Split('/');
            if (kv.Length == 2 && kv[0] == protocol)
                return kv[1];
        }

        return null;
    }

    private void ConnectToServer(string ip, ushort port)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, port);

        NetworkManager.Singleton.StartClient();
        Debug.Log($"[GameManager] 客户端正在连接至 {ip}:{port}");
    }

    private async Task TryCancelTicketBestEffortAsync()
    {
        if (string.IsNullOrEmpty(currentTicketId))
            return;

        try
        {
            object sdk = MatchmakingSDK.Instance;
            Type sdkType = sdk.GetType();

            string[] candidateMethods =
            {
                "DeleteTicketAsync",
                "CancelTicketAsync",
                "RemoveTicketAsync"
            };

            for (int i = 0; i < candidateMethods.Length; i++)
            {
                MethodInfo mi = sdkType.GetMethod(candidateMethods[i], BindingFlags.Instance | BindingFlags.Public);
                if (mi == null)
                    continue;

                object ret = mi.Invoke(sdk, new object[] { currentTicketId });
                if (ret is Task task)
                    await task;

                Debug.Log($"[GameManager] 已尝试调用 {candidateMethods[i]} 取消票据");
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameManager] 取消票据失败: {e.Message}");
        }
    }

    #endregion

    #region Server Flow / Game Start

    public void StartGameSceneFromLobby()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public async Task ShutdownServerAsync()
    {
        if (!IsDedicatedServerBuild)
            return;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.Shutdown();

        await MultiverseSDK.Instance.ShutdownAsync();
        Debug.Log("[GameManager] 服务器已关闭");
    }

    #endregion

    #region UI Helpers

    private void OpenLobbyPanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OpenPanel(lobbyPanelName);
    }

    private void CloseLobbyPanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(lobbyPanelName);
    }

    private void OpenSelectPanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OpenPanel(selectPanelName);
    }

    private void CloseSelectPanel()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(selectPanelName);
    }

    private void OpenGameplayHUD()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.OpenPanel(gameplayHUDPanelName);
    }

    private void CloseGameplayHUD()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClosePanel(gameplayHUDPanelName);
    }

    public void SetLoadingVisible(bool visible)
    {
        if (!IsClientBuild)
            return;

        Instantiate(UIManagerGO);

        if (visible)
            UIManager.Instance.OpenPanel(loadingPanelName);
        else
            UIManager.Instance.ClosePanel(loadingPanelName);
    }

    #endregion

    #region Public Query For Lua

    public bool IsMatchmaking()
    {
        return CurrentClientState == ClientFlowState.Matchmaking ||
               CurrentClientState == ClientFlowState.Connecting;
    }

    public bool IsWaitingGameStart()
    {
        return CurrentClientState == ClientFlowState.WaitingGameStart ||
               CurrentClientState == ClientFlowState.LoadingGameScene;
    }

    public bool IsLocalHeroSelectionLocked()
    {
        return GamePlayer.Local != null && GamePlayer.Local.IsHeroLocked;
    }

    public int GetLocalSelectedHeroPrefabId()
    {
        if (GamePlayer.Local == null)
            return -1;

        return GamePlayer.Local.SelectedHeroPrefabId;
    }

    public string GetLastError()
    {
        return LastError ?? string.Empty;
    }

    #endregion

    #region State

    private void SetClientState(ClientFlowState state)
    {
        CurrentClientState = state;
        Debug.Log($"[GameManager] ClientFlowState => {state}");
    }

    private void SetError(string message)
    {
        LastError = message;
        SetClientState(ClientFlowState.Error);
        SetLoadingVisible(false);
        Debug.LogError($"[GameManager] {message}");
    }

    #endregion
}