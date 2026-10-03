# 战斗请求封存与因果波次

## 目标实现

相同战斗请求多重集不受提交和单位遍历顺序影响。

## 技术方案

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

## 边界情况

同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatRequestHeader.cs`：当前关联实现定义 CombatSourceType、SourceDescriptor、CombatBuiltinSourceId、CombatBuiltinRecipeId、CombatRequestHeader（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：当前关联实现定义 CombatSystem、ShieldRequestComparer、HealRequestComparer、DamageRequestComparer、DamageAllocationGroup（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Combat/Requests/DeferredCombatRequest.cs`：当前关联实现定义 DeferredCombatRequest（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：SubmitDamage_ValidRequest_ReducesHealth、Omnivamp_HealsSourceForFractionOfSettledDamage、NaturalRegen_AppliesPerInterval_ForHealthAndCastResource、DamageFormula_ArmorReducesDamage、ZeroArmor_FullDamageApplied、CombatModifiers_ApplyOutgoingAndIncomingFinalPatches、FatalDamage_CompletesFormalDeathSettlement。
- `Assets/Scripts/Bootstrap/Tests/PlayMode/ClientBootstrapFirstWavePlayModeTests.cs`：GameScene_FirstWaveUsesFlowFieldsAndMoves、GameScene_MapViewAnchorsToStaticTopologyRootAtWorldOrigin。
- `Assets/Scripts/FrameSync/Tests/ChecksumNewStateCoverageTests.cs`：Checksum_ChangesWhenProjectileOnHitOverrideDiffers、Checksum_ChangesWhenPendingLifetimeOverrideDiffers、Checksum_ChangesWhenPassiveAbilityLevelDiffers、Checksum_ChangesWhenMinionThreatRefreshTickDiffers、Checksum_ChangesWhenMinionThreatTableDiffers、Checksum_ChangesWhenEquipmentTriggerCountDiffers、Checksum_ChangesWhenGameplayParticipantIdDiffers。
- `Assets/Scripts/Gameplay/Tests/AssistEventIntegrationTests.cs`：HeroDeath_RaisesAssistOnce_AndEmpowersAssistantRevenge、Killer_IsHighestEffectiveLifeDamageContributorInLethalBatch。
- `Assets/Scripts/Gameplay/Tests/BlightDetonationAndOnHitPassiveTests.cs`：AbilityHit_DetonatesAndConsumesAllStacks、Detonation_DoesNotRecurse、Detonation_NonHero_CapsPerStackDamage、ExternalBlightAndAbilityDamage_DoNotAffectStructure、Detonation_Hero_GrantsCooldownReduction、AttackDamage_DoesNotDetonate、WPassive_OnHitDealsBonusDamageAndAppliesBlight。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 七、`CombatSystemSnapshot`

正式与 Combat v13.2 对齐。

### 正式结构

```csharp
public struct CombatSystemSnapshot
{
    public CombatContributionEventLogSnapshot[]
        ContributionEventLogs;

    public DeferredCombatRequestSnapshot[]
        DeferredRequests;
}
```

```csharp
public struct CombatContributionEventLogSnapshot
{
    public UnitUid VictimUnitUid;
    public UnitUid LastHitContributorUid;

    public CombatContributionEventSnapshot[]
        Events;
}
```

```csharp
public struct CombatContributionEventSnapshot
{
    public UnitUid ContributorHeroUid;
    public byte Kind;      // Damage / Shield / Heal
    public fp Amount;
    public int LogicTick;
    public ushort SequenceInTick;
}
```

```csharp
public struct DeferredCombatRequestSnapshot
{
    public int ExecuteLogicTick;
    public int SourceLogicTick;
    public ushort DeferredSequenceInSourceTick;
    public CombatRequestKind RequestKind;

    public ShieldRequestSnapshot Shield;
    public DamageRequestSnapshot Damage;
    public HealRequestSnapshot Heal;
}
```

宽联合只能有与 `RequestKind` 对应的一个有效 Payload。

### Tick 末不保存

```text
ShieldQueue
DamageQueue
HealQueue
PendingDyingRecord
DeferredLifeDamageCache
DyingReviveCandidateRuntime
DeathResolution 临时集合
DeathRewardContext 临时集合
FormalDeathResult 构建缓存
CombatTickResult 构建态
CurrentSequenceLogicTick
NextSequenceInTick
SequenceExhausted
NextDeferredSequenceInSourceTick
DeferredSequenceExhausted
DeathSequenceInTick
DeferredRequestBuildScope
DyingResolutionScope
```

### Capture 断言

```text
ShieldQueue empty
DamageQueue empty
HealQueue empty
PendingDyingRecordSet empty
DeferredLifeDamageCache empty
DyingResolutionScope closed
CombatReactionSchedulingScope closed
DeferredRequestBuildScope closed
CombatTickResult 已冻结
MatchStatisticsRuntime 已完成消费
GoldIncomeAllocations 已提交
```

同时验证：

```text
所有 DeferredRequest.ExecuteLogicTick == CurrentTick + 1
同一 SourceLogicTick 内 DeferredSequenceInSourceTick 不重复
允许合法序列缺号
删除记录后不得重新编号
禁止自然回绕
按 ExecuteLogicTick、SourceLogicTick、
    DeferredSequenceInSourceTick 稳定升序保存
DamageContributionTracker 不包含重复 ContributorHeroUid
```

### Restore / Resolve / Rebuild

```text
Restore
    清空 Tick 内活动队列和瞬态 Scope。
    直接恢复 Tracker 与 DeferredRequestBuffer。
    不发布事件，不提交新请求。

Resolve
    验证 Victim、Contributor、Deferred Source / Target
    和静态 Recipe。
    无效稳定引用产生确定性恢复错误。
    不静默删除、不补建、不重新计算。

Rebuild
    重建 Tracker 与 DeferredRequest 查询索引。
    不重放 UnitDeath / UnitKill。
    不重新创建历史请求。
```

下一次 `CombatSystem.BeginTick(SnapshotTick)` 导入到期延迟请求。

### UnitWorld 清理接缝

非死亡 Despawn 或永久销毁时，UnitWorld 清理：

```text
该 UnitUid 作为 Victim 的 Tracker
该 UnitUid 作为 Contributor 的记录
以该 UnitUid 为 Target 的 DeferredRequest
```

作为 Source 的 DeferredRequest 不能静默删除。必须等待：

```text
CombatSystem.HasDeferredRequestFrom(UnitUid)
    == false
```

再最终注销、回池或 Destroy。

---

### 快照边界

完整 Tick 结束时，CombatSystem 只允许保存真正影响未来 Tick 的两类状态：

```text
DamageContributionTracker
DeferredCombatRequestBuffer
```

正式结构：

```csharp
public struct CombatSystemSnapshot
{
    public DamageContributionTrackerSnapshot[]
        DamageContributionTrackers;

    public DeferredCombatRequestSnapshot[]
        DeferredRequests;
}
```

```csharp
public struct DamageContributionTrackerSnapshot
{
    public UnitUid VictimUnitUid;

    public DamageContributionRecordSnapshot[]
        Records;
}
```

```csharp
public struct DamageContributionRecordSnapshot
{
    public UnitUid ContributorHeroUid;
    public int LastContributionLogicTick;
    public fp ContributionValue;
    public int ExpireLogicTick;
}
```

```csharp
public struct DeferredCombatRequestSnapshot
{
    public int ExecuteLogicTick;
    public int SourceLogicTick;
    public ushort DeferredSequenceInSourceTick;
    public CombatRequestKind RequestKind;

    public ShieldRequestSnapshot Shield;
    public DamageRequestSnapshot Damage;
    public HealRequestSnapshot Heal;
}
```

`DeferredCombatRequestSnapshot` 的宽联合只是逻辑表示；实际实现可使用三种强类型快照数组和统一顺序头，但必须保持同样的规范顺序与唯一有效 Payload 约束。

### Tick 末不保存的瞬态状态

以下内容必须在 Capture 前清空、关闭或完成消费：

```text
ShieldQueue
DamageQueue
HealQueue
PendingDyingRecord
DeferredLifeDamageCache
DyingReviveCandidateRuntime
DeathResolution 临时集合
DeathRewardContext 临时集合
FormalDeathResult 构建缓存
CombatTickResult 构建态
CurrentSequenceLogicTick
NextSequenceInTick
SequenceExhausted
DeathSequenceInTick
DeferredRequestBuildScope
DyingResolutionScope
```

以下输出不进入 `CombatSystemSnapshot`：

```text
CombatTickResult
FormalDeathResult 历史
GoldIncomeAllocation 历史
TeamBaseDestroyedSignal 历史
GoldIncomeRecordBatch
ConfirmedEarnedGoldTotal
MatchStatisticsRuntime 状态
账户持久化任务
```

其中 `CombatTickResult` 在重演对应 Tick 时重新生成；金币批次与确认累计由 `GoldIncomeRuntime` 管理；比赛统计由 `MatchStatisticsRuntimeSnapshot` 管理。

### Capture 断言

`CombatSystem.Capture` 必须执行确定性断言：

```text
ShieldQueue empty
DamageQueue empty
HealQueue empty
PendingDyingRecordSet empty
DeferredLifeDamageCache empty
DyingResolutionScope closed
CombatReactionSchedulingScope closed
DeferredRequestBuildScope closed
CombatTickResult 已冻结
MatchStatisticsRuntime 已完成消费
GoldIncomeAllocations 已由 CombatGoldIncomeProducer 提交
```

同时验证：

```text
所有 DeferredRequest.ExecuteLogicTick == CurrentTick + 1
同一 SourceLogicTick 内 DeferredSequenceInSourceTick 不重复
DeferredSequenceInSourceTick 只能由统一延迟序列分配器生成，禁止自然回绕
删除 DeferredRequest 后不得重新编号其它记录；合法序列缺号允许保留
Capture 按 ExecuteLogicTick、SourceLogicTick、DeferredSequenceInSourceTick 稳定升序规范序列化
延迟记录顺序不按 RequestKind、事件类型或来源 Handler 分组
DamageContributionTracker 不包含重复 ContributorHeroUid
全部 Tracker / Record 按规范顺序可序列化
```

任一断言失败都表示 Tick Pipeline 或 Combat 生命周期错误：

```text
禁止保存半结算快照
记录确定性诊断
终止当前错误模拟路径
```

`DeferredCombatRequestBuffer` 非空是合法状态；三条活动队列非空不是合法状态。

### Capture / Restore / Resolve / Rebuild

```text
Capture
    -> 按 VictimUnitUid 升序保存 DamageContributionTracker
    -> 每个 Tracker 内按 ContributorHeroUid 升序保存 Record
    -> 按 ExecuteLogicTick、SourceLogicTick、DeferredSequenceInSourceTick
       保存 DeferredCombatRequest

Restore
    -> 清空所有 Tick 内活动队列和瞬态 Scope
    -> 直接恢复 DamageContributionTracker 与 DeferredCombatRequestBuffer
    -> 不发布 UnitEventBus
    -> 不提交新的 CombatRequest

Resolve
    -> 验证每个 VictimUnitUid 在目标 UnitWorld 状态中存在
    -> 验证每个 ContributorHeroUid 在目标 UnitWorld 状态中存在且对应 Hero
    -> 验证同一 Tracker 内不存在重复 ContributorHeroUid
    -> 任一贡献引用验证失败时产生确定性恢复错误并终止当前恢复路径
    -> 不删除、不补建、不重新计算任何贡献记录
    -> 验证 DeferredRequest 的 Source / Target UnitUid 与静态 Recipe 引用

Rebuild
    -> 重建 VictimUnitUid -> Tracker 查询索引
    -> 重建 DeferredRequest 的 ExecuteLogicTick 查询索引
    -> 不重新计算历史贡献
    -> 不重新发布 UnitDeath / UnitKill
    -> 不重新创建延迟请求
```

恢复完成后，下一次 `CombatSystem.BeginTick(snapshotTick)` 正常导入到期 DeferredRequest。

`CombatSystem.Resolve` 只验证合法快照中的稳定引用和绑定关系，不负责把错误快照修剪成可运行状态。若贡献引用不存在，应优先暴露 `UnitWorldSnapshot` 与 `CombatSystemSnapshot` 不一致、清场接缝遗漏、快照字段缺失或恢复顺序错误，禁止通过静默删除掩盖问题。

### 与 UnitWorld 清场的接缝

正式死亡后，CombatSystem 在冻结 `FormalDeathResult` 后删除 Victim Tracker。

非死亡 `DespawnUnit`、永久销毁和回滚拓扑静默移除当前 UnitUid 时，UnitWorld 必须通过固定 Combat 清理接缝删除：

```text
该 UnitUid 作为 Victim 的 DamageContributionTracker
该 UnitUid 作为 Contributor 的贡献记录
以该 UnitUid 为 Target 的 DeferredCombatRequest
```

作为 Source 的 DeferredCombatRequest 不能在其执行前被静默删除。UnitWorld 必须先等待 `HasDeferredRequestFrom(UnitUid) == false`，再完成最终注销、回池或 Destroy。若是回滚拓扑恢复，则直接按目标快照恢复 DeferredRequest，不执行普通 Gameplay 等待规则。

正常英雄死亡与复活不全量清除其它单位对该英雄的合法跨 Tick状态；仅按上述 Victim 正式死亡规则和来源 Runtime 生命周期处理。

### . 最终模块结构

```text
CombatSystem
├── CombatRequestSequencer
│   ├── CurrentSequenceLogicTick
│   ├── NextSequenceInTick : ushort
│   ├── ShieldQueue
│   ├── DamageQueue
│   └── HealQueue
│
├── DeferredRequestRuntime
│   ├── CombatReactionSchedulingScope
│   ├── NextDeferredSequenceInSourceTick : ushort
│   ├── DeferredSequenceExhausted
│   ├── DeferredCombatRequestBuffer
│   ├── DeferredCombatRequestRecord
│   └── DeferredSequenceInSourceTick
│
├── RequestTypes
│   ├── CombatRequestHeader
│   ├── SourceDescriptor
│   ├── DeliveryDescriptor
│   ├── ShieldRequest
│   ├── DamageRequest
│   └── HealRequest
│
├── Pipelines
│   ├── NaturalRegenPipeline
│   ├── ShieldPipeline
│   ├── DamagePipeline
│   ├── HealPipeline
│   ├── DyingResolutionPipeline
│   │   ├── ImmediateSurvivalResolution
│   │   ├── DyingReviveCandidateResolution
│   │   └── DeathBatchFinalization
│   └── DeathRewardPipeline
│       ├── RewardRecipientResolver
│       ├── MinionRewardResolver
│       ├── HeroRewardResolver
│       ├── TowerRewardResolver
│       ├── MonsterRewardResolver
│       ├── ExperienceSettlement
│       ├── GoldIncomeAllocationBuilder
│       └── FormalDeathResultBuilder
│
├── LifeRuntime
│   ├── PendingDyingRecord
│   └── DeferredLifeDamageCache
│
├── ContributionRuntime
│   ├── DamageContributionTracker
│   ├── DamageContributionRecord
│   └── AssistResolver
│
├── Formula
│   ├── DamageRecipe / DamageFormula
│   ├── HealRecipe / HealFormula
│   ├── ShieldRecipe / ShieldFormula
│   ├── FormulaTerm
│   ├── CombatFormulaSlot
│   ├── CombatFormulaPatch
│   ├── CombatModifierOperation
│   ├── CombatOperand
│   ├── CombatOperandTerm
│   └── CombatValueRef
│
├── Modifier
│   ├── CombatModifierCollector
│   ├── CombatModifierRecord
│   ├── CombatModifierMatch
│   ├── CombatPolicyPatch
│   └── CombatModifierHandle
│
├── DeathAndRewards
│   ├── DeathResolution
│   ├── TeamBaseDestroyedSignal
│   ├── DeathRewardContext
│   ├── ExperienceAward
│   ├── GoldIncomeAllocation
│   └── FormalDeathResult
│
├── Snapshot
│   ├── CombatSystemSnapshot
│   ├── DamageContributionTrackerSnapshot
│   ├── DamageContributionRecordSnapshot
│   └── DeferredCombatRequestSnapshot
│
├── ExternalContracts
│   ├── CombatGoldIncomeProducer Output Contract
│   └── MatchStatisticsRuntime Consumer Contract
│
└── Results
    ├── DamageResult
    ├── HealResult
    ├── ShieldResult
    └── CombatTickResult
```

正式 `LifeState` 保存在 `Unit`，唯一写入权威为 `UnitWorld`。CombatSystem 不持有正常英雄复活 Runtime。

`GoldIncomeRuntime / MatchStatisticsRuntime / EquipmentShopRuntime / FrameSync Runtime` 均为外部正式消费者或服务，不属于 CombatSystem 内部模块树。

### Tick 与请求顺序

```text
SimulationTickContext.Current
    是全部战斗逻辑的唯一当前 Tick / ExecutionMode 来源。

当前 Tick：
    三条强类型活动队列
    + CombatSystem 自己的 ushort SequenceInTick
    + 每次取三个队首中的最小序号。

跨 Tick：
    UnitDeath / UnitKill 产生的普通战斗请求
    -> DeferredCombatRequestBuffer
    -> 下一 Tick BeginTick 导入并重新分配 SequenceInTick。
```

三条活动队列必须在 Tick 末清空；合法延迟请求通过独立跨 Tick Buffer 快照。

### 战斗公式与 Modifier

```text
Recipe
    负责基础公式。

CombatModifierRecord
    由具体生效点动态创建并挂载到 Unit.CombatModifierSet。

CombatFormulaPatch
    = FormulaSlot + Operation + CombatOperand。

CombatOperand
    = Constant + Σ(ValueRef × Coefficient)。
```

Modifier 不保存 `Priority / ExpireTick / RemainingUses / Handle`。Record 只保存挂载端填写的稳定 `Id`；`Handle` 仅由挂载端持有，用于 `Detach`；修正内容变化时由挂载端 Detach 后重新 Attach。

Modifier 生命周期与来源效果实例严格绑定，CombatSystem 只查询和应用。

---

### 单位事件

单位框架冻结 11 种强类型单位事件，但 CombatSystem 只负责其中 7 种接缝：

```text
CombatSystem 直接发布：
    DamageTaken
    DamageDealt
    HealTaken
    HealDealt
    UnitKill

CombatSystem 请求 UnitWorld 转换 LifeState 后，UnitWorld 发布：
    UnitDying
    UnitDeath
```

调度规则：

```text
DamageTaken / DamageDealt / HealTaken / HealDealt
    -> 新普通战斗请求在当前 Tick 执行。

UnitDying
    -> DyingResolutionScope 在当前 Tick 完成；
    -> 其它普通战斗请求仍进入当前 Tick。

UnitDeath / UnitKill
    -> 回调本身在正式死亡 Tick 即时执行；
    -> 新普通战斗请求延迟到下一 Tick。
```

事件不能倒改已经成立的结果；没有业务的 Handler 不进入对应 Publish 路由，不增加空函数。

### 生命周期

```text
生命归零
    -> PendingDyingRecord，LifeState 仍为 Alive
    -> 活动队列清空后 CombatSystem 调用 RequestEnterDying
    -> UnitWorld 写入 Dying 并发布 UnitDying
    -> ImmediateSurvival：RequestRecoverFromDying
    -> DyingReviveCandidate：由正式濒死复活接缝交给 UnitWorld
    -> 无救回：冻结贡献、奖励上下文与 DeathResolution
    -> ConfirmUnitDeath
    -> UnitWorld 写入 Dead 并发布 UnitDeath
```

正式 API 统一为：

```text
UnitWorld.RequestEnterDying
UnitWorld.RequestRecoverFromDying
UnitWorld.ConfirmUnitDeath
```

正常的 `Dead -> Respawning -> Alive`、死亡表现、回池、销毁和废墟生成全部由 UnitWorld 管理。普通死亡不全量清空长期 Modifier。

### 护盾

```text
白盾：吸收所有可吸收伤害。
物理盾：只吸收物理伤害。
魔法盾：只吸收魔法伤害。
黑盾：只吸收魔法伤害，并在有效期间提供控制免疫。
```

黑盾免疫由 `StatHandler` 与 `CrowdControlHandler` 绑定生命周期，CombatSystem 只负责伤害吸收匹配。

---

### 奖励、助攻与统计

```text
小兵：死亡小兵范围内的敌方英雄共享，英雄击杀者占大头。
英雄：击杀英雄和协助英雄共享，击杀者占大头。
防御塔：与英雄相同，BaseExperienceValue = 0，暂不处理镀层。
野怪：仅击杀英雄。
金币和经验只发放给英雄单位。
```

助攻贡献采用：

```text
ActualShieldDamage + ActualLifeDamage
```

即 Kind=Damage 事件的 `Amount`。大于 0 才写入 `CombatContributionEventLog`（§7.14.1）；事件跨 Tick 保存，按全局助攻时限过期，受每 Victim 容量上限约束；正式死亡前按 §7.14.3 冻结 `KillerHeroUid / AssistantHeroUids`。

经验在死亡所在 Gameplay LogicTick 立即结算并可回滚。

金币先输出：

```text
GoldIncomeAllocation
```

随后由外部 `CombatGoldIncomeProducer` 在固定阶段请求 `GoldIncomeRuntime`。CombatSystem 不创建正式 `GoldIncomeRecord`，不分配 `IncomeSequenceInTick`，也不维护确认累计。

KDA 和整局统计由所有模拟端的：

```text
MatchStatisticsRuntime
    <- CombatTickResult.FormalDeathResults
```

统一更新。

### 帧同步边界

`CombatSystemSnapshot` 正式只保存：

```text
DamageContributionTrackerSnapshot[]
DeferredCombatRequestSnapshot[]
```

活动队列、PendingDying、死亡解析临时状态和 CombatTickResult 必须在 Capture 前清空或完成消费。Restore 直接恢复跨 Tick 状态，Rebuild 只重建索引，不重新发布事件、不重新创建历史请求。

`Resolve` 遇到不存在的 Victim 或 Contributor 引用时必须产生确定性恢复错误，不能静默删除贡献记录。`NextDeferredSequenceInSourceTick` 与耗尽标记只属于当前 Tick 构建状态，不进入快照；已写入 `DeferredCombatRequestSnapshot` 的 `SourceLogicTick + DeferredSequenceInSourceTick` 才是跨 Tick 正式状态。该序列是稳定排序身份而非压缩数组索引：合法删除可以留下缺号，剩余记录不得重新编号，Capture 只检查唯一性并按序列稳定升序序列化。

### 收集、封存与结算波次

普通 `SubmitShield / SubmitDamage / SubmitHeal` 分为：

```text
Collect -> SealCurrentWave -> SettleTargetBatches
```

Collect 完成基本合法性验证并保存内部 Pending envelope，不分配最终
`SequenceInTick`。Seal 按正式阶段、因果波次、动作来源与动作内效果序号形成规范
目标批次，再分配当前 Tick 的最终 `SequenceInTick`。

```text
Wave 0: 当前 Tick 的基础攻击、技能、投射物和已导入 Deferred 请求
Wave N+1: Wave N 的 Damage/Heal/OnHit/Buff/Equipment Reaction 新请求
```

同一 Wave 全部目标批次提交完成后才进入下一 Wave。`UnitDeath / UnitKill` Reaction
产生的普通战斗请求仍依 D-010 延迟到下一 Tick。超过
`MaxCombatSettlementCyclesPerTick` 产生确定性错误。

### 公平性与确定性验收

必须覆盖：

- 重复执行逐位等价；
- 连续与 Snapshot/Restore/Replay 等价；
- 请求插入顺序无关；
- 对同一已接受的非随机请求集合，改变 Submit/注册顺序、技术 Prefab/阵营编号，或仅
  重标不会改变正式目标选择与随机样本归属的 UID 后，生命、护盾、死亡和非平局击杀不变；
- 人为重标 UID 的测试只用于证明 UID 不会通过中间状态写入形成隐式先手权，不要求
  随机 Crit 样本继续映射到相同标签，也不要求改变正式的完全同距离投射物目标平局规则；
- 同 Tick 相互致死仍允许双方正式死亡；
- Shield+Damage、Heal+Damage、多来源 Overkill、Reaction Wave；
- 方案 A 的最高有效生命伤害；
- 方案 C 同一 seed 重演一致、跨固定 seed 语料不固定偏向阵营或 PrefabId；
- 非法配置、波次耗尽和结算循环上限确定性失败。


## 需求演进

### 2026-10-02

变动内容：死亡和击杀反应即时分发，产生的新普通战斗请求延至下一 Tick。

legacyDecision：D-010

### 2026-08-24

变动内容：战斗封存波次和冻结批次起始状态；击杀按最高有效生命伤害，取代末次伤害者。

legacyDecision：D-049

