# 控制定义Bake与模块执行

## 本功能范围

本案细化“控制实例与模块参数”中的控制定义Bake与模块执行，仅覆盖下列明确接口与边界。

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

### 只保留一套 Definition

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

### CrowdControlDefinition 字段

CrowdControlDefinition 描述一个控制由哪些模块组成，以及它如何被免疫和识别。

#### Inspector 字段

| 字段 | 作用 |
|---|---|
| ControlId | 全局稳定配置 ID |
| Intensity | 控制烈度：Low、Medium、High |
| Tags | 轻量逻辑标签位 |
| DurationRule | 是否受 Tenacity 影响；不提供默认持续时间 |
| ParameterSchema | 参数 Key、类型、是否必填和字节容量 |
| Modules | 按执行顺序排列的模块配置 |

#### 同一 Definition 内的隐藏 Bake 字段

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

### 为什么仍然需要 Bake

Bake 不是生成第二个 Definition，而是把同一资产中的编辑器表达编译到隐藏字段：

| Inspector 表达 | Bake 后 |
|---|---|
| 字符串参数 Key | 稳定 ParamKeyId |
| float 模块常量 | fp |
| 模块配置数组 | 按 Hook 分组的紧凑 ControlModuleOp |
| 参数类型声明 | ParamLayout 的 Type、Offset、Size |

运行时只读取隐藏字段。这样既只有一份 CrowdControlDefinition，又不需要在热路径使用字符串、float、反射或可变编辑器数组。

### 为什么不需要 Kind

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

### 为什么不保存软控、硬控

软控、硬控是玩家根据当前实际限制形成的分类，不是底层固定规则。

例如同一个复合控制：

- 只贡献 20% Slow 时通常被理解为软控；
- 同时贡献 Move 与 Cast 禁止时会被理解为硬控；
- 某单位拥有动作保护时，当前动作可能继续，但新动作仍被阻止。

控制系统不存储 Soft、Hard。

如果 UI 或统计需要动态判断，可从当前模块输出派生：存在强制行为或阻止主要动作时可显示为强控制；只改变数值或命中规则时可显示为弱控制。

该派生值不参与控制逻辑。

### Bake 流程

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

### 模块的定位

模块是“一个很小、职责单一、可复用的控制函数”。

一个控制定义可以组合多个模块。例如眩晕只使用 BlockActions；变形组合 BlockActions 与 MaxMoveSlow；魅惑组合 BlockActions 与 ForcedBehavior；击退组合 BlockActions 与 ForcedMoveOnAdd。

Handler 只循环模块操作数组，不知道这些组合叫什么。

### 四个生命周期 Hook

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

### Inspector 中的模块配置

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

### ControlModuleOp

Definition 的隐藏 Bake 数据中，每个操作是紧凑值类型：

| 字段 | 作用 |
|---|---|
| ExecutorIndex | 静态执行函数表下标 |
| Hook | 所属生命周期阶段；Bake 后通常已按数组分开 |
| StaticData | 模块自己的少量整数或位掩码 |
| StaticFp0 / StaticFp1 | Bake 后定点静态参数 |
| ParamOffset0 / 1 / 2 | 由 ParamKey 编译得到的实例字节偏移 |

不同模块只解释自己需要的字段。Handler 不解释 StaticData 或 ParamOffset 的含义。

### 静态执行函数表

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

### 为什么不用每实例模块对象

假设单位当前有 5 个控制，每个控制 3 个模块：

| 方案 | 运行对象 |
|---|---:|
| 每实例模块对象 | 5 个 Instance + 15 个 Module 对象 |
| 本案 | 5 个 Instance，模块数组由全局定义共享 |

实例只保存 ControlId。模块操作数组从全局表中的 CrowdControlDefinition 读取。

### ControlAccumulator

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

### 建议的标准模块

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

### 自定义模块的最小边界

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

### 模块命令与重入

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

### 模块性能

重算成本近似为“活动实例数 × 每个 Definition 的 Collect 模块数”。

控制实例通常很少，且只在以下情况重算：

- Add；
- Remove；
- 免疫不影响既有实例，通常不重算；
- 相关 Stat 变化；
- 信号导致实例变化。

没有每帧对全部模块的无条件调用。

---



## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

