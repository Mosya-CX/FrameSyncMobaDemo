# 加载确认与单调时钟开局屏障

## 目标实现

各端完成内容和初始状态加载后，在同一开局授权下启动 Tick 0。

## 技术方案

采用载荷加载确认与 LaunchCommit 两阶段协议。同步网络时间给出授权时刻，本机 Stopwatch 单调时钟调度；墙钟和传输估计只在 Bootstrap。

## 边界情况

不得继续执行旧的载荷携带 UTC 授权方案；Ready 不执行 Gameplay；延迟、重复确认与过期授权不启动第二次对局。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`：当前关联实现定义 FrameSyncNetworkBridge、PresentationPingTracker、FrameSyncWireCodec（以源码为实际命名）。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：当前关联实现定义 InitialUnitSpawnAuthoring、GameBootstrap、ScoreboardBuffer（以源码为实际命名）。
- `Assets/Scripts/Bootstrap/LobbyNetworkBridge.cs`：当前关联实现定义 LocalLobbySlotDefinition、LobbyNetworkBridge、LobbyIdentity、LobbyWireCodec（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。
- `Assets/Scripts/FrameSync/Tests/GameBootstrapPayloadContractTests.cs`：Payload_PreservesFrozenPlayerSlotMappings、Payload_MappingTeamMismatch_FailsDeterministically、VersionHandshake_AnyCriticalMismatch_Fails。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/LocalNgoEndpointSceneTests.cs`：ClientBootstrap_TransitionsToLobbyAndBindsSession、ClientBootstrap_OnlineFlowOverride_SelectsUosSession、ServerBootstrap_TransitionsToLobbyAndStartsServer。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。
- `Assets/Scripts/Bootstrap/Tests/EditMode/LocalNgoSceneConfigurationTests.cs`：EndpointScene_UsesApplicationOwnedSceneAndPlayerLifecycle、LocalServerSlots_MatchGameSceneInitialSpawns。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-08-06

变动内容：曾采用 UTC 载荷启动；当前改为两阶段授权和单调调度。Tick 末 FinalizeTick 仍有效。

legacyDecision：D-033

### 2026-08-14

变动内容：加载确认和开局 Commit 两阶段，取代载荷内启动授权。

legacyDecision：D-044

### 2026-08-20

变动内容：开局采用单调时钟，作者毫秒独立于 TickRate；后续目标 Tick 估计补充此规则。

legacyDecision：D-045

