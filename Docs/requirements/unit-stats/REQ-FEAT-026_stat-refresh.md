# 属性脏标记与只读刷新

## 目标实现

Tick 末和恢复后属性一致，UI 只读观察数值变化。

## 技术方案

SimulationTickPipeline 在 Tick 末调用 StatHandler.FinalizeTick；WatchHook 由稳定修订通知观察者。

## 边界情况

恢复导致 Dirty 不得制造客户端独有校验状态；WatchHook 和展示浮点不成为权威。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Stats/StatChange.cs`：当前关联实现定义 StatChange（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：当前关联实现定义 StatHandler、StatConfig、ExperienceGainResult（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/StatHandlerChangeTests.cs`：GetChangeThisTick_BeforeFinalize_NoChange、GetChangeThisTick_AfterModifierAdd_ReturnsDelta、GetChangeThisTick_NetChangeSameAsBaseline_ReturnsFalseZero、FinalizeTick_SnapshotsFinalValueAsPreviousBaseline、GetChangeThisTick_AfterFinalizeTick_ReturnsZero、GetChangeThisTick_StatNotInPreset_ReturnsDefault。
- `Assets/Scripts/Gameplay/Tests/StatHandlerCalculationTests.cs`：GetStat_NoModifiers_ReturnsLevelBaseValue、GetStat_FlatAdd_SumsCorrectly、GetStat_BaseRatioAdd_AppliesPercentToLevelBase、GetStat_FinalRatioAdd_AppliesPercentAfterFlatAndBase、GetStat_FixedOrder_FlatThenBaseThenFinal、GetStat_ClampByStatDefinition_MaxValue、GetStat_ClampByStatDefinition_MinValue。
- `Assets/Scripts/Gameplay/Tests/StatHandlerModifierTests.cs`：AddModifier_ReturnsValidHandle_WithCorrectStatSeq、AddModifier_StatSeqMonotonicAcrossStatIds、AddModifier_InvalidStatId_Throws、SetModifierValue_UpdatesAndMarksDirty、SetModifierValue_WrongOwnerUid_ReturnsFalse、RemoveModifier_RemovesAndMarksDirty、RemoveModifier_AlreadyRemoved_ReturnsFalse。
- `Assets/Scripts/Gameplay/Tests/StatHandlerSeqTests.cs`：StatSeq_StartsAt1、StatSeq_NeverReusedAfterRemove、StatSeq_AcrossStatIds_SharedCounter、StatSeq_InvalidHandle_WhenStatSeqZero。
- `Assets/Scripts/Gameplay/Tests/StatHandlerSnapshotTests.cs`：CaptureRestore_RoundTrip_PreservesAllState、CaptureRestore_AfterModifications_ReturnsToCapturedState、AddModifier_AfterRestoreWithoutRuntimeStatEntry_RecreatesEntry、RollbackReplay_Equivalence、Restore_DoesNotTriggerDirtyRecompute_ValuesMatchSnapshot、Rebuild_MarksAllEntriesDirty、Determinism_SameSequence_SameSnapshot。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### Dirty、帧间变化与 WatchHook

#### Dirty 重算

以下操作标记属性 Dirty：

```text
AddModifier
SetModifierValue
RemoveModifier
Level 改变
StatPreset 初始化
明确的基础配置重置
```

`GetStat`：

```text
属性不是 Dirty
    -> 直接返回 FinalValue

属性是 Dirty
    -> 重新计算 LevelBaseValue
    -> 聚合该属性全部 Modifier
    -> 应用 StatDefinition 上下限
    -> 保存 FinalValue
    -> 清除 Dirty
```

`StatHandler` 不需要每个 LogicTick 全量重算全部属性。  
但在全局 Tick 的数值收尾阶段，应至少重新计算本 Tick 仍为 Dirty 的属性，保证下一 Tick 的“上一帧值”基线完整。

#### WatchHook 改为纯查询服务

`WatchHook` 不再提供：

```text
Watch
Unwatch
Listener
监听句柄
回调广播
运行时监听关系
UI 订阅
```

它只是 `StatHandler` 的帧间变化查询入口。

```csharp
public readonly struct StatChange
{
    public readonly bool Changed;
    public readonly fp Delta;
}
```

```csharp
public StatChange GetChangeThisTick(
    StatId statId);
```

语义：

```text
Delta
    = 当前 LogicTick 的最终值
      - 上一个 LogicTick 结束时的最终值

Changed
    = Delta != 0
```

查询时如果属性 Dirty，先完成当前最终值计算，再返回结果。

同一 Tick 内多次改变只看净变化：

```text
上一 Tick：1000
当前 Tick：+200，再 -50
当前结果：1150

Changed = true
Delta = +150
```

如果最终回到 1000：

```text
Changed = false
Delta = 0
```

数值系统不保存：

```text
谁查询过。
为什么查询。
请求端是否还存在。
UI 或 Gameplay 监听关系。
```

请求端需要变化信息时，在自己的固定 Tick 阶段主动查询。  
回滚后还需要查询就继续查询，不需要就不查询，不存在监听关系重建问题。

#### 帧间基线

每项 `StatRuntimeEntry` 保存：

```text
FinalValue
PreviousLogicTickFinalValue
Dirty
```

在固定数值收尾阶段：

```text
1. 重算仍为 Dirty 的属性。
2. 当前 Tick 结束后，FinalValue 成为下一 Tick 的 PreviousLogicTickFinalValue 基线。
```

这些字段属于正式快照状态。  
回滚恢复后，`GetChangeThisTick` 能继续回答历史 Tick 对应的变化结果。

---


## 需求演进

### 2026-08-06

变动内容：曾采用 UTC 载荷启动；当前改为两阶段授权和单调调度。Tick 末 FinalizeTick 仍有效。

legacyDecision：D-033

