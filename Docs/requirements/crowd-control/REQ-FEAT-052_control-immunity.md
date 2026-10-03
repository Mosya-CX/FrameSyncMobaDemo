# 控制免疫净化与汇总裁决

## 目标实现

多控制实例组合为可查询限制和单一强制行为。

## 技术方案

TagMask、Intensity、ImmunitySpec、UnitActionBlockMask 各司其职；Restrictions 并合，ForcedBehavior 按正式胜者规则，ForcedMove 由控制系统唯一仲裁。

## 边界情况

净化由效果拥有者选择规则；免疫不等于所有法术盾；结构外源控制在访问可选 Handler 前拒绝，自身合法控制保留。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlTypes.cs`：当前关联实现定义 CrowdControlId、CrowdControlIntensity、CrowdControlDurationRule、CrowdControlTagMask、CrowdControlTagQuery（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：Add_CreatesIndependentInstances_NoMerge、Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh、Cleanse_RemovesMatchingNonHigh_RespectsCount、Unstoppable_SuppressesOutput_AndRejectsForcedMove、DamageTakenSignal_RemovesSleepInstance、Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration、Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule。
- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_ClearsChaseRouteAndAttackIntentBeforeSnapshot、DespawnTarget_AtomicallyClearsWindupAndMainRuntime、FormalDeathInvalidation_DoesNotRevokeCommittedAttack、DespawnTarget_DoesNotRevokeCommittedAttack、FormalDeathInvalidation_RejectsNonIncreasingSequence、BeginAndCancel_DoNotConsumeSequence。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 标签的用途

标签只用于：

- 控制免疫匹配；
- 净化匹配；
- 状态查询；
- 少量模块规则匹配。

标签不直接执行控制效果。

效果来自 Modules。

### 数据结构

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

### 建议标签

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

### TagQuery

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

### 标签与模块的关系

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

### 三者不是同一个概念

| 机制 | 归属 | 控制实例是否创建 | 对当前动作的结果 |
|---|---|---:|---:|
| 控制免疫 | CrowdControlHandler | 否 | 该控制不会产生动作限制 |
| 不可阻挡 | CrowdControlHandler | 普通控制创建但被抑制；强制位移不创建 | 不被控制输出中断 |
| 净化 | CrowdControlHandler | 先创建，后移除 | 清除后续限制，但不回滚已经发生的中断 |

### CrowdControlIntensity

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

### 控制免疫

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

### CrowdControlImmunitySpec

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

### AddImmunity

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

### 一次性控制护盾与完整法术盾

两者都从“阻止 Add 创建控制实例”起效，但生命周期拥有者不同。

#### 一次性控制护盾

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

#### 完整法术盾

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

### 不可阻挡

不可阻挡完全由 CrowdControlHandler 保存和执行，不依赖单位框架的 ActionInterruptionGuard。

它与控制免疫的区别：

| 规则 | 控制免疫 | 不可阻挡 |
|---|---|---|
| Low / Medium 普通控制 | 拒绝创建 | 创建并计时，但不输出效果 |
| High 普通控制 | 不能阻止 | 创建并计时，但不输出效果 |
| ForcedMove | High 时仍可能创建 | 无论烈度都拒绝创建 |
| 已存在控制 | 不自动处理 | 立即从汇总结果中抑制 |
| 状态结束 | 没有被创建的控制不会补回 | 尚未到期的普通控制恢复剩余效果 |

#### 运行状态

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

#### 添加与移除

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

#### 抑制规则

不可阻挡期间：

- 新的普通控制仍创建独立实例并正常计算 ExpireTick；
- 普通控制的 OnAdd 即时效果命令被抑制；
- RebuildOutputs 输出空的控制 StateView 和空的 BehaviorOverride；
- SignalOps 与到期仍然推进实例生命周期；
- 带 ForcedMove 标签的新请求直接返回 RejectedByUnstoppable；
- 不创建强制位移实例，也不调用 MovementHandler。

不可阻挡结束后，Handler 重新执行 RebuildOutputs。仍未到期的普通控制从剩余时间继续生效，不重放已经被抑制的 OnAdd 命令。

不可阻挡本身不进入 CrowdControlStateView。需要查询时直接读取 Handler.IsUnstoppable。

### 为什么删除 CrowdControlInterruptMask

Definition 不再声明 InterruptMask。

控制模块只汇总一个 UnitActionBlockMask 类型的 BlockedActions。

Handler 将 BlockedActions 交给单位框架。单位框架已经知道：

- 当前运行的动作占用哪些 ActionMask；
- 哪些动作现在被禁止；
- 如何通过 ActionArbiter 取消不再允许的运行时。

因此由单位框架自动中断对应动作，不需要控制系统再保存一套容易不一致的 InterruptMask。

### UnitActionBlockMask

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

### 净化规则由谁定义

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

#### 解除全部可免除控制

~~~text
Query.All  = Control
~~~

#### 只解除减速

~~~text
Query.All  = Control | Slow
~~~

Airborne、KnockBack 等通常通过 Intensity = High 获得不可免除语义，不再要求 CleanseSpec 逐个排除这些标签。

### Cleanse

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

### 复合控制的净化

Cleanse 移除整个 Instance。

如果某个复合效果要求“只净化其中一部分”，应在施加时创建两个独立控制实例。

例如技能命中时分别 Add(Slow) 与 Add(SpecialMarkControl)。

不要在一个 Instance 内设计模块级局部净化；它会让句柄、剩余时间和 UI 语义变得模糊。

---

### CrowdControlStateView

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

### 多实例总原则

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

### 限制叠加

Root 贡献 VoluntaryMove、ControlMove 与 Mobility；Silence 贡献 AbilityCast。同时存在时结果是这些位的并集。Root 移除并重新扫描后，只剩 AbilityCast。

Handler 不会直接把 CanMove、CanCast 改回 true，而是把新的 BlockedActions 整体交给单位框架。

### 同种控制时间重叠

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

### 数值控制

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

### ForcedBehavior 胜者

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

### RebuildOutputs

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

### Tenacity

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

### 最简入口

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

### 为什么直接引用不等于高耦合

调用方只依赖目标 Unit 已公开的 Add、Remove、Cleanse 和 AddImmunity 控制能力。

它不依赖：

- Handler 内部实例 List；
- 模块执行表；
- 全局配置表具体容器；
- 单位动作中断实现；
- MovementHandler 内部状态。

这是明确的命令式边界，比隐藏在全局事件总线或 Port 转发层中更容易追踪顺序和返回结果。

### 外部效果接入契约

技能、Buff、装备或战斗结算等外部效果来源，在各自已经确定顺序的 Gameplay 生效点调用 Handler。控制系统只约束调用语义：

- 调用 Add 时显式传入 ControlId、DurationTicks 与参数 Writer；
- 需要结束自己创建的控制时保存 CrowdControlHandle，并调用 Remove；
- 需要持续免控时保存 CrowdControlImmunityHandle，并在来源失效时调用 RemoveImmunity；
- 跨死亡保留的来源不得沿用旧 ImmunityHandle 或 UnstoppableHandle，必须在 CrowdControlHandler 完成 ClearForRespawn 后重新注册；
- 临时来源死亡后不恢复；
- 依赖某个外部结算结果的控制，必须在该结果确定后再 Add；
- 同一效果内多个 Gameplay 步骤的先后顺序由效果定义明确，Handler 不猜测也不重排。

外部系统内部如何保存句柄、如何组织阶段或结果回调，不属于本文。

### 单位动作框架接口

~~~mermaid
flowchart TD
    A["Handler RebuildOutputs"] --> B["CrowdControlStateView"]
    B --> C["Unit.RefreshCapabilityState"]
    B --> D["ActionArbiter 直接读取"]
    D --> E["拒绝新动作或中断当前 Runtime"]
~~~

控制系统只维护自己的最终 StateView。死亡、Handler 装配、地图脚本等其它来源由 Unit 在粗粒度 CapabilityState 中汇总；Handler 不覆盖这些来源，也不要求单位框架复制一套细粒度控制状态。

### 强制行为

BehaviorPlanner 在读取普通 Intent 前调用 unit.CrowdControlHandler.TryGetBehaviorOverride。

有胜者时生成：

- MoveActionRequest；
- AttackActionRequest。

强制行为的来源 InstanceId 保留在 `CrowdControlBehaviorOverride` 中，不复制进全部 ActionRequest 公共字段。胜者改变或实例移除后，BehaviorPlanner 与 ActionArbiter 在固定阶段读取新的 Override 和 StateView，切换或中断旧 Runtime。

Handler 不覆盖 Unit.Intent。

### 强制移动

强制位移是空间运动覆盖，不是 Action。CrowdControlHandler 完成唯一实例与优先级仲裁后，直接把已获准轨迹交给 MovementHandler；不经过 BehaviorPlanner、ActionArbiter 或 UnitLocomotionAgent。

#### 唯一实例规则

| 当前状态 | 新请求结果 |
|---|---|
| 没有活动强制位移 | 创建实例并 StartForcedMove |
| 新 Priority 小于当前 | RejectedByHigherPriority，不创建实例 |
| 新 Priority 等于当前 | 新实例替换旧实例 |
| 新 Priority 大于当前 | 新实例替换旧实例 |
| 不可阻挡生效 | RejectedByUnstoppable，不创建实例 |

同一单位同时最多只有一个带 ForcedMove 标签的控制实例。`activeForcedMoveHandle` 是该唯一实例的权威引用，MovementHandler 不保存或比较控制优先级。

#### OnAdd 只提交一次

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

#### 直接调用路径

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


## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

### 2026-09-01

变动内容：建筑中央准入仅允许规定外源普通攻击，自身效果允许，合法拒绝是成功空操作。

legacyDecision：D-054

