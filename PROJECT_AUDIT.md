# FrameSyncMobaDemo — 项目事实审计

> 审计基线：`master` / `a7a061f`，审计日期 2026-08-25。本文以仓库中首方 C# 源码、Unity 配置、序列化资产和真实调用关系为主要证据；设计文档、README、历史 ExecPlan 和 handoff 只在明确标为“记录性证据”时使用。
>
> 审计边界：已清点首方运行源码约 77,023 行（不含 `Assets/3rd`、Tests、Editor-only 源）和测试源码约 40,695 行（165 个文件），以及全部程序集定义、Packages、ProjectSettings、Build Settings、场景、Formal/ClientContent 资产和 160 份非归档项目 Markdown 文档的目录/状态。第三方源码（XLua、DOTween、Odin 等）不被当作项目自研能力。未修改业务代码，也未在本次审计中重新执行 Unity 编译、Player Build 或测试。

## 1. 项目概述

这是一个 Unity 2022.3.62f1c1 的确定性帧同步 MOBA 技术演示/框架工程，不是完整商业游戏。它的实际目标是将固定 Tick 的 2D 固定点 Gameplay、Dedicated Server 权威帧、客户端预测/回滚、NGO/UTP 自定义消息、UOS 对局调度，以及少量英雄/小兵/防御塔内容切片组合成可测试的端到端样例。

项目使用 Unity URP 14.0.12、Netcode for GameObjects 1.12.2、Unity Transport 1.5.0、Input System 1.14.2、Addressables 1.22.3、Unity.Mathematics.FixedPoint、XLua，以及 UOS 的 Launcher/Matchmaking/Multiverse SDK。固定点、命令、快照和 Gameplay 的主要实现都是首方代码；这些包只是依赖，不能据此推断使用了 ECS、Burst 或 Job System。

当前完成度应表述为“核心框架和两个英雄的可运行垂直切片已实现，仍有集成验证与产品内容缺口”。代码已包含 Command→Simulation→Snapshot/Restore/Replay 的完整链路、战斗/单位/投射物/路径/输入/资源加载实现；但当前 schema-24 包尚未有同版本 Local C/S 与 UOS 实机验收，默认主场景没有丛林营地配置，UI 资源加载策略和若干产品页面/结算流程仍有限制。

审计时的 Unity MCP 项目查询工具在已连接 Editor 中未注册，无法用它重新获取项目元数据或触发测试；`console-get-logs` 可用，但当前 Error 条目主要是 MCP Hub 连接失败和本次调用未注册工具产生的插件错误，不能作为项目 C# 编译失败的证据。`Docs/Implementation/MODULE_STATUS.md` 记录了 2026-08-24 的通过结果，但那是历史记录，不是本次重新验证。

## 2. 整体架构

运行时的实际数据流如下。`Bootstrap` 是 Unity 场景、网络和表现的组合根；确定性程序集不引用 Bootstrap、InputSystem、NGO 或 UOS。

```text
ScriptableObject / 逻辑 Prefab / 地图 Authoring
    -> GlobalGameplayData.BakeOrThrow + 各注册表/Prefab 表
    -> GameBootstrap 构造 FrameSyncGameRuntime

Unity Input System / Lua Shop UI
    -> LocalInputEventBuffer
    -> PlayerCommandRequester (TargetTick + CommandSeq)
    -> CommandCollector 的规范化 GameplayCommand
    -> FrameSyncNetworkBridge (NGO CustomMessagingManager)

Server: CommandBundle -> AcceptedCommandRelay -> AuthorityFrame
Client: Relay/AuthorityFrame -> PredictionRollbackCoordinator
    -> SnapshotStore Restore -> Resolve -> Rebuild -> Replay

FrameSyncGameRuntime
    -> SimulationTickPipeline
    -> UnitWorld / PhysicsWorld / ProjectileWorld / CombatSystem
    -> GoldIncome / MatchRule / Non-hero systems

只读的 Gameplay 状态和 VisualEvent
    -> Unit/Projectile View Binder、Animator、VFX/SFX、Camera、Lua UI
```

程序集关系也基本符合这一方向：`RuntimeConfig`、`Deterministic`、`Physics` 和 `Unit` 是底层；`FrameSync` 组合 Gameplay；`PlayerInput` 依赖 FrameSync/Unit；`Bootstrap` 依赖所有前述层并依赖 NGO/UOS/XLua。`FrameSyncMoba.ClientContent` 依赖表现所需层，并通过 asmdef 的 `!UNITY_SERVER` 约束从 Dedicated Server 编译中排除。例外是 `LuaBridge` 和旧表现类仍处于共享 managed assemblies，而不是完全独立的 presentation-only 程序集。

### 启动与角色

`ClientBootstrap.Awake` / `ServerBootstrap.Awake` 先计算版本并初始化客户端或服务端应用流。`GameBootstrap.BuildAuthoritativeBootstrapPayload`（`Assets/Scripts/Bootstrap/GameBootstrap.cs:1045`）在服务器配置初始随机种子、物化初始单位、建立 PlayerSlot→Unit 映射并捕获初始快照；`ApplyGameBootstrapPayload`（:1108）在端点恢复该快照。所有客户端确认 Bootstrap 后，`OnAllClientsBootstrapApplied`（:924）广播 `MatchLaunchCommit`；随后 `GameBootstrap.Update` 的启动时钟门控才允许服务端或客户端 Tick。它不是“加载场景即开始模拟”。

## 3. 核心系统逐项审计

### 确定性基础、时间与运行时配置

**职责**

提供 deterministic Tick 上下文、随机状态、规范字节写入，以及将 Inspector 的现实时间转换为运行期整数 Tick 的入口。

**核心代码**

- `Assets/Scripts/Deterministic/Core/SimulationTickContext.cs`、`SimulationTickContextController.cs`
- `Assets/Scripts/Deterministic/Random/DeterministicRandomService.cs`
- `Assets/Scripts/Deterministic/Serialization/CanonicalByteWriter.cs`
- `Assets/Scripts/RuntimeConfig/DeterministicTimeAuthoring.cs`、`GlobalGameplayData.cs`
- 实例配置：`Assets/Config/Formal/GlobalGameplayData.asset`

**运行流程**

`SimulationTickPipeline.ExecuteTick` 调用 controller 的 `BeginTick`，Gameplay 通过 `SimulationTickContext.Current` 读取 Tick；`FrameSyncGameRuntime` 同时持有可 Capture/Restore 的 `DeterministicRandomService`。`GlobalGameplayData.BakeOrThrow` 验证配置并把 `DurationAuthoring`（毫秒）按 rounding policy 转为 Tick，生成 `BakedGlobalGameplayData` 供 Bootstrap/Simulation 使用。

**实际实现能力**

代码确实实现了可恢复的伪随机状态、little-endian 风格的原始 canonical writer、固定点 `fp/fp2` 主数值通路，以及 Tick 率验证。当前 Formal 资产值为 50 TPS；作者代码限制 TickRate 为 10–120 且是 5 的倍数。运行时快照、Command 和 checksum 中存整数 Tick，不保留 authoring 秒数。

**技术特点**

- `UnityEngine.Random` 在首方运行源码检索中为 0 次。
- 关键数学使用 `Unity.Mathematics.FixedPoint`，不是 `float` 位置/伤害模拟。
- 时间 authoring 与 simulation time 分离，便于调整 TickRate 时重新 Bake。

**局限 / 未完成部分**

这不是“全仓零 float/double”：`GlobalGameplayData` 的兼容 authoring 字段、Camera、UI 和 Presentation 仍使用 `float`/`double`。没有证据表明所有第三方或表现代码也符合确定性规则；应只对 Gameplay 核心作此结论。

**证据**

`GlobalGameplayData.BakeOrThrow`（`GlobalGameplayData.cs:252` 起）和 `BakedGlobalGameplayData` 的字段声明；当前资产 `GlobalGameplayData.asset:18–95`。

### Command、权威帧、预测与回滚

**职责**

将玩家行为封装为可排序的 Command，在服务器产生 AuthorityFrame，在客户端比较权威 Command/Checksum 并通过快照恢复、解析、重建、重演纠正预测。

**核心代码**

- `Assets/Scripts/FrameSync/GameplayCommand.cs`、`CommandCollector.cs`、`AuthorityFrame.cs`
- `FrameSyncGameRuntime.cs:129–520`
- `SimulationTickPipeline.cs:175–1115`
- `PredictionRollbackCoordinator.cs:141–707`、`SnapshotStore.cs`
- `GameplaySnapshot.cs`、`SharedGameplayChecksum.cs`、`AuthorityReplication.cs`

**运行流程**

`PlayerCommandRequester` 生成含 `TargetTick/PlayerSlot/ControlledUnitUid/CommandSeq` 的 Command，`CommandCollector.Collect` 收集，`ConsumeCanonicalCommands` 在目标 Tick 消费。`FrameSyncGameRuntime.ExecuteAuthorityTick` 调 `AuthorityFrameReplicator.ExecuteNextTick`；客户端的 `ExecutePredictionTick` 交给 `PredictionRollbackCoordinator`。后者在 `OnAuthorityFrameReceived` 排序连续帧，比较 command history 的 canonical bytes 与 tick checksum（`PredictionRollbackCoordinator.cs:319–323`），不一致则 `CorrectAndReplay`：从 `SnapshotStore` 恢复、使用权威帧/历史命令重演至原预测末端。

`SimulationTickPipeline.RestoreFromSnapshot` 显式分为 `RestorePhase`、`ResolvePhase`、`RebuildPhase`（:529–836）。它会按 Snapshot Unit topology 创建/删除逻辑 Unit，解析 UID 引用，最后重建物理网格等派生缓存；无效引用会抛 deterministic exception，而不是静默修复。

**实际实现能力**

这不是只有名为 Rollback 的类：SnapshotStore 是环形窗口；协调器保存预测快照、Command history、收到的 AuthorityFrame 以及 recovery range。Authority recovery 也有请求/响应数据类型和 archive。`GameplaySnapshot.CurrentSchemaVersion` 在实际源码中为 **24**（`GameplaySnapshot.cs:62`）；snapshot 中包含 Unit、Physics、Projectile、Combat、Gold、Match 和随机状态的聚合入口。

**技术特点**

- 命令排序比较器位于 `CommandCollector.cs:248`；权威比较使用规范编码而非仅比较业务字段。
- `SimulationTickPipeline.ExecuteTick` 是真实主循环：命令、控制/规划、移动/RVO、全局 Handler 子阶段、投射物、战斗、死亡/奖励和 capture 都在同一 Tick 边界组织。
- `GameplayParticipantId`、`OriginActionId` 和 `EffectOrdinal` 已进入 Snapshot/checksum 路径，以降低技术 UID 变化对 Crit/投射物平局的影响。

**局限 / 未完成部分**

当前代码实现不等于 schema-24 端点已完成实机闭环。`MODULE_STATUS.md`/`CURRENT_HANDOFF.md` 都记录相同协议版本的 Local C/S、UOS 重新构建验收仍待执行；本次未运行多进程或网络测试。也没有性能基准能证明给定网络延迟或大规模实体下的 rollback 成本。

**证据**

`FrameSyncGameRuntime` 构造器实际组装 `CombatSystem`、`ProjectileWorld`、`PredictionRollbackCoordinator` 和 authority recovery（:171–258）；`SimulationTickPipeline.ExecuteTick`（:175）；`PredictionRollbackCoordinator.CorrectAndReplay`（:421）；`GameplaySnapshot.cs:62`。

### NGO/UTP 传输、Lobby、UOS 应用流与 Dedicated Server 启动

**职责**

把 FrameSync 数据接到 Unity 网络传输，组织连接、选人、Bootstrap barrier、开局和结果通知；UOS 负责在线分配/对局启动的外部应用流。

**核心代码**

- `Assets/Scripts/Bootstrap/FrameSyncNetworkBridge.cs`
- `LobbyNetworkBridge.cs`、`ApplicationFlow.cs`、`UosNgoApplicationAdapters.cs`
- `ClientBootstrap.cs`、`ServerBootstrap.cs`、`LocalNgoEndpointDriver.cs`
- `BootstrapPayloadWireCodec.cs`、`MatchLaunchWireCodec.cs`

**运行流程**

`FrameSyncNetworkBridge.Bind` 注册 NGO `CustomMessagingManager` 的 named handlers。客户端 `SendLocalCommands` 打包 `GameplayCommandBundle`；服务器 `ReceiveBundle` 校验 sender/clientId，调用 runtime 接收，并广播 AcceptedCommandRelay。AuthorityFrame、RecoveryRequest/Response、MatchResult 走同一桥接；主消息使用 `ReliableSequenced`，独立 ping 使用 `Unreliable`。`LobbyNetworkBridge` 负责 identity、选英雄、lock、load/ready、bootstrap applied 和 launch commit 的消息。`ApplicationFlow` 中的 client/server 状态机通过 UOS adapter 的 async 初始化、匹配/分配与 NGO connect 接口推进。

**实际实现能力**

首方实现没有采用 `ServerRpc`、`ClientRpc` 或 `NetworkVariable`；检索结果为 0。它实际采用显式 message name、`FastBufferWriter` 和项目自己的 wire codec，因此 Command/AuthorityFrame 的传输形状可控。`GameBootstrap` 同时支持 local-development 与 UOS 组合流；`LocalNgoEndpointDriver` 可在本地启动 server/client endpoints。

**技术特点**

- 分离的 Command relay、最终 AuthorityFrame 和缺帧 recovery 消息。
- Bootstrap 是“服务器下发完整初始状态、各端确认、再按同步 server-time 开始”的两阶段流程。
- `FrameSyncMoba.ClientContent` 有 `!UNITY_SERVER` asmdef 约束；`DedicatedServerPresentationBuildPipeline.cs` 在临时 build scene 剥离 Canvas、Renderer、Animator、Audio、Particle、Camera/Light，并审计 server 输出不得包含 `StreamingAssets/aa`、catalog 或 bundle。

**局限 / 未完成部分**

UOS SDK 被实际调用不等于线上生产可用。当前记录仍有旧 UTP send-queue warning、旧 LocalNGO callback exception，以及新版包未重新验收。build 剥离和 Addressables server exclusion 有源代码与 EditMode 测试，但“已发布 Dedicated Server”不成立。

**证据**

`FrameSyncNetworkBridge.cs:19–497` 的八类消息、收发和可靠性；`GameBootstrap.cs:899–1188`；`DedicatedServerPresentationBuildPipeline.cs`；`ClientContent.asmdef:21`。

### UnitWorld、生命周期与动作仲裁

**职责**

为英雄、小兵、塔和潜在 Monster 提供 UID、队伍、生命周期、Prefab 组合、对象复用和统一的“Intent → Planner → Arbiter → Main/Base Runtime → Handler”执行边界。

**核心代码**

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`、`UnitWorld.cs`、`UnitRegistry.cs`
- `BehaviorPlanner.cs`、`ActionArbiter.cs`、`ActionRuntimeSet.cs`
- `GameplayParticipantId.cs`、`UnitUid.cs`、`RespawnTimer.cs`
- `Unit/Pool/UnitPoolRegistry.cs`、`Unit/Prototype/*`

**运行流程**

`UnitWorld.SpawnUnit`（:206）验证 prototype/participant ID，派生 `UnitUid`，租用或实例化逻辑 Prefab，初始化 handlers、注册 PhysicsEntity 和 UnitRegistry、创建 locomotion。每 Tick Pipeline 先刷新 crowd control/capability/runtime，再由 `BehaviorPlanner.Tick` 提出 `ActionRequest`，`ActionArbiter.Submit` 检查资源/控制/冲突，`ActionRuntimeSet` 持久化 Main/Base slot。`UnitWorld` 是 `Alive → Dying → Dead → Respawning → Alive` 的唯一状态变更边界，`CombatSystem` 通过它确认死亡。

**实际实现能力**

代码中有固定的 Main 与 Base ActionRuntime slot、Handler 自动状态 reconciliation、死亡/复活、非英雄 Despawn 和 rollback restore topology。Unit pooling 不是名义字段：`UnitPoolRegistry.TryRent/Return` 真的复用 GameObject；pool 满且 Fixed 时才 Destroy。`UnitWorld.CreateUnitForRollbackRestore` 会按快照重建 Unit/Physics 注册，`RemoveUnitForRollbackRestore` 移除多余对象。

**技术特点**

- Unit 本身仍是 prefab-authored `MonoBehaviour`，不是 ECS entity。
- `GameplayParticipantId` 与技术 `UnitUid` 分离；spawn 请求缺 participant ID 直接失败。
- Unit registry 和 Handler composition 用显式验证，缺必需组件会 fail visibly。

**局限 / 未完成部分**

运行时仍会在首次 spawn、rollback topology 变化等情况下 Instantiate/Destroy；对象池仅缓解部分 despawn/reuse，不能宣称“零实例化”或“零 GC”。池容量来自配置，仓库没有可用于简历的并发 Unit 压测数字。

**证据**

`UnitWorld.cs:206–293, 456–575, 609–773, 873–1044`；`ActionArbiter.cs`、`ActionRuntimeSet.cs`；`UnitPoolRegistry.cs:1–105`。

### Combat、Stats、Buff、Crowd Control 与奖励

**职责**

以强类型请求统一伤害/治疗/护盾结算，维护属性、Buff、控制、击杀/助攻、复活和金币收益，避免各技能直接改血量。

**核心代码**

- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`（1,823 行）和 `CombatActionIdentity.cs`
- `Stats/StatHandler.cs`、`Modifiers/CombatModifierSet.cs`
- `Buff/BuffHandler.cs`、`BuffEffect.cs`、`Effects/*`
- `CrowdControl/CrowdControlHandler.cs`、`CrowdControlDefinition.cs`
- `FrameSync/GoldIncomeRuntime.cs`、`NaturalGoldIncomeSystem.cs`

**运行流程**

Attack、Ability、Projectile、Buff 和 Equipment 提交 `DamageRequest`、`HealRequest` 或 `ShieldRequest`；`CombatSystem.BeginTick` 导入 deferred request，`SettleActiveRequests` 先 seal 请求、记录 wave 起点，再按 target/batch 分配 shield/life damage/heal，最后 `ResolveDying` 通过 UnitWorld 进入正式死亡。Buff/Equipment/Ability 通过 event handler 接收结算结果，事件产生的跨 Tick 请求进 deferred buffer。`StatHandler` 管理 fp 属性、modifier、shield、经验和 snapshot；Buff/CC handler 都有 Capture/Restore/Resolve。

**实际实现能力**

同 Tick fairness 是真实代码，而非文档名：Pipeline 以全局 Handler 子阶段推进，Combat 则在 sealed causal wave 中分配生命/护盾，`CombatSystem` 有 `AllocateShieldGroup`、`AllocateLifeDamage`、`ResolveLethalBatchKiller` 和 `FillSortedContributionVictims`。D-050 对概率 Crit 使用 match seed + action/target participant/effect identity 的纯哈希，不推进全局随机流。Buff 支持 apply/reapply、生命周期、周期/事件 reaction 和 rollback；CC 支持免疫、cleanse、强制行为输出和 snapshot。

**技术特点**

- fixed-point 属性/伤害、顺序号和稳定 contribution 记录。
- 伤害/治疗/护盾不让调用方直接修改 `CurrentHealth`。
- 逻辑死亡同步完成，但死亡回调产生的后续战斗请求延后，避免当前 wave 的递归次序问题。

**局限 / 未完成部分**

文档记录 Buff effect-module catalog 仍非完整，且完整 Unit suite 仍有 10 个保留失败；因此不应说所有 Buff/CC 组合均已验收。对 full multiplayer match 的公平性、assist/金钱仍缺 schema-24 新包实机验证。

**证据**

`CombatSystem.cs:114–235, 358–1335, 1551–1907`；`BuffHandler.cs:88–977`；`CrowdControlHandler.cs:495–1350`；`StatHandler.cs:125–1044`。

### Ability、Attack、Projectile 与 Equipment Shop

**职责**

以数据定义的技能阶段、普攻周期和投射物效果驱动战斗；提供购买/出售/撤销等商店交易和部分装备主动使用。

**核心代码**

- `Gameplay/Ability/AbilityHandler.cs`、`AbilityAsset.cs`、`CastModelDef.cs`、`Stages/*`
- `Gameplay/Attack/AttackHandler.cs`、`TowerAttackHandler.cs`
- `Gameplay/Projectile/ProjectileWorld.cs`、`ProjectileEffectDispatcher.cs`
- `FrameSync/ProjectileHitResolver.cs`
- `Gameplay/Equipment/EquipmentShopRuntime.cs`、`EquipmentHandler.cs`、`EquipmentEffectDispatch.cs`

**运行流程**

Ability slot 从 `AbilityLoadoutAsset` / definition registry 初始化；`AbilityHandler.HandleSignal` 处理 Focus/Commit/Cancel，并在 `TickUpdate` 推进 stage、冷却、费用、被动和 session。Attack 的 windup/commit 可以直接提交 Damage，或向 `ProjectileWorld` request spawn。ProjectileWorld 维护 pending/active projectile，驱动运动、碰撞命中和生命周期；`ProjectileEffectDispatcher` 对命中目标提交 Combat 请求、施加 Buff/CC。商店请求是 `GameplayCommandKind.EquipmentShop`，Pipeline dispatch 后由 `EquipmentShopRuntime.ProcessPurchase/ProcessSell/ProcessUndo` 修改 `EquipmentHandler` 和 Gold state。

**实际实现能力**

这里确有通用 CastModel 和 StageDef，而不是英雄 if/else 的空壳。`AbilityAsset.Bake` 把 authoring 转成 definition；`CastModelDef` 包含 Commit、HoldRelease、Channel、Active、Toggle、Sequential 等模型。逻辑资产中存在 Aatrox/Varus 的 loadout/技能/Buff，且逻辑 Prefab 有 8 个 Unit、8 个 Projectile；现有 Formal catalog 是两个英雄、两色近战/远程小兵和两塔的内容切片。投射物已用 `ObjectPool<PhysicsEntity2D>`，并对 AoE/moving equal-distance 目标按 distance、seeded participant score、participant ID、最后 UnitUid 排序。

**技术特点**

- `OriginActionId` 与 `EffectOrdinal` 从投射物传入 Damage header。
- 投射物碰撞是固定点 segment/shape 检测，不是 Unity Physics authority。
- 装备有数据库、6-slot handler、交易 snapshot、passive effect dispatch 和 canonical shop command。

**局限 / 未完成部分**

装备主动使用只有基本 command/cooldown/charge 通路；`EquipmentTargetPolicy` 没有当前正式取值，代码不能据此完成 target/range/NeedApproach 仲裁。内容资产的存在不证明完整英雄平衡、全技能视觉或所有大型 Monster 交互已经实现。

**证据**

`AbilityHandler.cs:90–1454`；`AttackHandler.cs:143–715`；`ProjectileWorld.cs:138–667`；`ProjectileEffectDispatcher.cs:12–375`；`EquipmentShopRuntime.cs:61–1062`。

### 固定点物理、寻路、RVO 与非英雄 AI

**职责**

提供 2D 物理实体、空间查询、Direct/A*、team flow field、确定性局部避障、墙体修正，以及小兵/塔/丛林 Monster 行为。

**核心代码**

- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`、`PhysicsSpatialGrid2D.cs`、`PhysicsEntity2D.cs`
- `Gameplay/Pathfinding/AStarPathService.cs`、`TeamFlowFieldService.cs`、`DeterministicRVOSystem.cs`、`RvoOrchestrator.cs`、`WallPenetrationResolver.cs`
- `Gameplay/NonHero/MinionSystem.cs`、`UnitAIController.cs`、`JungleCamp.cs`
- `TowerAttackHandler.cs`

**运行流程**

Pipeline 在路径评估后 `BuildRvoGrid`，由 RVO orchestration 修正 locomotion，随后 MovementHandler 应用路线；墙体穿透修正发生在移动后。最终 `PhysicsWorld.BuildUnitFinalGrid` 供 RangeQuery/Projectile 使用。`PhysicsSpatialGrid2D.CollectCandidates` 虽内部使用 Dictionary/HashSet 去重，但最后按 runtime UID sort，不把容器枚举顺序直接暴露给 Gameplay。MinionSystem 根据 wave ticket/schedule 生成并登记 AI；Tower handler 走同一 Attack/Combat/Projectile 通路。

**实际实现能力**

A*、8 向 flow field、预移动 RVO grid、radius-aware 网格、稳定 indexed min-heap 和固定点墙体推离都有首方实现及对应测试文件。`JungleCamp` 也不是空类：它有 Dormant/Idle/InCombat/Returning/WaitingRespawn 状态、SpawnAllMembers、MonsterAI、snapshot/resolve、leash 和 respawn 逻辑。

**技术特点**

- 不使用 Unity NavMesh 或 Unity Physics 作为 authoritative movement；首方运行源码检索未见 NavMesh API。
- 空间网格和 flow field 将范围查询/行进方向从全局遍历转为候选格或 O(1) cell lookup。
- RVO/寻路的 tie-break 使用 cell index、Dir priority 或稳定 key。

**局限 / 未完成部分**

默认 `Assets/Scenes/GameScene.unity:518` 的 `jungleCamps: []`；三个主要 fixture scene 也为空。因此“JungleCamp 框架已实现”可靠，“游戏已有可玩的丛林内容”不可靠。没有最大地图尺寸、单位数、寻路耗时或 RVO 并发压测数据。

**证据**

`PhysicsWorld.cs:12–224`；`PhysicsSpatialGrid2D.cs:13–160`；`JungleCamp.cs:58–454`；`GameScene.unity:518`；`SimulationTickPipeline.cs:175–400`。

### 玩家输入、表现层、Addressables 与 Lua UI

**职责**

将真实设备输入转换一次为 Command，并从只读 Gameplay 状态生成客户端 View、镜头、VFX/SFX 与 Lua/UI 页面。

**核心代码**

- `Assets/Scripts/PlayerInput/PlayerInputController.cs`、`LocalInputEventBuffer.cs`、`PlayerCommandRequester.cs`
- `Assets/Scripts/ClientContent/AddressablesClientContentService.cs`、`ClientContentRuntimeInstaller.cs`、`ClientUnitViewBinder.cs`、`ClientProjectileViewBinder.cs`
- `Assets/Scripts/Bootstrap/UI/UIManager.cs`、`GameFlowLuaBridge.cs`
- `Assets/Scripts/LuaBridge/LuaManager.cs`、`LuaHost.cs`、`LuaBridge.cs`
- `Assets/Scripts/Bootstrap/CameraController.cs`

**运行流程**

InputSystem callbacks 只将按键/鼠标事件压入 `LocalInputEventBuffer`；`PlayerInputController.LateUpdate` 调 `PlayerCommandRequester.ProcessFrame`。Requester 通过 gate、Aim resolver、target-tick resolver 生成 Move/Attack/Cast/Shop/SkillPoint command 并单独递增 `CommandSeq`。因此 replay 的 Simulation 只消费 GameplayCommand，不会重读设备状态。

客户端 `ClientContentRuntimeHost` 初始化 Addressables service，异步加载地图/indicator/视图；Unit/Projectile binder 每帧 reconcile 逻辑 world，以 UID 和对象引用双重判断 rollback replacement 后是否要 rebind。每个 projectile view address 保持一个 match-lifetime lease，避免短寿命投射物不断卸载/重载资源。`UIManager` 使用一个 `LuaManager/LuaEnv` 创建 page host，`GameFlowLuaBridge` 注入 UI 所需的只读查询和 Command requester 回调。

**实际实现能力**

Addressables 配置直接给出 63 个本地 address：8 个 Unit view、8 个 Projectile view、35 个 UI、7 个 VFX、4 个 shared、1 个 audio，且 `m_BuildRemoteCatalog: 0`。逻辑 Unit/Projectile Prefab 与异步 View 各为 8 个；地图也分为逻辑 Map 与 Addressable view。Lua 文件实际位于 `Assets/StreamingAssets/Lua/`，不是注释中的预留字段。Camera 和 smoothing 是 presentation，`CameraController` 中的 `Time.deltaTime` 没有进入 deterministic simulation。

**技术特点**

- 引用计数 lease、CancellationToken 和 generation 防止异步 completion 写回陈旧 UI/实体。
- Dedicated Server 通过 asmdef 与 build pipeline 双层排除客户端资源。
- Input 点击先检查 EventSystem，UI 遮挡不会生成世界 Command。

**局限 / 未完成部分**

`UIManager.InitializeAsync` 无条件遍历所有注册的 `pages` 并 acquire prefab，再根据 `Preload/OpenOnStart` 决定是否实例化。因此它不是按页资源懒加载；手册中也记录这一点。表现层存在异步加载/实例化、Unity GameObject 和 per-frame binder reconcile，不能声称纯数据渲染、GPU 驱动渲染或零 GC。首方运行源码也没有 ComputeShader、DrawMeshInstanced、CommandBuffer 渲染、ECS 或 Jobs/Burst 实现证据。

**证据**

`PlayerInputController.cs:60–247`；`PlayerCommandRequester.cs:299–852`；`UIManager.cs:101–154`；`ClientProjectileViewBinder.cs:45–213`；Addressables group assets 与 `AddressableAssetSettings.asset:20,44`。

### 构建、测试、Editor 工具与历史资产

**职责**

支持本地/Server 打包、Addressables 检查、资产 Bake 和测试；保留少量测试场景和历史 Prefab。

**核心代码**

- `Assets/Editor/LocalNgoBuildMenu.cs`
- `Assets/Scripts/Bootstrap/Editor/DedicatedServerPresentationBuildPipeline.cs`
- `Assets/Scripts/RuntimeConfig/Editor/*`、`Gameplay/Pathfinding/Editor/*`
- 10 个 test asmdef 与 `Assets/Scripts/**/Tests/*`

**运行流程**

Build pipeline 在 Dedicated Server 打包期间修改临时 scene/output 作用域并在 postprocess 审计；RuntimeConfig Editor 提供技能/CC/时间 Bake 验证。Build Settings 启用 6 个场景：FrameworkSmoke、ClientFrameworkSmoke、ClientBootstrap、ServerBootstrap、Lobby、GameScene；仓库中另有 CameraDebug、HeroTest、MinionTowerLongRun 等测试/诊断场景。

**实际实现能力**

项目具有十个 test asmdef，测试代码覆盖 Deterministic、FrameSync、Unit、Physics、Input、RuntimeConfig、Bootstrap、Lua 和 ClientContent。构建代码实际包含 server 输出的 Addressables catalog/bundle 检查、客户端错误平台 bundle 检查，以及临时场景表现组件剥离。

**技术特点**

- 构建前后检查是首方 Editor 代码，不是 README 中的人工步骤。
- 测试同时包含 EditMode 与 PlayMode；但测试文件/属性数量不是通过结果。

**局限 / 未完成部分**

`Assets/Archive/LegacyMonolithic*` 是旧的单体 Map/Unit/Projectile Prefab 备份，不能当当前运行内容。`CodeMergerWindow` 是 Editor 文件合并工具；`MonoSingleton`/`NetworkSingleton`/`SplineTentacleRenderer` 在首方非测试代码中没有外部引用命中，不能纳入核心架构能力。第三方 XLua Examples、DOTween、Odin 也不是本项目功能。

**证据**

`EditorBuildSettings.asset`；`DedicatedServerPresentationBuildPipeline.cs`；`Assets/Archive/`；首方测试目录统计。

## 4. 关键技术难点

1. **在 Unity MonoBehaviour/Prefab 体系中维持可回滚的确定性状态。** 难点在于 Unit、Physics、Projectile、AI、Combat、Gold 互相有运行引用，且 Unity 对象本身不能作为 Snapshot authority。项目以 stable UID/participant ID、值 Snapshot、Restore→Resolve→Rebuild 和 topology reconciliation 处理，体现于 `SimulationTickPipeline.cs:460–836`、`UnitWorld.cs:456–575`、`ProjectileWorld.cs:288–667`。代价是实现和测试面广，且 rollback 时仍可能重建 Unity 逻辑对象。

2. **预测客户端与服务器权威命令的双重校验。** 难点不只是“收到服务器位置”，而是需要比较同 Tick 的 canonical command 集合和演算结果，并处理缺帧、recovery 和预测领先限制。`PredictionRollbackCoordinator` 同时维护 command/verification/snapshot history，`AuthorityRecoveryArchive` 提供范围恢复。权衡是更多 Snapshot/编码和网络消息，而非状态同步带宽。

3. **同 Tick 战斗公平性与随机身份解耦。** 多个单位在同 Tick 攻击同一目标时，按 Unit traversal 直接改血会造成隐式先后优势。代码以 handler 全局子阶段、sealed combat wave、target batch allocation 和形式死亡延后解决；D-050 又将 Crit/投射物完全同距离裁决改为 action/participant hash，保留 UID 作为序列化/最后 collision fallback。实现集中在 `CombatSystem.cs` 和 `CombatActionIdentity.cs`，代价是请求 header、deferred、snapshot/checksum 都要携带更多 provenance。

4. **逻辑 Prefab 与客户端资源解耦、同时兼顾 Dedicated Server。** 逻辑 Spawn 不能异步依赖 Addressables，也不能将模型/动画/VFX 带进 Server。项目用 `GlobalPrefabTable` 保留 logic prefab + 可选 view address，ClientContent lease/binder 异步挂 view，build scope 则剥离 server scene/output。代价是至少两套 Prefab 以及额外一致性测试，且当前最终重建验收未完成。

5. **无需 Unity NavMesh 的确定性移动。** A*/FlowField/RVO/墙体修正都使用 fp、网格和确定 tie-break。在所有相邻系统（Physics grid、Locomotion、Movement、AI、Projectile）同时回滚时，状态边界和派生缓存边界较复杂。实现是单线程托管集合，避免非确定调度；没有利用 Job/Burst 的吞吐优化。

## 5. 性能与工程优化

| 有直接代码证据的措施 | 实现与价值 | 不能推导出的结论 |
|---|---|---|
| 固定点与 canonical bytes | `fp/fp2`、`CanonicalByteWriter`、stable ID 避免浮点平台差异，面向确定性而非单纯性能。 | 没有跨硬件 determinism benchmark。 |
| 空间网格 | `PhysicsSpatialGrid2D` 把 Unit/Projectile/RangeQuery 的候选收敛到重叠 cell，并在输出前排序。 | 没有查询复杂度或大地图 profile 数据。 |
| Flow field / indexed heap | `TeamFlowFieldService` 预烘焙方向，`AStarPathService` 有 indexed min-heap。 | 没有百/千单位寻路吞吐数据。 |
| 对象池 | Unit 使用 `UnitPoolRegistry`；Projectile PhysicsEntity 使用 `ObjectPool<PhysicsEntity2D>`。 | Unit 初生、rollback topology 和 presentation view 仍会 Instantiate/Destroy；无 GC Alloc 测量。 |
| 复用缓冲/稳定排序 | Combat、Projectile、网格和 binder 复用 List/Dictionary 缓冲，显式排序影响 Gameplay 的集合。 | 托管 `List/Dictionary/HashSet` 仍大量存在，不能说整个 Tick 零分配。 |
| Addressables lease | projectile address 层面常驻 lease，避免每发投射物卸载/重载 Prefab。 | 资源包体积、加载时长、内存峰值没有本次实测。 |
| Dedicated Server 剥离 | Build 阶段排除 ClientContent、Addressables catalog/bundle 与展示组件。 | 未证明线上 Server 的 CPU、内存或成本指标。 |

明确的反证：首方运行源码检索中 `Unity.Entities`、`Unity.Jobs`、`BurstCompile`、`NativeArray/NativeList/NativeHashMap`、`ComputeShader`、`DrawMeshInstanced` 均无命中。因此不可将项目描述为 ECS、DOTS、Burst/Job、GPU Compute 或 draw-call 优化项目。

## 6. 可量化信息

| 项目数据 | 可确认数值 | 依据与口径 |
|---|---:|---|
| Unity 版本 | 2022.3.62f1c1 | `ProjectSettings/ProjectVersion.txt` |
| 首方运行 C# | 77,023 行 | 统计 `Assets/Scripts`，排除 Tests、Editor 和 `Assets/3rd`；是规模，不是质量指标。 |
| 测试源码 | 165 文件 / 40,695 行 | 首方 `Assets/Scripts/**/Tests`。 |
| 静态 Test 属性 | 1,121 `[Test*]` / 34 `[UnityTest]` | 属性出现数，不能等同实际独立 test case 或本次通过数。 |
| 当前 TickRate | 50 TPS | `GlobalGameplayData.asset:18`。 |
| 配置上限 | MaxPlayers 10 | `GlobalGameplayData.asset:34`；只代表配置上限，非已压测并发人数。 |
| 快照与预测配置 | 180 Tick 窗口 / 最多领先 6 Tick / 每 Unity 帧最多 4 logic ticks | `GlobalGameplayData.asset:20–24`。 |
| 命令窗口 | Min lead 1 Tick / Max future 12 Tick | `GlobalGameplayData.asset:19–20`。 |
| 地址化根 | 63 | 6 个 `Client-*` group 实际 address 行总数：Audio 1、Projectile 8、Shared 4、UI 35、Unit 8、VFX 7。 |
| 逻辑/View Prefab | Unit 8/8，Projectile 8/8，另有逻辑 Map + Map View | 对应 Prefab 目录的实际文件数。 |
| ClientContent 源资产 | 263 文件，339,786,045 B（324.05 MiB） | 当前 worktree `Assets/ClientContent` 的文件大小；不是最终 Player 包体。 |
| 逻辑 Formal asset | 74 个 `.asset`，17 个 logic Prefab | 当前 `Assets/Config/Formal` 目录统计。 |
| Build Settings 场景 | 6 个启用 | 当前 `EditorBuildSettings.asset`。 |
| 记录性 build 数据 | 7 bundles + 1 catalog，共 612,459,164 B | `CURRENT_HANDOFF.md` 的历史成功记录，未在本次审计重新构建。 |

当前文档记录的测试状态不能直接当本次结果：`MODULE_STATUS.md` 记录 Deterministic 53/53、FrameSync 98/98、RuntimeConfig Editor 47/47、Unit 545 passed/10 retained failures；但 `CURRENT_HANDOFF.md` 同时含有 545 和 542 的 Unit 通过数表述。使用前必须重跑并消除该记录冲突。

## 7. 项目成熟度检查

### 完整实现（代码链路存在；不表示所有线上验收完成）

- 固定点 deterministic primitives、stable UID/participant identity、canonical Command/bytes、Tick 上下文与随机状态。
- UnitWorld 生命周期、Planner/Arbiter/Main-Base Runtime、Stats、Combat request/batch settlement、Snapshot/Restore/Resolve/Rebuild。
- Attack/Ability/Projectile 的通用框架，Buff/CC/Equipment shop 的主要运行时与 snapshot。
- 自定义 2D Physics grid、RangeQuery、A*/flow field/RVO、当前地图上的 minion/tower fixture。
- InputSystem→Command 的一次性转换、custom NGO message bridge、Addressables view binders、Lua UI host。

### 基本实现，但存在限制

- NGO/UOS Bootstrap、匹配和 Dedicated Server 打包：实现存在，但 schema-24 matching endpoints 的实机验收未完成，保留 UTP/旧 callback 风险记录。
- Addressables/Dedicated Server split：配置、lease、build strip/audit 已实现；最终 corrected Windows+Linux Player build 仍待用户执行。
- UI/Lua：真实脚本、页面、查询与请求接线存在；页面资源并非懒加载，产品完成度不等于完整 HUD/结算体验。
- Buff/CC、装备与奖励：框架和当前 content slice 可用，但 catalog/active targeting/全量组合验收不完整。

### 实验性 / Demo

- `HeroTestScene`、`FrameworkSmoke`、`ClientFrameworkSmoke`、`MinionTowerLongRunTest`、Camera debug 及其驱动脚本是测试/诊断环境，不能等同于正式游戏模式。
- `CodeMergerWindow` 是 Editor 导出辅助；`Tools` 中 generic singleton 基类和 `SplineTentacleRenderer` 没有发现首方非测试调用。
- README 链接的视频明确对应历史包，不是当前 schema-24 代码包验收。

### 仅预留接口 / 部分功能

- `EquipmentHandler.Use` 的基本入口存在，但 `EquipmentTargetPolicy` 未定义正式 target/range/approach 语义。
- `JungleCamp`/`MonsterAIController` 有完整框架和 snapshot，但默认可运行场景的 `jungleCamps` 为空，没有正式 Monster content/topology。
- 生产结果/返回/远程结算的 live acceptance 深度不足；不能从类名或 UI Prefab 推断闭环完成。

### 废弃 / 历史代码与资产

- `Assets/Archive/LegacyMonolithicMapPrefab`、`LegacyMonolithicUnitPrefabs`、`LegacyMonolithicProjectilePrefabs` 是旧单体 Prefab；当前 GlobalPrefab/logic-view split 不使用这些路径。
- `Docs/Implementation/Plans` 中大多数完成计划、`Docs/Archive` 和旧 README 视频说明是历史信息，不覆盖当前源码。
- `Assets/3rd` 的 XLua/DOTween 示例和 Odin 插件属于供应商内容。

## 8. 可能具有简历价值的技术点

| 候选技术点 | 为什么可能值得写 | 对应代码依据 | 夸大风险 |
|---|---|---|---|
| 确定性帧同步、预测回滚与权威恢复 | 有 canonical command、AuthorityFrame、checksum、Snapshot/Restore/Resolve/Rebuild/replay 的实际闭环代码。 | `FrameSyncGameRuntime`、`SimulationTickPipeline`、`PredictionRollbackCoordinator`。 | 不要写“已完成线上大规模验证”或“当前 UOS 版本已上线”。 |
| 固定点 MOBA 战斗公平性 | 同 Tick sealed wave、批量护盾/生命分配、action-keyed Crit 与 participant tie-break 有明确实现。 | `CombatSystem.cs`、`CombatActionIdentity.cs`、D-050 对应测试文件。 | 不要简化成“使用随机数实现公平”；也不能忽略保留 Unit test failures。 |
| 数据驱动 Unit/技能/装备框架 | ScriptableObject Bake、loadout、CastModel/StageDef、Buff/Equipment effect dispatch 和统一 Command 真实存在。 | `AbilityAsset.cs`、`AbilityHandler.cs`、`EquipmentShopRuntime.cs`。 | 两个英雄和少量装备是 vertical slice，不是完整英雄/道具库。 |
| 自研固定点 2D Physics 与寻路 | 空间网格、A*、FlowField、RVO、墙体修正并入同一 deterministic Tick。 | `PhysicsWorld`、`PhysicsSpatialGrid2D`、`Pathfinding/*`。 | 不要称为 ECS/Job/Burst 或公布未测吞吐量。 |
| Unity 客户端/Server 资源边界 | 逻辑 Prefab / Addressable View 拆分、lease/binder、Dedicated Server stripping/output audit 可解决真实打包问题。 | `ClientContent/*`、`DedicatedServerPresentationBuildPipeline.cs`。 | 不要把“资源拆分源码完成”写成“正式包已验收”。 |
| NGO/UTP 自定义协议接入 UOS 流程 | 自定义 named messages、wire codec、lobby barrier、UOS adapter 均有实现。 | `FrameSyncNetworkBridge.cs`、`LobbyNetworkBridge.cs`、`UosNgoApplicationAdapters.cs`。 | 不要写成自研传输层、RPC replication 或已验证生产 matchmaking。 |
| Lua 驱动 UI 与 Unity InputSystem 命令边界 | LuaEnv/page host、GameFlowLuaBridge、UI 阻断世界点击、统一 CommandSeq owner。 | `LuaManager.cs`、`UIManager.cs`、`PlayerCommandRequester.cs`。 | 不要称“Lua 热更新 Gameplay”；Lua 在这里是 UI/表现集成。 |

## 9. 项目中不应该对外夸大的内容

- “完整商业 MOBA”或“完整 5v5/10 人已压测”：`MaxPlayers=10` 只是 authoring 配置，仓库没有并发容量、帧耗时或网络压测证据。
- “线上 UOS/Dedicated Server 已完成验收”：当前协议版本为 schema 24 / GameplayDataVersion 4，matching endpoints 的新版 live acceptance 明确待办。
- “使用 ECS、DOTS、Burst、Jobs、NativeContainer、GPU Compute、实例化渲染优化”：首方运行代码没有这些证据。
- “零 GC / 已完成性能优化”：有 pool/网格/缓冲优化，但没有 GC Alloc、CPU、内存、Draw Call 或网络带宽测量；托管集合和 Unity Instantiate/Destroy 仍存在。
- “完整 Rollback 已被线上证明”：rollback/recovery 代码和定向测试存在，但多端新版实机闭环未重新验收。
- “丛林系统已落地”：只有 Camp/Monster framework；主/主要 fixture scene 都没有 camp 实例。
- “UI 按需异步加载”：当前 UI Manager 会获取所有注册 page prefab 的 Addressables lease。
- “所有测试通过”：当前资料明确保留 10 个 Unit failure 和 4 个 PlayMode fixture failure，且当前 handoff 内 Unit pass count 还互相矛盾。

## 10. 待进一步核验的问题

以下项目不能仅凭当前仓库确认，结论为 **无法确认**：

1. **schema-24 / GameplayDataVersion-4 的 Local C/S 与 UOS 多端实机成功率、断线恢复和结果结算。** 有代码和历史记录，没有匹配版本的本次 live log。
2. **最大同时 Unit/Projectile 数、rollback 频率、帧耗时、GC Alloc、内存峰值、网络 bytes/s、Draw Call。** 代码存在可优化结构，但没有 benchmark/profile 数据。
3. **当前 Unity Editor 是否在审计时完全无项目编译/Console 错误。** MCP 项目查询能力未注册；目前可见 Error 是 MCP plugin/调用错误，不能替代干净的 Unity compile + Console query。
4. **所有 10 个保留 Unit 测试失败的根因与是否仍可复现。** 文档只列类别，且 `CURRENT_HANDOFF` 的 542/545 通过数互相矛盾；需要重跑精确 fixture。
5. **正式 Monster/jungle content 的目标规模与发布时间。** Camp framework 有实现，但无主场景配置或 Formal monster prefab/catalog 证据。
6. **新 Player/Server build 的最终体积。** 当前 324.05 MiB 是源 ClientContent 目录；612,459,164 B 是旧记录的 Addressables build，不是本次正式 Player package 测量。
7. **第三方包的内部行为、授权、性能和生产兼容性。** 本审计只确认 manifest/asmdef 引用和首方调用，不把供应商源码行为归为项目实现。

## 已确认的文档不一致

这是需要后续维护者先处理的事实冲突，而非可忽略的措辞问题：

| 主题 | 与代码/当前修正案一致的事实 | 过时或冲突的文本 |
|---|---|---|
| GameplaySnapshot schema | `Assets/Scripts/FrameSync/GameplaySnapshot.cs:62` 为 **24**；D-050 当前修正案第 6 节也写 schema 升至 24、GameplayDataVersion 升至 4。 | `README.md:123,415`、`Docs/Architecture/DESIGN_INDEX.md:24`、`unit_behavior_framework_design_v27_4_action_arbitration_amendment.md:129` 仍写 23。 |
| 新包验收描述 | 当前版本要求 schema-24 / GameplayDataVersion-4 / bootstrap-wire-4 端点匹配。 | README 仍称待验收包为 schema-23；`Plans/INDEX.md:22` 也保留旧 23 的历史摘要。 |
| Unit suite 记录 | `MODULE_STATUS.md` 写 545 passed / 10 retained failures。 | `CURRENT_HANDOFF.md` 同时出现 545 和 542 passed / 10 retained failures；没有本次重跑结果可裁决。 |

在这些不一致修正前，后续分析应以代码常量、D-050 当前修正案和带日期的模块状态为优先，不能把 README 或 v27.4 的 schema 23 当作现行协议。
