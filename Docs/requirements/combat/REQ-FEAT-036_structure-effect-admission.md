# 建筑外源效果准入

## 目标实现

建筑只接受规定的外源普通攻击并拒绝其他外源效果。

## 技术方案

中央入口验证 CombatSourceType.Attack + CombatBuiltinSourceId.BasicAttack；Heal、Shield、Buff、Control 和 ForcedMove 在进入结算或存储前拒绝外源。

## 边界情况

Attack 类型技能但不同 SourceId 和 AttackEffect 也拒绝；结构自身效果可合法；没有 Control Handler 的外源拒绝不得抛缺 Handler 异常。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：当前关联实现定义 BuffHandler、BuffReactionKind（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Combat/CombatRequestHeader.cs`：当前关联实现定义 CombatSourceType、SourceDescriptor、CombatBuiltinSourceId、CombatBuiltinRecipeId、CombatRequestHeader（以源码为实际命名）。
- `Assets/Scripts/Gameplay/CrowdControl/CrowdControlHandler.cs`：当前关联实现定义 CrowdControlHandler（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CrowdControlHandlerTests.cs`：Add_CreatesIndependentInstances_NoMerge、Immunity_BlocksLowMedium_ConsumesOneShot_BypassesHigh、Cleanse_RemovesMatchingNonHigh_RespectsCount、Unstoppable_SuppressesOutput_AndRejectsForcedMove、DamageTakenSignal_RemovesSleepInstance、Drowsy_OnNaturalExpire_AddsSleepWithConfiguredDuration、Tenacity_ShortensDefaultDuration_IgnoredByIgnoreRule。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/UnitAnimationAssetTests.cs`：FullMatchAnimationFixtures_HaveCompleteBindableControllers、RuntimeUnits_HaveCompleteRuntimeComposition、FormalAnimatedUnits_BindAttackAndMovePlaybackToGameplayStats、FormalStructures_HaveNoAttackAnimation。
- `Assets/Scripts/Gameplay/Tests/PlayMode/UnitPrefabCompositionPlayModeTests.cs`：FormalSpawn_InstantiatesLiveComponentGraphAndRegistersPhysicsIdentity、FormalDeathInvalidation_UpdatesAttackOwnersTogether。
- `Assets/Scripts/Gameplay/Tests/SunderedSkyEquipmentTests.cs`：Catalog_ContainsSunderedSkyTreeWithStatsAndRecipes、EmpoweredStrike_Deals160PercentCritical_AndStartsCooldown、PerTargetCooldown_SecondHitOnSameHeroIsNormal、DifferentEnemyHero_StillTriggersEmpoweredStrike、HealRestoresMissingHealth_AndOverhealBecomesTempMaxHealth、RangedStrike_HealsWithRangedBaseAdRatio、MeleeWithProjectile_StillHealsWithMeleeRatio。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-09-01

变动内容：建筑中央准入仅允许规定外源普通攻击，自身效果允许，合法拒绝是成功空操作。

legacyDecision：D-054

