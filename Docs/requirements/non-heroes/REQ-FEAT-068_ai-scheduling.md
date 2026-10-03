# AI 注册调度与多态快照

## 目标实现

非英雄 AI 在 UnitWorld 中唯一注册并参与统一行为链。

## 技术方案

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

## 边界情况

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：当前关联实现定义 UnitAIController、MinionAIController、ThreatEntry、MonsterAIController、TowerAIController（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：当前关联实现定义 UnitWorld（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：SpawnMultipleKinds_GetByKind、SpawnUnit_DoubleSnapshot_RoundTrip、ClearForDeath_PreservesCombatModifiersOwnedBySourceSystems、ClearForRespawn_DoesNotClearStatBaseValues、ResetForPool_ResetsAllDynamicState、ManyUnits_StableReadOrder。
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：InternalRegistration_PublicLookupReturnsSameRuntime、StableReadOrder_IsIndependentOfRegistrationOrder、SuccessfulUnregister_RemovesLookupAndAllowsReregistration。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientBootstrapFirstWavePlayModeTests.cs`：GameScene_FirstWaveUsesFlowFieldsAndMoves、GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin。
- `Assets/Scripts/Gameplay/Tests/MinionThreatSystemTests.cs`：Acquisition_SetsInitialThreat_AndPicksClosestUnclaimed、DamageTaken_AddsThreat_InverselyProportionalToDistance、HigherThreatTarget_SwitchesOnlyWhenNotInWindup、Acquisition_PairsAlliesWithDistinctTargets、ThreatTable_SnapshotRoundTrip_PreservesEntries、SnapshotRoundTrip_PreservesLastThreatRefreshTick、RestoreReplacement_UnsubscribesPreviousController。
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：MinionWave_ExpandsCanonicalTeamLaneMemberOrder、MinionUnregister_RemovesUidWithoutLeavingTombstone、LaneNearestPoint_ProjectsOntoCenterlineSegment、MinionAI_BetweenDistantCenterlineNodes_RemainsInLaneAdvance、MinionAI_FarFromLane_ReturnStateStillUsesLaneFlowField、AIController_DoesNotTickOnSpawnTick、MinionAI_UsesLaneAdvanceOrderThroughPlanner。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 总体对象关系

本设计不增加 IUnitWorldSubsystem，也不建立统一的非英雄单位总控。

UnitWorld 只直接持有真正需要世界级集中推进的 MinionSystem，以及所有单位共用的一份 UnitAIControllerRegistry。JungleCamp 是地图场景中的地点，在开局装配时注册给 UnitWorld；防御塔没有集中业务，不设置 TowerSystem 或防御塔专用列表。

~~~mermaid
flowchart TD
    A["UnitWorld"] --> B["MinionSystem"]
    A --> C["UnitAIControllerRegistry"]
    A --> D["已注册 JungleCamp"]
    C --> E["小兵 / 野怪 / 防御塔 AI"]
    A --> F["按需接入的独立史诗野怪机制"]
~~~

明确不建立：

- IUnitWorldSubsystem
- NonHeroUnitManager
- JungleCampSystem
- TowerSystem 或 TowerManager
- 防御塔 AIController 专用列表
- EpicMonsterSpawner
- 统一史诗野怪刷新状态机

大龙、小龙、厄塔汗等史诗野怪的地图变化与生成规则差异明显。需要哪一种，就实现哪一种独立机制并接入 UnitWorld；只有已经完成的多个机制确实出现重复代码后，才抽取局部工具。

### 核心类的脚本角色

| 核心类 | 推荐角色 | 主要职责 | 主要依赖 |
|---|---|---|---|
| UnitWorld | 既有纯 C# 世界服务 | 同步生成、注册、生命周期、AIController 调度 | UnitRegistry、单位池、SimulationTickContext |
| MinionSystem | UnitWorld 持有的纯 C# 业务对象 | 波次日程、票据展开、稳定生成 | UnitWorld、兵线静态数据、比赛规则 |
| UnitAIController | 纯 C# 抽象控制器 | 读取 Unit 状态并下达 Order | Unit、UnitWorld、空间查询 |
| MinionAIController | 纯 C# 决策器 | 推进、索敌、追击、回线 | LaneRuntimeData、空间查询 |
| JungleCamp | 场景 MonoBehaviour | 表达营地点位、成员、战斗和刷新 | UnitWorld、场景 Transform |
| MonsterAIController | 纯 C# 决策器 | 待机、攻击、回营 | JungleCamp、Unit |
| TowerAIController | 纯 C# 决策器 | 按固定优先级选敌 | UnitActionStateView、空间查询 |
| TowerAttackHandler | AttackHandler 具体实现 | 塔的攻击提交和在途炮弹门控 | ProjectileWorld、当前攻击模块 |
| TowerTargetLinePresenter | 表现 MonoBehaviour | 绘制红线 | TowerAttackHandler、表现挂点 |

JungleCamp 使用 MonoBehaviour，是因为它本身就是地图中的一处地点，需要在 Scene 中编辑锚点、成员出生点和牵引范围。其 Gameplay 推进仍由 UnitWorld 在逻辑 Tick 中显式调用，不使用 MonoBehaviour.Update。

### 三个业务模块的边界

| 模块 | 负责 | 不负责 |
|---|---|---|
| 小兵 | 波次日程、组成展开、稳定生成、兵线归属、推进、选敌、追击与回线 | 流场构建、A*、RVO、攻击前后摇、投掷物、伤害公式 |
| 普通野怪 | 营地场景数据、主野怪、成员关系、战斗状态、脱战回营、刷新倒计时 | 史诗野怪规则、攻击命中、治疗公式、死亡奖励 |
| 防御塔 | 固定优先级索敌、不追击攻击指令、塔弹门控、红线逻辑来源 | 塔专用总控、移动、投掷物推进、伤害公式、废墟生成 |

### 所有 AI 都复用单位行为链

AIController 位于 Unit 之上。它读取 Unit 和世界状态，完成分析后向 Unit 下达已有 Order；后续仍由单位框架处理 Intent、规划、仲裁和 Runtime。

~~~mermaid
flowchart TD
    A["UnitAIController"] --> B["读取 Unit 与世界状态"]
    B --> C["选择或维持 Order"]
    C --> D["Unit 接收 Order"]
    D --> E["Intent 与 BehaviorPlanner"]
    E --> F["ActionArbiter 与 ActionRuntime"]
    F --> G["Handler 与外部系统"]
~~~

AIController 不直接：

- 写入 UnitIntent。
- 创建 ActionRuntime。
- 调用 MovementHandler 或 AttackHandler 启动行为。
- 修改 PhysicsEntity2D 的逻辑位置。
- 计算 A*、流场或 RVO。
- 修改生命值、LifeState 或战斗结算结果。

移动链路继续是：

~~~text
AI Order
    → Unit Intent
    → BehaviorPlanner
    → MoveActionRequest
    → MovementHandler
    → UnitLocomotionAgent
    → FlowField / AStar / Direct
~~~

攻击链路继续是：

~~~text
AttackOrder
    → AttackTarget Intent
    → BehaviorPlanner
    → AttackActionRuntime
    → AttackHandler
    → CombatSystem / ProjectileWorld
~~~

### 生命周期和事件边界

LifeState 的权威完全遵循单位框架 v27：

- Unit 保存 LifeState。
- UnitWorld 是 LifeState 的唯一正式写入者。
- CombatSystem 负责致死判定、死亡阻止和最终死亡结果，但只能在当前 Combat Settlement Cycle 中同步请求 UnitWorld 转换状态。
- 小兵和普通野怪再次出现属于新的同步生成，获得新的 UnitUid，不经过 Respawning。
- AIController、MinionSystem 和 JungleCamp 都不能直接写 LifeState。

正式死亡不能推迟到 Combat 阶段之后。UnitWorld 在 CombatSystem 的调用栈内写入 Dead、发布 UnitDeath；死亡回调结束后，各 Handler 只清理自己不应跨死亡保留的临时状态，然后更新非英雄管理关系并注销 AIController。UnitDeath Reaction 新增的 CombatRequest 仍可继续进入当前 Tick 的 Combat Settlement Cycle。

MinionSystem、JungleCamp 和 AIController 只处理自己的管理关系或决策状态，不负责清理 Buff、控制、技能、装备或 Modifier。普通死亡禁止全量调用 StatHandler.ClearModifiers 或 CombatModifierSet.Clear；具体来源只通过自己保存的 Handle 移除应结束的 Modifier。完整清理只发生在 UnitWorld 的 ResetForPool、新 RuntimeUid 初始化或永久销毁阶段。

UnitEventBus 仍是 Unit 内部固定的即时强类型 Handler 路由。非英雄 AI 不动态订阅 UnitEventBus，也不增加 GameplayEventQueue、AttackStarted、AttackCommitted 或 AttackHit。

需要唤醒 AI 的正式敌对行为，通过固定业务入口直接通知 UnitWorld 或对应营地。该通知只记录确定性事实并提前下一次 AI 决策，不在通知函数中递归启动行为。

---

### 定位与成员

UnitWorld 继续承担单位框架 v27 已确定的同步生成、注册、LifeState 转换、死亡表现、回池、销毁和英雄复活。本设计只补充非英雄业务对象与 AIController 注册表的使用方式。

~~~text
UnitWorld
    MinionSystem MinionSystem                     【需要快照】
    UnitAIControllerRegistry AIControllers        【需要快照】
    List<JungleCamp> JungleCamps                  【场景稳定引用】
~~~

AIControllers 是唯一的 AI 调度列表，可以同时包含：

- MinionAIController
- MonsterAIController
- TowerAIController
- 将来确实需要自主决策的召唤物控制器

UnitAIControllerRegistry 是 UnitUid → UnitAIController 的唯一通用映射权威，并按 OwnerUnitUid 稳定遍历。任何 Dictionary 枚举顺序都不能用于 Gameplay 决策。Controller 能否在当前 Tick 主动执行，由注册关系、Owner 是否存在、LifeState 和 UnitUid.SpawnLogicTick 共同推导，不保存额外的启用 Tick。

### AIController 的创建和注销

| 控制器 | 创建者 | 注销时机 |
|---|---|---|
| MinionAIController | MinionSystem 在同步生成 Unit 后创建 | 小兵正式进入 Dead、MinionSystem 注销管理关系之后 |
| MonsterAIController | JungleCamp 在同步生成成员后创建 | 野怪正式进入 Dead 并更新营地槽位之后，或营地规则清场时 |
| TowerAIController | 地图装配流程在塔 Unit 初始化后创建 | 防御塔正式进入 Dead 后 |

AIController 不保存在 Unit 字段中。它是读取 Unit 状态的外部顶层控制器，Unit 不反向拥有或驱动 AIController。

生成方只决定具体控制器类型并保存自己的业务单位 UID：

- MinionSystem 保存自己管理的小兵 UnitUid，不保存 UnitUid → Controller 映射。
- JungleCamp 通过 MemberUidsBySlot 保存本营地成员 UnitUid，不保存 Controller 映射。
- 地图装配创建并注册 TowerAIController，但不长期保存 Controller，也不为此建立 TowerList。
- 所有 UnitUid → Controller 查询统一进入 UnitWorld。

UnitWorld 提供最小入口：

~~~csharp
public bool RegisterAIController(
    UnitUid ownerUnitUid,
    UnitAIController controller);

public bool UnregisterAIController(
    UnitUid ownerUnitUid);

public bool TryGetAIController(
    UnitUid ownerUnitUid,
    out UnitAIController controller);

public void TickAIControllers();
~~~

注册表按 OwnerUnitUid 维持稳定顺序，避免每 Tick 全量排序。Gameplay Pipeline 约束注册与注销发生在 AIControllers 遍历之外；若实现阶段违反该约束，应产生确定性错误，而不是依靠容器偶然行为继续运行。

非英雄正式死亡时，UnitWorld 在注销 Controller 之前调用单位框架已经冻结的非英雄管理接缝：

~~~text
小兵
    → MinionSystem.UnregisterManagedUnit(UnitUid)

普通野怪
    → 由 MonsterAIController 的 CampId / CampSlotIndex
      定位 JungleCamp.OnMemberDeath(UnitUid)

防御塔
    → 当前模块没有防御塔管理列表，不更新不存在的关系

最后
    → UnitWorld.UnregisterAIController(UnitUid)
~~~

这条路由复用 UnitWorld 已有的 UnitUid → UnitAIController 关系，不增加 UnitUid → MinionSystem、UnitUid → JungleCamp 或防御塔专用映射。若未来存在独立地图目标系统，由该系统维护并更新自己的建筑业务关系，不属于本模块。

### 同步生成与下一 Tick AI 生效

所有小兵和野怪都调用单位框架 v27 的同步接口：

~~~csharp
UnitUid unitUid =
    unitWorld.SpawnUnit(request);

bool exists =
    unitWorld.TryGetUnit(unitUid, out Unit unit);
// 此处必须为 true。
~~~

UnitSpawnRequest 不携带 SpawnLogicTick。UnitWorld 在函数内部读取 SimulationTickContext.Current.Tick，并在当前 Tick 的全单位共享空间中分配 byte SpawnSequenceInTick。

同步生成完成后：

1. Unit 已完成新运行时初始化。
2. UnitUid 已确定。
3. Unit 已注册到 UnitRegistry 和物理实体注册表。
4. 调用方立即得到 UnitUid，并可立即查询 Unit。
5. 调用方可以立即创建、配置并注册对应 AIController。

不采用：

- SubmitSpawnRequest
- PendingSpawnQueue
- FlushSpawnRequests
- PendingActivation
- Unit 实体的下一 Tick 激活

Unit 实体没有延迟生成，但生成 Tick 内禁止主动 AI、主动 Order、Planner、ActionRuntime、普通主动移动、普通攻击和主动技能推进。新注册 AIController 从下一 LogicTick 开始执行；生成方不能通过提前下达初始 Order 绕过该边界。

这个限制直接由以下条件推导，不增加快照字段：

~~~text
CanRunActiveGameplayThisTick
    = SimulationTickContext.Current.Tick
      > UnitUid.SpawnLogicTick
~~~

生成 Tick 内，Unit 已经存在，可以被查询、成为目标、参与碰撞、受到伤害、治疗、Buff 和控制，并接收被动结果事件。

### 稳定 AI 调度

TickAIControllers 不接收 logicTick 或 SimulationTickContext 参数：

~~~csharp
public void TickAIControllers()
{
    for (int i = 0; i < _aiRegistry.Count; i++)
    {
        ref AIControllerRegistryEntry entry =
            ref _aiRegistry.GetEntry(i);

        UnitUid ownerUnitUid =
            entry.OwnerUnitUid;

        if (!_unitRegistry.TryGet(
                ownerUnitUid,
                out Unit owner) ||
            owner.LifeState != LifeState.Alive ||
            !owner.CanRunActiveGameplayThisTick)
        {
            continue;
        }

        entry.Controller.TickLogic();
    }
}
~~~

执行顺序只取决于 OwnerUnitUid。Unit.CanRunActiveGameplayThisTick 在属性内部读取 SimulationTickContext.Current.Tick，并判断其是否大于 UnitUid.SpawnLogicTick；AIController 接口不接收 SimulationTickContext 参数，也不保存第二套 AI Enabled 状态。控制器内部需要继续错峰决策时，可以根据 OwnerUnitUid 计算稳定初始相位，但不能使用 UnityEngine.Random、对象哈希值或容器枚举位置。

建议的相对 Gameplay 顺序：

~~~mermaid
flowchart TD
    A["波次与营地生成"] --> B["注册新 AIController"]
    B --> C["按 SpawnLogicTick 与 LifeState 过滤 AI"]
    C --> D["Unit 行为规划与仲裁"]
    D --> E["移动、攻击与投掷物"]
    E --> F["Combat Settlement Cycle"]
    F --> G["同步调用 UnitWorld 写入 Dying / Dead"]
    G --> H["UnitDeath Reaction 回到当前 Combat 循环"]
    H --> F
    G --> I["清理临时状态并注销管理关系与 AIController"]
    I --> J["Combat 后仅处理死亡表现、回池、销毁与废墟"]
~~~

UnitWorld 接受正式死亡判决并写入 Dead 后，先同步发布 UnitDeath。死亡 Reaction 与 Handler 临时状态清理完成后，再通知 MinionSystem 或 JungleCamp 更新管理关系，最后注销 AIController。实体可以继续保留到死亡动画结束，但动画结束、表现对象消失和 Transform 状态都不能作为 Gameplay 死亡依据。

### AI 业务通知

AIController 不订阅动态委托。对 AI 有意义的确定性事实，使用固定直接函数路由：

~~~text
正式 Gameplay 结果生产者
    → UnitWorld 的明确通知入口
    → 按 UnitUid 或空间范围定位相关业务对象
    → Controller / JungleCamp 只记录事实并提前决策
~~~

这类入口只覆盖已经存在的业务需求，例如：

- 敌方英雄对己方英雄实施了需要触发小兵协防的正式敌对行为。
- 普通野怪受到合法敌对行为，营地进入战斗。
- UnitWorld 已正式确认某个营地成员死亡。

通知中不启动 AttackActionRuntime，不修改 UnitIntent，不创建第二套事件队列，也不保存通用事件历史。

### 运行成本

- UnitAIControllerRegistry 使用连续稳定条目遍历。
- UnitUid 查询索引只做查找，不参与排序。
- 不使用每个控制器各自的 MonoBehaviour.Update。
- 不在 TickAIControllers 中使用 LINQ、闭包和临时集合。
- 单位已锁定合法目标时，控制器不重复执行完整空间查询。
- 完整决策按各控制器的 NextDecisionLogicTick 错峰。

这些约束直接属于 UnitWorld 的高频业务入口，不另设独立性能章节。

---

### 定位

UnitAIController 是纯 C# 抽象类。它统一所有 AI 的外部身份、Owner 解析、Order 输出和快照接口，但不统一小兵、野怪与防御塔的状态机。

单位框架 v27 已冻结统一四阶段回滚接口。UnitAIController 通过同一个强类型快照结构参与 UnitWorld 聚合：

~~~csharp
public interface IRollback<TState>
{
    void Capture(ref TState state);
    void Restore(in TState state);
    void Resolve(in RollbackContext context);
    void Rebuild(in RollbackContext context);
}

public abstract class UnitAIController
    : IRollback<UnitAIControllerSnapshot>
{
    public UnitUid OwnerUnitUid { get; protected set; }

    protected Unit Owner { get; set; }

    public abstract void TickLogic();

    public abstract void Capture(
        ref UnitAIControllerSnapshot state);

    public abstract void Restore(
        in UnitAIControllerSnapshot state);

    public abstract void Resolve(
        in RollbackContext context);

    public abstract void Rebuild(
        in RollbackContext context);
}
~~~

基类只负责：

- 保存所属单位的稳定 OwnerUnitUid。
- 解析或重新绑定可重建的 Owner 引用。
- 检查 Owner 是否仍可参与 AI 模拟。
- 提供 IssueOrderIfChanged 等少量保护级辅助函数。
- 规定统一的 Capture、Restore、Resolve、Rebuild 接缝。

基类不强制保存：

- NextDecisionLogicTick
- CurrentTargetUid
- 通用 AIState
- 通用黑板
- 通用威胁表

这些字段是否存在，由具体控制器的真实业务决定。

### 为什么采用多态快照

同一份 UnitAIControllerRegistry 中存在多种具体 Controller，但全项目不再使用 object 快照，也不拆成三份控制器注册表。因此采用一个强类型数据快照：

~~~text
UnitAIControllerSnapshot
    UnitAIControllerKind ControllerKind
    UnitUid OwnerUnitUid

    MinionAIControllerState MinionState
    MonsterAIControllerState MonsterState
    TowerAIControllerState TowerState
~~~

其中：

- ControllerKind 标识当前有效的具体分支。
- OwnerUnitUid 用于恢复 UnitWorld 的 UnitUid → Controller 注册关系。
- 具体 Controller 只读写自己的 State 分支。

不增加没有真实字段的 CommonState，也不把三种 AI 合并为通用运行状态机。

聚合关系：

~~~mermaid
flowchart TD
    A["UnitWorld 初始化空快照"] --> B["Controller Capture 对应分支"]
    B --> C["保存 OwnerUnitUid 与真实 Controller 状态"]
    D["UnitWorld 恢复注册关系"] --> E["Controller Restore 对应分支"]
    E --> F["Resolve 与 Rebuild"]
~~~

ControllerKind 只属于快照分支标识，不是 AIController 的可变 Gameplay 决策状态。AI 主动生效时间由 OwnerUnitUid.SpawnLogicTick 推导，不属于 Controller 或注册表状态。

### 子类实现规则

以小兵控制器为例：

~~~csharp
public sealed class MinionAIController
    : UnitAIController
{
    public override void Capture(
        ref UnitAIControllerSnapshot state)
    {
        state.ControllerKind =
            UnitAIControllerKind.Minion;
        state.OwnerUnitUid = OwnerUnitUid;
        state.MinionState =
            new MinionAIControllerState
        {
            // 只保存本控制器权威维护的运行状态。
        };
    }

    public override void Restore(
        in UnitAIControllerSnapshot state)
    {
        if (state.ControllerKind !=
            UnitAIControllerKind.Minion)
        {
            throw new DeterministicRollbackException();
        }

        OwnerUnitUid = state.OwnerUnitUid;
        // 恢复 state.MinionState。
    }
}
~~~

MonsterAIController 和 TowerAIController 分别读写 MonsterState 与 TowerState。Restore 必须验证 ControllerKind，错误分支属于确定性恢复错误，不能静默忽略。

四个阶段的职责：

| 阶段 | AIController 职责 |
|---|---|
| Capture | 写入 OwnerUnitUid、ControllerKind 和自己的状态分支 |
| Restore | 直接恢复历史稳定字段，不查询外部对象 |
| Resolve | 通过 OwnerUnitUid、CampId、HomeLaneId 等恢复引用 |
| Rebuild | 清理或重建查询缓冲、调试缓存等派生内容 |

UnitWorld 负责聚合全部 Controller，并捕获和恢复注册顺序、UnitUid → Controller 映射以及 Controller 自身真实存在的运行状态。

每次 Capture 前由 UnitWorld 把 UnitAIControllerSnapshot 初始化为 default，未使用的状态分支必须保持默认值，不能残留上一帧或另一个 Controller 的数据。UnitWorld 不根据 UnitKind 猜测 Controller 类型。

### 快照字段权威

AIController 的具体 State 分支只能保存该控制器权威维护且会影响未来决策的字段。

不得重复保存：

| 已有权威 | 不在 AI State 复制的内容 |
|---|---|
| Unit | LifeState、CapabilityState、Intent |
| UnitActionStateView | 当前主行为、阶段、FocusTarget |
| MovementHandler / UnitLocomotionAgent | 路径、移动执行和强制位移状态 |
| AttackHandler | 当前攻击、攻击计时和攻击序列 |
| ProjectileWorld | 投掷物位置、命中和生命周期 |
| JungleCamp | 营地主目标、成员和刷新状态 |

恢复顺序由帧同步设计负责，但恢复完成后，UnitAIController 必须通过 OwnerUnitUid 重新解析 Owner；任何 Unit、JungleCamp、Projectile 或查询缓冲引用都属于可重建引用。

### 输入、输出与决策频率

AIController 可以读取：

| 输入 | 用途 |
|---|---|
| Unit | LifeState、CapabilityState、Intent、ActionStateView、Handler 只读状态 |
| PhysicsEntity2D | 逻辑位置、朝向、形状和 Bounds |
| UnitWorld | 通过 UnitUid 解析目标 |
| 空间查询服务 | 获取有限范围内的候选 |
| 所属业务对象 | 小兵读取兵线，野怪读取 JungleCamp |

数值读取统一调用 Owner.StatHandler.GetStat。AIController 不缓存 AttackRange、AttackSpeed 或 MoveSpeed 作为第二套权威，也不创建或保存 StatModifierHandle；数值 Modifier、StatSeq 和句柄生命周期完全属于单位框架 v27 的 StatHandler 与实际效果来源。

唯一业务输出是单位框架已有 Order。AI Order 是本地确定性 Gameplay 输入，不是玩家 Command，也不进入网络输入队列。

控制器只在以下情况重新下达 Order：

- 目标发生变化。
- 当前 Order 不再表达正确意图。
- 当前行为被拒绝且不能继续。
- 需要在推进、交战、回线或回营之间切换。

相同 Order 不应每 Tick 重复提交。

每个具体控制器可以保存自己的 NextDecisionLogicTick，并在 TickLogic 内读取 currentLogicTick。正式业务通知只允许把下一次决策提前，不在通知调用栈里直接执行完整决策。

---

### 核心类关系

~~~mermaid
classDiagram
class UnitWorld {
  MinionSystem MinionSystem
  UnitAIControllerRegistry AIControllers
  RegisterAIController()
  UnregisterAIController()
  TickAIControllers()
}

class IRollback {
  Capture()
  Restore()
  Resolve()
  Rebuild()
}

class UnitAIController {
  UnitUid OwnerUnitUid
  TickLogic()
  Capture()
  Restore()
  Resolve()
  Rebuild()
}

class MinionAIController
class MonsterAIController
class TowerAIController
class JungleCamp
class TowerAttackHandler

IRollback <|.. UnitAIController
UnitAIController <|-- MinionAIController
UnitAIController <|-- MonsterAIController
UnitAIController <|-- TowerAIController
UnitWorld *-- MinionSystem
UnitWorld o-- UnitAIController
UnitWorld o-- JungleCamp
~~~

塔的攻击与表现关系：

~~~mermaid
flowchart TD
    A["TowerAIController 选择目标"] --> B["Unit 行为链"]
    B --> C["TowerAttackHandler"]
    C --> D["ProjectileWorld"]
    C --> E["只读锁定状态"]
    E --> F["TowerTargetLinePresenter"]
~~~

### 最终职责摘要

~~~text
UnitWorld
    同步生成和处置 Unit。
    唯一正式写入 LifeState。
    唯一维护 UnitUid → UnitAIController 注册关系。
    在正式死亡链中同步更新非英雄管理关系并注销 Controller。
    由 UnitUid.SpawnLogicTick 推导新生单位主动生效时间。
    稳定调度所有 UnitAIController。

UnitAIController
    位于 Unit 之上读取状态并下达 Order。
    通过统一接口多态捕获和恢复子类自己的状态。

MinionSystem
    决定何时、在哪条兵线、按什么稳定顺序生成哪些小兵。
    只保存自己管理的小兵 UnitUid，不保存 Controller 映射。

MinionAIController
    决定单个小兵推进、攻击、追击或回线。

JungleCamp
    表示地图上的普通野怪营地。
    维护主野怪、成员、战斗、脱战和刷新。

MonsterAIController
    决定单只普通野怪待机、攻击或回营。

TowerAIController
    按固定六级优先级选择下一名攻击目标。

TowerAttackHandler
    实现塔的具体攻击。
    保证上一发塔弹结束以前不能开始下一次攻击。

TowerTargetLinePresenter
    只表现当前逻辑锁定关系。
~~~

### 明确不存在的重复权威

| 状态或规则 | 唯一权威 |
|---|---|
| Unit LifeState 与生命周期处置 | UnitWorld |
| UnitUid 与 SpawnSequenceInTick | UnitWorld |
| UnitUid → UnitAIController | UnitWorld.UnitAIControllerRegistry |
| 新生单位主动 Gameplay 生效条件 | UnitUid.SpawnLogicTick 与当前 LogicTick 的派生结果 |
| 世界级小兵波次日程 | MinionSystem |
| MinionSystem 管理的小兵 UID 集合 | MinionSystem |
| 单只小兵决策 | MinionAIController |
| 普通营地成员、主目标和刷新 | JungleCamp |
| 单只野怪行动 | MonsterAIController |
| 防御塔下一目标 | TowerAIController |
| 防御塔攻击阶段与锁定目标 | TowerAttackHandler |
| 塔弹生命周期 | ProjectileWorld |
| 红线视觉对象 | TowerTargetLinePresenter |

最终不建立：

- Unit.AIController 字段。
- IUnitWorldSubsystem。
- JungleCampSystem。
- TowerSystem、TowerManager 或防御塔专用列表。
- EpicMonsterSpawner 或统一史诗营地基类。
- 每波小兵长期运行对象。
- AI 公共运行状态机或作为运行时决策字段的 ControllerKind。
- 通用无限容量威胁表。
- GameplayEventQueue。
- 第二套移动、攻击或投掷物系统。

### 玩家路径

```text
物理输入
    -> 该技能的 InputMappingTemplate
    -> 施法意图（Proposal：信号意图 / 仅本地 Aim / 无动作）
    -> 单位 Planner / Arbiter 裁断（Unit Framework v27.3 §3）
    -> Rejected：不产生 Command
    -> Accepted / Interrupt：翻译为技能层语言
    -> AbilitySignalVerb + Aim
    -> CastAbilityCommand
```

执行期（有 Planner 的单位）：

```text
CastAbilityCommand
    -> UnitIntent(CastAbility)
    -> 行为链（Planner）产出 ActionRequest
    -> Arbiter 裁断
    -> AbilityHandler.HandleSignal(AbilitySignal)
```

无 Planner 的单位走既有直通路径：

```text
CastAbilityCommand
    -> AbilityHandler.HandleSignal(AbilitySignal)
```

### AI 路径

```text
AIController
    -> 读取 AbilityDef / CastModelDef / AbilityRuntime
    -> 根据 AI 决策生成 AbilityAction
    -> AbilityHandler
    -> AbilitySignal
```

AI 不需要：

```text
模拟按键按住。
模拟鼠标左键。
模拟技能键松开。
经过玩家 Command Request。
增加通用 AbilityControlOrder 中间层。
读取玩家输入映射模板。
```

例如 AI 使用蓄力技能：

```text
决定开始蓄力
    -> AbilityAction(Focus)

后续 Tick 决定释放
    -> AbilityAction(Commit, AimSnapshot)

生命周期或 AI 决策需要取消
    -> AbilityAction(Cancel)
```

AI 直接使用技能系统已有语言。

### 不强制新增 AI 快照结构

本设计不要求技能系统额外增加：

```text
AIAbilityPlanSnapshot
通用 AI 技能协议
AI 输入状态
```

具体 AI 若需要跨 Tick 保存“计划何时释放”，由其现有 Behavior / AIController Runtime 自行保存。

AbilitySession 的：

```text
FocusLogicTick
当前 Stage
Blackboard
```

继续由 AbilityHandler 自己快照，AI 不复制。

---


## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：AI 直接使用已有意图与技能语言，不模拟物理输入。

legacyDecision：D-018

