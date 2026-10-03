# UOS 指示器重绑定与韦鲁斯 Q 权威恢复

## 本次执行范围

本计划对应原编码 0153 的一次执行：UOS 指示器重绑定与韦鲁斯 Q 权威恢复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [权威帧校验与恢复](../../requirements/frame-sync/REQ-FEAT-010_authoritative-frame.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能指示器与本地生命周期](../../requirements/player-input/REQ-FEAT-081_skill-indicator.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [UOS 配置与连接模式](../../requirements/match-flow/REQ-FEAT-004_uos-configuration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [大厅槽位与开局配置](../../requirements/match-flow/REQ-FEAT-002_lobby-slots.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [加载确认与单调时钟开局屏障](../../requirements/match-flow/REQ-FEAT-003_startup-barrier.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：`SkillIndicatorDriver`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Ability/AbilityRuntime.cs`：`AbilitySession`、`AbilityRuntime`、`AbilityRuntimeSnapshot`、`AbilitySessionSnapshot`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/PlayerInput/PlayerCommandRequester.cs`：`IPlayerGameplayCommandRequester`、`IPlayerShopCommandRequester`、`IPlayerAbilityInputProfileProvider`、`IPlayerAbilityAimProfileProvider`、`ILocalAbilityRuntimeView`、`GameplayCommandRequestReceipt`、`LocalAbilityInputStateKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：

```csharp
        public void Configure(
            GameObject directionPrefab,
            GameObject rangeCirclePrefab,
            GameObject groundTargetPrefab)
        {
            // ClientContentRuntimeHost may release the previous Addressables
            // leases before rebinding a newly acquired generation.  Instances
            // cloned from that generation must not survive the lease release:
            // their Material/Texture dependencies can already be unloaded.
            // Always rebuild the generic presentation from the assets owned by
            // the new leases, even when the address resolves to the same
            // prefab object identity.
            ReleaseGenericInstances();
            directionIndicatorPrefab =
                directionPrefab;
            this.rangeCirclePrefab =
                rangeCirclePrefab;
            this.groundTargetPrefab =
                groundTargetPrefab;
            Debug.Log(
                $"[IndicatorBind] Configure driver={name} " +
                $"direction={DescribePrefab(directionIndicatorPrefab)} " +
                $"range={DescribePrefab(this.rangeCirclePrefab)} " +
                $"ground={DescribePrefab(this.groundTargetPrefab)}");
            EnsureInstances();
        }
```

`Assets/Scripts/Bootstrap/GameBootstrap.cs`：

```csharp
        private void OnDestroy()
        {
            isDestroying = true;
            contentLoadCancellation?.Cancel();
            if (uiManager != null)
                uiManager.Initialized -= OnUiManagerInitialized;
            FrameSyncGameRuntime.UnregisterActiveInstance(
                Runtime);
            AnimationPresentationClock.Clear(UnitWorld);
            if (frameSyncNetworkBridge != null)
            {
                frameSyncNetworkBridge.MatchResultReady -=
                    OnMatchResultReady;
                frameSyncNetworkBridge.AcceptedCommandsReceived -=
                    OnAcceptedCommandsReceived;
            }
            if (GameSessionContext.LobbyBridge != null)
            {
                GameSessionContext.LobbyBridge.StartScheduled -=
                    OnLobbyStartScheduled;
                GameSessionContext.LobbyBridge
                    .AllClientsBootstrapApplied -=
                    OnAllClientsBootstrapApplied;
            }
            ReleaseMatchMapTopology();
            matchContentScope?.Dispose();
            matchContentScope = null;
            contentLoadCancellation?.Dispose();
            contentLoadCancellation = null;
        }
```

### 输入输出与边界

**权威帧校验与恢复**

AuthorityFrame 必须含 SharedGameplayChecksum；完整 Command 字节和金币批次摘要参与校验。AuthorityRecovery 仅补发缺失帧。

不提供进程重启恢复、局中加入或 BaseSnapshot；本地恢复锚点丢失即终止当前对局连接。

**技能指示器与本地生命周期**

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

**UOS 配置与连接模式**

UosApplicationConfig 读取 Unity.UOS.Common.Settings.MatchmakingConfigID；命令行仅作显式覆盖。本地模式由 LocalNgoEndpointDriver 拥有，UOS 模式由 LobbyFlowController 拥有。

匹配配置 ID 与启动 Profile ID 不可混用；令牌和服务器密钥不进源码或共享日志。

**大厅槽位与开局配置**

LobbySessionFlowNetwork 和 LobbyNetworkBridge 管理玩家槽位与屏障；GameStartConfig 固定 PlayerSlotConfig 列表。英雄可重复选择，正 HeroConfigId 由内容闭包校验。

断线、重复 Ready、人数不一致、内容或版本不一致必须有明确失败状态；大厅消息不进入 GameplayCommand。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：PlayMode，程序集 `FrameSyncMoba.Bootstrap.PlayModeTests`，函数 `GameBootstrapPlayModeTests`；输入/夹具与期望见真实断言，失败保留回执与 Console。

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
