# 自适应命令目标 Tick

## 目标实现

网络时延变化时合理选择命令执行 Tick，仍保留静态下界。

## 技术方案

静态下界=max(LocalSimulationTick+1, LatestSynchronizedServerTick+MinCommandLeadTicks)。RTT 使用整数 SRTT 与 RTTVar，按半 RTT、抖动预算、处理预算估计服务器 Tick，再以本地和估计服务器未来窗口封顶。

## 边界情况

冷启动、样本过少或陈旧时返回静态下界；同一模拟 Tick 的命令复用一个 TargetTick；开局前样本年龄不能冒充 Gameplay 已推进。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/CommandTargetTickResolver.cs`：当前关联实现定义 CommandTargetTickResolver（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/CommandTargetTickResolverAdaptiveTests.cs`：AvailableTiming_AddsEstimatedArrivalAndSlackLowerBound、UnavailableTiming_FallsBackToStaticFormalFormula、InvalidInitialLocalTick_DoesNotHitEmptyCache、SameBuildTick_ReusesOneTimingDecision、AdaptiveTimingBeyondFutureWindow_UsesLatestLegalTick、StaticLowerBeyondEstimatedCeiling_RemainsAuthoritative。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/PlayerInputSimulationPlayModeTests.cs`：HoldReleaseDefault_RightClickMoveThenLeftClickCommitsOnce、UiPointerBlocking_SimulatedClicks_ProduceNoWorldCommands、LocalAimDefault_SimulatedPressAimOnly_LeftCommit_RightClosesAim、ToggleNoAim_SimulatedWPressCommitsImmediately、VarusWThenQ_PendingFocusKeepsIndicatorAndBothCommands。
- `Assets/Scripts/PlayerInput/Tests/PlayerCommandRequesterTests.cs`：EventBuffer_AssignsStableSequenceAndRejectsOverflow、HoldRelease_AllocatesFocusBeforeCommitAtSameTargetTick、ControlledUnitChange_ClearsLocalAbilityState、TargetTickResolver_UsesFormalLeadFormulaAndBuildTick、ShopRequests_UseCanonicalCommandsAndSharedSequence、SkillPointRequest_UsesCanonicalCommandAndSharedSequence、HoldReleaseDefault_PressFocus_ReleaseNoOp_LeftClickCommitsAndDedups。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### `TargetTick`

```text
TargetTick =
    max(
        LocalSimulationTick + 1,
        LatestSynchronizedServerTick + MinCommandLeadTicks
    )
```

配置：

```text
MinCommandLeadTicks
MaxFutureCommandTicks
```

---

### 整数网络估计公式

首样本 SRTT=R，RTTVar=round(R/2)。后续 SRTT=round((7*SRTT+R)/8)，RTTVar=round((3*RTTVar+abs(R-旧SRTT))/4)。

EstimatedServerTickNow=ServerTickAtResponse+ceil((ceil(SRTT/2)+SampleAgeMs)*TickRate/1000)。

NetworkBudgetTicks=ceil((ceil(SRTT/2)+max(RTTVar*JitterMultiplier,MinimumJitterMs)+ProcessingMs)*TickRate/1000)。

候选=EstimatedServerTickNow+NetworkBudgetTicks+DesiredServerSlackTicks，先显式限制到本地与估计服务器 MaxFutureCommandTicks，再取 max(StaticLower,CappedAdaptiveCandidate)。静态下界更晚时仍优先；超未来窗口到服务端可能 late retarget；静态下界违反本地窗口或溢出明确失败。

本机时戳、RTT 和服务器锚点仅客户端传输估计，不进 Snapshot/checksum/random/replay。开局前样本推进年龄从 LaunchServerTime 起算，真实 freshness 仍按 response age。


## 需求演进

### 2026-09-01

变动内容：整数 RTT 平滑与目标 Tick 自适应，仍保留静态下界和冷启动回退。

legacyDecision：D-053

