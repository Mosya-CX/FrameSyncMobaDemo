# 韦鲁斯测试套件与数值

## 目标实现

用通用作者扩展点组成 Q 蓄力、W 开关印记、E 范围和 R 投射物。

## 技术方案

Q 满蓄力 1.5 秒，最多保持 4 秒；伤害/距离按 ChargeRatio 线性插值，额外 AD 从 80% 到 120%，范围 925 到 1625；穿透每额外目标衰减 15%，下限 33%。W 消耗开关后开始 40 秒冷却。

## 边界情况

怪物印记每层上限 120，不泛化到所有非英雄；英雄引爆每层退基础技能冷却 13%；复仇强化期杀非英雄只排队一个普通 Buff，不能覆盖强化值。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Ability/Stages/ChargeProjectileStageDef.cs`：当前关联实现定义 ChargeProjectileStageDef（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Ability/Stages/ChargeStageDef.cs`：当前关联实现定义 ChargeStageDef（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Buff/Effects/AbilityHitStackDetonationBuffEffect.cs`：当前关联实现定义 AbilityHitStackDetonationBuffEffect（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Buff/Effects/KillStatGrowthBuffEffect.cs`：当前关联实现定义 KillStatGrowthBuffEffect（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/ChargeAbilityTests.cs`：ChargeStage_RatioIncreasesWithElapsedTicks、ChargeStage_SelfSlowAppliesWhileChargingAndRemovesOnRelease、ChargeStage_TimeoutCancelsAndRefundsHalfCost、ChargeStage_ConsumesActiveToggleAndStartsCooldown、ChargeProjectileStage_InterpolatesDamageRangeAndOverride、ChargeProjectile_SnapshotRoundTrip_PreservesOverride。
- `Assets/Scripts/Gameplay/Tests/ActionArbiterConcurrencyTests.cs`：LockedMainCast_AllowsAuthoredDashInBaseSlot、MovableHold_AllowsMove_ReleasePreemptsMove、HoldTimeout_ReconcilesReleaseResourcesAndRestores、SameAbilityStageTransition_MigratesMainToBaseSlot、AutomaticDashTransition_MigratesWithoutCancellingSession、SequentialRecastWindow_ReleasesMainRuntimeButKeepsSession、Planner_DoesNotResubmitEquivalentActiveMove。
- `Assets/Scripts/Gameplay/Tests/AssistEventIntegrationTests.cs`：HeroDeath_RaisesAssistOnce_AndEmpowersAssistantRevenge、Killer_IsHighestEffectiveLifeDamageContributorInLethalBatch。
- `Assets/Scripts/Gameplay/Tests/BlightDetonationAndOnHitPassiveTests.cs`：AbilityHit_DetonatesAndConsumesAllStacks、Detonation_DoesNotRecurse、Detonation_NonHero_CapsPerStackDamage、ExternalBlightAndAbilityDamage_DoNotAffectStructure、Detonation_Hero_GrantsCooldownReduction、AttackDamage_DoesNotDetonate、WPassive_OnHitDealsBonusDamageAndAppliesBlight。
- `Assets/Scripts/Gameplay/Tests/PassivePAbilityTests.cs`：KillNonHero_GrantsAttackSpeedAndDerivedStats、KillHero_AppliesThreeTimesBonus、Kill_RefreshesBuffDurationToFiveSeconds、Cooldown_ResolvesPerAbilityLevel、EmpoweredExpiry_ReappliesOneNormalBuffAfterNonHeroKills、AssistHero_AppliesEmpoweredBonusToAssistant。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。


## 需求演进

### 2026-10-02

变动内容：韦鲁斯测试套件使用普通点/方向提交、hold-release 与纯开关组合。

legacyDecision：D-029

### 2026-08-05

变动内容：Q 蓄力和 W 印记由通用 Stage、Buff 与投射物覆盖实现。

legacyDecision：D-030

### 2026-08-06

变动内容：按等级冷却、蓄力减速/超时退款和复仇被动；旧启动授权携带方案已由后续修订替代。

legacyDecision：D-031

### 2026-08-06

变动内容：强化复仇结束后最多补一个普通 Buff；分段 checksum 和 StatSeq 排序便于定位。

legacyDecision：D-032

### 2026-08-06

变动内容：助攻事件和复仇反应接入正式贡献链。

legacyDecision：D-034

