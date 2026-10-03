# 控制数字规则与快照恢复

## 本功能范围

本案细化“控制实例与模块参数”中的控制数字规则与快照恢复，仅覆盖下列明确接口与边界。

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

### Inspector 精度边界

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

### 运行时数字规则

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

### 全局 Tick 访问

以下接口内部直接读取 SimulationTickContext.Current.Tick：

- Add；
- Advance；
- AddImmunity；
- AddUnstoppable；
- OnDamageTaken / OnOwnerActionStarted / OnForcedMoveFinished；
- GetRemainingTicks。

调用方不传 `Tick`，也不传 `SimulationTickContext`。需要当前 Tick 的模块执行函数同样直接读取全局上下文，不在 Handler 内继续转传一条 context 参数链。

一次函数内只读取一次并保存到局部变量，避免同一函数跨阶段读取到不同值。

### 统一回滚接口与帧同步关注数据

CrowdControlHandler 是有状态 Handler，按单位框架 v26 实现：

~~~csharp
IRollback<CrowdControlHandlerSnapshot>
~~~

本案只冻结四阶段语义和必须覆盖的数据，不规定快照数组布局、序列化格式或内存池实现。

#### 需要 Capture / Restore 的权威数据

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

#### 不进入 Snapshot 的数据

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

#### 四阶段语义

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

### 最终核心类关系

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

### 建议文件数量

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

### 最终数据流

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


## 需求演进

### 2026-08-06

变动内容：控制配置采用唯一 Definition、模块表和参数布局，不引入额外分类层。

legacyDecision：D-036

