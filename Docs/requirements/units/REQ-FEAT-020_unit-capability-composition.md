# 单位根与能力装配

## 目标实现

单位由明确类型、空间引用、属性与 Handler 能力组成。

## 技术方案

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

## 边界情况

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Unit/Core/CapabilityState.cs`：当前关联实现定义 CapabilityState（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：当前关联实现定义 Unit（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/UnitTag.cs`：当前关联实现定义 UnitTagUid、UnitTag（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Prototype/UnitPrototype.cs`：当前关联实现定义 UnitPrototype（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/GlobalUnitPrototypeTableTests.cs`：Add_And_TryGet、Add_DuplicateId_Throws、TryGet_MissingId_ReturnsFalse、ValidateAll_ValidPasses、ValidateAll_DuplicateStatId_Throws、ValidateAll_InvalidStatId_Throws、ValidateAll_NullBaseStats_SkipsValidation。
- `Assets/Scripts/Gameplay/Tests/UnitPrototypeTests.cs`：UnitPrototype_DefaultValues、UnitPrototype_PreservesAllFields。
- `Assets/Scripts/Gameplay/Tests/CapabilityStateTests.cs`：NewlyConstructedUnit_HasAllCapabilitiesTrue、ConfirmUnitDeath_DisablesAllCapabilities、RequestEnterDying_DoesNotDisableCapabilities、BeginRespawn_KeepsCapabilitiesDisabled、CompleteRespawn_ResetsAllCapabilitiesToTrue、FullDeathRespawnCycle_RestoresCapabilities、DeathRespawnOnOneUnit_DoesNotAffectAnother。
- `Assets/Scripts/Gameplay/Tests/PlayMode/UnitPrefabCompositionPlayModeTests.cs`：FormalSpawn_InstantiatesLiveComponentGraphAndRegistersPhysicsIdentity、FormalDeathInvalidation_UpdatesAttackOwnersTogether。
- `Assets/Scripts/Gameplay/Tests/SpawnUnitTests.cs`：SpawnUnit_ReturnsUnitWithCorrectIdentity、SpawnUnit_AllocatesDeterministicUid、SpawnUnit_SameInput_SameUid、SpawnUnit_DifferentTick_DifferentUid、SpawnUnit_InitializesStatHandler、SpawnUnit_StatHandlerLevelGrowth、SpawnUnit_CreatesEmptyCombatModifierSet。

## 细化功能与技术附录

- [单位身份与根状态](REQ-FEAT-020-PART-1_unit-identity-state.md)：接口、数据流、公式、边界与配置。
- [单位能力状态与战斗修正](REQ-FEAT-020-PART-2_capability-combat-modifiers.md)：接口、数据流、公式、边界与配置。
- [Handler装配与单向依赖](REQ-FEAT-020-PART-3_handler-dependencies.md)：接口、数据流、公式、边界与配置。
- [单位移动与空间写入接缝](REQ-FEAT-020-PART-4_movement-spatial-seams.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

