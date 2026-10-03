# 大厅与 UOS 端到端组合

## 本次执行范围

本计划对应原编码 0124 的一次执行：大厅与 UOS 端到端组合。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [UOS 配置与连接模式](../../requirements/match-flow/REQ-FEAT-004_uos-configuration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [大厅槽位与开局配置](../../requirements/match-flow/REQ-FEAT-002_lobby-slots.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Bootstrap/LocalNgoEndpointDriver.cs`：`LocalNgoEndpointDriver`。
- `Assets/Scripts/Bootstrap/ServerBootstrap.cs`：`ServerBootstrap`。
- `Assets/Scripts/Bootstrap/ClientBootstrap.cs`：`ClientBootstrap`。
- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：`FrameSyncNetworkBridge`、`PresentationPingTracker`、`FrameSyncWireCodec`。
- `Assets/Scripts/Bootstrap/LobbyFlowController.cs`：`LobbyFlowController`。
- `Assets/Scripts/Bootstrap/LobbyNetworkBridge.cs`：`LocalLobbySlotDefinition`、`LobbyNetworkBridge`、`LobbyIdentity`、`LobbyWireCodec`。
- `Assets/Scripts/FrameSync/AuthorityFrame.cs`：`AuthorityFrameFlags`、`AuthorityFrame`、`CanonicalCommandCodec`、`CanonicalReader`。
- `Assets/Scripts/Gameplay/Equipment/PlayerSlot.cs`：`PlayerSlot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Bootstrap/LocalNgoEndpointDriver.cs`：

```csharp
        private void Update()
        {
            TryStartEndpoint();
            if (OwnsClientConnectionLifecycle(
                    GameSessionContext.FlowMode))
            {
                NotifyClientConnectedOnce();
                LogConnectionWaitOnce();
            }
            if (gameplayProofLogged ||
                GameSessionContext.Bootstrap == null ||
                !lobbyBridge.HasAppliedBootstrap ||
                GameSessionContext.Bootstrap.Runtime == null ||
                GameSessionContext.Bootstrap.Runtime.CurrentTick < 8)
                return;

            gameplayProofLogged = true;
            string endpoint = dedicatedServer
                ? "server"
                : $"client slot {localPlayerSlot}";
            string controlledUnit =
                dedicatedServer ||
                !GameSessionContext.Bootstrap
                    .IsLocalPlayerBound
                    ? "none"
                    : GameSessionContext.Bootstrap
                        .LocalControlledUnitUid
                        .ToString();
            Debug.Log(
                $"[LocalNGO] Gameplay active on {endpoint}: " +
                $"tick={GameSessionContext.Bootstrap.Runtime.CurrentTick}, " +
                $"controlledUnit={controlledUnit}.");
        }
```

`Assets/Scripts/Bootstrap/ServerBootstrap.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using Unity.Netcode;
using Unity.UOS.Multiverse;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Dedicated Server process startup scene. Local mode loads the Lobby
    /// scene and lets the Lobby driver start the NGO server. UOS mode reads
    /// the allocation, starts the server, notifies UOS Ready and derives the
    /// Lobby slots deterministically from the allocated players.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerBootstrap :
        MonoBehaviour
    {
        [SerializeField] private bool
            enableOnlineApplicationFlow;
        [SerializeField] private GlobalGameplayData
            globalGameplayData;
        [SerializeField] private NetworkManager
            networkManager;
        [SerializeField] private int defaultHeroConfigId =
            1001;

        private void Awake()
        {
            GameSessionContext.ResetSession();
            GameSessionContext.IsDedicatedServer =
                true;
            FrameSyncDiagnosticsUnityHost.EnsureInitialized(
                true);
            GameSessionContext.FlowManagedExternally =
                true;
            enableOnlineApplicationFlow =
                UosApplicationConfig.IsOnlineFlowRequested(
                    enableOnlineApplicationFlow);
            GameSessionContext.FlowMode =
                enableOnlineApplicationFlow
                    ? FrameFlowMode.UosOnline
                    : FrameFlowMode.LocalDirect;
            GameSessionContext.Versions =
                GameSessionContext.ComputeVersions(
                    globalGameplayData);
            GameSessionContext.HeroDisplayTable =
                globalGameplayData != null
                    ? globalGameplayData.HeroDisplayTable
                    : null;
            SharedGameplayChecksum.DetailedLoggingEnabled =
                Array.IndexOf(
                    Environment.GetCommandLineArgs(),
                    "-checksumDetail") >= 0;
            MarkNetworkRootPersistent();
        }

        private async void Start()
        {
            if (!enableOnlineApplicationFlow)
            {
                LoadLobby();
                return;
            }

            try
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**UOS 配置与连接模式**

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。

**大厅槽位与开局配置**

LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。

断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。

**加载确认与单调时钟开局屏障**

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode()
        {
            Assert.IsTrue(
                LocalNgoEndpointDriver
                    .OwnsClientConnectionLifecycle(
                        FrameFlowMode.LocalDirect),
                "Local direct clients are owned by LocalNgoEndpointDriver.");
            Assert.IsFalse(
                LocalNgoEndpointDriver
                    .OwnsClientConnectionLifecycle(
                        FrameFlowMode.UosOnline),
                "UOS clients are owned exclusively by LobbyFlowController.");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
