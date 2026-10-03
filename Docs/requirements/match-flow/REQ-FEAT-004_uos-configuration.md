# UOS 配置与连接模式

## 目标实现

在线模式使用一处配置并正确衔接匹配与 NGO 连接。

## 技术方案

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

## 边界情况

匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Bootstrap/LobbyFlowController.cs`：当前关联实现定义 LobbyFlowController（以源码为实际命名）。
- `Assets/Scripts/Bootstrap/UosApplicationConfig.cs`：当前关联实现定义 UosApplicationConfig（以源码为实际命名）。
- `Assets/Scripts/Bootstrap/UosNgoApplicationAdapters.cs`：当前关联实现定义 PlayerPrefsTestAccountPersistence、UosClientSession、UosMatchmakingApplicationClient、NgoConnectionService、UosDedicatedServerPlatform（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/EditMode/UosApplicationConfigTests.cs`：IsProfileTestServer_RecognizesOnlyTrue、IsProfileTestServer_PrefersMultiverseServerInfo。
- `Assets/Scripts/Bootstrap/Tests/EditMode/ApplicationFlowTests.cs`：TestAccount_CommandLineOverridesPersistedIdentity、Lobby_RequiresEveryAssignedPlayerAtFullReadyBarrier、VersionHandshake_RejectsAnyCriticalMismatch、ClientFlow_ReachesLobbyOnlyAfterNgoConnects、ClientConnectionLifecycle_HasExactlyOneOwnerPerFlowMode、ClientFlow_CancelWaitingAssignment_DeletesTicketAndReturnsMain、ClientFlow_CancelWhileTicketCreationIsPending_DeletesLateTicket。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/LocalNgoEndpointSceneTests.cs`：ClientBootstrap_TransitionsToLobbyAndBindsSession、ClientBootstrap_OnlineFlowOverride_SelectsUosSession、ServerBootstrap_TransitionsToLobbyAndStartsServer。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-10-02

变动内容：UOS 配置唯一来源，命令行显式覆盖，密钥不进入文档。

legacyDecision：D-027

