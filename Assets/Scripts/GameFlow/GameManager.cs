using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Unity.UOS.Matchmaking;
using Unity.UOS.Matchmaking.Model;         
using Unity.UOS.Matchmaking.Server;   
using Unity.UOS.Multiverse;         
using Unity.UOS.Matchmaking.Internal.Utility;
using Sirenix.OdinInspector; 

public sealed class GameManager : MonoSingleton<GameManager>
{
    private enum LaunchMode
    {
        Client,
        DedicatedServer
    }

    [SerializeField, LabelText("启动端")] 
    private LaunchMode launchMode = LaunchMode.Client;

    [SerializeField, LabelText("大厅场景名字")] 
    private string lobbySceneName = "Lobby";
    [SerializeField, LabelText("正式游戏场景名字")] 
    private string gameSceneName = "GameScene";

    private string matchConfigId;

    private string currentRoomId;
    private string currentTicketId;

    public bool IsClientBuild => launchMode == LaunchMode.Client;
    public bool IsDedicatedServerBuild => launchMode == LaunchMode.DedicatedServer;

    protected override void Awake()
    {
        base.Awake();   
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (launchMode == LaunchMode.Client)
        {
            try
            {
                MatchmakingSDK.Initialize();
                matchConfigId = await QuickStartConfig.Create();
                Debug.Log($"Match Config ID: {matchConfigId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"客户端初始化失败: {e.Message}");
                return;
            }
            SceneManager.LoadScene(lobbySceneName);
        }
        else
        {
            await StartDedicatedServerAsync();
        }
    }

    #region Client Flow

    /// <summary>
    /// 客户端开始匹配（由UI按钮调用）
    /// </summary>
    public async Task StartMatchmakingAsync()
    {
        if (string.IsNullOrEmpty(matchConfigId))
        {
            Debug.LogError("未设置匹配配置ID");
            return;
        }

        try
        {
            // 1️⃣ 创建玩家列表（至少包含当前玩家）
            var players = new List<Player>
            {
                new Player { id = SystemInfo.deviceUniqueIdentifier } // 使用设备唯一标识作为玩家ID
            };

            // 2️⃣ 创建匹配票据，需要传入 configId 和 players
            currentTicketId = await MatchmakingSDK.Instance.CreateTicketAsync(
                configId: matchConfigId,
                players: players,
                regionId: null,   // 不指定地域
                roomId: null      // 不加入特定房间
            );

            // 3️⃣ 轮询直到匹配成功或失败
            Ticket ticket = null;
            while (true)
            {
                await Task.Delay(2000); // 每2秒轮询一次

                ticket = await MatchmakingSDK.Instance.GetTicketAsync(currentTicketId);

                // 使用 SDK 提供的常量比较状态
                if (ticket.status == MatchmakingSDK.TicketStatusMatched)
                {
                    break;
                }
                if (ticket.status == MatchmakingSDK.TicketStatusError)
                {
                    Debug.LogError($"匹配失败: {ticket.assignment?.msg ?? "未知错误"}");
                    return;
                }
                // 其他状态（created, awaitingAssignment）继续等待
            }

            // 4️⃣ 获取分配信息
            var assignment = ticket.assignment;
            string ip = assignment.ip;
            // 从 gamePorts 中解析端口（假设使用 "http" 协议，可根据实际调整）
            string portStr = ParsePort(assignment.gamePorts, "http");
            if (!ushort.TryParse(portStr, out ushort port))
            {
                Debug.LogError("无法解析端口");
                return;
            }
            currentRoomId = assignment.roomId;

            // 5️⃣ 连接至服务器（客户端模式）
            ConnectToServer(ip, port);
        }
        catch (Exception e)
        {
            Debug.LogError($"匹配异常: {e.Message}");
        }
    }

    /// <summary>
    /// 从 gamePorts 字符串中解析指定协议的端口
    /// </summary>
    private string ParsePort(string gamePorts, string protocol)
    {
        // 格式示例: "http/7654,grpc/7865"
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
        Debug.Log($"客户端正在连接至 {ip}:{port}");
    }

    #endregion

    #region Dedicated Server Flow

    private async Task StartDedicatedServerAsync()
    {
        Debug.Log("专用服务器启动中...");
        
        try
        {
            // 初始化 Multiverse SDK
            await MultiverseSDK.Initialize();

            // 初始化 Matchmaking Server SDK
            await MatchmakingServerSDK.Initialize();

            // 获取房间的匹配信息
            // 注意：对于按需模式（OnDemand）可直接调用 GetMatchInfo；
            // 若为舰队模式，需先使用 WatchMatchInfo 等待分配。
            // 这里简单处理，假设直接获取成功。
            var match = await MatchmakingServerSDK.Instance.GetMatchInfo();
            currentRoomId = match.RoomId;

            Debug.Log($"房间 {currentRoomId} 包含 {match.Teams.Count} 个队伍");

            // 加载游戏场景（NetworkManager 会自动同步给连接上来的客户端）
            SceneManager.LoadScene(gameSceneName);

            // 以服务器模式启动 NetworkManager
            NetworkManager.Singleton.StartServer();

            // 通知 Multiverse 服务器已就绪，可以接受连接
            await MultiverseSDK.Instance.ReadyAsync();

            Debug.Log("专用服务器已就绪");
        }
        catch (Exception e)
        {
            Debug.LogError($"服务器启动失败: {e.Message}");
            // 可根据需要调用 Shutdown 或退出进程
        }
    }

    #endregion

    #region Game Control

    /// <summary>
    /// 服务器端主动加载游戏场景（通常用于测试）
    /// </summary>
    public void StartGameScene()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// 关闭服务器（游戏结束时调用）
    /// </summary>
    public async Task ShutdownServerAsync()
    {
        if (launchMode != LaunchMode.DedicatedServer)
            return;

        NetworkManager.Singleton.Shutdown();
        await MultiverseSDK.Instance.ShutdownAsync();
        Debug.Log("服务器已关闭");
    }

    #endregion
}