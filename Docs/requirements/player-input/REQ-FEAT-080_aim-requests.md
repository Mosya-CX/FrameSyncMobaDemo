# 世界鼠标 Aim 与类型化 Request

## 目标实现

输入层输出规范 AimSnapshot，命令层锁定目标 Tick。

## 技术方案

鼠标世界解析选择地面点或 Unit，Direction 规范化；Move/Attack/Ability typed Request 返回回执，AimSnapshot 使用唯一正式字段。

## 边界情况

输入层不 Clamp 技能距离；本地目标仅最低检查，最终执行重查；Focus 与 Commit 同 TargetTick 保持序列顺序。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：当前关联实现定义 AbilitySignal、AbilitySignalVerb、AimKind、AimSnapshot（以源码为实际命名）。
- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：当前关联实现定义 PlayerInputController（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientFrameworkSmokeSceneTests.cs`：ClientFixture_BindsAssignedUnitAndAdvances。
- `Assets/Scripts/Bootstrap/Tests/EditMode/GameplayCommandSendLedgerTests.cs`：UnchangedCollector_BuildsOnlyOneReliableBundleCandidate、AdjacentToggleInputs_SendTheirDistinctCommandSequences、RebuiltCollector_DoesNotResendSuccessfulIdentity。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：HoldReleaseDefault_RightClickMoveThenLeftClickCommitsOnce、UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands、LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim、ToggleNoAim_SimulatedWPressCommitsImmediately、VarusWThenQ_PendingFocusKeepsIndicatorAndBothCommands。
- `Assets/Scripts/FrameSync/Tests/AuthorityReplicationTests.cs`：CommandBundle_ProducesStablePerTickReplacementRelays、LateCommand_IsRetargetedToCurrentServerTick_NotRejected、AcceptedCommand_AfterTickFreeze_LateDuplicateIsIgnored、AcceptedCommand_AfterOwnerInvalidation_DuplicateSkipsAuthorization、DistinctCommandSequences_OnAdjacentTicks_AreBothAccepted、DirectionAim_CastAbility_RoundTripsCanonically、WireContracts_DoNotExposeCallerOwnedByteArrays。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 输出

```csharp
public struct GameplayPointerSnapshot
{
    public bool HasGroundPoint;
    public fp2 GroundPoint;

    public bool HasHoveredUnit;
    public UnitUid HoveredUnitUid;
}
```

### 地面点

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

### Unit 选择

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

### 本地预测差异

鼠标解析基于客户端当前预测世界。

因此允许：

```text
本地点击时目标存在。
目标 Tick 时目标已经死亡。
Command 最终执行失败。
```

输入模块不自动替换目标或改变 Command 类型。

---

### 普通右键

当本地技能输入状态允许右键进入世界操作时：

```text
右键敌方可选 Unit
    -> RequestAttack(TargetUnitUid)

否则存在 GroundPoint
    -> RequestMove(GroundPoint)
```

### 本地攻击目标最小检查

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

### 不自动改写

```text
Attack Command 在目标 Tick 失败
    不自动转换为 Move。

Move Command
    不携带 Pathfinding 策略、RVO 参数或 StopRange。
```

### 不同技能状态下的右键

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

### 结构

继续复用技能系统和 FrameSync Command 已有的正式结构：

```text
AimSnapshot
    Kind
    TargetUnitUid
    TargetPoint
    Direction
```

输入模块不得重新定义第二套网络 Aim Schema。

### 规范字段

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

### Direction Aim

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

### 不在输入层 Clamp 技能距离

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

### 类型化 Request

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

### Request 回执

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

### Request 层职责

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

### 同 Tick 顺序

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

### Request 失败

Request 返回 `false`：

```text
不改变为 FocusRequested 或 CommitRequested。
保持原本本地输入状态。
```

它只表示本地 Command Request 未成功建立，不表示目标 Tick 的 Gameplay 结果。

---


## 需求演进

### 2026-10-02

变动内容：松键和左键合并为一次 Commit，右键不取消蓄力。

legacyDecision：D-017

