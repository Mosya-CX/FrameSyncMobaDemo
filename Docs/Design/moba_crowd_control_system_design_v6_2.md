# Unity 帧同步 MOBA 通用控制系统程序设计案 v6.2

> 核心组件：CrowdControlHandler。  
> 适配：单位行为框架 v26、现行技能、Buff、装备、战斗系统，以及单位寻路与移动系统 v11。  
> 设计目标：用少量可组合模块覆盖大多数 MOBA 控制效果；运行时无 Kind 分支、无控制 Buff 依赖、无每实例模块对象。  
> 范围：只设计控制实例、控制模块、动作限制、参数、标签、免疫、不可阻挡、净化、轻量信号、强制行为与强制位移接入。回滚部分只冻结统一接口语义和需要保存的数据，不规定序列化实现。
> 玩法语义参考：[英雄联盟中文 Wiki：群体控制](https://leagueoflegends.fandom.com/zh/wiki/%E7%BE%A4%E4%BD%93%E6%8E%A7%E5%88%B6)。

> v6.2 小版本：对齐第五轮固定复活接缝；CrowdControlHandler 只清理自身生命阶段状态，跨死亡保留的外部来源在各自的复活逻辑中重新注册 Handle，临时来源不恢复。  
> v6.1 小版本：明确控制免疫与不可阻挡句柄只属于当前生命阶段；死亡清理后，跨死亡保留的外部来源必须在复活阶段重新注册。

---

## 目录

1. [零、最终架构决定](#零最终架构决定)
2. [一、CrowdControlHandler：唯一运行时入口](#一crowdcontrolhandler唯一运行时入口)
3. [二、CrowdControlDefinition：唯一静态配置](#二crowdcontroldefinition唯一静态配置)
4. [三、CrowdControlModule：可组合模块执行表](#三crowdcontrolmodule可组合模块执行表)
5. [四、CrowdControlInstance 与 Key 参数黑板](#四crowdcontrolinstance-与-key-参数黑板)
6. [五、CrowdControlTagMask：轻量标签系统](#五crowdcontroltagmask轻量标签系统)
7. [六、控制免疫、不可阻挡与净化](#六控制免疫不可阻挡与净化)
8. [七、控制汇总结果与多实例叠加](#七控制汇总结果与多实例叠加)
9. [八、直接 Handler 接入与现行系统边界](#八直接-handler-接入与现行系统边界)
10. [九、常见控制的模块组合](#九常见控制的模块组合)
11. [十、配置精度、运行时约束与帧同步关注数据](#十配置精度运行时约束与帧同步关注数据)
12. [十一、控制系统最终结构](#十一控制系统最终结构)

---

# 零、最终架构决定

## 0.1 一句话结构

~~~mermaid
flowchart TD
    A["外部效果生效点"] --> B["目标 Unit.CrowdControlHandler.Add"]
    B --> C["全局控制配置表"]
    C --> D["模块执行表"]
    D --> E["CrowdControlInstance"]
    E --> F["CrowdControlHandler 汇总"]
    F --> G["单位框架动作限制"]
~~~

核心所有权：

| 内容 | 拥有者 |
|---|---|
| 所有控制静态配置 | 全局 Gameplay 配置单例 |
| 控制实例与持续时间 | 目标 CrowdControlHandler |
| 控制免疫、不可阻挡与信号缓存 | 目标 CrowdControlHandler |
| 唯一生效的强制位移控制 | 目标 CrowdControlHandler |
| 模块函数注册表 | 全局只读模块执行表 |
| 单位动作中断 | 单位框架 / ActionArbiter |
| 已获准强制位移的轨迹执行 | MovementHandler |
| 伤害、治疗、护盾 | CombatSystem |
| Buff 生命周期 | BuffHandler |

CrowdControlHandler 不缓存 Database，不持有 BuffHandler，不保存技能或装备来源描述。

## 0.2 本版删除的结构

本版正式删除：

- CrowdControlDefinition.Kind；
- CrowdControlDefinition.Icon；
- CrowdControlDefinition.CategoryFlags；
- CrowdControlDefinition.Aggregation；
- CrowdControlInterruptMask；
- BlockMask 与 FineBlockMask 两套含义相近的字段；
- CrowdControlPort；
- CrowdControlApplyRequest；
- CrowdControlInstance.SourceType；
- CrowdControlInstance.SourceConfigId；
- CrowdControlInstance.MergeKey；
- 相同来源自动刷新或合并；
- AddDefault、ResetDuration、ExtendDuration；
- CrowdControlDefinition.DefaultDurationSeconds；
- 独立 CrowdControlRuntimeDef；
- 由调用方手工选择的固定参数槽；
- 向 Handler 接口传递 SimulationTickContext；
- 通用信号实例、SignalId、信号 Payload 与逐实例信号游标；
- 强制位移经过 ActionArbiter 的转发路径；
- 单位框架中的不可阻挡控制权威；
- 控制系统内的伤害、治疗、护盾模块；
- 控制系统自己的调试、表现和完整快照设计章节。

## 0.3 ADR-01：如何实现可配置控制

### Decision

控制效果使用“全局静态定义 + 编译后的模块操作数组 + 静态执行函数表”。

### Context

需要同时满足：

- 不按 Kind 写大型 switch 或 if-else；
- 大多数控制通过配置组合完成；
- 新增特殊控制时不修改 Handler；
- 不为每个实例创建一组模块对象；
- Unity Inspector 可以编辑；
- 运行时只使用 fp 和整数帧。

### Options considered

| 方案 | 配置能力 | 热路径成本 | 主要问题 |
|---|---|---:|---|
| Handler 按 Kind 分支 | 低 | 最低 | Handler 持续膨胀，组合效果困难 |
| 每个 Definition 持有多态模块对象 | 高 | 虚调用与对象访问 | SerializeReference、AOT 和实例组织更复杂 |
| 模块 ID + 静态执行表 | 高 | 数组索引 + 静态委托 | 需要一次构建期编译 |
| 完整脚本虚拟机 | 最高 | 较高 | 对数量有限的控制明显过度设计 |

### Chosen option

模块 ID + 静态执行表。

### Why this option won

- Handler 完全不认识 Stun、Slow、Taunt 等具体类型；
- 标准模块可以自由组合；
- 新模块只注册一次执行函数；
- Definition 自身保存已 Bake 的紧凑操作数组；
- 实例只保存 ID、时间和 Key 参数块；
- 不需要反射、LINQ、每实例委托或模块对象。

### Consequences

- 编辑器配置需要经过 Bake 转为运行时数据；
- 真正全新的机制仍要编写一个模块执行函数；
- 模块必须遵守控制系统的窄能力边界。

### Revisit triggers

只有当控制配置开始需要循环、条件树、复杂局部变量或数百种动态脚本时，才重新评估小型虚拟机。

## 0.4 ADR-02：控制是否进入 CombatSystem 管线

### Decision

外部效果在自己的确定性生效点直接调用目标 Unit 的 CrowdControlHandler，不给 CombatSystem 增加控制队列。

### Context

战斗系统 v8 明确使用三条强类型队列处理：

- DamageRequest；
- HealRequest；
- ShieldRequest。

控制不修改生命、治疗量或护盾值，也经常需要立即影响同一帧后续动作。

### Options considered

| 方案 | 优点 | 代价 |
|---|---|---|
| CombatSystem 增加 ControlQueue | 可共享 CombatSeq | 扩大 CombatSystem 职责，增加第四条队列和延迟状态 |
| 新建全局 CrowdControlQueue | 可集中排序 | 多一层缓存、序号、清理与帧同步关注数据 |
| 直接 Unit.CrowdControlHandler.Add | 最短、立即、容易追踪 | 依赖调用点本身已有确定性顺序 |

### Chosen option

直接调用 Handler。

### Why this option won

技能 Stage、攻击 Impact、装备触发和 DamageResult 回调本身已经在确定性顺序中执行。再进入一个控制队列不会增加正确性，只会增加一次延迟和一套状态。

### Consequences

- 调用点必须是正式 Gameplay 生效点；
- 同一生效点包含伤害与控制时，效果定义必须明确先后顺序；
- DamageResult 成功后才生效的控制，在结果回调中直接 Add。

### Revisit triggers

只有当 Gameplay 改为多线程并行写 Unit，或跨线程不允许立即修改目标 Handler 时，再引入专用 ControlCommandBuffer。

## 0.5 控制系统的硬边界

控制模块允许：

- 汇总被禁止的单位动作；
- 汇总状态标签；
- 汇总减速、攻速降低、视野限制等控制数值；
- 提供一个强制行为候选，供 BehaviorPlanner 生成 MoveActionRequest 或 AttackActionRequest 并交由 ActionArbiter 仲裁；
- 由 Handler 向 MovementHandler 提交已经仲裁通过的强制位移；
- 在自然到期或收到控制信号时添加、移除控制。

控制模块禁止：

- 创建 DamageRequest；
- 创建 HealRequest；
- 创建 ShieldRequest；
- 直接修改生命、资源、护盾或属性；
- 直接改写 ActionRuntimeSet；
- 直接写 PhysicsEntity2D.Transform；
- 创建或删除 Buff；
- 播放动画、音效和 VFX。

如果一个技能同时造成伤害和眩晕，它是两个明确步骤：

~~~text
CombatSystem 结算伤害
CrowdControlHandler 添加眩晕
~~~

不是“眩晕模块顺便造成伤害”。

---

# 一、CrowdControlHandler：唯一运行时入口

## 1.1 Unity 角色

CrowdControlHandler 继承单位框架的 `UnitHandler`。`UnitHandler` 本身继承 MonoBehaviour，因此 Handler 仍然是挂在 Unit 对象上的 Unity 组件。

它使用 MonoBehaviour 的原因：

- 与 Unit 预制体装配一致；
- 可直接由 Unit 缓存；
- 生命周期随 Unit；
- 方便确认单位是否具备控制管理能力。

它不使用：

- Update；
- FixedUpdate；
- Coroutine；
- Time.time；
- deltaTime；
- Invoke。

所有 Gameplay 时间直接读取全局参数 SimulationTickContext.Current.Tick。

Handler 的对外接口不接收 Tick、SimulationTickContext 或 deltaTime 参数。

## 1.2 推荐类轮廓

~~~csharp
[DisallowMultipleComponent]
public sealed class CrowdControlHandler :
    UnitHandler,
    IRollback<CrowdControlHandlerSnapshot>
{
    private readonly List<CrowdControlInstance> instances = new(8);
    private readonly List<CrowdControlImmunity> immunities = new(4);
    private readonly List<CrowdControlUnstoppable> unstoppables = new(2);
    private readonly List<ControlModuleCommand> pendingCommands = new(4);

    private CrowdControlSignalMask pendingSignals;
    private readonly int[] signalEffectiveTicks =
        new int[(int)CrowdControlSignalType.Count];

    private int nextInstanceId = 1;
    private int nextImmunityId = 1;
    private int nextUnstoppableId = 1;
    private CrowdControlHandle activeForcedMoveHandle;
    private CrowdControlStateView state;
    private CrowdControlBehaviorOverride behaviorOverride;
    private bool dirty;
    private int batchDepth;

    public CrowdControlStateView State => state;
    public int Count => instances.Count;
    public bool IsUnstoppable => unstoppables.Count != 0;

    public override void InitializeForNewRuntime();
    public override void ClearForDeath();
    public override void ClearForRespawn();
    public override void ResetForPool();

    public CrowdControlAddResult Add(
        CrowdControlId id,
        int durationTicks,
        in CrowdControlParamWriter parameters);

    public bool Remove(CrowdControlHandle handle, ControlRemoveReason reason);
    public int RemoveAll(ControlRemoveReason reason);
    public int Cleanse(in CrowdControlCleanseSpec spec);

    public CrowdControlImmunityHandle AddImmunity(
        in CrowdControlImmunitySpec spec);

    public bool RemoveImmunity(CrowdControlImmunityHandle handle);

    public CrowdControlUnstoppableHandle AddUnstoppable(
        in CrowdControlUnstoppableSpec spec);

    public bool RemoveUnstoppable(
        CrowdControlUnstoppableHandle handle);

    public void Advance();

    public void OnDamageTaken(
        in DamageTakenEvent evt);

    public void OnOwnerActionStarted();
    public void OnForcedMoveFinished(
        CrowdControlHandle sourceHandle);

    public bool HasAnyTag(CrowdControlTagMask tags);
    public bool MatchesTags(in CrowdControlTagQuery query);
    public bool TryGetBehaviorOverride(
        out CrowdControlBehaviorOverride value);

    public void Capture(
        ref CrowdControlHandlerSnapshot state);

    public void Restore(
        in CrowdControlHandlerSnapshot state);

    public void Resolve(
        in RollbackContext context);

    public void Rebuild(
        in RollbackContext context);
}
~~~

接口轮廓强调职责和数据方向，不要求逐字照抄。

Owner 由 `UnitHandler.BindOwner` 统一绑定，CrowdControlHandler 不再保存第二份所属单位字段，也不提供重复的 Initialize 接口。

## 1.3 Handler 的重要字段

| 字段 | 是否权威运行状态 | 作用 |
|---|---:|---|
| Owner | 否 | 继承自 UnitHandler 的所属 Unit 引用；不进入控制快照 |
| instances | 是 | 当前全部控制实例，按 InstanceId 升序 |
| immunities | 是 | 当前全部控制免疫规则 |
| unstoppables | 是 | 当前全部不可阻挡来源；非空即抑制控制输出 |
| pendingSignals | 是 | 尚未在 Advance 中广播的信号位 |
| signalEffectiveTicks | 是 | 每种信号最近一次发生的 EffectiveTick；保留两 Tick 语义 |
| pendingCommands | 临时 | 模块回调产生的延迟命令，防止重入修改实例列表 |
| nextInstanceId | 是 | 生成单位内唯一控制实例 ID |
| nextImmunityId | 是 | 生成单位内唯一免疫 ID |
| nextUnstoppableId | 是 | 生成单位内唯一不可阻挡来源 ID |
| activeForcedMoveHandle | 是 | 当前唯一生效的强制位移控制实例 |
| state | 可重建 | 当前轻量汇总结果 |
| behaviorOverride | 可重建 | 当前唯一强制行为结果 |
| dirty | 否 | 是否需要重新汇总 |
| batchDepth | 临时 | 批量移除期间推迟 RebuildOutputs，结束时只重算一次 |

没有：

- Database 字段；
- BuffHandler 字段；
- CombatSystem 字段；
- 按 Kind 建立的控制状态字段；
- 每种控制一张实例列表。

RebuildOutputsIfDirty 只有在 dirty = true 且 batchDepth = 0 时执行。Cleanse、RemoveAll 和模块命令批处理通过 batchDepth 保证一次批量变化只汇总一次。

## 1.4 UnitHandler 生命周期与真实单位事件

CrowdControlHandler 使用单位框架 v26 的统一生命周期，不把生命周期清理伪装成 UnitEvent Reaction。

### 新运行时初始化

~~~text
InitializeForNewRuntime():
    断言 instances、immunities、unstoppables 均为空
    nextInstanceId = 1
    nextImmunityId = 1
    nextUnstoppableId = 1
    activeForcedMoveHandle = Invalid
    pendingSignals = None
    signalEffectiveTicks 全部设为 InvalidTick
    state = Empty
    behaviorOverride = Empty
    dirty = false
~~~

只有对象池单位获得新 UnitUid、进入新的运行时生命周期时，三个 ID 才重置为 1。英雄死亡和复活期间 UnitUid 不变，因此不重置。

### 死亡、复活与回池

~~~text
ClearForDeath():
    RemoveAll(Death)

ClearForRespawn():
    RemoveAll(Respawn)
    // 只做幂等兜底清理，不重建外部来源 Handle

ResetForPool():
    RemoveAll(Despawn)
~~~

三个入口都必须可重复调用，但正式死亡流程只由 UnitWorld 在生命周期清理阶段调用一次 `ClearForDeath()`。它会清除控制实例、免疫、不可阻挡、信号和强制行为，并停止仍匹配当前来源 Handle 的强制位移。

`immunities` 与 `unstoppables` 中的全部条目都属于当前生命阶段。`ClearForDeath()` 会统一使其 Handle 失效，不提供 `SurviveDeath` 配置。即使创建它们的 Buff、技能、装备或其它来源跨死亡保留，也不得继续使用死亡前的旧 Handle。

复活时，UnitWorld 在完成复活状态初始化后按固定 Handler 顺序调用 `ClearForRespawn()`。CrowdControlHandler 的职责到清空自身残留状态为止；它不读取永久 Buff、常驻装备被动或固定技能被动，也不替这些来源重建 Handle。跨死亡保留的来源必须在各自的复活逻辑中，根据当前 Runtime 重新调用 `AddImmunity()` 或 `AddUnstoppable()`；临时来源不恢复。

唯一顺序约束是：外部来源重新注册必须晚于本单位的 `CrowdControlHandler.ClearForRespawn()`，否则随后执行的兜底清理会使新 Handle 失效。本设计只声明这一接入前置条件，不规定 UnitWorld 的完整 Handler 排序表。

CrowdControlHandler 不声明 `OnUnitDeath`，也不进入 `UnitDeathEvent` 的 UnitEventBus 路由。死亡对控制系统只有生命周期清理，没有独立 Reaction。

### SupportedUnitEvents

控制系统当前只声明一个真实单位结果事件：

| UnitEvent | 正式入口 | 真实用途 |
|---|---|---|
| DamageTaken | OnDamageTaken(in DamageTakenEvent evt) | `ActualLifeDamage > 0` 时记录 ActualDamageTaken 信号，用于解除 Sleep 等控制 |

~~~text
OnDamageTaken(evt):
    若 evt.ActualLifeDamage <= 0:
        return

    RaiseSignal(ActualDamageTaken)
~~~

事件结构只在即时入口中读取，不进入信号缓存。`OnOwnerActionStarted` 是 ActionArbiter 的直接固定调用；`OnForcedMoveFinished` 是 MovementHandler 的直接回调，二者都不进入 UnitEventBus。

## 1.5 全局配置访问

Handler 需要静态定义时，直接通过 GameplayConfig.Instance.CrowdControls.Get(controlId) 读取项目现有全局配置。

Handler 不缓存单个 Database 引用。

建议 Handler 在一次函数调用开始时把 CrowdControlDefinition 存入局部变量，避免同一调用反复访问全局属性；函数结束后不保存。

~~~mermaid
flowchart LR
    A["Handler.Add"] --> B["GameplayConfig 单例"]
    B --> C["CrowdControls.Get(id)"]
    C --> D["局部 definition"]
    D --> E["执行本次 Add"]
~~~

## 1.6 Add

### 定位

Add 表示“尝试创建一个新的控制实例”。普通控制只要通过参数、免疫和生命周期校验，就一定创建独立实例。

唯一例外是带 `ForcedMove` 标签的强制位移控制。它还要通过不可阻挡与唯一强制位移优先级仲裁；被拒绝时不创建实例。

即使以下内容完全相同：

- ControlId；
- 参数；
- 添加者；
- 持续时间；
- 同一帧；

每次成功 Add 仍然得到新的 InstanceId。

控制系统不做 Buff 式合并、叠层或来源刷新。

创建后的实例不提供持续时间改写接口。Handle 只用于确切移除和查询；同一效果再次生效时重新 Add 一个新实例。

### 输入

| 参数 | 说明 |
|---|---|
| id | 全局控制静态定义 ID |
| durationTicks | 本次基础持续 Tick；必须是运行时整数 |
| parameters | 按稳定 Key 写入的本次动态参数 |

durationTicks 可使用常量 Infinite 表示外部句柄生命周期。

### 返回

CrowdControlAddResult 是轻量值类型：

| 字段 | 作用 |
|---|---|
| Status | Added、BlockedByImmunity、RejectedByUnstoppable、RejectedByHigherPriority、InvalidDefinition、InvalidParams、InvalidDuration、OwnerRejected |
| Handle | 只有 Added 时有效 |
| BlockingImmunityId | 只有被免疫拦截时有效，用于 Gameplay 反馈或诊断 |

调用方不再用 InvalidHandle 猜测失败原因。

### 执行流程

~~~mermaid
flowchart TD
    A["Add"] --> B["读取 Tick"]
    B --> C["从全局表获取 Definition"]
    C --> D["按 Key 编译参数块"]
    D --> U{"不可阻挡且为 ForcedMove?"}
    U -->|是| RU["拒绝创建"]
    U -->|否| E{"可被控制免疫?"}
    E -->|是| F{"TagQuery 免疫匹配?"}
    E -->|否| P
    F -->|是| RI["拒绝创建"]
    F -->|否| P{"ForcedMove 优先级通过?"}
    P -->|否| RP["拒绝创建"]
    P -->|是| H["创建独立 Instance"]
    H --> I["OnAdd 执行一次"]
    I --> J["必要时原子替换旧位移"]
    J --> K["刷新汇总并返回 Handle"]
~~~

### 核心算法

~~~text
Add(id, durationTicks, parameters):
    currentTick = SimulationTickContext.Current.Tick
    def = GameplayConfig.Instance.CrowdControls.Get(id)

    若 def 不存在:
        返回 InvalidDefinition

    若 Owner 当前生命周期不接受控制:
        返回 OwnerRejected

    paramBlock =
        def.ParamLayout.Materialize(parameters)

    若 Key 不存在、类型不匹配、缺少必填 Key
       或超过固定字节容量:
        返回 InvalidParams

    isForcedMove =
        def.Tags 包含 ForcedMove

    若 IsUnstoppable 且 isForcedMove:
        返回 RejectedByUnstoppable

    若 CanBeResisted(def):
        按 Priority 降序、ImmunityId 升序检查 immunities:
            若 immunity.Query 匹配 def.Tags:
                若 immunity.RemainingBlocks > 0:
                    immunity.RemainingBlocks--
                    为 0 时移除 immunity
                返回 BlockedByImmunity(
                    immunity.ImmunityId)

    replacedForcedMoveHandle = Invalid

    若 isForcedMove
       且 activeForcedMoveHandle 有效:
        current = 按 Handle 读取当前位移控制实例
        newPriority =
            paramBlock.ReadShort(
                ForcedMovePriorityOffset)
        currentPriority =
            current.Params.ReadShort(
                ForcedMovePriorityOffset)

        若 newPriority < currentPriority:
            返回 RejectedByHigherPriority

        // 更高或相同优先级均由新实例替换。
        replacedForcedMoveHandle =
            activeForcedMoveHandle

    effectiveTicks = def.DurationExecutor(
        durationTicks,
        Owner.StatHandler.GetStat(StatId.Tenacity))

    若不是 Infinite 且 effectiveTicks <= 0:
        返回 InvalidDuration

    instance = new CrowdControlInstance(
        id = nextInstanceId++,
        controlId = id,
        startTick = currentTick,
        expireTick = Infinite
            ? Infinite
            : currentTick + effectiveTicks,
        params = paramBlock)

    instances.Add(instance)

    若 isForcedMove:
        // 丢弃旧轨迹尚未广播的完成事实，
        // 让后续完成信号只属于新的活动来源。
        ClearSignal(ForcedMoveFinished)
        activeForcedMoveHandle = instance.Handle

    若不是 IsUnstoppable:
        按 def.OnAddOps 的编译顺序:
            从 ModuleExecutorTable 取执行函数
            执行一次并把返回命令写入 pendingCommands

    FlushModuleCommands(
        replacedForcedMoveHandle)

    若 replacedForcedMoveHandle 有效:
        Remove(
            replacedForcedMoveHandle,
            Replaced)

    dirty = true
    RebuildOutputsIfDirty()
    返回 Added(Owner.UnitUid, instance.InstanceId)
~~~

`ForcedMovePriority` 是带 `ForcedMove` 标签控制的标准 `short` 参数 Key。Bake 必须校验该标签同时配置 `ForcedMoveOnAdd` 模块和 MovementHandler 所需的轨迹参数。

模块执行期间不允许直接增删 instances。所有新增、移除和强制位移等结果先变成 ControlModuleCommand，完成当前遍历后统一执行。强制位移模块只在新实例 `OnAdd` 时生成一次启动或替换命令；实例存续期间不重复提交。

## 1.7 Remove

Remove 按 Handle 删除一个确切实例，不检查净化标签。

`ControlRemoveReason` 至少区分：

| Reason | 语义 |
|---|---|
| Explicit | 创建者主动结束自己的实例 |
| NaturalExpire | ExpireTick 到达；唯一允许触发自然到期转换的原因 |
| Cleanse | 被一次性净化操作移除 |
| Replaced | 强制位移被同优先级或更高优先级的新实例替换 |
| SuppressedByUnstoppable | 不可阻挡开始时移除当前强制位移 |
| Death | UnitWorld 正式死亡清理 |
| Respawn | 复活阶段的幂等兜底清理 |
| Despawn | 回池或销毁前清理 |
| OwnerEnded | 外部生命周期整体结束 |

适用场景：

- Buff 或装备绑定句柄结束；
- 技能主动撤销自己的控制；
- 模块收到信号后移除自身；
- 单位生命周期清理。

执行流程：

~~~text
Remove(handle, reason):
    校验 handle.TargetUnitUid == Owner.UnitUid
    按 InstanceId 找到实例
    找不到则返回 false

    def = 全局配置表.Get(instance.ControlId)

    按 def.OnRemoveOps 的编译顺序:
        执行模块
        传入明确 reason
        收集返回命令

    若 handle == activeForcedMoveHandle:
        activeForcedMoveHandle = Invalid
        追加 StopForcedMove(handle) 命令

    从 instances 删除该实例
    FlushModuleCommands()
    dirty = true
    RebuildOutputsIfDirty()
    返回 true
~~~

只有 reason = NaturalExpire 时，ApplyControlOnExpire 等模块才执行转换。其它原因都不触发自然到期转换。

## 1.8 RemoveAll

RemoveAll 用于死亡、回池、销毁等生命周期清理，不是净化。

~~~text
RemoveAll(reason):
    按 InstanceId 升序复制全部 Handle
    开启批处理
    逐个 Remove(handle, reason)
    清空 immunities
    清空 unstoppables
    pendingSignals = None
    signalEffectiveTicks 全部设为 InvalidTick
    activeForcedMoveHandle = Invalid
    结束批处理
    FlushModuleCommands()
    RebuildOutputsIfDirty()
    返回成功移除数
~~~

它不检查 Tags 或 Intensity。调用方必须传入 Death、Despawn、OwnerEnded 等非 NaturalExpire 原因，避免错误触发到期转换。

## 1.9 Advance

Advance 由现有单位逻辑推进器每个 LogicTick 调用一次，但不接收 Tick 或 SimulationTickContext 参数。它统一消费此前记录的控制信号，再处理控制、免疫和不可阻挡的到期。

~~~text
Advance():
    currentTick =
        SimulationTickContext.Current.Tick

    signalMask = pendingSignals
    pendingSignals = None

    按 SignalType 固定枚举顺序:
        若 signalMask 不包含该信号:
            continue

        signalEffectiveTick =
            signalEffectiveTicks[type]

        若 currentTick - signalEffectiveTick
           > SignalRetentionTicks:
            continue

        按 InstanceId 升序扫描实例:
            若 signalEffectiveTick
               < instance.StartTick:
                continue

            def = 全局表.Get(instance.ControlId)

            若 def.SignalMask 不包含 type:
                continue

            按该 type 对应 SignalOps:
                执行模块
                把结果写入 pendingCommands

    FlushModuleCommands()

    按 InstanceId 升序收集:
        ExpireTick != Infinite
        且 ExpireTick <= currentTick
        的实例句柄

    按收集顺序:
        Remove(handle, NaturalExpire)

    清理已到期的 immunity 与 unstoppable
    FlushModuleCommands()
    RebuildOutputsIfDirty()
~~~

信号和到期实例都先收集或生成命令，再统一修改实例列表，避免遍历中改变容器。

Unit 框架必须保证每单位每个 LogicTick 调用一次。Handler 可以保存仅用于开发期断言的 LastAdvancedTick；正式 Gameplay 不依赖重复调用 Advance 推进状态。

## 1.10 轻量信号模块

信号是单位框架与控制实例之间的轻量事实转发器。它只告诉控制实例“发生了什么”，不告诉实例应该执行 Remove、AddControl 或其它命令。

推荐只定义控制确实需要的信号：

| SignalType | 记录条件 | 示例 |
|---|---|---|
| ActualDamageTaken | DamageTakenEvent.ActualLifeDamage 大于 0 | 打破 Sleep |
| OwnerActionStarted | 单位框架确认动作启动 | 某些控制在动作开始后移除 |
| ForcedMoveFinished | 来源 Handle 等于当前活动强制位移 | 位移结束后移除实例 |

信号不保存通用 Payload，不携带伤害公式、动作 Runtime 或移动轨迹。外部入口先完成必要条件判断，再记录对应信号位。

### 信号数据

~~~csharp
public enum CrowdControlSignalType : byte
{
    ActualDamageTaken = 0,
    OwnerActionStarted = 1,
    ForcedMoveFinished = 2,
    Count = 3,
}

[Flags]
public enum CrowdControlSignalMask : ushort
{
    None               = 0,
    ActualDamageTaken  = 1 << 0,
    OwnerActionStarted = 1 << 1,
    ForcedMoveFinished = 1 << 2,
}
~~~

Handler 只保存：

| 数据 | 作用 |
|---|---|
| pendingSignals | 尚未在 Advance 中广播的信号位 |
| signalEffectiveTicks[type] | 每种信号最近一次发生的 EffectiveTick |

没有 SignalInstance、SignalId、Payload、Processed、逐实例信号游标或动态订阅表。

### 记录算法

~~~text
RaiseSignal(type):
    currentTick =
        SimulationTickContext.Current.Tick

    若 signalEffectiveTicks[type]
       == currentTick:
        返回 false

    signalEffectiveTicks[type] = currentTick
    pendingSignals |= type.ToMask()
    返回 true

ClearSignal(type):
    pendingSignals &= ~type.ToMask()
    signalEffectiveTicks[type] = InvalidTick
~~~

同一 LogicTick 内同种信号无论出现多少次都只记录一次。不同类型的信号按 SignalType 的固定枚举顺序广播，不为信号建立帧内序列号。

若上一 Tick 的同类信号尚未广播，本 Tick 又发生同类事实，只保留更新后的 EffectiveTick；信号表达“最近发生过”，不表达发生次数。

信号默认保留最近两个 LogicTick 的发生时间：

~~~text
SignalRetentionTicks = 2

IsSignalRecent(type):
    currentTick =
        SimulationTickContext.Current.Tick

    若 signalEffectiveTicks[type] == InvalidTick:
        return false

    return currentTick
           - signalEffectiveTicks[type]
           <= SignalRetentionTicks
~~~

保留发生时间不表示重复触发。信号位在 Advance 中广播一次后即从 pendingSignals 清除；signalEffectiveTicks 只用于时间判断和短期查询。

### 明确入口

~~~text
OnDamageTaken(evt):
    若 evt.ActualLifeDamage <= 0:
        return

    RaiseSignal(ActualDamageTaken)

OnOwnerActionStarted():
    RaiseSignal(OwnerActionStarted)

OnForcedMoveFinished(sourceHandle):
    若 sourceHandle
       != activeForcedMoveHandle:
        return

    RaiseSignal(ForcedMoveFinished)
~~~

调用方只在对应 Gameplay 事实已经成立后调用这些入口。Handler 不依赖动态 C# event/delegate。

### 实例判断

实例响应信号前只比较生效 Tick。控制实例的 `StartTick` 就是该实例的生效 Tick；信号使用 `signalEffectiveTicks[type]`：

~~~text
signalEffectiveTick < instance.StartTick:
    信号早于实例，不响应

signalEffectiveTick >= instance.StartTick:
    允许 Definition 的 SignalOps 自行响应
~~~

同 Tick 统一视为同一个逻辑批次，不区分同 Tick 内的函数调用先后。该约定换取不保存帧内信号序号的轻量结构。

强制位移是唯一实例，因此新强制位移生效前，Handler 必须清除尚未广播的 `ForcedMoveFinished` 位，并重置该类型的去重 Tick。这样旧轨迹同 Tick 产生的完成事实不会误作用到新实例；后续若新轨迹在同 Tick 完成，仍只保存一份属于新活动来源的信号。这个重置只针对该专用信号，不影响其它信号。

SignalMask 由 Bake 根据 Definition 实际配置的 SignalOps 自动生成，不要求设计师重复填写。Tags 可以用于模块内部筛选控制类别，但信号本身只表达发生的事实。

## 1.11 查询接口

### State

返回轻量 CrowdControlStateView，不分配内存。

### HasAnyTag

判断 state.ActiveTags 与输入 tags 是否存在交集，适合 Blind、Airborne、Grounded 等高频查询。

### MatchesTags

对当前 ActiveTags 执行完整 TagQuery。用于少量复杂规则，不用于找具体实例。

### TryGetBehaviorOverride

若当前存在强制行为胜者，返回其临时值；否则返回 false。

### GetRemainingTicks

可选接口。按 Handle 找实例并返回 ExpireTick - SimulationTickContext.Current.Tick 与 0 的较大值；Infinite 返回约定常量。

### FillInstances

仅当玩法、AI 或外部系统确实需要逐实例读取时提供。调用方传入可复用 List，Handler 不返回新数组。

---

# 二、CrowdControlDefinition：唯一静态配置

## 2.1 只保留一套 Definition

本案不再建立 CrowdControlRuntimeDef。

全局 GameplayConfig.CrowdControls 直接保存或引用 CrowdControlDefinition。Handler 按 ControlId 取得的就是这一份 Definition。

~~~mermaid
flowchart TD
    A["CrowdControlDefinition"] --> B["编辑器 Bake 自身运行字段"]
    B --> C["全局 GameplayConfig.CrowdControls"]
    C --> D["CrowdControlHandler 按 ID 读取"]
~~~

Definition 内部允许同时存在：

- Inspector 可编辑字段；
- 隐藏的已 Bake 紧凑字段。

这是同一个资产中的两组字段，不是两种静态定义、两张表或两个运行对象。隐藏字段只解决 float、字符串 Key 和编辑器数组不适合直接进入运行热路径的问题。

## 2.2 CrowdControlDefinition 字段

CrowdControlDefinition 描述一个控制由哪些模块组成，以及它如何被免疫和识别。

### Inspector 字段

| 字段 | 作用 |
|---|---|
| ControlId | 全局稳定配置 ID |
| Intensity | 控制烈度：Low、Medium、High |
| Tags | 轻量逻辑标签位 |
| DurationRule | 是否受 Tenacity 影响；不提供默认持续时间 |
| ParameterSchema | 参数 Key、类型、是否必填和字节容量 |
| Modules | 按执行顺序排列的模块配置 |

### 同一 Definition 内的隐藏 Bake 字段

| 字段 | 作用 |
|---|---|
| ParamLayout | KeyId 到 Type、Offset、Size 的紧凑映射 |
| OnAddOps | 仅包含 OnAdd 模块 |
| CollectOps | 仅包含汇总模块 |
| SignalOps | 仅包含信号模块 |
| OnRemoveOps | 仅包含移除模块 |
| SignalMask | 快速跳过无关 Signal |

明确没有：

- DefaultDurationSeconds；
- DefaultDurationTicks；
- Kind；
- Icon；
- CategoryFlags；
- Aggregation；
- InterruptMask；
- SourceType；
- 软控或硬控字段。

持续时间始终由技能、Buff、装备或其他调用方以 Tick 传入 Add。同一 Root Definition 可以被不同来源分别施加 10、30 或 90 Tick。

ScriptableObject 的 asset name 可以作为编辑器识别名称。UI 如需图标和本地化名称，应以 ControlId 在表现配置中映射。

## 2.3 为什么仍然需要 Bake

Bake 不是生成第二个 Definition，而是把同一资产中的编辑器表达编译到隐藏字段：

| Inspector 表达 | Bake 后 |
|---|---|
| 字符串参数 Key | 稳定 ParamKeyId |
| float 模块常量 | fp |
| 模块配置数组 | 按 Hook 分组的紧凑 ControlModuleOp |
| 参数类型声明 | ParamLayout 的 Type、Offset、Size |

运行时只读取隐藏字段。这样既只有一份 CrowdControlDefinition，又不需要在热路径使用字符串、float、反射或可变编辑器数组。

## 2.4 为什么不需要 Kind

Kind 往往同时被拿来做三件互相冲突的事：

- UI 分类；
- 逻辑分支；
- 净化和免疫匹配。

本案分别处理：

| 需求 | 替代方式 |
|---|---|
| 执行效果 | Modules |
| 免疫、净化、查询 | Tags |
| UI 名称和图标 | 表现配置按 ControlId 映射 |

因此 Handler 中不会出现按 Stun、Slow、Taunt 逐项判断的条件链。

## 2.5 为什么不保存软控、硬控

软控、硬控是玩家根据当前实际限制形成的分类，不是底层固定规则。

例如同一个复合控制：

- 只贡献 20% Slow 时通常被理解为软控；
- 同时贡献 Move 与 Cast 禁止时会被理解为硬控；
- 某单位拥有动作保护时，当前动作可能继续，但新动作仍被阻止。

控制系统不存储 Soft、Hard。

如果 UI 或统计需要动态判断，可从当前模块输出派生：存在强制行为或阻止主要动作时可显示为强控制；只改变数值或命中规则时可显示为弱控制。

该派生值不参与控制逻辑。

## 2.6 Bake 流程

~~~text
Bake(definition):
    校验 ControlId 唯一
    校验 Intensity 为 Low / Medium / High
    校验 Tags 只使用已注册位
    校验 Tags 包含 Control 基础标签

    根据 ParameterSchema:
        把字符串 Key 转为稳定 ParamKeyId
        校验同一个 Key 的类型全局一致
        按 Size 和 Alignment 分配紧凑 Offset
        生成 ParamLayout

    按 Modules 配置顺序:
        解析 ModuleId
        校验模块需要的 ParamKey 存在且类型正确
        把模块 float 静态参数转为 fp
        把模块 ParamKey 编译为 ParamOffset
        编译为紧凑 ControlModuleOp
        按 Hook 分入 OnAdd / Collect / Signal / OnRemove 数组

    生成 SignalMask
    若 Tags 包含 ForcedMove:
        校验存在 ForcedMoveOnAdd 模块
        校验 ForcedMovePriority 为 short
        校验轨迹参数 Key 完整
    把隐藏 Bake 字段写回同一 Definition
    注册到全局 GameplayConfig.CrowdControls
~~~

所有 float 转换都发生在编辑器或离线数据生成阶段。比赛运行时只读取 fp、Tick、ParamKeyId 和紧凑 Offset。

---

# 三、CrowdControlModule：可组合模块执行表

## 3.1 模块的定位

模块是“一个很小、职责单一、可复用的控制函数”。

一个控制定义可以组合多个模块。例如眩晕只使用 BlockActions；变形组合 BlockActions 与 MaxMoveSlow；魅惑组合 BlockActions 与 ForcedBehavior；击退组合 BlockActions 与 ForcedMoveOnAdd。

Handler 只循环模块操作数组，不知道这些组合叫什么。

## 3.2 四个生命周期 Hook

为保持精简，只提供四个 Hook：

| Hook | 调用时机 | 允许结果 |
|---|---|---|
| OnAdd | 实例创建后一次 | 提交强制位移或其它一次性延迟命令 |
| Collect | 汇总状态时 | 写入 ControlAccumulator |
| OnSignal | Advance 广播已记录信号 | RemoveSelf、AddControl 等 |
| OnRemove | 实例移除前一次 | 清理关联动作，或自然到期转换 |

不提供每帧 OnTick 模块。

原因：

- 持续时间由 Handler 统一推进；
- 强制行为由 Planner 查询当前 Override；
- 强制移动由 MovementHandler 推进；
- 每帧遍历模块会制造不必要热路径。

若未来出现确实无法由信号或外部系统表达的周期控制，再增加显式 ScheduledSignal，不直接开放所有模块 OnTick。

## 3.3 Inspector 中的模块配置

Authoring 层可以使用一个统一序列化结构：

~~~csharp
[Serializable]
public struct CrowdControlModuleAuthoring
{
    public ushort ModuleId;
    public ControlModuleAuthoringArg[] Args;
}
~~~

Args 只存在于编辑器资产。自定义 Inspector 根据模块注册元数据，把它显示成有含义的字段，例如 BlockedActions、SlowKey、PriorityKey，而不是让设计师直接填写 Arg0、Arg1。

这种方式不要求每个模块建立一个 SerializeReference 派生类。Bake 后 Args 被编译进紧凑 ControlModuleOp，运行时不保留数组对象和字段名称。

## 3.4 ControlModuleOp

Definition 的隐藏 Bake 数据中，每个操作是紧凑值类型：

| 字段 | 作用 |
|---|---|
| ExecutorIndex | 静态执行函数表下标 |
| Hook | 所属生命周期阶段；Bake 后通常已按数组分开 |
| StaticData | 模块自己的少量整数或位掩码 |
| StaticFp0 / StaticFp1 | Bake 后定点静态参数 |
| ParamOffset0 / 1 / 2 | 由 ParamKey 编译得到的实例字节偏移 |

不同模块只解释自己需要的字段。Handler 不解释 StaticData 或 ParamOffset 的含义。

## 3.5 静态执行函数表

全局只读表：

~~~csharp
public readonly struct CrowdControlModuleExecutor
{
    public readonly ControlOnAddFn OnAdd;
    public readonly ControlCollectFn Collect;
    public readonly ControlSignalFn OnSignal;
    public readonly ControlOnRemoveFn OnRemove;
}

CrowdControlModuleExecutor[] ModuleExecutors;
~~~

注册发生一次：

~~~text
ModuleExecutors[BlockActionsId] = BlockActionsExecutor
ModuleExecutors[MaxMoveSlowId] = MaxMoveSlowExecutor
ModuleExecutors[ForcedBehaviorId] = ForcedBehaviorExecutor
...
~~~

运行时调用：

~~~text
executor = ModuleExecutors[op.ExecutorIndex]
executor.Collect(instance, op, accumulator)
~~~

这不是按控制 Kind 分支，而是一个密集数组索引加静态委托调用。

## 3.6 为什么不用每实例模块对象

假设单位当前有 5 个控制，每个控制 3 个模块：

| 方案 | 运行对象 |
|---|---:|
| 每实例模块对象 | 5 个 Instance + 15 个 Module 对象 |
| 本案 | 5 个 Instance，模块数组由全局定义共享 |

实例只保存 ControlId。模块操作数组从全局表中的 CrowdControlDefinition 读取。

## 3.7 ControlAccumulator

原设计中的 Aggregation 字段被删除。

取而代之的是 Handler 重算时创建或清空一个内部“控制汇总器”：

| 字段 | 写入规则 |
|---|---|
| BlockedActions | BlockActions 模块按位 OR |
| ActiveTags | 活动 Definition Tags 按位 OR |
| MoveSlowRatio | MaxMoveSlow 模块取最大值 |
| AttackSpeedSlowRatio | MaxAttackSpeedSlow 模块取最大值 |
| VisionScale | MinVisionScale 模块取最小值 |
| BehaviorCandidate | ForcedBehavior 模块执行稳定胜者比较 |

每种模块自己定义如何写汇总器，因此 Definition 不需要一个含义模糊的 Aggregation 枚举。

## 3.8 建议的标准模块

| 模块 | Hook | 作用 |
|---|---|---|
| BlockActions | Collect | OR 一组 UnitActionMask |
| MaxMoveSlow | Collect | 从已编译参数偏移读取 fp，取最大移动减速 |
| MaxAttackSpeedSlow | Collect | 从已编译参数偏移读取 fp，取最大攻速降低 |
| MinVisionScale | Collect | 从已编译参数偏移读取 fp，取最小视野比例 |
| BasicAttackMiss | Collect | 设置基础攻击失误状态 |
| ForcedBehavior | Collect | 提供魅惑、恐惧、嘲讽等候选 |
| ForcedMoveOnAdd | OnAdd | 只执行一次；生成启动或替换 ResolvedForcedMove 的命令 |
| RemoveOnSignal | OnSignal | 指定信号到来时 RemoveSelf |
| AddControlOnNaturalExpire | OnRemove | 自然到期时添加另一个控制 |

标准模块数量应保持小。一个新配置能由这些模块组合完成时，不新增代码。

## 3.9 自定义模块的最小边界

新增真正特殊的模块只需要：

1. 分配稳定 ModuleId；
2. 定义 Inspector 配置和参数 Key / 类型要求；
3. 编写一个或多个静态 Hook 函数；
4. 在模块表注册；
5. 在 Bake 校验器登记。

不修改：

- CrowdControlHandler；
- CrowdControlInstance；
- 其他控制 Definition；
- 单位框架主循环。

## 3.10 模块命令与重入

模块不能在遍历中直接调用 Handler.Add 或 Remove。

它返回轻量 ControlModuleCommand：

| Command | 作用 |
|---|---|
| RemoveSelf | 当前遍历完成后移除当前实例 |
| AddControl | 当前遍历完成后添加另一个 ControlId |
| StartForcedMove | 直接要求 MovementHandler 启动已获准轨迹 |
| ReplaceForcedMove | 直接要求 MovementHandler 原子替换轨迹 |
| StopForcedMove | 仅在来源 Handle 仍匹配时停止轨迹 |

Handler 用复用 List 保存命令，按产生顺序 Flush。强制位移命令直接交给 MovementHandler，不经过 BehaviorPlanner 或 ActionArbiter。

同一轮 Flush 新产生的命令追加到尾部。应设置最大递归命令数，防止错误配置 A 到期添加 B、B 立即添加 A 的无限循环。

## 3.11 模块性能

重算成本近似为“活动实例数 × 每个 Definition 的 Collect 模块数”。

控制实例通常很少，且只在以下情况重算：

- Add；
- Remove；
- 免疫不影响既有实例，通常不重算；
- 相关 Stat 变化；
- 信号导致实例变化。

没有每帧对全部模块的无条件调用。

---

# 四、CrowdControlInstance 与 Key 参数黑板

## 4.1 Instance 的最小字段

~~~csharp
public readonly struct CrowdControlInstance
{
    public readonly int InstanceId;
    public readonly CrowdControlId ControlId;
    public readonly int StartTick;
    public readonly int ExpireTick;
    public readonly CrowdControlParamBlock Params;
}
~~~

只有五项核心数据。Instance 是值类型；创建控制只把紧凑值写入 Handler 已复用的容器，不为实例创建模块对象或黑板 Dictionary。

明确不保存：

- SourceType；
- SourceConfigId；
- MergeKey；
- Kind；
- 模块对象列表；
- Definition 对象引用；
- 剩余秒数；
- 伤害、治疗或护盾数据。

## 4.2 为什么不保存来源

控制实例只需要回答：

- 我是什么配置；
- 我何时开始和结束；
- 我的模块需要哪些动态参数。

技能 ID、装备 ID、Buff ID 对控制计算没有通用意义，因此不进入基础实例。

如果 Charm 或 Taunt 需要施加者 UnitUid，调用方用 TargetUnit 等参数 Key 写入。它只对绑定这个 Key 的 ForcedBehavior 模块有意义。

## 4.3 不合并，也不修改既有实例

每次 Add 都创建独立实例。例如连续两次添加 30 Tick 的同一控制，会分别得到 Instance 101 与 Instance 102。

创建后：

- 不 ResetDuration；
- 不 ExtendDuration；
- 不 PatchParams；
- Handle 只用于 Remove 和查询。

同一效果再次生效时重新 Add。若一个外部 Buff 必须结束自己添加的控制，它保存 Handle，并在 Buff 移除时调用一次 Remove。

## 4.4 对外按 Key，内部按 Offset

参数系统分为三部分：

~~~mermaid
flowchart TD
    A["Inspector 字符串 Key"] --> B["Bake 为 ParamKeyId"]
    B --> C["调用方 Writer.Set Key Value"]
    C --> D["Add 按 ParamLayout 写入 ParamBlock"]
    D --> E["模块按已编译 Offset 读取"]
~~~

设计师和调用方都不分配槽位。模块热路径也不做字符串查找或哈希。

## 4.5 CrowdControlParamKey

Inspector 使用可读字符串，例如：

- TargetUnit；
- MoveSlowRatio；
- Direction；
- Distance；
- BehaviorPriority。

Bake 时使用项目统一的 StableStringId32 生成 CrowdControlParamKey：

~~~csharp
public readonly struct CrowdControlParamKey
{
    public readonly uint Value;
}
~~~

要求：

- 不使用 string.GetHashCode；
- 构建时检查哈希碰撞；
- 同名 Key 在全项目只能注册一种值类型；
- 代码侧生成常量，不在运行时从字符串计算。

## 4.6 支持的数据类型

ParamType 只允许有限值类型：

| ParamType | 字节 | 示例 |
|---|---:|---|
| Byte | 1 | 小枚举、开关 |
| Short | 2 | 小范围优先级 |
| Int | 4 | Tick、索引 |
| Long | 8 | 大整数 |
| Bool | 1 | 运行标志 |
| Fp | 按项目 fp | 比例、距离 |
| UnitUid | 按 UnitUid | 行为目标 |
| Fp2 | 2 × fp | 方向、目标点 |
| Mask32 / Mask64 | 4 / 8 | 位标志 |

enum 使用明确的 byte、short 或 int 底层类型。

不支持：

- float / double 运行值；
- string；
- Unity Object；
- object；
- List；
- Dictionary；
- 未注册的任意 struct。

## 4.7 ParameterSchema 与 ParamLayout

ParameterSchema 是 Inspector 配置：

| 字段 | 作用 |
|---|---|
| Key | 可读字符串 |
| Type | 初始类型，之后不可更改 |
| Required | Add 时是否必须提供 |

Bake 后，同一 Definition 内生成 ParamLayout：

| 字段 | 作用 |
|---|---|
| ParamKeyId | 稳定 Key |
| Type | 运行时类型校验 |
| Offset | 在固定字节块中的位置 |
| Size | 实际占用字节 |

Offset 按 Size 与 Alignment 自动分配。设计师不会看到或填写 Offset。

示例：KnockBack

| Key | Type | Required | 自动布局示例 |
|---|---|---:|---:|
| Direction | Fp2 | 是 | Offset 0 |
| Distance | Fp | 是 | Offset 16 |
| MoveTicks | Int | 是 | Offset 24 |

具体 Offset 由 Bake 结果决定，示例数值不构成配置约定。

## 4.8 CrowdControlParamWriter

调用方使用短生命周期值类型 Writer：

~~~csharp
CrowdControlParamWriter writer = default;
writer.Set(ControlParamKeys.TargetUnit, targetUid);
writer.Set(ControlParamKeys.MoveSlowRatio, slowFp);
~~~

Set 的签名使用 Set<T>(CrowdControlParamKey key, in T value) where T : unmanaged，避免装箱。

Set<T>：

1. 根据 T 得到已注册 ParamType；
2. 记录 ParamKeyId、ParamType、Size 和原始值；
3. 同一个 Key 重复 Set 时覆盖 Writer 中的旧值；
4. 超过最大 Entry 数立即失败。

Writer 建议最多保存 8 个 Entry，每个 Entry 最多 16 字节，可覆盖 fp2。它只存在于调用栈或技能阶段临时状态，不进入 CrowdControlInstance。

值的初始类型同时记录在全局 ParamKey 注册表、Definition.ParameterSchema 和 Writer Entry 中；实例 ParamBlock 不重复保存 TypeCode，因为可由 ControlId 对应的 ParamLayout 唯一恢复。

## 4.9 Add 时物化参数块

CrowdControlParamBlock 建议使用固定 64 字节数据区：

~~~csharp
public struct CrowdControlParamBlock
{
    private FixedBytes64 data;
}
~~~

FixedBytes64 可以是项目自有定长字节结构；若已经依赖 Unity.Collections，也可使用对应 fixed-bytes 类型，不为此单独增加依赖。

物化算法：

~~~text
Materialize(definition.ParamLayout, writer):
    block.Clear()
    writtenRequiredMask = 0

    遍历 writer entries:
        layout = 按 ParamKeyId 查找
        找不到 -> InvalidParams
        entry.Type != layout.Type -> InvalidParams
        entry.Size != layout.Size -> InvalidParams

        把 entry.Value 复制到
            block[layout.Offset, layout.Size]

        标记对应 Required Key 已写入

    若存在未写入的 Required Key:
        返回 InvalidParams

    返回 block
~~~

Definition 的 ParamLayout 通常只有数项，Add 阶段可以使用排序数组二分或小数组线性查找。模块运行热路径不走这次 Key 查找。

未写入的可选 Key 保持全零语义。真正的静态常量应写在 Module Authoring 中并 Bake 到 ControlModuleOp，不为动态黑板再维护 DefaultParams。

## 4.10 模块读取

模块配置在 Inspector 绑定 ParamKey。Bake 时已经把 Key 编译为 ControlModuleOp.ParamOffset。

外部低频读取仍然可以按 Key：

~~~csharp
instance.TryGetParam(
    ControlParamKeys.TargetUnit,
    out UnitUid target);
~~~

TryGetParam 通过 instance.ControlId 取得 Definition.ParamLayout，校验类型后读取对应 Offset。

模块热路径则直接使用 Bake 好的 Offset：

~~~text
slow = instance.Params.ReadFp(op.ParamOffset0)
target = instance.Params.ReadUnitUid(op.ParamOffset1)
~~~

因此对外获得了 Key 黑板的易用性，对内仍然是固定偏移读取。

## 4.11 容量规则

建议初始上限：

- 每个 Definition 最多 8 个动态 Key；
- ParamBlock 最多 64 字节；
- 单个值最多 16 字节。

超限是构建错误，不在运行时退化成 Dictionary。

若实际内容证明 64 字节不足，统一提升 ParamBlock 容量；不要为单个控制引入堆分配的可变容器。

---

# 五、CrowdControlTagMask：轻量标签系统

## 5.1 标签的用途

标签只用于：

- 控制免疫匹配；
- 净化匹配；
- 状态查询；
- 少量模块规则匹配。

标签不直接执行控制效果。

效果来自 Modules。

## 5.2 数据结构

控制标签数量少，推荐使用一个或两个 ulong：

~~~csharp
public readonly struct CrowdControlTagMask
{
    public readonly ulong Low;
    public readonly ulong High;
}
~~~

如果 64 位足够，只保留一个 ulong。

基本操作：

| 操作 | 成本 |
|---|---:|
| Any | 1–2 次 AND |
| All | 1–2 次 AND + 比较 |
| None | 1–2 次 AND + 比较 |
| Union | 1–2 次 OR |

## 5.3 建议标签

标签应表达真实规则，不表达玩家评价。

可以有：

- Control；
- Slow；
- Root；
- Silence；
- Blind；
- Disarm；
- ForcedBehavior；
- ForcedMove；
- Displacement；
- Airborne；
- Grounded；
- Nearsight；
- Sleep；
- Suppression；
- Polymorph。

不要有：

- SoftControl；
- HardControl；
- GoodControl；
- StrongControl；
- IconType；
- UIGroup。

## 5.4 TagQuery

~~~csharp
public readonly struct CrowdControlTagQuery
{
    public readonly CrowdControlTagMask All;
    public readonly CrowdControlTagMask Any;
    public readonly CrowdControlTagMask None;
}
~~~

匹配公式：

~~~text
matchesAll =
    (tags & query.All) == query.All

matchesAny =
    query.Any 为空
    或 (tags & query.Any) 非空

matchesNone =
    (tags & query.None) 为空

Match = matchesAll && matchesAny && matchesNone
~~~

同一个查询结构用于：

- 免疫；
- 净化；
- Handler.MatchesTags；
- 少量模块筛选。

净化的数量限制不属于标签匹配。它由单独的轻量值类型保存：

~~~csharp
public readonly struct CrowdControlCleanseSpec
{
    public readonly CrowdControlTagQuery Query;
    public readonly int MaxRemoveCount;
}
~~~

## 5.5 标签与模块的关系

模块决定行为，标签决定“如何被外部规则识别”。

例如：

| Definition | Modules | Tags |
|---|---|---|
| Root | BlockActions(Move, Mobility) | Control、Root |
| KnockBack | BlockActions + ForcedMoveOnAdd | Control、ForcedMove、Displacement、Airborne |
| Blind | BasicAttackMiss | Control、Blind |
| Slow | MaxMoveSlow | Control、Slow |

`Control` 是所有 CrowdControlDefinition 的必备基础标签，由 Bake 强制校验。其它标签按实际机制添加，不为“以后也许有用”的分类预留位。

标签错误不会自动生成效果；Bake 校验器应检查常见一致性。运行时只有少量跨实例规则读取标签：

- `Control`：确认实例属于不可阻挡要抑制的控制域；
- `ForcedMove`：进入唯一强制位移仲裁并直接拒绝不可阻挡期间的新请求。

实际效果仍由 Modules 执行，因此这两个轻量位判断不会恢复按 Kind 调用具体控制函数的结构。

---

# 六、控制免疫、不可阻挡与净化

## 6.1 三者不是同一个概念

| 机制 | 归属 | 控制实例是否创建 | 对当前动作的结果 |
|---|---|---:|---:|
| 控制免疫 | CrowdControlHandler | 否 | 该控制不会产生动作限制 |
| 不可阻挡 | CrowdControlHandler | 普通控制创建但被抑制；强制位移不创建 | 不被控制输出中断 |
| 净化 | CrowdControlHandler | 先创建，后移除 | 清除后续限制，但不回滚已经发生的中断 |

## 6.2 CrowdControlIntensity

CrowdControlDefinition 增加固定的控制烈度：

~~~csharp
public enum CrowdControlIntensity : byte
{
    Low,
    Medium,
    High
}
~~~

| 烈度 | 推荐用途 | 能否被控制免疫阻止 | 能否被 Cleanse |
|---|---|---:|---:|
| Low | Slow、Blind、Cripple、Nearsight | 是 | 是 |
| Medium | Root、Stun、Silence、ForcedBehavior | 是 | 是 |
| High | KnockUp、KnockBack、Pull、Fling 等强制位移 | 否 | 否 |

烈度由设计师在 Definition 中明确配置，不从 Tags 或 Modules 自动推导。Suppression 等特殊控制是否为 High，由项目玩法决定。

统一判定：

~~~text
CanBeResisted(definition):
    return definition.Intensity != High
~~~

控制免疫和 Cleanse 共用这条烈度规则，因此二者对控制等级的免除范围一致。

High 仍然会自然到期，也能被拥有 Handle 的系统用 Remove 正常结束；“不可免除”只限制控制免疫与 Cleanse。

不可阻挡不使用 CanBeResisted。它抑制所有烈度的控制输出，并拒绝所有带 `ForcedMove` 标签的控制请求，因此 High 也不能绕过不可阻挡。

## 6.3 控制免疫

控制免疫发生在 Add 创建实例之前。

`immunities` 是 Handler 内部的应用门禁，不是控制汇总结果：它不进入 `CrowdControlStateView`，单位框架也不负责再次判断免疫。只有 Add 通过门禁后创建的实例，才可能参与后续模块与状态汇总。

Handler 先判断 Intensity。High 直接跳过全部控制免疫；Low 与 Medium 再按 Definition.Tags 匹配：

~~~text
全控制免疫:
    All = Control

只免疫 Root:
    All = Root

只免疫减速:
    All = Slow
~~~

匹配成功：

- 不创建 Instance；
- 不执行任何 OnAdd 模块；
- 不改变汇总状态；
- 不提交强制移动；
- 返回 CrowdControlAddResult.BlockedByImmunity。

## 6.4 CrowdControlImmunitySpec

| 字段 | 作用 |
|---|---|
| Query | 匹配哪些 Definition Tags |
| DurationTicks | 保护持续时间；Infinite 表示句柄绑定 |
| BlockCount | 0 表示不限次数，正数表示剩余拦截次数 |
| Priority | 多个免疫同时匹配时的稳定优先级 |

无论 Query 如何配置，控制免疫都不会拦截 Intensity = High；该限制由 Handler 固定执行。

Handler 内部实例还保存：

| 字段 | 作用 |
|---|---|
| ImmunityId | 单位内唯一 ID |
| ExpireTick | LogicTick + 有效 Tick |
| RemainingBlocks | -1 表示不限次数，正数表示剩余次数 |

不保存技能或 Buff 来源。外部系统用 ImmunityHandle 负责解除自己创建的免疫。

ImmunityHandle 只在当前生命阶段有效。`ClearForDeath()` 会清空全部 immunity；跨死亡保留的来源在自身复活逻辑中重新注册，Handler 不保存来源信息，也不自动重建。

## 6.5 AddImmunity

~~~text
AddImmunity(spec):
    currentTick =
        SimulationTickContext.Current.Tick
    校验 spec.Query 不是完全空查询
    校验 DurationTicks

    immunity.Id = nextImmunityId++
    immunity.Query = spec.Query
    immunity.ExpireTick =
        Infinite 或 currentTick + spec.DurationTicks
    immunity.RemainingBlocks =
        spec.BlockCount == 0
        ? -1
        : spec.BlockCount
    immunity.Priority = spec.Priority

    按 Priority 降序、ImmunityId 升序保持稳定顺序
    返回 (Owner.UnitUid, ImmunityId)
~~~

新增免疫默认只阻止未来控制，不自动移除既有控制。

如果某技能是“解除并免疫”，调用顺序明确写为：

~~~text
handler.Cleanse(cleanseSpec)
handler.AddImmunity(spec)
~~~

## 6.6 一次性控制护盾与完整法术盾

两者都从“阻止 Add 创建控制实例”起效，但生命周期拥有者不同。

### 一次性控制护盾

它只属于 CrowdControlHandler：

~~~text
Query.All = Control
BlockCount = 1
DurationTicks = 指定时长或 Infinite
~~~

当一个 Low 或 Medium 控制匹配时：

- Add 返回 BlockedByImmunity；
- RemainingBlocks 从 1 变为 0；
- 控制护盾移除；
- 不创建控制实例。

High 控制不会触发或消耗该控制护盾。

### 完整法术盾

单位框架 v26 中，完整法术盾由 `StatHandler` 的 `ShieldInstance` 拥有；CrowdControlHandler 只提供它所需的控制免疫能力，不拥有法术盾实例，也不处理护盾数值。

双方只遵守以下接入契约：

1. 法术盾生效时，生命周期拥有者调用 AddImmunity，注册无限次数的 Low / Medium 控制免疫；
2. 生命周期拥有者保存返回的 ImmunityHandle；
3. 法术盾失效时，生命周期拥有者调用 RemoveImmunity；
4. 同一次命中的伤害与控制必须共享“命中开始时法术盾是否有效”的判定，避免伤害先耗尽法术盾后，随后添加的控制错误穿透。

控制 Add 被阻止时：

- 不扣减法术盾数值；
- 不销毁法术盾；
- 不消耗控制免疫次数；
- High 控制仍可创建。

因此“控制本身破不了法术盾”由 BlockCount = 0 的句柄绑定免疫保证。

护盾吸收、耗尽、到期和命中开始状态属于 StatHandler 与战斗系统。本设计案只冻结 ImmunityHandle 的添加、保存和解除契约。

## 6.7 不可阻挡

不可阻挡完全由 CrowdControlHandler 保存和执行，不依赖单位框架的 ActionInterruptionGuard。

它与控制免疫的区别：

| 规则 | 控制免疫 | 不可阻挡 |
|---|---|---|
| Low / Medium 普通控制 | 拒绝创建 | 创建并计时，但不输出效果 |
| High 普通控制 | 不能阻止 | 创建并计时，但不输出效果 |
| ForcedMove | High 时仍可能创建 | 无论烈度都拒绝创建 |
| 已存在控制 | 不自动处理 | 立即从汇总结果中抑制 |
| 状态结束 | 没有被创建的控制不会补回 | 尚未到期的普通控制恢复剩余效果 |

### 运行状态

不可阻挡可能由多个外部生命周期重叠提供，因此不使用一个容易被提前清除的 bool：

~~~csharp
public struct CrowdControlUnstoppable
{
    public int UnstoppableId;
    public int ExpireTick;
}

public readonly struct CrowdControlUnstoppableSpec
{
    public readonly int DurationTicks;
}
~~~

`Infinite` 表示由 Handle 绑定生命周期。Handler 只需要判断 `unstoppables.Count != 0`，不需要优先级、TagQuery 或次数。

UnstoppableHandle 同样只在当前生命阶段有效。`ClearForDeath()` 会清空全部 unstoppable；跨死亡保留的来源在自身复活逻辑中重新注册，不恢复死亡前的旧 Handle。

### 添加与移除

~~~text
AddUnstoppable(spec):
    currentTick =
        SimulationTickContext.Current.Tick

    创建独立 unstoppable entry
    分配 nextUnstoppableId
    计算 ExpireTick

    若这是从“无不可阻挡”变为“有不可阻挡”:
        若 activeForcedMoveHandle 有效:
            Remove(
                activeForcedMoveHandle,
                SuppressedByUnstoppable)

        dirty = true
        RebuildOutputsIfDirty()

    返回 CrowdControlUnstoppableHandle

RemoveUnstoppable(handle):
    删除确切 entry

    若这是最后一个 entry:
        dirty = true
        RebuildOutputsIfDirty()
~~~

不可阻挡开始时，正在执行的强制位移控制会被移除，并通过其来源 Handle 停止 MovementHandler 中的轨迹。它不会在不可阻挡结束后恢复。

### 抑制规则

不可阻挡期间：

- 新的普通控制仍创建独立实例并正常计算 ExpireTick；
- 普通控制的 OnAdd 即时效果命令被抑制；
- RebuildOutputs 输出空的控制 StateView 和空的 BehaviorOverride；
- SignalOps 与到期仍然推进实例生命周期；
- 带 ForcedMove 标签的新请求直接返回 RejectedByUnstoppable；
- 不创建强制位移实例，也不调用 MovementHandler。

不可阻挡结束后，Handler 重新执行 RebuildOutputs。仍未到期的普通控制从剩余时间继续生效，不重放已经被抑制的 OnAdd 命令。

不可阻挡本身不进入 CrowdControlStateView。需要查询时直接读取 Handler.IsUnstoppable。

## 6.8 为什么删除 CrowdControlInterruptMask

Definition 不再声明 InterruptMask。

控制模块只汇总一个 UnitActionBlockMask 类型的 BlockedActions。

Handler 将 BlockedActions 交给单位框架。单位框架已经知道：

- 当前运行的动作占用哪些 ActionMask；
- 哪些动作现在被禁止；
- 如何通过 ActionArbiter 取消不再允许的运行时。

因此由单位框架自动中断对应动作，不需要控制系统再保存一套容易不一致的 InterruptMask。

## 6.9 UnitActionBlockMask

原 BlockMask 和 FineBlockMask 合并并改名为 UnitActionBlockMask。

名称直接说明它表示“禁止哪些单位动作”。

建议位：

- VoluntaryMove；
- Turn；
- VoluntaryAttack；
- AbilityCast；
- Mobility；
- EquipmentActive；
- SummonerSpell；
- ControlMove；
- ControlAttack。

是否中断当前动作由单位框架判断；是否禁止新动作同样读取这一个 Mask。

Root 可以禁止 VoluntaryMove、ControlMove 与 Mobility，而不禁止 AbilityCast。

Stun 可以禁止 VoluntaryMove、Turn、VoluntaryAttack、AbilityCast、Mobility、ControlMove 与 ControlAttack。

Suppression 可额外禁止 EquipmentActive 与 SummonerSpell。

## 6.10 净化规则由谁定义

Cleanse 是一次性操作，不是状态：

- 没有 CleanseInstance；
- 没有持续时间；
- 不进入 immunities；
- 调用完成后不保留任何“净化中”数据。

Cleanse 与控制免疫共用 CanBeResisted：

~~~text
Low / Medium:
    可以被控制免疫阻止
    也可以被 Cleanse 移除

High:
    不能被控制免疫阻止
    也不能被 Cleanse 移除
~~~

净化技能或装备构造 CrowdControlCleanseSpec。Query 只负责在可被免除的控制中继续筛选 Tags；MaxRemoveCount 定义最多移除几个，0 表示不限制。

### 解除全部可免除控制

~~~text
Query.All  = Control
~~~

### 只解除减速

~~~text
Query.All  = Control | Slow
~~~

Airborne、KnockBack 等通常通过 Intensity = High 获得不可免除语义，不再要求 CleanseSpec 逐个排除这些标签。

## 6.11 Cleanse

Cleanse 按 CleanseSpec.Query 移除完整实例。

~~~mermaid
flowchart TD
    A["Cleanse(spec)"] --> B["按 InstanceId 扫描"]
    B --> C{"Intensity 为 High?"}
    C -->|是| B
    C -->|否| D{"Tags 匹配?"}
    D -->|否| B
    D -->|是| E["记录 Handle"]
    E --> F{"达到 MaxRemoveCount?"}
    F -->|否| B
    F -->|是| S["停止收集"]
    B --> R["按收集顺序 Remove"]
    S --> R
    R --> H["一次刷新汇总"]
~~~

核心算法：

~~~text
Cleanse(spec):
    query = spec.Query

    若 query 没有任何 All / Any / None 条件:
        返回 0

    removeList.Clear()

    按 InstanceId 升序扫描:
        def = 全局表.Get(instance.ControlId)

        若 !CanBeResisted(def):
            continue

        若 Match(def.Tags, query):
            removeList.Add(instance.Handle)

            若 spec.MaxRemoveCount > 0
               且数量已达到 spec.MaxRemoveCount:
                break

    开始批处理，暂不逐次 RebuildOutputs

    按 removeList 顺序:
        Remove(handle, Cleanse)

    结束批处理
    FlushModuleCommands()
    RebuildOutputsIfDirty()
    返回成功移除数
~~~

净化不按来源、技能 ID 或 MergeKey 匹配。

## 6.12 复合控制的净化

Cleanse 移除整个 Instance。

如果某个复合效果要求“只净化其中一部分”，应在施加时创建两个独立控制实例。

例如技能命中时分别 Add(Slow) 与 Add(SpecialMarkControl)。

不要在一个 Instance 内设计模块级局部净化；它会让句柄、剩余时间和 UI 语义变得模糊。

---

# 七、控制汇总结果与多实例叠加

## 7.1 CrowdControlStateView

~~~csharp
public readonly struct CrowdControlStateView
{
    public readonly UnitActionBlockMask BlockedActions;
    public readonly CrowdControlTagMask ActiveTags;
    public readonly fp MoveSlowRatio;
    public readonly fp AttackSpeedSlowRatio;
}
~~~

四个字段：

| 字段 | 用途 |
|---|---|
| BlockedActions | 单位框架启动与中断动作 |
| ActiveTags | Blind、Airborne、Grounded 等高频状态查询 |
| MoveSlowRatio | 当前最终移动减速 |
| AttackSpeedSlowRatio | 当前最终攻速降低 |

Nearsight 的具体 VisionScale 和强制行为数据由专用查询返回，不继续膨胀通用 View。

## 7.2 多实例总原则

实例从不合并，汇总结果按模块规则组合。

| 输出 | 组合规则 |
|---|---|
| BlockedActions | 按位 OR |
| ActiveTags | 按位 OR |
| MoveSlowRatio | 最大值 |
| AttackSpeedSlowRatio | 最大值 |
| VisionScale | 最小值 |
| ForcedBehavior | 稳定选择一个胜者 |

这些不是 Definition 上的 Aggregation 配置，而是各标准模块的固定语义。

## 7.3 限制叠加

Root 贡献 VoluntaryMove、ControlMove 与 Mobility；Silence 贡献 AbilityCast。同时存在时结果是这些位的并集。Root 移除并重新扫描后，只剩 AbilityCast。

Handler 不会直接把 CanMove、CanCast 改回 true，而是把新的 BlockedActions 整体交给单位框架。

## 7.4 同种控制时间重叠

| 实例 | 区间 |
|---|---|
| Stun 101 | [100, 130) |
| Stun 102 | [110, 140) |

总限制区间为 [100, 140)。

原因是两个实例各自存在，BlockActions 在重叠期取 OR。

不是：

- 自动合并为一个实例；
- 时长相加成 60 Tick；
- 后一个覆盖前一个；
- 按来源刷新。

## 7.5 数值控制

三个 Slow：

| Instance | MoveSlowRatio Key 的值 | ExpireTick |
|---|---:|---:|
| 201 | 0.30 | 160 |
| 202 | 0.50 | 140 |
| 203 | 0.20 | 180 |

MaxMoveSlow 模块结果：

| 区间 | MoveSlowRatio |
|---|---:|
| 202 存在 | 0.50 |
| 202 结束、201 存在 | 0.30 |
| 201 结束、203 存在 | 0.20 |

弱 Slow 实例没有被删除，只是暂时不是最大值。

## 7.6 ForcedBehavior 胜者

ForcedBehavior 模块从已绑定的参数 Key 读取：

- BehaviorId；
- Priority；
- TargetUnitUid 或方向；
- InstanceId 由实例提供。

比较顺序：

1. Priority 更高；
2. StartTick 更晚；
3. InstanceId 更大。

其他 ForcedBehavior 实例继续保留。胜者结束后，下一名在重算时自然接管。

## 7.7 RebuildOutputs

~~~text
RebuildOutputs():
    accumulator.Clear()

    若 IsUnstoppable:
        state = CrowdControlStateView.Empty
        behaviorOverride = Empty
        dirty = false
        return

    按 InstanceId 升序扫描 instances:
        def = 全局表.Get(instance.ControlId)
        accumulator.ActiveTags |= def.Tags

        按 def.CollectOps 顺序:
            executor =
                ModuleExecutorTable[op.ExecutorIndex]
            executor.Collect(
                Owner,
                instance,
                op,
                accumulator)

    newState = accumulator.ToStateView()
    newBehavior = accumulator.BehaviorCandidate

    state = newState
    behaviorOverride = newBehavior
    dirty = false
~~~

Handler 不调用 `Unit.SetCrowdControlBlocks`，也不把控制限制复制进单位框架的第二个细粒度 Mask。单位框架 v26 在固定阶段直接读取 `CrowdControlStateView`：

- Unit 读取它刷新粗粒度 `CapabilityState`；
- ActionArbiter 直接读取它判断新动作；
- ActionArbiter 在固定阶段读取它检查当前 Runtime 是否仍允许继续。

不可阻挡不要求单位框架额外保留动作。Handler 在 RebuildOutputs 前已经把控制来源输出抑制为空，因此单位框架读取不到由这些控制产生的新动作禁止。

## 7.8 Tenacity

是否受韧性影响不由 Kind 决定。

CrowdControlDefinition 使用 DurationRule：

| Rule | 语义 |
|---|---|
| DefaultTenacity | 使用目标最终 Tenacity |
| IgnoreTenacity | 不缩短 |

DurationRule 是创建时间的通用规则，不是控制效果分支。

Handler 在 Add 内读取一次：

~~~text
Tenacity =
    Owner.StatHandler.GetStat(StatId.Tenacity)
~~~

~~~text
effectiveTicks =
    ceil(baseTicks × (1 - clamp(Tenacity)))

effectiveTicks =
    max(MinControlTicks, effectiveTicks)
~~~

Airborne、Suppression 等 Definition 选择 IgnoreTenacity。Slow 与其它普通控制一样，只通过 Tenacity 缩短持续时间，不修改减速比例。

Tenacity 只在 Add 时计算，不追溯修改已经存在的 ExpireTick。

---

# 八、直接 Handler 接入与现行系统边界

## 8.1 最简入口

Unit 直接暴露已缓存 Handler：

~~~csharp
target.CrowdControlHandler.Add(
    controlId,
    durationTicks,
    parameters);
~~~

不使用：

- CrowdControlPort；
- CrowdControlApplyRequest；
- 全局 ControlQueue；
- CombatSystem ControlPipeline。

## 8.2 为什么直接引用不等于高耦合

调用方只依赖目标 Unit 已公开的 Add、Remove、Cleanse 和 AddImmunity 控制能力。

它不依赖：

- Handler 内部实例 List；
- 模块执行表；
- 全局配置表具体容器；
- 单位动作中断实现；
- MovementHandler 内部状态。

这是明确的命令式边界，比隐藏在全局事件总线或 Port 转发层中更容易追踪顺序和返回结果。

## 8.3 外部效果接入契约

技能、Buff、装备或战斗结算等外部效果来源，在各自已经确定顺序的 Gameplay 生效点调用 Handler。控制系统只约束调用语义：

- 调用 Add 时显式传入 ControlId、DurationTicks 与参数 Writer；
- 需要结束自己创建的控制时保存 CrowdControlHandle，并调用 Remove；
- 需要持续免控时保存 CrowdControlImmunityHandle，并在来源失效时调用 RemoveImmunity；
- 跨死亡保留的来源不得沿用旧 ImmunityHandle 或 UnstoppableHandle，必须在 CrowdControlHandler 完成 ClearForRespawn 后重新注册；
- 临时来源死亡后不恢复；
- 依赖某个外部结算结果的控制，必须在该结果确定后再 Add；
- 同一效果内多个 Gameplay 步骤的先后顺序由效果定义明确，Handler 不猜测也不重排。

外部系统内部如何保存句柄、如何组织阶段或结果回调，不属于本文。

## 8.4 单位动作框架接口

~~~mermaid
flowchart TD
    A["Handler RebuildOutputs"] --> B["CrowdControlStateView"]
    B --> C["Unit.RefreshCapabilityState"]
    B --> D["ActionArbiter 直接读取"]
    D --> E["拒绝新动作或中断当前 Runtime"]
~~~

控制系统只维护自己的最终 StateView。死亡、Handler 装配、地图脚本等其它来源由 Unit 在粗粒度 CapabilityState 中汇总；Handler 不覆盖这些来源，也不要求单位框架复制一套细粒度控制状态。

## 8.5 强制行为

BehaviorPlanner 在读取普通 Intent 前调用 unit.CrowdControlHandler.TryGetBehaviorOverride。

有胜者时生成：

- MoveActionRequest；
- AttackActionRequest。

强制行为的来源 InstanceId 保留在 `CrowdControlBehaviorOverride` 中，不复制进全部 ActionRequest 公共字段。胜者改变或实例移除后，BehaviorPlanner 与 ActionArbiter 在固定阶段读取新的 Override 和 StateView，切换或中断旧 Runtime。

Handler 不覆盖 Unit.Intent。

## 8.6 强制移动

强制位移是空间运动覆盖，不是 Action。CrowdControlHandler 完成唯一实例与优先级仲裁后，直接把已获准轨迹交给 MovementHandler；不经过 BehaviorPlanner、ActionArbiter 或 UnitLocomotionAgent。

### 唯一实例规则

| 当前状态 | 新请求结果 |
|---|---|
| 没有活动强制位移 | 创建实例并 StartForcedMove |
| 新 Priority 小于当前 | RejectedByHigherPriority，不创建实例 |
| 新 Priority 等于当前 | 新实例替换旧实例 |
| 新 Priority 大于当前 | 新实例替换旧实例 |
| 不可阻挡生效 | RejectedByUnstoppable，不创建实例 |

同一单位同时最多只有一个带 ForcedMove 标签的控制实例。`activeForcedMoveHandle` 是该唯一实例的权威引用，MovementHandler 不保存或比较控制优先级。

### OnAdd 只提交一次

ForcedMoveOnAdd 模块只在成功创建的新实例 OnAdd 时产生一次命令：

~~~text
没有旧活动实例:
    MovementHandler.StartForcedMove(
        BuildResolvedForcedMove(newInstance))

替换旧活动实例:
    MovementHandler.ReplaceForcedMove(
        BuildResolvedForcedMove(newInstance))
~~~

实例存续期间，CrowdControlHandler 不会再次提交同一个位移请求。逐 Tick 轨迹推进完全由 MovementHandler 的 ForcedMoveRuntime 负责。

### 直接调用路径

~~~text
CrowdControlHandler.OnAdd
    -> ControlModuleCommand
    -> MovementHandler.StartForcedMove
       或 ReplaceForcedMove
    -> MovementHandler.ForcedMoveRuntime
    -> PhysicsEntity2D
~~~

交接值使用 ResolvedForcedMove，至少携带 SourceControlHandle、轨迹配置 ID、DurationTicks、Direction 或 TargetPosition、WallPolicy。它不携带 Priority、Immunity 或路线恢复策略。

控制实例被移除时，Handler 调用 MovementHandler.StopForcedMove(sourceHandle)。MovementHandler 只有在运行轨迹的 SourceControlHandle 仍然匹配时才停止，保证旧实例在原子替换后执行 OnRemove 不会误停新轨迹。

MovementHandler 完成轨迹时调用 Handler.OnForcedMoveFinished(sourceHandle)。Handler 校验它仍是 activeForcedMoveHandle 后只记录 ForcedMoveFinished 信号；对应实例在 Advance 中由自己的 SignalOps 决定移除。

模块不直接写位置，不自己逐帧移动，也不要求 MovementHandler 保存路径。UnitLocomotionAgent 在后续 Tick 读取 PhysicsEntity2D 的实际位置，自行判断偏离或重新寻路。

---

# 九、常见控制的模块组合

## 9.1 Stun

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Stun |
| DurationRule | DefaultTenacity |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、Turn、VoluntaryAttack、AbilityCast、Mobility、ControlMove、ControlAttack |

没有 Stun 函数，也没有 Kind == Stun 分支。

## 9.2 Root

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Root |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、ControlMove、Mobility |

Root 是否允许 Turn 或普通 AbilityCast 由这个模块参数明确表达。

## 9.3 Silence

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Silence |
| Modules | BlockActions |
| BlockedActions | AbilityCast |

如果某项目规则允许特定技能在 Silence 下使用，由单位框架的 Ability 启动规则处理，不给控制模块硬编码技能白名单。

## 9.4 Blind

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Blind |
| Modules | BasicAttackMiss |
| 参数 | 无或命中规则位 |

AttackHandler 在命中阶段查询 ActiveTags 或专用轻量接口。

Blind 不禁止 VoluntaryAttack 启动。

## 9.5 Slow

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Slow |
| Modules | MaxMoveSlow |
| 参数 Key | MoveSlowRatio：调用方写入 fp |

多个 Slow 实例全部保留，MaxMoveSlow 模块取最大值。

## 9.6 Cripple

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Cripple |
| Modules | MaxAttackSpeedSlow |
| 参数 Key | AttackSpeedSlowRatio |

不与 Slow 共用含义模糊的 Aggregation 通道。

## 9.7 Taunt

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Taunt |
| Modules | BlockActions、ForcedBehavior |
| BlockedActions | VoluntaryMove、VoluntaryAttack、AbilityCast |
| 参数 Key | TargetUnitUid、Priority、BehaviorId |

ForcedBehavior 模块提供 ControlAttack / ControlMove 候选，Planner 执行。

## 9.8 Charm

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Charm |
| Modules | BlockActions、ForcedBehavior、可选 MaxMoveSlow |
| 参数 Key | TargetUnitUid、Priority、MoveSlowRatio |

同一组标准模块组合出“向目标移动且减速”的效果。

## 9.9 Fear / Flee

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、ForcedBehavior、Fear |
| Modules | BlockActions、ForcedBehavior |
| 参数 Key | Direction、Priority、MoveScale |

方向必须由确定性 Gameplay 逻辑用 Direction Key 写入 fp2，不使用 Unity Random。

## 9.10 KnockBack

| 项 | 配置 |
|---|---|
| Intensity | High |
| Tags | Control、ForcedMove、Displacement、Airborne |
| DurationRule | IgnoreTenacity |
| Modules | BlockActions、ForcedMoveOnAdd |
| 参数 Key | Direction fp2、Distance fp、MoveTicks int、ForcedMovePriority short |

由于 Intensity = High，控制免疫不会拦截 KnockBack，Cleanse 也不能移除它。

不可阻挡期间，该请求无论 High 与否都直接被 Handler 拒绝，不创建实例。不存在不可阻挡时，Handler 按 ForcedMovePriority 决定启动、替换或拒绝；MovementHandler 不参与优先级判断。

## 9.11 Suppression

| 项 | 配置 |
|---|---|
| Intensity | High 或 Medium，由项目决定 |
| Tags | Control、Suppression |
| DurationRule | IgnoreTenacity |
| Modules | BlockActions |
| BlockedActions | VoluntaryMove、Turn、VoluntaryAttack、AbilityCast、Mobility、EquipmentActive、SummonerSpell、ControlMove、ControlAttack |

若项目要求 Suppression 不可被控制免疫和 Cleanse，配置为 High；若允许解除，配置为 Medium。

## 9.12 Polymorph

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Polymorph |
| Modules | BlockActions、MaxMoveSlow |
| 参数 Key | MoveSlowRatio |

模型变化和图标不属于控制 Definition。

## 9.13 Sleep

| 项 | 配置 |
|---|---|
| Intensity | Medium |
| Tags | Control、Sleep |
| Modules | BlockActions、RemoveOnSignal |
| Signal | ActualDamageTaken |

CombatSystem 在 DamageTaken 正式成立后通过 UnitEventBus 即时调用 Handler。Handler 只记录轻量事实；Sleep 模块只返回 RemoveSelf，不造成任何伤害。

## 9.14 Drowsy 到 Sleep

Drowsy：

| 项 | 配置 |
|---|---|
| Intensity | Low |
| Tags | Control、Drowsy |
| Modules | AddControlOnNaturalExpire |
| 静态模块参数 | SleepControlId |
| 参数 Key | SleepDurationTicks（Int，必填）：转化后 Sleep 的持续 Tick，由施加方在 Add 时写入 |

OnRemove 只有 reason = NaturalExpire 时返回 AddControl 命令。

> v6.2 决议（D-036）：转化后控制的持续时间不放入模块静态参数，而是由
> Drowsy 实例的动态参数 `SleepDurationTicks` 提供。设计案只声明了
> `SleepControlId` 一个静态参数；持续时间属于每次施加的意图，按 4.9 的
> 参数哲学走 Key 参数。Bake 校验 `AddControlOnNaturalExpire` 必须绑定一个
> Int 类型参数偏移。

Cleanse Drowsy 不生成 Sleep。

## 9.15 复合效果何时拆实例

同一个控制 Definition 适合表达“总是一起出现、一起结束、一起净化”的模块组合。

需要拆成多个 Instance 的情况：

- 两部分持续时间不同；
- 两部分净化规则不同；
- 其中一部分可被免疫、另一部分必须保留；
- 两部分需要不同外部句柄；
- 其中一部分不是控制，而是伤害、治疗、护盾或 Buff。

---

# 十、配置精度、运行时约束与帧同步关注数据

## 10.1 Inspector 精度边界

Inspector 允许：

- float 比例；
- Vector2 编辑值；
- 字符串 ParamKey；
- 编辑器字符串和 Tooltip。

这些只属于 CrowdControlDefinition 的模块配置和 ParameterSchema。控制持续时间不在 Definition 中配置，由技能、Buff 或装备自己的 Authoring 转成 Tick 后传入 Add。

Bake 后：

| Authoring | Runtime |
|---|---|
| float ratio | fp |
| Vector2 | fp2 |
| ParamKey string | CrowdControlParamKey |
| enum / string name | 稳定整数 ID |

比赛运行时不从 float 再转换。

## 10.2 运行时数字规则

必须使用：

- LogicTick：全局整数本地帧；
- StartTick / ExpireTick：int；
- 持续时间：Tick；
- 小数：fp；
- 向量：fp2；
- 标识：稳定整数 ID；
- 标签：位掩码。

禁止使用：

- Time.time；
- deltaTime；
- float Gameplay 运算；
- Unity InstanceID；
- 随机 GUID；
- 未排序 Dictionary 枚举决定胜者或净化顺序。

## 10.3 全局 Tick 访问

以下接口内部直接读取 SimulationTickContext.Current.Tick：

- Add；
- Advance；
- AddImmunity；
- AddUnstoppable；
- OnDamageTaken / OnOwnerActionStarted / OnForcedMoveFinished；
- GetRemainingTicks。

调用方不传 `Tick`，也不传 `SimulationTickContext`。需要当前 Tick 的模块执行函数同样直接读取全局上下文，不在 Handler 内继续转传一条 context 参数链。

一次函数内只读取一次并保存到局部变量，避免同一函数跨阶段读取到不同值。

## 10.4 统一回滚接口与帧同步关注数据

CrowdControlHandler 是有状态 Handler，按单位框架 v26 实现：

~~~csharp
IRollback<CrowdControlHandlerSnapshot>
~~~

本案只冻结四阶段语义和必须覆盖的数据，不规定快照数组布局、序列化格式或内存池实现。

### 需要 Capture / Restore 的权威数据

| 数据 | 关注原因 |
|---|---|
| nextInstanceId | 影响未来 Handle 与稳定比较 |
| instances 全部字段 | 影响未来限制、到期和模块参数 |
| nextImmunityId | 影响免疫稳定顺序 |
| immunities 全部字段 | 影响未来 Add 是否成功 |
| nextUnstoppableId | 影响未来不可阻挡 Handle |
| unstoppables 全部字段 | 决定是否抑制控制输出、拒绝强制位移 |
| activeForcedMoveHandle | 决定唯一强制位移权威实例与停止来源校验 |
| pendingSignals | 决定下一次 Advance 会广播哪些事实 |
| signalEffectiveTicks | 决定同 Tick 去重、实例生效 Tick 过滤和两 Tick 查询 |

### 不进入 Snapshot 的数据

| 数据 | 原因 |
|---|---|
| CrowdControlStateView | 从 instances Collect 得到 |
| behaviorOverride | 从 instances Collect 得到 |
| dirty | 恢复后设为 true |
| Definition 引用 | 由 ControlId 从全局表读取 |
| Module Executor 引用 | 全局静态表 |
| pendingCommands | Handler 公共调用结束前必须 Flush；只在单次调用内存在 |
| batchDepth | 快照点不得位于批处理内部 |
| Owner | 由 UnitHandler 与 Unit 聚合关系提供 |

### 四阶段语义

~~~text
Capture(ref state):
    断言 batchDepth == 0
    断言 pendingCommands 为空
    复制全部权威字段到 state

Restore(state):
    直接替换全部权威字段
    不调用 Add / Remove / Cleanse
    不执行模块 Hook
    不发送信号
    不调用 MovementHandler

Resolve(context):
    当前为空
    // Instance、Handle 与参数块只保存稳定逻辑身份，
    // 没有要按对象引用重新解析的数据。

Rebuild(context):
    dirty = true
    RebuildOutputsIfDirty()
~~~

这里的 `Rebuild(in RollbackContext context)` 是统一回滚阶段；普通控制变化使用内部 `RebuildOutputs()`。二者命名和职责不得混用。

建议系统快照点不要落在模块 Hook 与 FlushModuleCommands 之间。若主循环保证调用原子完成，pendingCommands 只需作为临时数据，不必跨帧保存。

MovementHandler 的 `ForcedMoveRuntime` 由移动系统独立恢复。本系统只恢复 `activeForcedMoveHandle`，不复制轨迹进度、起点或墙体策略，也不在 Rebuild 阶段重放 Start / Replace。

---

# 十一、控制系统最终结构

## 11.1 最终核心类关系

~~~mermaid
classDiagram
direction TB

class Unit {
  CrowdControlHandler
  RefreshCapabilityState()
}

class UnitHandler {
  <<MonoBehaviour>>
  Owner
  InitializeForNewRuntime()
  ClearForDeath()
  ClearForRespawn()
  ResetForPool()
}

class CrowdControlHandler {
  <<UnitHandler>>
  instances
  immunities
  unstoppables
  pendingSignals
  activeForcedMoveHandle
  Add()
  Remove()
  Cleanse()
  AddImmunity()
  AddUnstoppable()
  Advance()
  ClearForDeath()
  OnDamageTaken(DamageTakenEvent)
  OnOwnerActionStarted()
  OnForcedMoveFinished()
  Capture()
  Restore()
  Resolve()
  Rebuild()
}

class CrowdControlInstance {
  <<struct>>
  InstanceId
  ControlId
  StartTick
  ExpireTick
  Params
}

class CrowdControlParamBlock {
  FixedBytes64
  TryGetParam()
}

class CrowdControlHandlerSnapshot {
  <<struct>>
  ids
  instances
  immunities
  unstoppables
  signals
  activeForcedMoveHandle
}

class CrowdControlDefinition {
  <<ScriptableObject>>
  ControlId
  Intensity
  Tags
  ParameterSchema
  Modules
  ParamLayout
  HookOps
}

class GameplayConfig {
  <<singleton>>
  CrowdControls
  ModuleExecutors
}

class CrowdControlModuleExecutor {
  OnAdd
  Collect
  OnSignal
  OnRemove
}

class MovementHandler {
  StartForcedMove()
  ReplaceForcedMove()
  StopForcedMove()
}

Unit o-- CrowdControlHandler
UnitHandler <|-- CrowdControlHandler
CrowdControlHandler o-- CrowdControlInstance
CrowdControlInstance *-- CrowdControlParamBlock
CrowdControlHandler ..> CrowdControlHandlerSnapshot : rollback
CrowdControlHandler --> GameplayConfig
GameplayConfig o-- CrowdControlDefinition
GameplayConfig o-- CrowdControlModuleExecutor
CrowdControlHandler --> MovementHandler : approved move
~~~

## 11.2 建议文件数量

~~~text
CrowdControl/
  CrowdControlHandler.cs
  CrowdControlDefinition.cs
  CrowdControlInstance.cs
  CrowdControlParams.cs
  CrowdControlModules.cs
  CrowdControlTypes.cs
  Editor/
    CrowdControlDefinitionBaker.cs
~~~

不为每种控制建立一个类或文件。

## 11.3 最终数据流

~~~mermaid
flowchart TD
    A["外部确定性生效点"] --> B["CrowdControlHandler.Add"]
    B --> C{"不可阻挡且 ForcedMove?"}
    C -->|是| R["拒绝创建"]
    C -->|否| D{"可被免疫且 TagQuery 匹配?"}
    D -->|是| R
    D -->|否| E{"ForcedMove 优先级通过?"}
    E -->|否| R
    E -->|是或非强制位移| F["创建独立 Instance"]
    F --> G["执行已编译模块表"]
    G --> H{"Instance 带 ForcedMove 标签?"}
    H -->|是，仅一次| M["MovementHandler Start / Replace"]
    H -->|否| I["汇总 StateView 与 BehaviorOverride"]
    M --> I
    I --> J{"Handler 正处于不可阻挡?"}
    J -->|是| K["输出空控制结果"]
    J -->|否| L["输出 Block、Tag、数值、强制行为"]
    L --> U["单位框架限制与中断动作"]
    L --> V["BehaviorPlanner 生成 Move / Attack ActionRequest"]
    V --> W["ActionArbiter 仲裁"]
~~~

## 11.4 最终结论

本案以三个最小机制覆盖大多数控制：

~~~text
Modules
    定义控制做什么

Tags
    定义控制如何被识别、免疫和净化

Key Params
    对外按稳定 Key 写读
    对内按紧凑 Offset 执行
~~~

CrowdControlHandler 只负责：

- 创建独立实例；
- 管理时间；
- 遵守 UnitHandler 的新运行时、死亡、复活和回池生命周期；
- 调用已编译模块；
- 汇总结果；
- 处理控制免疫、不可阻挡、净化和轻量信号；
- 将真实 DamageTaken 单位事件转换为不携带 Payload 的内部信号；
- 仲裁唯一强制位移，并在 OnAdd 只向 MovementHandler 提交一次；
- 把动作限制和强制行为候选交给单位框架。

它不按 Kind 分支，不合并同来源控制，不依赖 Buff，不进入 CombatSystem 第四条管线，也不承担伤害、治疗、护盾、表现、快照序列化或回滚协调。恐惧、魅惑、嘲讽等强制行为经过 BehaviorPlanner 与 ActionArbiter；击退、击飞、拉扯等强制位移绕过两者，由 Handler 仲裁后直接交给 MovementHandler。
