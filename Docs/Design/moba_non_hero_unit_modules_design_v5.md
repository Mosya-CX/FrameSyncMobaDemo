# Unity 帧同步 MOBA 非英雄单位模块程序设计案 v5

> 适用范围：类英雄联盟 MOBA 的小兵、普通野怪营地与防御塔。  
> 对接基线：单位行为框架 v27、寻路与移动系统当前公开接缝、当前攻击模块公开接缝、投掷物系统当前公开接缝。  
> 本文负责 Gameplay 业务结构，不规定网络协议、快照序列化格式和回滚存储介质。

字段标记：

| 标记 | 含义 |
|---|---|
| 【静态配置】 | 对局开始前确定，运行时只读 |
| 【需要快照】 | 会影响后续 Gameplay，帧同步设计师需要纳入状态保存 |
| 【可重建】 | 恢复后可由稳定身份或其它权威状态重新建立 |
| 【单 Tick 临时】 | 不允许跨 LogicTick 留存 |

本文遵循统一时间命名：

| 语义 | 命名 |
|---|---|
| 当前逻辑帧局部变量 | currentLogicTick |
| 一个确定的逻辑帧时间点 | ...LogicTick |
| 一段逻辑帧时长 | ...Ticks |
| 下一次执行时间 | Next...LogicTick |

所有 Gameplay 方法需要当前时间时，统一在函数内部读取：

~~~csharp
int currentLogicTick =
    SimulationTickContext.Current.Tick;
~~~

SimulationTickContext 不作为普通参数层层传递，也不能被缓存成第二套当前 Tick 权威。

---

## 目录

1. 总体结构与职责边界
2. UnitWorld：单位生命周期入口与 AIController 统一调度
3. UnitAIController：抽象决策层与多态快照
4. MinionSystem：小兵波次生成
5. MinionAIController：兵线推进与小兵 AI
6. JungleCamp：普通野怪营地与刷新
7. MonsterAIController：野怪战斗、追击与回营
8. TowerAIController：防御塔索敌
9. TowerAttackHandler：防御塔攻击周期与红线
10. 最终类关系与权威边界

---

# 1. 总体结构与职责边界

## 1.1 总体对象关系

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

## 1.2 核心类的脚本角色

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

## 1.3 三个业务模块的边界

| 模块 | 负责 | 不负责 |
|---|---|---|
| 小兵 | 波次日程、组成展开、稳定生成、兵线归属、推进、选敌、追击与回线 | 流场构建、A*、RVO、攻击前后摇、投掷物、伤害公式 |
| 普通野怪 | 营地场景数据、主野怪、成员关系、战斗状态、脱战回营、刷新倒计时 | 史诗野怪规则、攻击命中、治疗公式、死亡奖励 |
| 防御塔 | 固定优先级索敌、不追击攻击指令、塔弹门控、红线逻辑来源 | 塔专用总控、移动、投掷物推进、伤害公式、废墟生成 |

## 1.4 所有 AI 都复用单位行为链

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

## 1.5 生命周期和事件边界

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

# 2. UnitWorld：单位生命周期入口与 AIController 统一调度

## 2.1 定位与成员

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

## 2.2 AIController 的创建和注销

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

## 2.3 同步生成与下一 Tick AI 生效

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

## 2.4 稳定 AI 调度

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

## 2.5 AI 业务通知

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

## 2.6 运行成本

- UnitAIControllerRegistry 使用连续稳定条目遍历。
- UnitUid 查询索引只做查找，不参与排序。
- 不使用每个控制器各自的 MonoBehaviour.Update。
- 不在 TickAIControllers 中使用 LINQ、闭包和临时集合。
- 单位已锁定合法目标时，控制器不重复执行完整空间查询。
- 完整决策按各控制器的 NextDecisionLogicTick 错峰。

这些约束直接属于 UnitWorld 的高频业务入口，不另设独立性能章节。

---

# 3. UnitAIController：抽象决策层与多态快照

## 3.1 定位

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

## 3.2 为什么采用多态快照

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

## 3.3 子类实现规则

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

## 3.4 快照字段权威

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

## 3.5 输入、输出与决策频率

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

# 4. MinionSystem：小兵波次生成

## 4.1 定位与成员

MinionSystem 是 UnitWorld 直接持有的纯 C# 业务对象。它负责世界级波次日程、波次组成展开和稳定同步生成，但不拥有生成后小兵的移动、攻击或长期群体状态。

~~~text
MinionSystem
    MinionWaveSchedule Schedule                     【静态配置】
    LaneRuntimeData[] Lanes                         【静态配置】

    int WaveIndex                                   【需要快照】
    int NextWaveLogicTick                           【需要快照】
    List<MinionSpawnTicket> PendingTickets          【需要快照或确定性重建】
    int NextTicketCursor                            【需要快照】
    List<UnitUid> ManagedMinionUids                 【需要快照】
~~~

MinionSystem 不维护：

- UnitUid → MinionAIController 映射。
- 每一波的长期 Wave 实例。
- 波次队长。
- 整波共享目标。
- 阵型状态机。

波次生成结束后，每个小兵由自己的 MinionAIController 决策；单位之间的拥堵、避让和路线选择交给移动系统。

ManagedMinionUids 只保存 MinionSystem 当前管理的小兵身份，用于本模块自己的单位管理和生命周期清理。Controller 解析统一调用 UnitWorld.TryGetAIController。小兵正式进入 Dead 后，UnitWorld 直接通知 MinionSystem 注销对应 UnitUid。

本模块交给帧同步设计的完整状态结构为：

~~~text
MinionSystemSnapshot
    int WaveIndex
    int NextWaveLogicTick
    MinionSpawnTicket[] PendingTickets
    int NextTicketCursor
    UnitUid[] ManagedMinionUids
~~~

Schedule、Lanes 和波次组成是静态配置；Controller 状态由 UnitWorld 聚合，不复制到 MinionSystemSnapshot。

## 4.2 波次配置

波次日程适合使用 ScriptableObject 编辑，并在对局初始化时解析为只读确定性数据：

~~~text
MinionWaveSchedule                                【静态配置】
    int FirstWaveLogicTick
    int WaveIntervalTicks
    MinionWavePhase[] Phases

MinionWavePhase                                   【静态配置】
    int StartWaveIndex
    MinionWaveComposition[] CompositionCycle

MinionWaveComposition                             【静态配置】
    MinionWaveMember[] Members

MinionWaveMember                                  【静态配置】
    int UnitPrototypeId
    int Count
    int FirstSpawnOffsetTicks
    int SpawnStepTicks
    int FormationGroup
~~~

Inspector 可以使用秒配置首次出兵、波次间隔和成员间隔；初始化时统一换算为 LogicTick/Ticks，运行时不读取 Time.time 或浮点秒累计。

Phase 与 CompositionCycle 用于表达：

- 不同时间阶段的炮车波频率。
- 中后期波次组成变化。
- 特殊模式替换波次。
- 比赛规则向指定队伍和兵线追加超级兵。

MinionSystem 只读取比赛规则已经给出的确定性波次修饰结果，不自行扫描兵营、防御塔或表现对象推导超级兵条件。

## 4.3 兵线场景数据

兵线是地图结构，使用 LaneAuthoring 直接编辑：

~~~text
LaneAuthoring : MonoBehaviour
    ushort LaneId
    TeamSpawnAuthoring[] TeamSpawns
    Transform[] CenterlinePoints
    float CorridorHalfWidth
~~~

对局初始化后得到只读数据：

~~~text
LaneRuntimeData                                   【静态配置】
    ushort LaneId
    LaneTeamSpawnData[] TeamSpawns
    fp2[] CenterlinePoints
    fp CorridorHalfWidth
~~~

CenterlinePoints 只用于：

- 标识 HomeLane。
- 判断是否追击过远。
- 计算最近回线点。
- 地图编辑可视化。

正常推进仍读取移动系统已有的队伍流场。兵线中心线不重新生成第二套路径，也不直接传给 MovementHandler。

## 4.4 生成票据

~~~text
MinionSpawnTicket
    int SpawnLogicTick                            【需要快照或确定性重建】
    TeamId TeamId                                 【需要快照或确定性重建】
    ushort LaneId                                 【需要快照或确定性重建】
    int UnitPrototypeId                           【需要快照或确定性重建】
    int StableEntryIndex                          【需要快照或确定性重建】
    fp2 SpawnPosition                             【需要快照或确定性重建】
    fp2 SpawnForward                              【需要快照或确定性重建】
~~~

票据只表达某个波次成员何时调用同步 SpawnUnit。StableEntryIndex 是波次展开后的稳定排序位置，不是单位生成序号。

同 Tick 票据使用固定比较键：

~~~text
SpawnLogicTick
TeamId
LaneId
StableEntryIndex
~~~

UnitUid 仍由 UnitWorld 根据当前 LogicTick、RuntimeEntityPrefabId 和全单位共享的 byte SpawnSequenceInTick 构造。MinionSystem 不维护自己的帧内生成序列。

## 4.5 核心波次算法

波次算法只包含两个步骤：到期时展开波次，随后按稳定顺序同步生成到期成员。

~~~text
TickWave:
    currentLogicTick = SimulationTickContext.Current.Tick

    当 currentLogicTick 已到达 NextWaveLogicTick:
        根据 WaveIndex 选择当前 Phase 与 Composition
        按 TeamId、LaneId 和成员配置顺序展开票据
        WaveIndex 增加
        NextWaveLogicTick 增加 WaveIntervalTicks

    从 NextTicketCursor 开始:
        依次处理 SpawnLogicTick 不晚于 currentLogicTick 的票据
        每张票据同步生成 Unit 并注册 MinionAIController
        推进 NextTicketCursor
~~~

若一次逻辑推进跨过多个波次时间点，使用循环补齐所有到期波次，不能只生成最后一波。Composition 的选择规则为：

~~~text
选择 StartWaveIndex 不大于 WaveIndex 的最后一个 Phase
cycleIndex = Phase 内波次偏移 mod CompositionCycle.Length
~~~

这是本模块唯一需要完整伪代码说明的核心算法。具体分钟数、兵种比例和超级兵条件保留在配置与比赛规则中。

## 4.6 单个小兵的同步生成

~~~mermaid
flowchart TD
    A["到期 MinionSpawnTicket"] --> B["UnitWorld.SpawnUnit"]
    B --> C["立即取得 UnitUid"]
    C --> D["保存 ManagedMinionUids"]
    D --> E["创建并配置 MinionAIController"]
    E --> F["UnitWorld 注册 Controller"]
    F --> G["下一 LogicTick 开始 AI"]
~~~

MinionSystem 只保存管理所需的 UnitUid，不保存 Controller 引用映射。近战兵、远程兵、炮车兵和超级兵的数值、攻击方式与投掷物定义来自 UnitPrototype 和既有 Handler 装配。

## 4.7 正式死亡与管理注销

小兵正式进入 Dead 后，UnitWorld 在注销 MinionAIController 之前同步调用：

~~~csharp
public bool UnregisterManagedUnit(
    UnitUid unitUid);
~~~

MinionSystem 只从 ManagedMinionUids 中注销这一个 UID，不处理 LifeState、死亡奖励、Handler、Modifier、死亡表现或对象池。第一版直接稳定扫描 UID 数组，找到后写入无效 UID 作为墓碑；重复通知返回 false，不重复修改状态。墓碑在固定维护阶段批量压缩，避免每次死亡都移动后续元素。

这项注销在 UnitDeath 回调及死亡临时状态清理之后、UnitWorld.UnregisterAIController 之前完成。死亡动画结束与最终回池不会再次改变 MinionSystem 的管理关系。

## 4.8 性能与恢复关注点

- PendingTickets 保持有序并用游标消费，不在每 Tick 重排全部票据。
- 已消费票据只在达到容量阈值时批量压缩。
- 波次配置初始化后冻结，不在运行时使用 LINQ 展开。
- ManagedMinionUids 只保存 UID，不复制 Unit 或 Controller 状态。
- UID 注销采用稳定扫描、墓碑和延迟批量压缩，不为此新增 UnitUid → Index 权威映射。
- 对象池预热和复用由 UnitWorld 负责。
- WaveIndex、NextWaveLogicTick、ManagedMinionUids 和无法从日程唯一重建的未消费票据会影响未来生成与管理，必须进入快照。
- 恢复后 UnitWorld 仍是 SpawnSequenceInTick 的唯一权威，MinionSystem 不能根据票据自行恢复序列计数。

---

# 5. MinionAIController：兵线推进与小兵 AI

## 5.1 定位与状态

MinionAIController 决定单个小兵推进、索敌、追击或回线。它不移动 Unit，也不直接调用 AttackHandler 启动攻击。

~~~text
MinionAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    ushort HomeLaneId                             【需要快照】
    MinionAIState State                           【需要快照】
    int NextDecisionLogicTick                     【需要快照】
    int TargetLockUntilLogicTick                  【需要快照】
    fp2 EngageOrigin                              【需要快照】
    UnitUid PendingAssistTargetUid                【需要快照】
    int PendingAssistExpireLogicTick              【需要快照】
    Unit Owner                                    【可重建】
    UnitQueryBuffer CandidateBuffer               【可重用，可重建】
~~~

~~~text
MinionAIState
    AdvanceLane
    EngageTarget
    ReturnToLane
~~~

~~~text
MinionAIProfile                                   【静态配置】
    int DecisionIntervalTicks
    int TargetLockTicks
    int AssistAggroDurationTicks
    fp AcquireRadiusSq
    fp MaxChaseFromEngageOriginSq
    fp MaxDistanceFromHomeLaneSq
~~~

Profile 只保存 AI 参数，不复制攻击距离、移动速度、攻击前摇或目标是否可选中。这些继续读取 Unit 的数值和 Handler 公开状态。

~~~mermaid
stateDiagram-v2
    [*] --> AdvanceLane
    AdvanceLane --> EngageTarget: 选择目标
    EngageTarget --> ReturnToLane: 目标失效或追击越界
    ReturnToLane --> AdvanceLane: 回到兵线
    ReturnToLane --> EngageTarget: 协防或发现目标
~~~

当前正式攻击目标已经由 Unit Intent、ActionStateView 和 AttackHandler 表达，MinionAIController 不保存一份长期 CurrentTargetUid。PendingAssistTargetUid 只表示尚待下一次决策消费的协防事实。

## 5.2 推进和回线

AdvanceLane 状态下，控制器保证 Unit 当前 Order 表达沿 HomeLane 推进。移动系统根据该语义选择队伍流场和局部避障，AI 不传递 FlowFieldId、AStar 标志或 RVO 开关。

当小兵因击退、恐惧或其它控制偏离兵线时，控制和强制位移先按既有行为仲裁执行。重新具备普通行动能力后，AI 根据逻辑位置决定继续推进还是前往最近回线点。

回线点由 LaneRuntimeData.CenterlinePoints 计算。AI 只提交目的位置，实际使用 A* 还是 Direct 仍由 UnitLocomotionAgent 决定。

## 5.3 目标选择

默认优先级：

| 优先级 | 候选 |
|---:|---|
| 0 | 有效的英雄协防目标 |
| 1 | 当前行为正在攻击且仍合法的目标 |
| 2 | 敌方小兵 |
| 3 | 敌方英雄或召唤物 |
| 4 | 当前允许攻击的敌方建筑 |

同一优先级的稳定比较键：

~~~text
PriorityBand 升序
DistanceSq 升序
UnitUid 升序
~~~

当前目标仍合法且 TargetLockUntilLogicTick 未到时直接保持。锁定到期并不表示必须换目标，只有严格更高优先级的候选才能抢占；同优先级的微小距离变化不会造成频繁切换。

统一合法性过滤至少检查：

- 目标可以由 UnitWorld 解析。
- 目标 LifeState 允许被选中。
- CapabilityState.IsTargetable 为 true。
- 阵营关系满足当前规则。
- 目标位于索敌和追击边界内。
- 当前地图规则允许小兵攻击该单位分类。

## 5.4 英雄协防

小兵协防只响应已经由 Gameplay 正式确认的敌对行为，不读取动画、特效或玩家输入。

固定业务通知到达后，MinionAIController 检查：

- 攻击者是敌方英雄。
- 受害者是己方英雄。
- 攻击者和受害者位于该小兵协防范围内。
- 攻击者仍合法且没有越过硬追击边界。

满足条件时更新 PendingAssistTargetUid，并把 NextDecisionLogicTick 提前到当前或下一次允许决策的 LogicTick。通知函数只记录事实，不在回调中直接创建攻击 Runtime。

同一 Tick 收到多个协防候选时，不引入事件序列号。控制器用固定比较键选出唯一候选：

~~~text
DistanceSq 升序
AttackerUnitUid 升序
~~~

这个选择与通知调用顺序无关。

## 5.5 追击边界

从推进转入交战时记录 EngageOrigin。继续追击需要同时满足：

- 与 EngageOrigin 的平方距离不超过最大追击距离。
- 与 HomeLane 中心线的平方距离不超过兵线追击宽度。
- 目标没有进入禁止小兵追击的地图区域。

超出限制后，控制器清除不再正确的攻击 Order，切换 ReturnToLane，并下达前往最近回线点的移动 Order。进入兵线走廊后恢复 AdvanceLane。

## 5.6 核心决策算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果 Owner 不存在、不能参与模拟或暂时不能普通决策:
        返回

    如果未到 NextDecisionLogicTick 且没有协防唤醒:
        返回

    如果存在未过期且合法的协防目标:
        必要时下达 AttackOrder
        State = EngageTarget
        记录 EngageOrigin
        安排下一次决策
        返回

    如果 Unit 当前攻击目标仍合法且没有越过追击边界:
        保持当前 Order
        安排下一次决策
        返回

    在有限范围内按稳定比较键选择新目标
    找到目标:
        下达 AttackOrder
        State = EngageTarget
        记录 EngageOrigin
    否则当前位置偏离 HomeLane:
        下达回线 MoveOrder
        State = ReturnToLane
    否则:
        下达 LaneAdvanceOrder
        State = AdvanceLane

    安排下一次决策
~~~

## 5.7 多态快照实现

MinionAIController 的 Capture 只写 UnitAIControllerSnapshot.MinionState，Restore 只读取该分支并验证 ControllerKind。

Owner、LaneRuntimeData 引用和 CandidateBuffer 不进入状态分支。Resolve 通过 OwnerUnitUid 和 HomeLaneId 重新关联；Rebuild 只清理或重建 CandidateBuffer 等派生缓存。

## 5.8 性能约束

- 完整索敌只在决策时间或协防唤醒后执行。
- 当前目标合法时不申请候选缓冲。
- 使用 fp 平方距离，不开平方。
- CandidateBuffer 复用，不创建临时 List。
- 不复制移动路径或 FlowField 状态。
- 空闲小兵的初始决策相位由 OwnerUnitUid 稳定错开。

---

# 6. JungleCamp：普通野怪营地与刷新

## 6.1 定位

JungleCamp 表示地图中的一处普通野怪营地，实现为场景 MonoBehaviour：

~~~csharp
public sealed class JungleCamp : MonoBehaviour
~~~

它直接承载场景编辑字段和营地运行状态，不再额外建立 JungleCampBakeData、静态营地配置类或 GlobalGameplayData 中的营地副本。

JungleCamp 不使用 Update。开局时按 CampId 注册给 UnitWorld，随后由固定 Gameplay Pipeline 调用 TickLogic；函数内部自行读取 SimulationTickContext.Current.Tick。

## 6.2 Inspector 字段与确定性初始化

~~~text
JungleCamp
    ushort CampId                                 【静态配置】
    TeamId CampTeamId                             【静态配置】
    Transform CampAnchor                          【场景引用】
    float InitialSpawnSeconds                     【Inspector 配置】
    float RespawnDelaySeconds                     【Inspector 配置】
    float SoftLeashRadius                         【Inspector 配置】
    float HardLeashRadius                         【Inspector 配置】
    float DisengageDelaySeconds                   【Inspector 配置】
    byte MainMonsterSlotIndex                     【静态配置】
    JungleCampSpawnSlot[] SpawnSlots              【静态配置】

JungleCampSpawnSlot
    byte SlotIndex
    int UnitPrototypeId
    Transform SpawnPoint
~~~

初始化时，JungleCamp 自己把场景字段转换为内部确定性值：

~~~text
CampAnchorPosition : fp2
InitialSpawnLogicTick : int
RespawnDelayTicks : int
SoftLeashRadiusSq : fp
HardLeashRadiusSq : fp
DisengageDelayTicks : int
SpawnPositionBySlot : fp2[]
SpawnForwardBySlot : fp2[]
~~~

这些值仍属于同一个 JungleCamp，不生成额外 BakeData 层。逻辑 Tick 不再读取 Transform.position 或浮点秒累计。

CampId 在地图内唯一，SpawnSlots 按 SlotIndex 稳定排序。MainMonsterSlotIndex 必须由设计者明确指定，不能根据体型、血量、Prototype 名称或 UnitSubKindId 临时猜测。

## 6.3 营地运行状态

~~~text
JungleCampState
    Dormant
    Idle
    InCombat
    Returning
    WaitingRespawn
~~~

~~~text
JungleCamp Runtime
    JungleCampState State                         【需要快照】
    UnitUid[] MemberUidsBySlot                    【需要快照】
    bool[] MemberAliveBySlot                      【需要快照】
    bool MainMonsterDead                          【需要快照】
    UnitUid PrimaryTargetUid                      【需要快照】
    int LastHostileActionLogicTick                【需要快照】
    int NextRespawnLogicTick                      【需要快照】
    int ResetBeginLogicTick                       【需要快照】
~~~

Unit、MonsterAIController 和 Transform 引用都属于可重建引用。

本营地交给帧同步设计的完整状态结构为：

~~~text
JungleCampSnapshot
    ushort CampId
    JungleCampState State
    UnitUid[] MemberUidsBySlot
    bool[] MemberAliveBySlot
    bool MainMonsterDead
    UnitUid PrimaryTargetUid
    int LastHostileActionLogicTick
    int NextRespawnLogicTick
    int ResetBeginLogicTick
~~~

不保存 CombatGroupState。普通营地的共同作战关系已经由固定成员槽位、State 和 PrimaryTargetUid 表达，不再增加 CombatGroup 类或第二套共享目标状态。

~~~mermaid
stateDiagram-v2
    [*] --> Dormant
    Dormant --> Idle: 首次生成
    Idle --> InCombat: 合法敌对行为
    InCombat --> Returning: 主怪存活且脱战
    Returning --> Idle: 全部成员回营
    Returning --> InCombat: 再次合法开战
    InCombat --> WaitingRespawn: 主怪已死且战斗结束
    WaitingRespawn --> Idle: 整营重新生成
~~~

## 6.4 初次生成

到达 InitialSpawnLogicTick 后，JungleCamp 按 SlotIndex 升序：

1. 调用 UnitWorld.SpawnUnit 同步生成成员。
2. 立即取得 UnitUid。
3. 创建绑定 CampId 与 SlotIndex 的 MonsterAIController。
4. 注册到统一 AIControllers。
5. 写入 MemberUidsBySlot 和 MemberAliveBySlot。
6. 清理旧主目标、主怪死亡标记和刷新时间。
7. 进入 Idle。

同 Tick 生成多个营地时，UnitWorld 按 CampId 升序推进 JungleCamp；每个营地再按 SlotIndex 升序调用 SpawnUnit。UnitWorld 仍统一分配 SpawnSequenceInTick。

## 6.5 战斗状态和共享目标

任一存活成员主动发现目标或收到合法敌对行为后，先调用所属 JungleCamp 的明确入口。营地验证目标后：

- 设置 PrimaryTargetUid。
- 更新 LastHostileActionLogicTick。
- 进入 InCombat。
- 把全部存活 MonsterAIController 的下一次决策提前。

普通营地第一版只维护一个共享主目标，不建立无限容量仇恨表。这样既避免每只野怪重复扫描，也避免成员因通知顺序立即分散。

脱战条件由营地统一判断，例如：

- 任一存活成员越过 HardLeashRadius。
- 当前目标失效且超过 DisengageDelayTicks。
- 所有合法目标离开 SoftLeashRadius。
- 地图规则明确要求重置营地。

主野怪仍存活时，营地进入 Returning。存活成员分别由 MonsterAIController 下达 ReturnToCampOrder。生命恢复、Buff 清理或控制清理必须走既有正式系统，JungleCamp 不能直接写 CurrentHealth 或 Handler 内部字段。

## 6.6 主野怪死亡与刷新

普通营地开始刷新倒计时的必要且充分业务条件是：

~~~text
MainMonsterDead == true
并且
State != InCombat
~~~

主野怪在战斗中死亡时，只记录 MainMonsterDead，不立即计时。剩余小怪可以继续战斗。等营地正式退出战斗后，再清理残余成员并开始整营刷新。

成员死亡由 UnitWorld 在正式写入 LifeState.Dead、发布 UnitDeath 并完成死亡临时状态清理后直接通知 JungleCamp。营地只更新自己的成员槽位和刷新业务状态，不参与死亡判定，也不清理该 Unit 的 Handler 或 Modifier。OnMemberDeath 返回后，UnitWorld 再注销对应 MonsterAIController。

核心算法：

~~~text
OnMemberDeath:
    根据死亡 UnitUid 找到 SlotIndex
    MemberAliveBySlot[SlotIndex] = false

    如果是 MainMonsterSlotIndex:
        MainMonsterDead = true

    如果没有存活成员:
        结束当前战斗关系

    尝试启动刷新倒计时

TryStartRespawnCountdown:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果 MainMonsterDead 为 false:
        返回

    如果 State 为 InCombat:
        返回

    请求 UnitWorld 按非死亡规则清场仍存活的次要成员
    清理成员槽位
    NextRespawnLogicTick = currentLogicTick + RespawnDelayTicks
    State = WaitingRespawn
~~~

主野怪死亡后清理的存活次要成员属于营地规则清场，不属于正式死亡：不写入 Dead，不发布 UnitDying、UnitDeath 或 UnitKill，也不产生击杀奖励和死亡 Reaction。具体非死亡处置入口、Gameplay 停用及最终回池由 UnitWorld 负责，本模块只提交受影响的成员 UID 并清理自己的槽位，不定义第二套处置流程。

WaitingRespawn 状态到达 NextRespawnLogicTick 后，重新同步生成全部槽位。新一代成员获得新的 UnitUid，不进入 Respawning，也不会与上一代残余小怪重叠。

## 6.7 边界情况

| 情况 | 结果 |
|---|---|
| 只击杀次要野怪，主野怪存活 | 不启动刷新 |
| 主野怪死亡，营地仍在战斗 | 记录死亡，暂不计时 |
| 主野怪死亡，战斗随后结束 | 清理剩余次要成员并开始刷新 |
| 全部成员死亡 | 营地立即具备非战斗条件并开始刷新 |
| 主怪存活时营地脱战 | 正常 Returning，不刷新 |
| WaitingRespawn 期间再次收到旧成员通知 | 通过代际 UID 和槽位当前 UID 校验后忽略 |

最后一条避免对象池中的旧 Unit 或延迟业务通知污染新一代营地状态。

## 6.8 史诗野怪边界

JungleCamp 只实现普通营地规则。它不负责：

- 大龙地图生成与复活规则。
- 小龙元素轮换、龙魂或远古龙。
- 厄塔汗形态、出生区域和地图改造。
- 先锋等拥有独立阶段的地图机制。

未来需要某一种史诗野怪时，单独实现该机制并接入 UnitWorld，不预先建立 EpicMonsterSpawner 或 EpicMonsterCampBase。

## 6.9 性能与恢复关注点

- JungleCamp 数量有限，由 UnitWorld 按 CampId 稳定推进。
- 营地成员按固定槽位数组保存，不使用运行时 HashSet。
- 共享 PrimaryTargetUid，避免所有成员重复完整索敌。
- 距离判断使用 fp 平方距离。
- State、成员 UID、成员存活标记、主怪死亡标记、主目标和所有未来 LogicTick 会影响刷新与战斗，必须进入快照。
- 场景 Transform 和 MonsterAIController 引用不进入快照，恢复后按 CampId、SlotIndex 和 UnitUid 重建。

---

# 7. MonsterAIController：野怪战斗、追击与回营

## 7.1 定位与状态

MonsterAIController 负责单只普通野怪的行动决策。成员关系、共享目标、主怪死亡和刷新计时属于 JungleCamp。

~~~text
MonsterAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    ushort CampId                                 【需要快照】
    byte CampSlotIndex                            【需要快照】
    MonsterAIState State                          【需要快照】
    int NextDecisionLogicTick                     【需要快照】
    Unit Owner                                    【可重建】
    JungleCamp Camp                               【可重建】
~~~

~~~text
MonsterAIState
    CampIdle
    EngageTarget
    ReturnToCamp
~~~

~~~text
MonsterAIProfile                                  【静态配置】
    int DecisionIntervalTicks
    fp AggroRadiusSq
    fp ReturnArriveDistanceSq
~~~

牵引半径、脱战延迟和出生位置属于 JungleCamp，不在每个控制器中复制。PrimaryTargetUid 也只由 JungleCamp 保存。

## 7.2 待机和开战

CampIdle 时不下达无意义的原地移动 Order。到达决策时间后，主动作战型野怪可以在营地锚点附近做有限范围扫描。

发现目标后，MonsterAIController 不私自进入战斗，而是向 JungleCamp 提交该候选。JungleCamp 验证并决定是否让整营进入 InCombat，再统一唤醒成员。

普通野怪默认不随机巡逻。确实需要巡逻的特定野怪可以配置固定巡逻点，但不为所有营地增加随机漫游状态和随机种子。

## 7.3 交战

营地处于 InCombat 时，控制器读取 JungleCamp.PrimaryTargetUid：

- 目标合法时，必要时下达 AttackOrder。
- Planner 根据攻击距离决定原地攻击或追击。
- 目标失效时，通知 JungleCamp 重新判断主目标或脱战。

控制器不复制营地主目标，也不自行选择与营地不同的普通目标。

追击继续复用：

~~~text
AttackOrder
    → AttackTarget Intent
    → ChaseForAttack
    → MovementHandler
    → UnitLocomotionAgent
    → AStar 或 Direct
~~~

## 7.4 回营

营地进入 Returning 后，MonsterAIController：

1. 清除已经不正确的攻击 Order。
2. 下达目标为自身出生槽位的 ReturnToCampOrder。
3. 在营地允许前不重新主动索敌。
4. 到达槽位后清除移动 Order，并通知 JungleCamp 当前成员已归位。

~~~mermaid
stateDiagram-v2
    [*] --> CampIdle
    CampIdle --> EngageTarget: 营地进入战斗
    EngageTarget --> ReturnToCamp: 营地开始重置
    ReturnToCamp --> EngageTarget: 营地允许重新开战
    ReturnToCamp --> CampIdle: 营地重置完成
~~~

ReturnToCampOrder 只表达目的，不携带 AStar、Direct 或重寻路参数。控制效果和强制位移仍由 ActionArbiter 与移动系统按既有优先级处理。

## 7.5 核心决策算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick
    解析 Owner 与 JungleCamp

    如果 Owner 不存在或不能参与模拟:
        返回

    如果 Camp 为 Dormant 或 WaitingRespawn:
        清除不再正确的 Order
        返回

    如果 Camp 为 Returning:
        保证当前为 ReturnToCampOrder
        返回

    如果 Camp 为 InCombat:
        PrimaryTarget 合法:
            保证当前为 AttackOrder
        否则:
            请求 Camp 判断脱战
        返回

    如果 Camp 为 Idle 且到达 NextDecisionLogicTick:
        执行有限主动索敌
        找到候选时交给 Camp 验证
        安排下一次决策
~~~

## 7.6 技能型野怪

拥有技能的野怪继续通过 Unit 已装配的 AbilityHandler 执行技能。MonsterAIController 可以按确定性条件下达既有施法 Order，但不复制技能 Stage、冷却、前摇或命中状态。

行为差异显著的野怪可以派生专用 MonsterAIController。普通野怪和 Boss 行为不能全部塞入一个枚举与巨型 switch。

## 7.7 多态快照与性能

MonsterState 只保存 CampId、CampSlotIndex、State 和 NextDecisionLogicTick。Owner 与 Camp 引用在 Resolve 中恢复，营地主目标继续从 JungleCamp 读取；Rebuild 不生成新的权威状态。

- Idle 主动扫描按 OwnerUnitUid 稳定错峰。
- InCombat 成员复用营地主目标，不重复全量扫描。
- Returning 只检查与出生槽位的平方距离。
- 不维护动态威胁字典或通用行为树。

---

# 8. TowerAIController：防御塔索敌

## 8.1 定位与状态

TowerAIController 位于塔 Unit 之上，只负责选择目标并下达不允许追击的 AttackOrder。

~~~text
TowerAIController
    UnitUid OwnerUnitUid                          【基类稳定身份】
    int NextDecisionLogicTick                     【需要快照】
    TowerAIProfile Profile                        【静态配置】
    Unit Owner                                    【可重建】
    UnitQueryBuffer CandidateBuffer               【可重用，可重建】
~~~

~~~text
TowerAIProfile
    int DecisionIntervalTicks                     【静态配置】
~~~

防御塔攻击距离、攻击速度、前后摇和投掷物定义来自 Unit 数值与 TowerAttackHandler，不在 TowerAIProfile 中复制。

TowerAIController 与所有其它控制器一起注册到 UnitWorld.AIControllers，不建立 TowerList。它不处理：

- 攻击计时和攻击序列。
- 投掷物生成、移动、命中与回收。
- 红线 LineRenderer。
- 防御塔死亡、处置和废墟生成。

防御塔正式进入 Dead 后，UnitWorld 直接注销 TowerAIController，并根据 UnitDisposePolicy 处理死亡表现、销毁和塔废墟。当前模块没有需要同步更新的 TowerSystem、TowerManager 或防御塔 UID 列表；未来若存在独立地图目标系统，只由该系统维护自己的建筑业务关系。

## 8.2 固定索敌优先级

防御塔索敌顺序固定为：

| 优先级 | 目标 |
|---:|---|
| 0 | 正在攻击己方英雄的敌方英雄 |
| 1 | 敌方召唤物 |
| 2 | 敌方炮车兵或超级兵 |
| 3 | 敌方近战兵 |
| 4 | 敌方远程兵 |
| 5 | 最近的敌方英雄 |

同一优先级使用：

~~~text
DistanceSq 升序
UnitUid 升序
~~~

完整比较键为：

~~~text
(PriorityBand, DistanceSq, UnitUid)
~~~

召唤物、炮车兵、超级兵、近战兵和远程兵通过项目稳定的 UnitKind 与 UnitSubKindId 映射，不使用 Unity Tag、对象名称或表现 Prefab 判断。

## 8.3 判断敌方英雄是否正在攻击己方英雄

最高优先级是当前状态查询，不依赖 AttackStarted、AttackCommitted 或 AttackHit 事件。

对攻击范围内的敌方英雄候选，读取：

~~~text
candidate.ActionStateView.MainKind
candidate.ActionStateView.FocusTarget
~~~

满足以下条件时归入优先级 0：

1. MainKind == ActionKind.Attack。
2. FocusTarget 可以解析为有效 Unit。
3. FocusTarget 是防御塔一方的英雄。
4. 候选英雄仍是合法塔目标。

UnitActionStateView 只负责暴露当前行为，不允许 TowerAIController 反向修改。AI 仍通过 AttackOrder 改变自己所属塔的行为。

不保存 PendingProtectionTargetUid，也不增加保护仇恨持续时间。需求表达的是正在攻击，而不是曾经攻击；英雄停止攻击己方英雄后，下次完整索敌时自然回到其普通优先级。

## 8.4 合法目标过滤

进入优先级比较前统一检查：

- UnitWorld 可以解析目标。
- LifeState 允许目标参加战斗。
- CapabilityState.IsTargetable 为 true。
- 双方阵营敌对。
- 目标位于 TowerAttackHandler 当前攻击距离内。
- 地图规则没有禁止该目标被塔攻击。
- 目标属于六个允许优先级类别之一。

攻击距离以 AttackHandler 和数值系统为权威。若需要避免边界处反复进入和离开，可以配置很小的退出滞后距离，但不能在 TowerAIProfile 再复制完整攻击距离。

## 8.5 与在途炮弹的目标锁定

TowerAIController 读取 TowerAttackHandler.HasUnresolvedProjectile。

为 true 时：

- 不重新选择目标。
- 不清除当前锁定目标。
- 不下达新的 AttackOrder。
- 不执行完整候选扫描。

上一发炮弹进入终止状态后，下一次决策重新按完整六级优先级选择目标。这样 TowerAIController、TowerAttackHandler、红线和塔弹始终围绕同一个锁定目标，不会各自指向不同单位。

## 8.6 核心索敌算法

~~~text
TickLogic:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果塔不存在、不能参与模拟或不能攻击:
        清除不再正确的 AttackOrder
        返回

    如果 TowerAttackHandler.HasUnresolvedProjectile:
        返回

    如果尚未到 NextDecisionLogicTick 且当前 Order 仍正确:
        返回

    查询攻击范围内的合法候选
    对每个候选计算 PriorityBand
    选择 (PriorityBand, DistanceSq, UnitUid) 最小者

    找到目标:
        必要时下达不允许追击的 AttackOrder
    没有目标:
        清除攻击 Order

    安排下一次决策
~~~

## 8.7 不允许追击

塔的 AttackOrder 必须表达 allowChase = false。目标离开攻击范围后，Planner 只能等待或清除攻击意图，不能创建 MoveActionRequest。

塔 Unit 不需要为了复用行为链装配空 MovementHandler。它仍可拥有 BehaviorPlanner、ActionArbiter 与 TowerAttackHandler，但 CapabilityState 不提供普通移动能力。

## 8.8 多态快照与性能

TowerState 只保存真正由控制器维护的 NextDecisionLogicTick。Owner 和 TowerAttackHandler 引用在 Resolve 中恢复。当前锁定目标和攻击阶段属于 TowerAttackHandler，不复制到 AI State。

- 炮弹未结束时跳过候选查询。
- CandidateBuffer 复用，不使用 LINQ 排序。
- 单次遍历直接维护当前最佳比较键。
- 使用 fp 平方距离。
- 同优先级最终由 UnitUid 打破平局。

---

# 9. TowerAttackHandler：防御塔攻击周期与红线

## 9.1 定位和边界

防御塔具有一条特殊攻击规则：

~~~text
上一发塔弹命中锁定目标，或者因目标失效、超时等规则正式结束以前，
不能开始下一次攻击。
~~~

TowerAttackHandler 是 AttackHandler 的防御塔具体实现：

~~~csharp
public sealed class TowerAttackHandler
    : AttackHandler
~~~

它实现当前攻击模块已经公开的接缝：

~~~csharp
public override AttackPlanStatus GetAttackPlanStatus(
    UnitUid targetUid);

public override void BeginAttack(
    UnitUid targetUid);

public override bool CommitAttack();

public override void CancelBeforeCommit();

public override void ResetAttackTimer(
    AttackTimerResetReason reason);
~~~

这些函数不接收 logicTick 或 SimulationTickContext。需要当前时间时，在函数内部读取 SimulationTickContext.Current.Tick。

本设计不要求公共攻击模块：

- 新增防御塔专用字段。
- 新增防御塔专用 AttackPlanStatus。
- 修改 AttackSequenceIndex 规则。
- 增加 AttackStarted、AttackCommitted 或 AttackHit 事件。
- 改变普通攻击 Handler 的投掷物实现。

TowerAttackHandler 只是现有抽象攻击能力的一种具体实现。

## 9.2 权威运行状态

TowerAttackHandler 在当前攻击模块规定的普通攻击运行状态之外，只增加：

~~~text
ProjectileUid LastCommittedProjectileUid         【需要快照】
~~~

普通攻击目标、攻击阶段、计时器、是否已 Commit 和 AttackSequenceIndex 继续由攻击模块当前设计负责。本文不重新定义，也不在 TowerAIController 中保存副本。

TowerAttackHandler 不保存：

- Projectile 对象引用。
- 投掷物位置和运动状态副本。
- 投掷物命中结果副本。
- ProjectileEndReason 历史。
- 动态投掷物结束委托。

上一发炮弹是否未结束通过 ProjectileWorld 查询：

~~~text
projectileState = ProjectileWorld.GetState(
    LastCommittedProjectileUid)

HasUnresolvedProjectile =
    projectileState == Pending
    或 projectileState == Active
~~~

当前投掷物系统把从未存在和已经结束都表示为 Missing。LastCommittedProjectileUid 只在 RequestSpawn 成功并返回有效 UID 后写入，因此该字段查询到 Missing 时，可以确定上一发已不再处于待生成或飞行状态。

## 9.3 攻击规划和开始

GetAttackPlanStatus 首先执行塔的普通攻击合法性判断：

- 目标存在且可选中。
- 目标仍在当前攻击范围内。
- 塔当前具有攻击能力。
- 当前攻击计时允许开始。

上述条件满足但 HasUnresolvedProjectile 为 true 时，返回攻击模块已有的等待就绪状态，不新增塔专用枚举。

BeginAttack 再次检查在途炮弹门控，防止调用者绕过 Planner。门控关闭时不能覆盖旧锁定目标或启动新的攻击前摇。

CancelBeforeCommit 只取消尚未正式 Commit 的本次攻击，并按当前攻击模块规则清理目标和阶段。已经生成的上一发塔弹不因新一次前摇取消而被修改。

ResetAttackTimer 只遵循攻击模块已有的计时重置语义。即使普通攻击计时被重置，只要上一发投掷物仍是 Pending 或 Active，塔仍不能开始下一次攻击。

## 9.4 下一次攻击门控

下一次塔攻击同时受到普通攻击计时和上一发炮弹状态约束：

~~~text
CanBeginNextTowerAttack:
    currentLogicTick = SimulationTickContext.Current.Tick

    如果当前攻击模块判断普通攻击尚未 Ready:
        返回 false

    查询 LastCommittedProjectileUid
    如果投掷物为 Pending 或 Active:
        返回 false

    返回 true
~~~

| 情况 | 是否可开始下一次攻击 |
|---|---|
| 炮弹已结束，但普通攻击尚未 Ready | 否 |
| 普通攻击已 Ready，但炮弹仍 Pending | 否 |
| 普通攻击已 Ready，但炮弹仍 Active | 否 |
| 炮弹 Missing，普通攻击也 Ready | 是 |
| 普攻计时被重置，但炮弹仍 Active | 否 |

目标死亡、投掷物超时或规则取消会让投掷物正式结束并变为 Missing，此时解除门控，避免防御塔永久停火。

## 9.5 CommitAttack 的塔实现

TowerAttackHandler 在自己的 CommitAttack 中完成塔弹生成。它不修改 AttackHandler 基类，也不要求公共攻击模块额外返回 ProjectileUid。

核心流程：

~~~text
CommitAttack:
    currentLogicTick = SimulationTickContext.Current.Tick

    按当前攻击模块规则检查本次攻击能否 Commit
    重新解析并验证锁定目标
    如果目标无效或超出攻击范围:
        按 Commit 前失败规则取消
        返回 false

    按既有规则立即逻辑转向目标
    构造锁定该目标的 ProjectileSpawnRequest
    projectileUid = ProjectileWorld.RequestSpawn(request)

    如果 projectileUid 无效:
        返回 false

    LastCommittedProjectileUid = projectileUid
    完成当前攻击模块规定的成功 Commit 状态更新
    返回 true
~~~

RequestSpawn 同步返回预分配的 ProjectileUid，但投掷物可能仍处于 Pending，所以门控必须同时检查 Pending 与 Active。

塔弹推荐规则：

~~~text
TargetUnitUid = 当前锁定目标
运动方式 = 确定性跟踪目标
目标过滤 = 只能命中锁定 TargetUnitUid
SameTarget = Once
EndOnFirstValidHit = true
~~~

路径上的其它单位不能替锁定目标承受塔弹。

## 9.6 红线逻辑状态

红线表达防御塔当前 `AttackTarget` 意图，而不是塔弹轨迹，也不是上一发塔弹的历史锁定目标。红线只由客户端表现层读取 Gameplay 的只读当前意图，不向 Gameplay 回写，也不进入快照或校验和。

表现结果：

- 当前 `AttackTarget` 合法且存活时显示红线，与攻击前摇、后摇和塔弹飞行进度无关。
- AI 把 `AttackTarget` 替换为下一目标时，红线在同一表现帧直接切换到新目标。
- 当前目标死亡、不可选中、意图被清除且没有后续目标时，红线立即停止渲染。
- 不允许回退读取 `TowerAttackHandler.LockedTargetUid`；该字段可能描述已经结束的历史塔弹。

## 9.7 TowerTargetLinePresenter

红线由塔预制体上的表现组件负责：

~~~csharp
public sealed class TowerTargetLinePresenter
    : MonoBehaviour
~~~

它读取：

- 防御塔发射端表现挂点。
- 塔 Unit 当前只读 `AttackTarget` 意图。
- 当前意图对应且仍存活、可选中的敌方单位表现挂点。

Presenter 负责启用、更新和关闭红色 LineRenderer。`UNITY_SERVER` 构建不创建 LineRenderer；客户端 Presenter 不拥有 Gameplay 目标，不向 AI 或 AttackHandler 回写状态，也不进入快照。

线段端点可以在渲染帧平滑跟随模型；目标 UID 的替换直接衔接新端点。是否应该显示红线仍由只读当前意图和目标合法性决定，不能由 Animator Event、本地特效状态或历史塔弹锁定决定。

## 9.8 恢复与性能关注点

- 每座塔只额外保存一个 LastCommittedProjectileUid。
- ProjectileWorld.GetState 必须是 UID 索引查询，不能扫描全部活跃投掷物。
- HasUnresolvedProjectile 是派生值，不额外保存 bool。
- LastCommittedProjectileUid 必须与恢复后的 Pending 或 Active 投掷物状态对应，具体恢复顺序由帧同步设计负责。
- TowerTargetLinePresenter 恢复后重新读取逻辑状态，不保存 LineRenderer 进度。

---

# 10. 最终类关系与权威边界

## 10.1 核心类关系

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

## 10.2 最终职责摘要

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

## 10.3 明确不存在的重复权威

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
