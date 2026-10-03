# 同步生成死亡复活与回池

## 目标实现

单位生死、复活、规则移除和池化形成单一生命周期。

## 技术方案

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

## 边界情况

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Unit/Core/UnitRegistry.cs`：当前关联实现定义 UnitRegistry（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：当前关联实现定义 UnitWorld（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Prototype/UnitRespawnConfig.cs`：当前关联实现定义 RespawnHealthRule、RespawnResourceRule、UnitRespawnConfig（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/UnitRegistryTests.cs`：DuplicateUid_IsRejectedWithoutChangingRegisteredRuntime、MissingAndAliasUnregister_AreRejectedWithoutMutation、NullMutationInputs_AreRejected。
- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：SpawnMultipleKinds_GetByKind、SpawnUnit_DoubleSnapshot_RoundTrip、ClearForDeath_PreservesCombatModifiersOwnedBySourceSystems、ClearForRespawn_DoesNotClearStatBaseValues、ResetForPool_ResetsAllDynamicState、ManyUnits_StableReadOrder。
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：InternalRegistration_PublicLookupReturnsSameRuntime、StableReadOrder_IsIndependentOfRegistrationOrder、SuccessfulUnregister_RemovesLookupAndAllowsReregistration。
- `Assets/Scripts/Gameplay/Tests/HeroRespawnTests.cs`：RespawnDelay_UsesBasePlusElapsedMinutes、RespawnPosition_IsCapturedFromInitialSpawn、CompleteRespawn_TeleportsToHomeSpawnPosition。
- `Assets/Scripts/Bootstrap/Tests/EditMode/FrameworkSmokeBootstrapTests.cs`：Bootstrap_BakesAssetsSpawnsUnitAndBoundsCatchUpTicks、Bootstrap_BindsSelectedHeroPrototypeToPlayerSpawn。

## 细化功能与技术附录

- [同步单位生成与AI注册](REQ-FEAT-028-PART-1_spawn-ai-registration.md)：接口、数据流、公式、边界与配置。
- [正式死亡清理与处置](REQ-FEAT-028-PART-2_death-disposal.md)：接口、数据流、公式、边界与配置。
- [英雄复活与对象池新生命周期](REQ-FEAT-028-PART-3_hero-respawn-pooling.md)：接口、数据流、公式、边界与配置。
- [生命周期稳定顺序与恢复](REQ-FEAT-028-PART-4_lifecycle-restore-order.md)：接口、数据流、公式、边界与配置。
- [开局生成与事件接入流程](REQ-FEAT-028-PART-5_startup-unit-events.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-10-02

变动内容：生成 Tick 可被动参与，主动工作晚于出生 Tick。

legacyDecision：D-008

### 2026-10-02

变动内容：正式死亡由 UnitWorld 同步执行，来源系统仅清理自己的句柄。

legacyDecision：D-009

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

