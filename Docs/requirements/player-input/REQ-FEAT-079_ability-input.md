# 技能输入组合与提交去重

## 目标实现

按下、松开和左键提交组成已选择的技能物理输入模式。

## 技术方案

从 CastModelDef 离线派生 PressCommit、LocalAimPrimaryCommit、PressFocusReleaseOrPrimaryCommit，运行本地 FocusRequested/CommitRequested/GameplayFocusing 状态。

## 边界情况

激活 hold-release 后松键和左键走同一 Commit，首次成功抑制重复；右键不 Cancel 但可移动/普攻；配置不复制 Gameplay 费用/范围/时长。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：当前关联实现定义 PlayerInputController（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientFrameworkSmokeSceneTests.cs`：ClientFixture_BindsAssignedUnitAndAdvances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：HoldReleaseDefault_RightClickMoveThenLeftClickCommitsOnce、UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands、LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim、ToggleNoAim_SimulatedWPressCommitsImmediately、VarusWThenQ_PendingFocusKeepsIndicatorAndBothCommands。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 不重复配置技能 Gameplay 数据

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

### 输入翻译组合（可组合的翻译层）

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

### 映射模板：配置数据 + 离线合法性检查（不做 Bake）

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

### 本版本默认组合与允许的组合

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

### 输入 → 施法意图 → Planner / Arbiter → 技能层语言

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

### 状态

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

### 为什么需要 `FocusRequested`

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

### 为什么需要 `CommitRequested`

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

### 左键默认 Commit

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

### `PressCommit`

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

### `LocalAimPrimaryCommit`

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

### 默认蓄力组合（Focus + 左键 Commit）

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

### 十一、按下启用、左键提交

本节以韦鲁斯 Q 型技能作为正式参考流程。

### Q 按下

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

### Q 松开（本版本默认组合下无操作）

```text
AbilityKeyReleased(Q)
    -> 本版本默认：不产生任何 AbilitySignal。
    -> 不 Request Commit、不 Request Cancel。
    -> 保持当前本地技能输入状态。
    -> 若该技能显式配置了 AbilityKeyReleased 翻译（如松键
Commit、三段式预备），则按映射模板执行。
```

### 左键

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

### 右键

在该技能已进入 `FocusRequested` 或 `GameplayFocusing` 后：

```text
SecondaryClick
    -> 不发送 AbilitySignal.Cancel。
    -> 不关闭技能指示器。
    -> 按普通右键规则生成 Move / Attack。
```

输入层不能把右键硬编码成技能取消。

### Commit 执行

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

### 蓄力时间

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

### Focus 与 Commit 同 Tick

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

### 移动与攻击

```text
[ ] 右键空地只生成一个 Move Request。
[ ] 右键敌方 Unit 只生成一个 Attack Request。
[ ] Attack 目标失效时不自动转换为 Move。
[ ] 鼠标位于阻断 UI 上时不生成世界 Command。
```

### 普通非智能施法

```text
[ ] Q/W/E/R 固定映射槽位 0/1/2/3。
[ ] LocalAim 技能按键只打开本地 Aim。
[ ] LocalAim 中左键生成 Commit。
[ ] LocalAim 中右键或 Escape 只关闭本地 Aim。
[ ] 本地取消不生成 AbilitySignal.Cancel。
[ ] 无目标技能按键直接生成 Commit。
```

### HoldRelease

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

### Session 与指示器

```text
[ ] Focus Request 尚未执行时可显示预备指示器。
[ ] Focus 执行成功后指示器读取真实 Session。
[ ] CommitRequested 时重复 Commit 被阻断。
[ ] Commit 成功推进 Stage 后指示器自动关闭。
[ ] Commit 未被 Gameplay 接受且 Session 仍等待 Commit 时恢复 Focusing。
[ ] ControlledUnitUid 改变时关闭本地指示器。
```

### AI

```text
[ ] AI 不引用 Unity Input System。
[ ] AI 不读取 InputMappingTemplate。
[ ] AI 可直接产生 Focus / Commit / Cancel AbilityAction。
[ ] AI 与玩家最终进入相同 AbilityHandler 信号语义。
```

### 帧同步

```text
[ ] 输入模块不计算 TargetTick。
[ ] Request 层统一分配 CommandSeq。
[ ] AimSnapshot 不包含屏幕坐标、Camera 或 Collider。
[ ] 回滚不重新读取设备。
[ ] 重演只使用保存的 Command。
[ ] 蓄力时间只由 FocusLogicTick 和 CommitLogicTick 计算。
```

### 性能

```text
[ ] Idle 无每帧托管分配。
[ ] Aiming / Focusing 无每帧托管分配。
[ ] Pointer Raycast 使用 NonAlloc 或复用方案。
[ ] InputAction 初始化后缓存。
```

---


## 需求演进

### 2026-10-02

变动内容：输入模式从施法模型离线派生，不重复 Gameplay 配置。

legacyDecision：D-016

### 2026-10-02

变动内容：松键和左键合并为一次 Commit，右键不取消蓄力。

legacyDecision：D-017

