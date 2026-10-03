# Buff 上限优先级与驱逐

## 目标实现

超过软上限时按确定优先级考虑驱逐，永久 Buff 保留。

## 技术方案

MaxBuffs 为 byte 默认 255；Priority 0 最高。仅新 ConfigId 首次施加检查：最低优先级非永久为候选，同级按 ConfigId 稳定顺序末项；新 Priority<=候选才驱逐。

## 边界情况

否则允许超过软上限；驱逐使用 ManualRemove 正常移除流程；重施已有 Buff 不重复驱逐。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Buff/BuffDefinition.cs`：当前关联实现定义 BuffDefinition（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：当前关联实现定义 BuffHandler、BuffReactionKind（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/PlayMode/BlightStackMarkStructurePlayModeTests.cs`：ExternalBlightOnStructure_CreatesNoPresentationMark。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。
- `Assets/Scripts/Gameplay/Tests/AssistEventIntegrationTests.cs`：HeroDeath_RaisesAssistOnce_AndEmpowersAssistantRevenge、Killer_IsHighestEffectiveLifeDamageContributorInLethalBatch。
- `Assets/Scripts/Gameplay/Tests/BlightDetonationAndOnHitPassiveTests.cs`：AbilityHit_DetonatesAndConsumesAllStacks、Detonation_DoesNotRecurse、Detonation_NonHero_CapsPerStackDamage、ExternalBlightAndAbilityDamage_DoNotAffectStructure、Detonation_Hero_GrantsCooldownReduction、AttackDamage_DoesNotDetonate、WPassive_OnHitDealsBonusDamageAndAppliesBlight。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### . Buff 上限与优先级驱逐（正式扩展）

> 状态：正式（2026-08-02 由仓库所有者确认，DECISION_LOG D-025）。
> 不是替代。

### 定位

`BuffHandler.MaxBuffs` 定义单个单位同时激活的 BuffRuntime 数量上限。

当到达上限时，新 Buff 的首次施加按优先级驱逐一个已有非永久 Buff，
为新 Buff 腾出槽位。

### 配置

```text
BuffHandler.MaxBuffs
    byte，默认 255（当前项目实际不限制）

BuffDefinition.Priority
    byte，0 = 最高，255 = 最低
    仅用于驱逐仲裁
```

### 驱逐规则

```text
1. 仅在首次 Apply（新 BuffConfigId）时检查；重复施加不触发驱逐。
2. 永久 Buff（LifeRule.Infinite）永不被驱逐。
3. 候选 = 当前所有非永久 Buff 中 Priority 最大（优先级最低）者；
   同优先级时取稳定 BuffConfigId 排序中的最后一个。
4. 驱逐条件 = 新 Buff.Priority <= 候选.Priority（新 Buff 不低于候选优先级）。
5. 不满足条件时不驱逐，新 Buff 照常添加（数量可超过 MaxBuffs，软上限）。
6. 被驱逐 Buff 按标准移除流程执行，RemovalReason = ManualRemove。
```

### 确定性要求

候选选择必须使用稳定 BuffConfigId 排序，禁止依赖 Dictionary/HashSet
枚举顺序或 ScriptableObject 实例地址。当前实现满足该要求。

```text
UnitWorld
    决定何时调用生命周期接口
    保证固定 Handler 顺序

BuffHandler
    决定哪些 BuffRuntime 保留或删除
    遍历保留 Runtime

BuffEffectConfig
    决定自己拥有的生命阶段 Handle 如何释放和重建
```

---


## 需求演进

### 2026-10-02

变动内容：软 Buff 上限按优先级考虑驱逐，永久 Buff 不驱逐。

legacyDecision：D-025

