# 技能指示器与本地生命周期

## 目标实现

本地瞄准和 Gameplay 蓄力时显示当前阶段的范围投影。

## 技术方案

SkillIndicatorDriver 根据 AbilityCastView 和 StageDef 通过本地 Resolver 显示方向、圆或点目标；不是单独 Gameplay 状态。

## 边界情况

受控单位变化、禁用、死亡、UI 门禁和 Session 结束关闭；游戏回滚后跟随 Runtime，不反写命令或技能距离。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/PlayerInput/AbilityIndicatorController.cs`：当前关联实现定义 AbilityIndicatorController（以源码为实际命名）。
- `Assets/Scripts/PlayerInput/PlayerInputController.cs`：当前关联实现定义 PlayerInputController（以源码为实际命名）。
- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs`：当前关联实现定义 SkillIndicatorDriver（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/GameBootstrapPlayModeTests.cs`：ClientComposition_InitializesFromProjectAssets、DestroyDuringContentLoad_ReleasesTransferredScope、ExternalFlow_PrimesLoadingBeforeContentInitialization、GenericSkillIndicators_BindDedicatedRuntimeMaterials、GenericSkillIndicators_RebindBeforeLeaseRelease_ReplacesOwnedInstances。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：HoldReleaseDefault_RightClickMoveThenLeftClickCommitsOnce、UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands、LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim、ToggleNoAim_SimulatedWPressCommitsImmediately、VarusWThenQ_PendingFocusKeepsIndicatorAndBothCommands。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientFrameworkSmokeSceneTests.cs`：ClientFixture_BindsAssignedUnitAndAdvances。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 数据来源

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

### 不同本地状态

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

### 关闭规则

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


## 需求演进

### 2026-10-02

变动内容：韦鲁斯测试套件使用普通点/方向提交、hold-release 与纯开关组合。

legacyDecision：D-029

