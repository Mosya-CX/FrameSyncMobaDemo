# 提交运动寿命与回收

## 目标实现

投射物在固定 Tick 阶段提交、移动、命中和回收。

## 技术方案

ProjectileWorld 拥有每 Tick spawn sequence；CommitSpawns 是唯一创建入口，随后 AdvanceMotion、UpdateLifecycle、ResolveHits、EmitEffects、FlushDestroy。

## 边界情况

FrameSync 不维护第二个投射物 BeginTick 序列；Pending 与 Active 状态均可恢复；池化实体和逻辑实例各有释放 owner。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Projectile/ProjectileRuntime.cs`：当前关联实现定义 ProjectileHitRecord、ProjectileRuntime（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：当前关联实现定义 PendingSpawnEntry、ProjectileWorld（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。
- `Assets/Scripts/Gameplay/Tests/AbilityAimStageTests.cs`：AreaDamageStage_CentersOnAimTargetPoint、AreaDamageStage_DoesNotDamageEnemyStructure、SpawnProjectileStage_FiresTowardAimDirection。
- `Assets/Scripts/Gameplay/Tests/ChargeAbilityTests.cs`：ChargeStage_RatioIncreasesWithElapsedTicks、ChargeStage_SelfSlowAppliesWhileChargingAndRemovesOnRelease、ChargeStage_TimeoutCancelsAndRefundsHalfCost、ChargeStage_ConsumesActiveToggleAndStartsCooldown、ChargeProjectileStage_InterpolatesDamageRangeAndOverride、ChargeProjectile_SnapshotRoundTrip_PreservesOverride。
- `Assets/Scripts/ClientContent/Tests/PlayMode/ProjectileViewBinderPlayModeTests.cs`：ProjectileViewLeaseStaysResidentAcrossLifetimes。

## 细化功能与技术附录

- [投射物状态与空间归属](REQ-FEAT-040-PART-1_projectile-spatial-ownership.md)：接口、数据流、公式、边界与配置。
- [投射物延迟提交与查询](REQ-FEAT-040-PART-2_projectile-deferred-submit.md)：接口、数据流、公式、边界与配置。
- [投射物运动命中与寿命顺序](REQ-FEAT-040-PART-3_projectile-motion-order.md)：接口、数据流、公式、边界与配置。
- [投射物实体池与回收](REQ-FEAT-040-PART-4_projectile-pool.md)：接口、数据流、公式、边界与配置。
- [投射物快照恢复与稳定遍历](REQ-FEAT-040-PART-5_projectile-snapshot.md)：接口、数据流、公式、边界与配置。
- [投射物跨模块接缝与主流程](REQ-FEAT-040-PART-6_projectile-integration.md)：接口、数据流、公式、边界与配置。


## 需求演进

### 2026-10-02

变动内容：投射物保存 pending/active 状态，生成序列由 ProjectileWorld 拥有。

legacyDecision：D-012

### 2026-10-02

变动内容：总案保留目标和事实，精确细节拆为独立功能

