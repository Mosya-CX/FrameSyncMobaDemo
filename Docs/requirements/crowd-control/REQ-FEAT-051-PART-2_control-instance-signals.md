# 控制实例增删与信号查询

## 本功能范围

本案细化“控制实例与模块参数”中的控制实例增删与信号查询，仅覆盖下列明确接口与边界。

## 目标实现

配置型控制通过静态模块表和有界参数块执行。

## 技术方案

CrowdControlHandler 唯一运行入口；Definition 经 Bake 形成 module op 与 ParamLayout；实例按 Key 暴露、按 Offset 存储，不创建每实例模块对象。

## 边界情况

不新增 Kind、软硬控枚举或 Combat 控制管线；模块重入使用明确延迟规则；缺键和容量溢出可见失败。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

实现证据与已有测试位置关联总案；字段存在不能认定行为已验收。

### Add

#### 定位

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

#### 输入

| 参数 | 说明 |
|---|---|
| id | 全局控制静态定义 ID |
| durationTicks | 本次基础持续 Tick；必须是运行时整数 |
| parameters | 按稳定 Key 写入的本次动态参数 |

durationTicks 可使用常量 Infinite 表示外部句柄生命周期。

#### 返回

CrowdControlAddResult 是轻量值类型：

| 字段 | 作用 |
|---|---|
| Status | Added、BlockedByImmunity、RejectedByUnstoppable、RejectedByHigherPriority、InvalidDefinition、InvalidParams、InvalidDuration、OwnerRejected |
| Handle | 只有 Added 时有效 |
| BlockingImmunityId | 只有被免疫拦截时有效，用于 Gameplay 反馈或诊断 |

调用方不再用 InvalidHandle 猜测失败原因。

#### 执行流程

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

#### 核心算法

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

### Remove

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

### RemoveAll

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

### Advance

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

### 轻量信号模块

信号是单位框架与控制实例之间的轻量事实转发器。它只告诉控制实例“发生了什么”，不告诉实例应该执行 Remove、AddControl 或其它命令。

推荐只定义控制确实需要的信号：

| SignalType | 记录条件 | 示例 |
|---|---|---|
| ActualDamageTaken | DamageTakenEvent.ActualLifeDamage 大于 0 | 打破 Sleep |
| OwnerActionStarted | 单位框架确认动作启动 | 某些控制在动作开始后移除 |
| ForcedMoveFinished | 来源 Handle 等于当前活动强制位移 | 位移结束后移除实例 |

信号不保存通用 Payload，不携带伤害公式、动作 Runtime 或移动轨迹。外部入口先完成必要条件判断，再记录对应信号位。

#### 信号数据

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

#### 记录算法

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

#### 明确入口

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

#### 实例判断

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

### 查询接口

#### State

返回轻量 CrowdControlStateView，不分配内存。

#### HasAnyTag

判断 state.ActiveTags 与输入 tags 是否存在交集，适合 Blind、Airborne、Grounded 等高频查询。

#### MatchesTags

对当前 ActiveTags 执行完整 TagQuery。用于少量复杂规则，不用于找具体实例。

#### TryGetBehaviorOverride

若当前存在强制行为胜者，返回其临时值；否则返回 false。

#### GetRemainingTicks

可选接口。按 Handle 找实例并返回 ExpireTick - SimulationTickContext.Current.Tick 与 0 的较大值；Infinite 返回约定常量。

#### FillInstances

仅当玩法、AI 或外部系统确实需要逐实例读取时提供。调用方传入可复用 List，Handler 不返回新数组。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

