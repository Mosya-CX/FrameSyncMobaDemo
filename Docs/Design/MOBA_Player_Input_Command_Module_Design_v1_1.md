# MOBA 玩家输入与 Gameplay Command 生成模块设计案 v1.1

> 目标：为 Unity 帧同步 MOBA 提供本地玩家的移动、普通攻击以及 Q/W/E/R 四个技能键输入，并把本地设备操作转换成现有帧同步系统的类型化 Gameplay Command Request。  
> 当前版本支持 **鼠标 + 键盘、单个本地玩家、全面非智能施法**：输入层是
> "物理事件 → 技能信号 / 指令" 的可组合翻译层；**本版本默认配置为蓄力/
> 引导技能按下启用、左键提交，技能键松开不产生任何 AbilitySignal**，
> 但架构允许任何技能按组合配置（含松键触发、三段式触发）。
> UI 输入继续由 Unity Input System 与 `InputSystemUIInputModule` 直接处理，不经过本模块。

---

# 目录

1. [总体定位与边界](#一总体定位与边界)
2. [总体结构](#二总体结构)
3. [Unity Input System 配置](#三unity-input-system-配置)
4. [玩家输入模式的离线派生](#四玩家输入模式的离线派生)
5. [本地输入事件缓冲](#五本地输入事件缓冲)
6. [Gameplay 输入门禁](#六gameplay-输入门禁)
7. [鼠标世界解析](#七鼠标世界解析)
8. [移动与普通攻击](#八移动与普通攻击)
9. [本地技能输入状态](#九本地技能输入状态)
10. [通用技能输入规则](#十通用技能输入规则)
11. [按下启用、左键提交](#十一按下启用左键提交)
12. [AimSnapshot](#十二aimsnapshot)
13. [Gameplay Command Request 接口](#十三gameplay-command-request-接口)
14. [技能系统与 AI 边界](#十四技能系统与-ai-边界)
15. [技能指示器](#十五技能指示器)
16. [生命周期与 UI 交互](#十六生命周期与-ui-交互)
17. [确定性、回滚与重演](#十七确定性回滚与重演)
18. [错误处理与性能](#十八错误处理与性能)
19. [当前版本明确不做](#十九当前版本明确不做)
20. [推荐实现顺序](#二十推荐实现顺序)
21. [验收测试](#二十一验收测试)
22. [编码准入结论](#二十二编码准入结论)

---

# 一、总体定位与边界

## 1.1 模块职责

玩家输入模块负责：

```text
监听 Unity Input System 的 Gameplay Action。
把按下、松开和鼠标点击转换为稳定的本地输入事件。
读取当前鼠标屏幕坐标。
把鼠标解析为地面 fp2 和候选 UnitUid。
解析右键移动或普通攻击。
处理 Q/W/E/R 技能槽输入。
读取由 CastModelDef / 技能数据离线校验的输入映射模板。
维护本地技能输入状态。
调用技能指示器。
把输入事件与映射模板分析为施法意图（Proposal）。
把施法意图交给单位 Planner / Arbiter 裁断；
通过后翻译为现有 AbilitySignal 语言并生成类型化 Command。
防止重复 Commit（重复左键、快速连按）产生重复 Command。
处理 UI、Application Flow 和受控单位变化造成的输入阻断。
```

它不负责：

```text
定义第二套 Ability 协议。
定义第二套网络 Command Schema。
直接创建最终 CanonicalCommandBytes。
分配最终 CommandSeq 或 TargetTick。
向网络发送数据。
执行 AbilityHandler。
计算蓄力时间、最大蓄力时间、伤害或射程。
判断目标 Tick 时技能一定能成功。
替 AI 设计技能决策层。
修改 Unit、AbilityRuntime、AttackHandler 或 Locomotion。
驱动普通 UI。
进入 GameplaySnapshot 或 SharedGameplayChecksum。
回滚时重新读取设备输入。
```

## 1.2 当前输入范围

```text
鼠标右键：
    普通状态下解析为 Attack 或 Move。
    尚未发送真实 Focus 的本地 Aim 中，取消本地 Aim。
    已发送 Focus 的技能中，不发送 Cancel；
    按普通规则继续解析 Attack 或 Move。

鼠标左键：
    当前存在可 Commit 的本地技能输入上下文时，
    默认生成 Commit。
    没有技能输入上下文时不生成 Gameplay Command。

Escape：
    本地 Aim 阶段关闭本地指示器。
    已启用的技能是否允许玩家 Cancel，
    由离线派生的输入模式决定。
    当前 HoldRelease 默认不允许玩家 Cancel。

Q / W / E / R：
    固定对应 AbilitySlot 0 / 1 / 2 / 3。
```

当前不加入：

```text
A 键攻击移动
S 键停止
H 键保持
强制攻击
多单位编队
宠物快捷键
智能施法
自动寻找最近目标
手柄和触屏
```

## 1.3 UI 边界

UI 使用自己的 Unity Input System UI Action：

```text
Point
Click
RightClick
ScrollWheel
Navigate
Submit
Cancel
```

由：

```text
EventSystem
InputSystemUIInputModule
Lua / C# UI 页面
```

直接处理。

玩家输入模块：

```text
不转发 UI 输入。
不替 UI 生成 GameplayCommand。
只查询 UI 是否阻断 Gameplay 键盘或鼠标输入。
```

---

# 二、总体结构

```mermaid
flowchart TD
    A[Unity Input System Gameplay Actions] --> B[UnityGameplayInputSource]
    B --> C[LocalInputEventBuffer]
    C --> D[LocalPlayerGameplayInputModule]

    E[IGameplayInputGate] --> D
    F[GameplayPointerResolver] --> D
    G[InputMappingTemplate] --> D
    H[ILocalAbilityRuntimeView] --> D
    I[AbilityIndicatorController] --> D
    J[ILocalControlledUnitView] --> D

    D --> K[IPlayerGameplayCommandRequester]
    K --> L[FrameSyncGameRuntime]
    L --> M[CommandCollector]
```

## 2.1 核心对象

```text
LocalPlayerGameplayInputModule
    本模块组合根。
    按本地事件顺序处理输入。
    维护本地技能输入状态和提交去重。

UnityGameplayInputSource
    订阅 Unity InputAction。
    只写 LocalInputEventBuffer。

LocalInputEventBuffer
    保存本 Unity 帧内的按下、松开和鼠标事件。

GameplayPointerResolver
    把屏幕坐标解析为地面坐标和候选 UnitUid。

InputMappingTemplate
    每技能一份，由 CastModelDef / 技能数据离线校验。
    只描述物理输入如何翻译为现有技能信号。

ILocalAbilityRuntimeView
    只读观察当前受控单位的 AbilitySession 和阶段。
    不修改技能 Runtime。

AbilityIndicatorController
    显示本地 Aim 和 Gameplay Session 指示器。
    不执行技能。

IGameplayInputGate
    判断 UI 和应用流程是否阻断 Gameplay 输入。

IPlayerGameplayCommandRequester
    由 FrameSync Request 层实现。
```

## 2.2 玩家与 AI 的分流

```text
玩家：
Unity Input System
    -> 玩家输入模块
    -> CastAbilityCommand
    -> OrderTranslator
    -> AbilityAction
    -> AbilityHandler
    -> AbilitySignal

AI：
AIController
    -> AbilityAction
    -> AbilityHandler
    -> AbilitySignal
```

AI：

```text
不经过 Unity Input System。
不经过玩家输入模块。
不生成帧同步网络 Command。
不读取 InputMappingTemplate。
```

玩家和 AI 只在现有技能系统的：

```text
AbilityAction
AbilitySignal
AbilityRuntime
CastModelDef
```

语义上汇合。

---

# 三、Unity Input System 配置

## 3.1 Action Map

```text
PlayerInputActions.inputactions
    Gameplay
    UI
```

`UI` Map 配置给 `InputSystemUIInputModule`。

`Gameplay` Map：

| Action | Type | Control Type | 默认绑定 |
|---|---|---|---|
| `PointerPosition` | Value | Vector2 | `<Pointer>/position` |
| `PrimaryClick` | Button | Button | `<Mouse>/leftButton` |
| `SecondaryClick` | Button | Button | `<Mouse>/rightButton` |
| `Cancel` | Button | Button | `<Keyboard>/escape` |
| `AbilityQ` | Button | Button | `<Keyboard>/q` |
| `AbilityW` | Button | Button | `<Keyboard>/w` |
| `AbilityE` | Button | Button | `<Keyboard>/e` |
| `AbilityR` | Button | Button | `<Keyboard>/r` |

## 3.2 技能键按下与松开都采集为事件

```text
AbilityQ / W / E / R performed
    -> AbilityKeyPressed

AbilityQ / W / E / R canceled
    -> AbilityKeyReleased
```

`AbilityKeyReleased` 是**原始输入事件**，不直接等于任何信号。它翻译
成什么由该技能的输入组合决定（见 §4.2）：

```text
本版本默认组合
    AbilityKeyReleased -> None（不 Commit、不 Cancel）

其它技能可以配置
    AbilityKeyReleased -> Commit
    AbilityKeyReleased -> Cancel（默认不启用）
```

禁止在输入层把某一事件硬编码为"永远无信号"或"永远等于某信号"；
每个事件到信号的映射都由该技能的映射模板确定（离线检查，不做
Bake；运行时只读取已校验配置）。

`PointerPosition` 不写事件队列，只用于：

```text
每帧更新指示器。
按钮事件发生时捕获 ScreenPositionAtEvent。
```

## 3.3 显式订阅

推荐使用：

```text
InputActionReference
或生成的强类型 InputAction Wrapper
```

禁止：

```text
SendMessage
BroadcastMessage
字符串方法名回调
运行时反射寻找 Action
```

`OnEnable` 订阅，`OnDisable` 取消订阅。

---

# 四、玩家输入模式的离线派生

## 4.1 不重复配置技能 Gameplay 数据

玩家输入配置中禁止出现：

```text
MinFocusTicks
MaxFocusTicks
AutoCommitTick
技能伤害
技能射程
技能宽度
蓄力曲线
阶段持续时间
Cooldown
```

这些数据只能存在于：

```text
AbilityDef
CastModelDef
StageDef
AbilityRuntime
AbilitySession
Blackboard
```

玩家输入层只关心：

> 一个物理事件按该技能的映射模板，应该产生什么施法意图：
> 某个技能信号（Focus / Commit / Cancel）、仅本地 Aim，还是无动作。

## 4.2 输入翻译组合（可组合的翻译层）

输入层把物理事件翻译给技能层 / Command 层。翻译不是三种写死的模式，
而是一组可任意组合的绑定：

```csharp
public enum InputTrigger : byte
{
    AbilityKeyPressed,   // 技能键按下
    AbilityKeyReleased,  // 技能键松开
    PrimaryClick,        // 鼠标左键
    SecondaryClick,      // 鼠标右键
    Cancel,              // Escape
}

public enum InputTranslation : byte
{
    None,           // 不产生 AbilitySignal
    LocalAimOnly,   // 只打开本地瞄准（表现），不产生信号
    CancelLocalAim, // 只关闭本地瞄准（表现），不产生信号
    Focus,          // AbilitySignal.Focus
    Commit,         // AbilitySignal.Commit
    Cancel,         // AbilitySignal.Cancel（默认不配置）
}

public struct InputBinding
{
    public InputTrigger Trigger;
    public InputTranslation Translation;
    public bool CaptureAim; // 触发时是否捕获 AimSnapshot
}

public struct InputMappingTemplate
{
    public InputBinding[] Bindings; // 每种 Trigger 最多一条
}
```

当前版本不需要在映射模板中重复保存任何技能时间或数值。

组合自由：

```text
同一 AbilitySignal.Commit 可以由左键、松键或两者同时提供。
三段式（按下 -> 松开 -> 左键）可以分别绑定不同翻译。
每个技能独立配置自己的 Bindings。
```

输入层不把"松开"写死成无信号，也不把"左键"写死成唯一 Commit；
是否映射、映射成什么由该技能的映射模板决定。**映射模板没有规定的
事件，一律不动作**（不产生信号、不改变本地状态）。
当前蓄力型施法模型（HoldRelease）的默认预设采用松键 None（见 §4.4）；
这只是该模型的当前默认，不是硬约束，其它技能仍可在配置期自由组合。
重复触发去重（§9.3）保证同一种信号不会因为多事件绑定而发出多次。

输入层是**分析器**：它只回答"这个输入事件按模板应该产生什么意图"，
不直接决定施法是否成立。施法成立与否由单位 Planner / Arbiter 裁断
（见 §4.5）。

## 4.3 映射模板：配置数据 + 离线合法性检查（不做 Bake）

输入映射模板是**每技能的配置数据**（Inspector / ScriptableObject），
CastModelDef 是生成默认模板的唯一来源：

```text
AbilityDef
    -> CastModelDef
    -> 默认映射模板（每技能一份 Bindings）
    -> 允许技能数据自定义覆盖
```

运行时直接读取已校验的模板：

```text
AbilitySlot
    -> ActiveAbilityId
    -> AbilityDef
    -> 该技能的映射模板（Bindings）
```

本设计**不做 Bake**（不生成派生的代码或烘焙产物）。合法性在编辑期
离线检查：

```text
同一 Trigger 只能绑定一条翻译。
蓄力/引导类技能必须至少存在一条 Commit 来源。
需要 Aim 的模板必须有合法的 Indicator / Aim 定义。
QWER 槽位必须引用存在的 AbilityDef。
```

离线检查失败即配置报错；运行时只读取已通过检查的配置，禁止临时猜测。

建议的默认组合（默认预设）：

```text
CommitCastModelDef
    且无需本地 Aim
        AbilityKeyPressed -> Commit

CommitCastModelDef
    且 ResolveIndicatorStage 需要玩家 Aim
        AbilityKeyPressed  -> LocalAimOnly
        PrimaryClick       -> Commit + CaptureAim
        SecondaryClick     -> CancelLocalAim
        Cancel             -> CancelLocalAim

HoldReleaseCastModelDef
        AbilityKeyPressed   -> Focus
        PrimaryClick        -> Commit + CaptureAim
        AbilityKeyReleased  -> None
        SecondaryClick      -> None（继续 Move / Attack）
        Cancel              -> None
```

自定义 CastModelDef 必须提供可离线检查的映射模板派生，否则：

```text
离线检查失败。
禁止在运行时按类型猜测。
```

## 4.4 本版本默认组合与允许的组合

本版本冻结的默认组合（HoldRelease / Channel）：

```text
技能键按下
    -> Focus

技能键松开
    -> 不产生任何 AbilitySignal
    -> 不 Commit、不 Cancel

鼠标左键
    -> Commit

鼠标右键
    -> 不发送 Cancel
    -> 继续正常解析 Move / Attack

Escape
    -> 不发送 Cancel
```

蓄力/引导的结束由 Gameplay 侧决定：左键 Commit 后进入 Release
Stage，或引导时长/阶段规则在 Ability 系统内自然结束；输入层不
因为松键而提交或取消。

架构允许的组合（任何技能都可以在配置期通过 Bindings 定义）：

```text
经典蓄力（松键释放）
    AbilityKeyPressed  -> Focus
    AbilityKeyReleased -> Commit + CaptureAim
    PrimaryClick       -> Commit + CaptureAim

三段式（按下蓄力、松开预备、左键最终释放）
    AbilityKeyPressed  -> Focus
    AbilityKeyReleased -> None / CancelLocalAim / 预留阶段信号
    PrimaryClick       -> Commit + CaptureAim
```

本版本只冻结"默认预设不含松键 Commit"（蓄力型 HoldRelease 当前即采用
该默认预设）；任何技能都可以在配置期定义自己的组合（含松键触发、
三段式触发），组合必须通过离线合法性检查，运行时不允许临时猜测。

模板未规定的事件一律不动作：

```text
例如模板里没有 AbilityKeyReleased 条目
    -> 松开技能键不产生任何信号、不改变状态
例如模板里没有 SecondaryClick 条目
    -> 右键不产生 Cancel，也不改变技能 Session
```

## 4.5 输入 → 施法意图 → Planner / Arbiter → 技能层语言

输入层是分析器，施法是否成立由单位行为层裁断：

```text
物理输入事件
    + 该技能的映射模板
    -> 施法意图（Proposal）
        信号意图（Focus / Commit / Cancel）
        或仅本地 Aim
        或无动作（模板未规定）

施法意图
    -> 单位 Planner / Arbiter 裁断（Unit Framework v27.3 §3）
    -> Rejected：不产生 Command，保持本地状态
    -> Accepted / Interrupt：翻译为技能层语言
         AbilitySignalVerb + Aim
         -> CastAbilityCommand
```

执行期：

```text
有 Planner 的单位
    CastAbilityCommand
    -> UnitIntent(CastAbility)
    -> 行为链（Planner）产出 ActionRequest
    -> Arbiter 裁断
    -> AbilityHandler.HandleSignal(AbilitySignal)

无 Planner 的单位（兼容路径）
    CastAbilityCommand
    -> AbilityHandler.HandleSignal(AbilitySignal)
```

输入模块在意图阶段不修改任何 Gameplay 状态。

---

# 五、本地输入事件缓冲

## 5.1 回调只写事件

InputAction 回调中禁止：

```text
访问 Gameplay Runtime。
执行 Raycast。
修改本地技能状态。
调用 Command Request。
```

回调只写 `LocalInputEventBuffer`。

在：

```text
LocalPlayerGameplayInputModule.LateUpdate()
```

中统一处理，使：

```text
UI 已经有机会处理本帧输入。
同 Unity 帧事件拥有明确顺序。
Command Request 只从一个入口生成。
```

## 5.2 事件结构

```csharp
public enum LocalGameplayInputEventKind : byte
{
    PrimaryClick,
    SecondaryClick,
    Cancel,
    AbilityKeyPressed,
    AbilityKeyReleased
}
```

```csharp
public struct LocalGameplayInputEvent
{
    public ulong LocalEventSequence;
    public LocalGameplayInputEventKind Kind;
    public AbilitySlot AbilitySlot;
    public Vector2 ScreenPositionAtEvent;
}
```

对于非技能键事件：

```text
AbilitySlot 写入规范默认值。
```

`LocalEventSequence`：

```text
只用于本地稳定处理顺序。
不进入 GameplayCommand。
不发送网络。
不进入快照。
```

## 5.3 同帧顺序

严格按：

```text
LocalEventSequence 升序
```

处理。

例如快速点击：

```text
Q performed
Q canceled
```

可在同一 Unity 帧中依次生成：

```text
Focus
Commit
```

Command Request 层按调用顺序分配 `CommandSeq`。

## 5.4 缓冲上限

建议：

```text
MaxLocalInputEventsPerUnityFrame = 64
```

溢出时：

```text
记录本地诊断。
丢弃新事件。
不产生不完整 Command。
```

它不是确定性 Gameplay 错误。

---

# 六、Gameplay 输入门禁

## 6.1 接口

```csharp
public interface IGameplayInputGate
{
    bool CanAcceptKeyboardGameplayInput
    {
        get;
    }

    bool IsPointerGameplayBlocked(
        Vector2 screenPosition);
}
```

## 6.2 普通阻断

以下情况阻断新的 Gameplay 输入：

```text
输入框拥有键盘焦点。
聊天或控制台正在输入。
鼠标位于消费点击的 UI 上。
UI 捕获 Pointer。
存在模态页面。
Application Flow 不在 GameplayRunning。
本地玩家未绑定 ControlledUnitUid。
比赛已经权威结束。
```

## 6.3 松键语义由技能输入组合决定

一旦玩家输入模块成功发送 `Focus Request`，技能键松开映射成什么由
该技能的映射模板组合决定；本版本默认组合为 None。键盘 Gate 关闭、
聊天框获得焦点等场景都不会让松键额外改变技能 Session。

但以下情况可以直接清理本地输入上下文：

```text
离开对局。
客户端断线。
ControlledUnitUid 改变。
Gameplay Action Map 被永久禁用。
```

Gameplay 中真实 AbilitySession 的中断由 Unit / Ability 生命周期负责。

## 6.4 门禁不是最终合法性

门禁不判断：

```text
沉默
眩晕
冷却
法力
目标 Tick 距离
目标 Tick 目标存活
```

这些由正式 Gameplay 执行层判断。

---

# 七、鼠标世界解析

## 7.1 输出

```csharp
public struct GameplayPointerSnapshot
{
    public bool HasGroundPoint;
    public fp2 GroundPoint;

    public bool HasHoveredUnit;
    public UnitUid HoveredUnitUid;
}
```

## 7.2 地面点

```text
ScreenPosition
    -> Gameplay Camera Ray
    -> Ground Plane / Ground Surface
    -> 逻辑平面坐标
    -> Command 精度量化
    -> fp2 GroundPoint
```

Command 不保存：

```text
屏幕坐标
Camera Ray
Unity Vector3
Collider
Transform
```

## 7.3 Unit 选择

表现对象使用：

```text
UnitSelectionProxy
    UnitUid
    SelectionPriority
```

多个命中时：

```text
过滤无代理 Collider。
按 UnitUid 去重。
按 Ray Distance 升序。
距离相同时按 SelectionPriority。
仍相同时按 UnitUid。
选择第一项。
```

最终 Attack 或 Unit Aim 只保存稳定 `UnitUid`。

## 7.4 本地预测差异

鼠标解析基于客户端当前预测世界。

因此允许：

```text
本地点击时目标存在。
目标 Tick 时目标已经死亡。
Command 最终执行失败。
```

输入模块不自动替换目标或改变 Command 类型。

---

# 八、移动与普通攻击

## 8.1 普通右键

当本地技能输入状态允许右键进入世界操作时：

```text
右键敌方可选 Unit
    -> RequestAttack(TargetUnitUid)

否则存在 GroundPoint
    -> RequestMove(GroundPoint)
```

## 8.2 本地攻击目标最小检查

```text
UnitUid 当前存在。
不是受控单位自身。
阵营敌对。
当前不是 Dead。
允许成为普通攻击选择对象。
```

不检查：

```text
攻击距离
攻击冷却
当前控制状态
目标 Tick 是否仍有效
```

## 8.3 不自动改写

```text
Attack Command 在目标 Tick 失败
    不自动转换为 Move。

Move Command
    不携带 Pathfinding 策略、RVO 参数或 StopRange。
```

## 8.4 不同技能状态下的右键

```text
Idle
    -> 普通 Move / Attack。

LocalAiming
    -> 关闭本地 Aim。
    -> 本次右键不同时生成 Move / Attack。

FocusRequested
GameplayFocusing
CommitRequested
    且该技能配置了蓄力（Focus）组合
    -> 不发送 Cancel。
    -> 不关闭指示器。
    -> 按普通规则生成 Move / Attack。
```

是否允许移动或攻击 Order 与当前 AbilitySession 并存，由 Ability、Behavior 和 Action Arbitration 规则决定。

为了实现本设计描述的蓄力技能：

```text
Move Order 不得因为输入层规则自动取消 Focus Session。
```

---

# 九、本地技能输入状态

## 9.1 状态

```csharp
public enum LocalAbilityInputStateKind : byte
{
    Idle,

    // 只打开了本地指示器，尚未产生真实 Gameplay Session。
    LocalAiming,

    // Focus Command 已成功加入本地 Command Buffer，
    // 但对应预测 Tick 可能尚未执行。
    FocusRequested,

    // AbilityRuntime 已观察到真实 Focus Session。
    GameplayFocusing,

    // Commit Command 已成功加入本地 Command Buffer，
    // 等待预测或权威 Gameplay 推进 Session。
    CommitRequested
}
```

```csharp
public struct LocalAbilityInputState
{
    public LocalAbilityInputStateKind Kind;
    public AbilitySlot Slot;
    public UnitUid ControlledUnitUidAtBegin;

    public GameplayCommandRequestReceipt
        LastRequestReceipt;
}
```

这是本地输入和表现状态：

```text
不进入 GameplaySnapshot。
不进入 SharedGameplayChecksum。
不发送网络。
不由回滚恢复。
```

## 9.2 为什么需要 `FocusRequested`

Focus Command 可能被安排到未来预测 Tick。

在此期间玩家可能已经：

```text
松开技能键（无操作）。
点击左键。
```

输入模块必须允许：

```text
FocusRequested
    -> CommitRequested
```

并依赖 `CommandSeq` 保证 Focus 先于 Commit。

## 9.3 为什么需要 `CommitRequested`

例如：

```text
玩家按住 Q（Focus 已提交）。
左键提交。
随后松开 Q。
```

松键不产生 Commit；去重只针对重复的左键或重复的提交触发。

规则：

```text
FocusRequested / GameplayFocusing
    + 第一个合法 Commit Trigger
    -> Request Commit
    -> 成功后立即进入 CommitRequested。

CommitRequested
    + 重复左键
    -> 忽略。
```

去重在 Command Request 成功时立即生效，不等待 AbilitySession 真正结束。

---

# 十、通用技能输入规则

## 10.1 左键默认 Commit

冻结规则：

```text
只要当前存在可 Commit 的本地技能输入上下文：
    鼠标左键默认映射为 AbilitySignal.Commit。

没有技能输入上下文：
    鼠标左键不产生 Gameplay Command。
```

适用：

```text
LocalAiming
FocusRequested
GameplayFocusing
```

不适用：

```text
Idle
CommitRequested
```

## 10.2 `PressCommit`

```text
技能键按下
    -> 捕获所需 Aim。
    -> Request Commit。
    -> 成功后进入 CommitRequested。
```

适用于：

```text
自施法
无目标技能
按键立即触发技能
```

## 10.3 `LocalAimPrimaryCommit`

```text
技能键按下
    -> 打开本地 Aim。
    -> 进入 LocalAiming。
    -> 不发送 Focus。

左键
    -> 构造 AimSnapshot。
    -> Request Commit。
    -> 成功后进入 CommitRequested。

右键或 Escape
    -> 只关闭本地 Aim。
    -> 回到 Idle。
    -> 不发送 Cancel。
```

因为此时没有真实 AbilitySession。

## 10.4 默认蓄力组合（Focus + 左键 Commit）

```text
技能键按下
    -> Request Focus。
    -> 成功后进入 FocusRequested。

技能键松开（本版本默认组合）
    -> 不产生任何 AbilitySignal。
    -> 不 Request Commit、不 Request Cancel。
-> 若该技能显式配置了松键翻译，则按映射模板执行。

鼠标左键
    -> Request Commit。

鼠标右键
    -> 不发送 Cancel。
    -> 不关闭指示器。
    -> 继续正常 Move / Attack。

Escape
    -> 当前默认不发送 Cancel。
```

Focus 已经成功进入 Command Buffer 后，该技能不再是“仅本地指示器”。

---

# 十一、按下启用、左键提交

本节以韦鲁斯 Q 型技能作为正式参考流程。

## 11.1 Q 按下

```text
AbilityKeyPressed(Q)
    -> 读取槽位当前 ActiveAbilityId。
    -> 读取该技能的 InputMappingTemplate。
    -> 该技能配置了蓄力默认组合（按下 -> Focus）。
    -> RequestCastAbility(
           Slot = Q,
           Signal = Focus,
           Aim = None)。
```

Request 成功：

```text
Local State = FocusRequested。
显示可用的预备指示器。
```

预测 Gameplay 执行 Focus 后：

```text
AbilityHandler 接收 Focus。
建立真实 AbilitySession。
写入 FocusLogicTick。
进入 Hold / Focus Stage。
Local State = GameplayFocusing。
指示器改为读取真实 Session。
```

## 11.2 Q 松开（本版本默认组合下无操作）

```text
AbilityKeyReleased(Q)
    -> 本版本默认：不产生任何 AbilitySignal。
    -> 不 Request Commit、不 Request Cancel。
    -> 保持当前本地技能输入状态。
    -> 若该技能显式配置了 AbilityKeyReleased 翻译（如松键
Commit、三段式预备），则按映射模板执行。
```

## 11.3 左键

```text
PrimaryClick
    且 Pointer 未被 UI 阻断
    且状态为 FocusRequested 或 GameplayFocusing
    -> 捕获当前 AimSnapshot
    -> Request Commit
    -> 成功后进入 CommitRequested
```

本版本默认组合下，左键是蓄力技能的 Commit 触发：

```text
AbilitySignal.Commit
```

架构允许其它技能把松键也绑定到同一个 `AbilitySignal.Commit`；
去重（§9.3）保证无论几个物理事件触发 Commit，都只产生一次。

## 11.4 右键

在该技能已进入 `FocusRequested` 或 `GameplayFocusing` 后：

```text
SecondaryClick
    -> 不发送 AbilitySignal.Cancel。
    -> 不关闭技能指示器。
    -> 按普通右键规则生成 Move / Attack。
```

输入层不能把右键硬编码成技能取消。

## 11.5 Commit 执行

目标 Tick：

```text
AbilitySignal.Commit
    -> 读取 AbilitySession。
    -> 由技能系统计算蓄力 Tick。
    -> 推进到 Release Stage。
    -> 生成 Projectile。
    -> 完成 Session。
    -> 进入 Cooldown。
```

输入模块不负责：

```text
计算蓄力比例。
生成 Projectile。
开始 Cooldown。
结束 AbilitySession。
```

## 11.6 蓄力时间

技能系统计算：

```text
RawChargeTicks =
    CommitLogicTick - FocusLogicTick
```

如何处理：

```text
最小蓄力
最大蓄力
自动推进
伤害和射程曲线
```

完全由 CastModelDef、StageDef 和 AbilityRuntime 决定。

输入模块没有对应配置。

## 11.7 Focus 与 Commit 同 Tick

快速点按可能让：

```text
Focus
Commit
```

落在同一个 `TargetTick`。

允许：

```text
Focus.CommandSeq < Commit.CommandSeq
```

目标 Tick 按规范 Command 顺序执行：

```text
先 Focus。
后 Commit。
```

技能系统得到：

```text
ChargeTicks = 0
```

后续处理由技能自身静态配置决定。

禁止为了输入层方便强制把 Commit 延迟一个 Tick。

---

# 十二、`AimSnapshot`

## 12.1 结构

继续复用技能系统和 FrameSync Command 已有的正式结构：

```text
AimSnapshot
    Kind
    TargetUnitUid
    TargetPoint
    Direction
```

输入模块不得重新定义第二套网络 Aim Schema。

## 12.2 规范字段

```text
None
    所有 Payload 清零。

Self
    所有 Payload 清零。
    施法者由 Command Header 确定。

Point
    只保留量化 TargetPoint。

Unit
    只保留 TargetUnitUid。

Direction
    只保留量化 Direction。
```

未使用字段必须写规范零值。

## 12.3 Direction Aim

```text
Direction =
    Normalize(
        PointerGroundPoint
        - ControlledUnitLogicPosition)
```

长度低于合法阈值：

```text
不提交 Commit。
保持当前输入状态。
```

Command 保存最终量化方向。目标 Tick 不根据鼠标重新计算。

## 12.4 不在输入层 Clamp 技能距离

输入层不处理：

```text
最大施法距离
最小施法距离
自动 Clamp
追击施法
命中预测
```

这些由技能、行为和执行层决定。

---

# 十三、Gameplay Command Request 接口

## 13.1 类型化 Request

```csharp
public interface IPlayerGameplayCommandRequester
{
    bool RequestMove(
        in fp2 targetPoint);

    bool RequestAttack(
        in UnitUid targetUnitUid);

    bool RequestCastAbility(
        AbilitySlot slot,
        AbilitySignalVerb signal,
        in AimSnapshot aim,
        out GameplayCommandRequestReceipt receipt);
}
```

`AbilitySignalVerb` 必须复用技能系统已有的：

```text
Focus
Commit
Cancel
```

不得在输入模块定义第二套 `CastPhase` 或 `AbilityControlPhase`。

## 13.2 Request 回执

```csharp
public struct GameplayCommandRequestReceipt
{
    public int TargetTick;
    public uint CommandSeq;
}
```

用途：

```text
关联 FocusRequested / CommitRequested。
判断对应预测 Tick 是否已经执行。
防止重复 Commit。
在请求执行失败后恢复本地输入状态。
```

回执：

```text
只在本地使用。
不额外进入 Command Payload。
不进入 GameplaySnapshot。
```

如果现有 Request 层已经返回等价信息，直接复用，不新增结构。

## 13.3 Request 层职责

```text
读取 PlayerSlot 和 ControlledUnitUid。
分配 CommandSeq。
计算 TargetTick。
填充 Command Header。
规范序列化 Payload。
写入 CommandCollector。
发送 GameplayCommandBundle。
```

TargetTick 继续使用 FrameSync 主设计，不由输入模块计算。

## 13.4 同 Tick 顺序

快速点按时：

```text
Focus Request
Commit Request
```

可以拥有相同 `TargetTick`，但必须：

```text
Focus.CommandSeq < Commit.CommandSeq
```

CommandCollector 和 CommandDispatcher 必须保留该玩家同 Tick CommandSeq 顺序。

## 13.5 Request 失败

Request 返回 `false`：

```text
不改变为 FocusRequested 或 CommitRequested。
保持原本本地输入状态。
```

它只表示本地 Command Request 未成功建立，不表示目标 Tick 的 Gameplay 结果。

---

# 十四、技能系统与 AI 边界

## 14.1 玩家路径

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

## 14.2 AI 路径

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

## 14.3 不强制新增 AI 快照结构

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

# 十五、技能指示器

## 15.1 数据来源

指示器直接读取：

```text
CastModelDef.ResolveIndicatorStage
StageDef
AbilityRuntime
AbilitySession
Blackboard
Local Aim
```

输入模块不配置：

```text
射程
宽度
半径
蓄力比例
最大蓄力时间
```

## 15.2 不同本地状态

```text
LocalAiming
    -> 使用静态 AbilityDef / StageDef 和 Local Aim。

FocusRequested
    -> 可显示基于静态配置的预备指示器。

GameplayFocusing
    -> 使用真实 AbilitySession、Stage 和 Blackboard。

CommitRequested
    -> 保持指示器，直到 Gameplay Runtime 推进。
```

## 15.3 关闭规则

不要在左键或松键回调中直接强制关闭指示器。

指示器在以下情况下关闭：

```text
AbilitySession 结束。
当前 Stage 不再要求指示器。
技能进入 Cooldown。
Gameplay 生命周期中断该 Session。
ControlledUnitUid 改变。
离开 Gameplay Flow。
```

这样：

```text
Commit Request 被立即拒绝
    -> 指示器不会错误关闭。

Commit 在 Gameplay 执行时失败
    -> Session 仍在 Focus Stage 时可继续显示。
```

---

# 十六、生命周期与 UI 交互

## 16.1 Gameplay Action Map 启用

```text
Application Flow == GameplayRunning
本地 PlayerSlot 已绑定
ControlledUnitUid 有效
Gameplay Scene 已加载
```

## 16.2 禁用

以下情况禁用 Gameplay Action Map 并清理本地输入状态：

```text
离开 Gameplay Scene。
进入最终 Result。
客户端断线。
失去 ControlledUnit。
进入观战状态。
```

真实 AbilitySession 由 Unit / Ability 生命周期中断，输入模块不伪造 Cancel。

## 16.3 ControlledUnit 变化

本地状态保存：

```text
ControlledUnitUidAtBegin
```

当前受控 UnitUid 不一致时：

```text
关闭本地指示器。
清理本地输入状态。
不向新单位补发旧 Commit。
```

## 16.4 UI 阻断指针

```text
PrimaryClick 位于阻断 UI 上
    -> 不 Commit。

SecondaryClick 位于阻断 UI 上
    -> 不生成 Move / Attack。
    -> 不改变已启用技能 Session。
```

## 16.5 UI 打开后的技能键

已经成功发送 Focus 后，技能键松开（本版本默认组合）：

```text
不产生任何 AbilitySignal（不 Commit、不 Cancel）。
```

普通新技能键 Press 仍受键盘 Gate 阻断。
若该技能显式配置了松键翻译，则按映射模板组合执行。

---

# 十七、确定性、回滚与重演

## 17.1 本地状态不参与 Gameplay 确定性

以下不进入 GameplaySnapshot 或 Checksum：

```text
InputAction phase
鼠标屏幕坐标
Hover Collider
LocalInputEventBuffer
LocalEventSequence
InputMappingTemplate
LocalAbilityInputState
Request Receipt
技能指示器
UI Gate
Gameplay Camera
```

## 17.2 Command 是输入事实

一旦 Request 成功创建：

```text
Command Header
AbilitySignalVerb
AimSnapshot
```

它就是帧同步输入事实。

回滚时：

```text
不重新读取鼠标键盘。
不再次处理 LocalInputEvent。
不重新解析 ScreenPosition。
不重新执行玩家输入状态机。
```

只重放已有：

```text
AuthorityFrame Command
AcceptedCommandRelay
本地预测 Command
```

## 17.3 Focus / Commit 的 Tick 决定蓄力

```text
FocusLogicTick
CommitLogicTick
```

来自确定性 Gameplay Command 执行 Tick。

不使用：

```text
Unity Time
InputAction 持续秒数
本地 Stopwatch
渲染帧数量
```

## 17.4 本地状态与 Gameplay Runtime 同步

`ILocalAbilityRuntimeView` 至少提供等价只读信息：

```text
指定 Owner 和 Slot 是否存在活动 Session。
Session 当前是否仍等待 Commit。
当前 Stage 是否需要指示器。
```

同步规则：

```text
FocusRequested：
    到达 Receipt.TargetTick 后观察到 Focus Session
        -> GameplayFocusing。

    到达 Receipt.TargetTick 后仍无 Session
        -> Idle，并关闭指示器。

CommitRequested：
    Session 已结束或离开等待 Commit 的 Stage
        -> Idle。

    Commit 的 TargetTick 已执行，
    但 Session 仍明确等待 Commit
        -> 该 Commit 未被 Gameplay 接受。
        -> 回到 GameplayFocusing。
```

输入模块不修改 AbilityRuntime，只观察。

---

# 十八、错误处理与性能

## 18.1 本地诊断

```text
GameplayInputDiagnostic
    LocalEventSequence
    EventKind
    Result
    ScreenPosition
    ControlledUnitUid
    AbilitySlot
    RequestReceipt optional
```

结果例如：

```text
AcceptedRequest
BlockedByUi
NoControlledUnit
NoGroundPoint
MappingTemplateMissing
AimBuildFailed
RequestGatewayRejected
DuplicateCommitIgnored
LocalEventBufferOverflow
```

不发送网络，不进入 Gameplay。

## 18.2 离线检查错误

以下情况必须在编辑期离线检查阶段失败：

```text
CastModelDef 无法派生输入映射模板。
需要 Aim 但没有合法 Indicator / Aim 定义。
HoldRelease 模型无法产生 Focus 或 Commit 信号。
QWER 槽位引用不存在的 AbilityDef。
同一 Trigger 绑定多条翻译。
蓄力/引导类技能没有任何 Commit 来源。
```

运行时不临时猜测。

## 18.3 无每帧 GC

避免：

```text
每帧 new List
LINQ
闭包捕获
字符串 Action 查找
Physics RaycastAll 新数组
```

推荐：

```text
缓存 InputAction。
复用 LocalInputEventBuffer。
使用 NonAlloc Raycast。
复用候选缓冲。
AimSnapshot 和事件使用 struct。
只在非 Idle 状态更新技能指示器。
```

---

# 十九、当前版本明确不做

```text
智能施法
按下立即朝鼠标快速施法的独立模式
按键重绑定 UI
A 键攻击移动
左键单位选择
自动选择最近目标
输入层自动 Clamp 技能距离
输入层计算蓄力参数
AI 模拟玩家输入
AI 经过网络 Command
通用 AI AbilityControlOrder 中间层
多本地玩家
手柄
触屏
输入宏
```

当前已经支持：

```text
普通本地 Aim 后左键 Commit。
按键立即 Commit。
按下 Focus、左键 Commit。
本版本默认：技能键松开不产生任何信号；
组合层允许按技能配置松键/三段式触发。
重复左键/重复提交去重。
已启用 HoldRelease 技能期间右键继续 Move / Attack。
```

---

# 二十、推荐实现顺序

```text
1. 配置 Gameplay 和 UI Action Map。
2. 实现 UnityGameplayInputSource。
3. 实现 LocalInputEventBuffer。
4. 实现 IGameplayInputGate。
5. 实现 GameplayPointerResolver。
6. 接通 Move / Attack Request。
7. 为 CastModelDef 增加输入映射模板与离线合法性检查。
8. 接通 AbilitySlot -> ActiveAbilityId -> 映射模板查询。
9. 实现 Idle / LocalAiming。
10. 接通 PressCommit 和 LocalAimPrimaryCommit。
11. 扩展 CastAbility Request 返回 RequestReceipt。
12. 实现 FocusRequested / GameplayFocusing / CommitRequested。
13. 接通技能键 performed / canceled。
14. 实现蓄力默认组合（Focus + PrimaryClick Commit）与可选松键绑定。
15. 实现重复 Commit 去重。
16. 接入 ILocalAbilityRuntimeView。
17. 接入 AbilityIndicatorController。
18. 增加 UI 与 Application Flow 生命周期。
19. 增加同 Tick Focus / Commit 测试。
20. 增加回滚不重读设备测试。
```

---

# 二十一、验收测试

## 21.1 移动与攻击

```text
[ ] 右键空地只生成一个 Move Request。
[ ] 右键敌方 Unit 只生成一个 Attack Request。
[ ] Attack 目标失效时不自动转换为 Move。
[ ] 鼠标位于阻断 UI 上时不生成世界 Command。
```

## 21.2 普通非智能施法

```text
[ ] Q/W/E/R 固定映射槽位 0/1/2/3。
[ ] LocalAim 技能按键只打开本地 Aim。
[ ] LocalAim 中左键生成 Commit。
[ ] LocalAim 中右键或 Escape 只关闭本地 Aim。
[ ] 本地取消不生成 AbilitySignal.Cancel。
[ ] 无目标技能按键直接生成 Commit。
```

## 21.3 HoldRelease

```text
[ ] 技能键按下生成 Focus。
[ ] 本版本默认：技能键松开不产生任何 AbilitySignal
    （不 Commit、不 Cancel）；若技能显式配置松键翻译则按配置执行。
[ ] 鼠标左键同样生成 Commit。
[ ] 左键 Commit 后重复左键不产生第二条 Commit。
[ ] 本版本默认 Focus 后仅左键 Commit（松键无操作）；
    配置了松键 Commit 的技能必须产生同一个 AbilitySignal.Commit。
[ ] Focus 与 Commit 可落在同一 TargetTick。
[ ] 同 Tick 时 Focus.CommandSeq 小于 Commit.CommandSeq。
[ ] 右键不发送 Cancel、不关闭指示器。
[ ] 右键仍可生成 Move / Attack。
[ ] Escape 默认不取消 HoldRelease Session。
[ ] Commit 后 Projectile、Session 结束和 Cooldown 由技能系统处理。
```

## 21.4 Session 与指示器

```text
[ ] Focus Request 尚未执行时可显示预备指示器。
[ ] Focus 执行成功后指示器读取真实 Session。
[ ] CommitRequested 时重复 Commit 被阻断。
[ ] Commit 成功推进 Stage 后指示器自动关闭。
[ ] Commit 未被 Gameplay 接受且 Session 仍等待 Commit 时恢复 Focusing。
[ ] ControlledUnitUid 改变时关闭本地指示器。
```

## 21.5 AI

```text
[ ] AI 不引用 Unity Input System。
[ ] AI 不读取 InputMappingTemplate。
[ ] AI 可直接产生 Focus / Commit / Cancel AbilityAction。
[ ] AI 与玩家最终进入相同 AbilityHandler 信号语义。
```

## 21.6 帧同步

```text
[ ] 输入模块不计算 TargetTick。
[ ] Request 层统一分配 CommandSeq。
[ ] AimSnapshot 不包含屏幕坐标、Camera 或 Collider。
[ ] 回滚不重新读取设备。
[ ] 重演只使用保存的 Command。
[ ] 蓄力时间只由 FocusLogicTick 和 CommitLogicTick 计算。
```

## 21.7 性能

```text
[ ] Idle 无每帧托管分配。
[ ] Aiming / Focusing 无每帧托管分配。
[ ] Pointer Raycast 使用 NonAlloc 或复用方案。
[ ] InputAction 初始化后缓存。
```

---

# 二十二、编码准入结论

## 22.1 结论

```text
玩家输入模块 v1.1：
    Go，可以进入编码阶段。
```

本版已经覆盖当前基础需求：

```text
移动
普通攻击
Q/W/E/R
普通非智能施法
无目标立即施法
本地 Aim 后左键 Commit
按下启用的蓄力技能
本版本默认技能键松开不提交（左键提交）
鼠标左键 Commit
重复 Commit 去重
已启用技能期间右键不取消
玩家与 AI 复用同一技能系统语言
```

## 22.2 编码时必须保持的三个跨模块契约

```text
1. CastModelDef / 技能数据可以离线校验输入映射模板的合法性，
   且不复制任何技能时间或数值配置；
   运行时直接读取已校验模板，不做 Bake。

2. FrameSync CastAbility Request 返回或暴露等价的
   TargetTick + CommandSeq 回执，
   用于 Focus / Commit 本地状态关联和去重。

3. Ability 系统提供只读 Runtime View，
   让输入与指示器观察 Session 是否活动、
   是否仍等待 Commit，以及当前 Stage 是否需要指示器。
```

这三项已经在本设计中给出明确职责，不是待定架构问题。

## 22.3 不允许程序员自行改变

```text
左键、松键等不同物理事件若都触发 Commit，必须映射为同一个
AbilitySignal.Commit，不得发明第二个 Commit 信号。
松键是否映射、映射成什么由技能映射模板组合决定（本版本默认 None）。
不得让右键自动发送 HoldRelease Cancel。
不得在输入模块重复配置 MinFocusTicks 或 MaxFocusTicks。
不得让 AI 模拟 Unity 输入。
不得在回滚时重新读取 InputAction。
不得在输入模块生成第二套 Ability 协议或网络 Command。
```

满足上述契约后，本模块可以直接开始实现和集成测试。
