# 稳定 UID 与参与者身份

## 目标实现

技术实体身份和玩法随机身份均有明确来源。

## 技术方案

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

## 边界情况

参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatActionIdentity.cs`：当前关联实现定义 OriginActionId、CombatActionIdentityFactory、CombatFairnessKey（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Projectile/ProjectileUid.cs`：当前关联实现定义 ProjectileUid（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/GameplayParticipantId.cs`：当前关联实现定义 GameplayParticipantDomain、GameplayParticipantId（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：当前关联实现定义 UnitUid（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Gameplay/Tests/UnitUidTests.cs`：SameComponents_ProduceEqualIdentity、ChangingAnyComponent_ChangesIdentity、CompareTo_UsesFormalLexicographicOrder、SortingSameValues_IsIndependentOfInsertionOrder。
- `Assets/Scripts/ClientContent/Tests/PlayMode/ProjectileViewBinderPlayModeTests.cs`：ProjectileViewLeaseStaysResidentAcrossLifetimes。
- `Assets/Scripts/FrameSync/Tests/ChecksumNewStateCoverageTests.cs`：Checksum_ChangesWhenProjectileOnHitOverrideDiffers、Checksum_ChangesWhenPendingLifetimeOverrideDiffers、Checksum_ChangesWhenPassiveAbilityLevelDiffers、Checksum_ChangesWhenMinionThreatRefreshTickDiffers、Checksum_ChangesWhenMinionThreatTableDiffers、Checksum_ChangesWhenEquipmentTriggerCountDiffers、Checksum_ChangesWhenGameplayParticipantIdDiffers。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/ProjectileFalloffTests.cs`：PiercingFalloff_ReducesDamagePerExtraHit、PiercingFalloff_OverridePath_MatchesStaticConfig、PiercingFalloff_ClampsAtMinDamageRatio。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### UID 公共要求

`UnitUid` 和 `ProjectileUid` 是不同强类型，由各自正式系统定义具体字段和序列类型。

共同要求：

```text
包含稳定 SpawnLogicTick。
包含全局 RuntimeEntityPrefabId。
包含所属系统定义的稳定 SpawnSequence。
可序列化。
可比较。
相同输入重演时生成相同 UID。
```

帧同步层不强制 Unit 与 Projectile 使用相同序列类型，也不要求共享计数器。

### 序列归属

不建立全项目共享序列号。

每个系统自行明确：

```text
序列名称
作用域
何时重置
数据类型
排序位置
溢出行为
是否进入快照
```

示例：

```text
Projectile SpawnSequence
    可按 Tick 重置。

Combat Request Sequence
    可按 Tick 重置并使用 ushort。

AttackSequenceIndex
    可跨 Tick 累积。

Shop OperationSequence
    可整场递增。

Presentation EventSequence
    由表现事件生产者定义作用域。
```

### 确定性要求

序列分配前的请求集合必须使用稳定顺序。禁止依赖：

```text
Dictionary 枚举顺序
HashSet 枚举顺序
网络包实际抵达顺序
Unity 对象创建顺序
编辑器资源加载顺序
```

发生溢出时必须采用所属系统文档冻结的确定性处理，禁止自然回绕导致 UID 或事件身份碰撞。

### 快照边界

序列是否进入快照由其生命周期决定：

```text
只在 Tick 内存在且快照仅保存 Tick 边界
    -> 通常不进入快照。

跨 Tick 或整场累积
    -> 必须进入所属系统快照。
```

帧同步总控不替子系统猜测序列恢复方式。

---

### GameplayParticipantId

每个权威 Unit 必须拥有不可变且当前存活集合内唯一的 `GameplayParticipantId`。它由
稳定玩法出生来源组成，不得读取 PrefabId、UnitUid、注册顺序、Unity InstanceID 或阵营
遍历位置：

```text
Domain / Scope / Generation / Ordinal
```

正式来源：

- 初始单位：`StableSpawnOrder`；
- 小兵：`Team + Lane + SpawnLogicTick + StableEntryIndex` 的正式票据身份；
- 野怪：`CampId + SpawnLogicTick + MemberSlot`；
- 派生单位：显式父来源、生成 Tick 与子序号；
- 测试/工具：必须显式提供稳定身份，不得由 GameObject 顺序生成。

该身份进入 Unit Snapshot 与 SharedGameplayChecksum。Restore 必须精确恢复并验证唯一性，
不得根据恢复时的 UnitUid 重新推导。

### OriginActionId 与 EffectOrdinal

会产生随机 Crit 或参与投射物平局裁决的动作使用：

```text
OriginActionId
    SourceParticipantId
    SourceType
    SourceId
    OriginLogicTick
    SourceLocalSequence

EffectOrdinal
    动作内稳定效果序号
```

普攻使用攻击开始 Tick 与攻击本地序号；技能使用 AbilitySession 的 StartLogicTick、
SessionUid 和 AbilityId；投射物继承生成它的 OriginActionId，每个伤害效果使用数组中的
稳定 EffectOrdinal。多目标随机结果还包含目标 `GameplayParticipantId`，因此无需依赖
TargetUnitUid 排序来分配随机样本。

事件派生伤害必须把父伤害的 `EffectOrdinal` 折入子效果序号；`OnHitEventData` 和
`DamageEventData` 均携带父序号。两个不同父效果即使触发相同 Recipe/配置，也不得
复用同一子 Crit 键。负 `EffectOrdinal` 在 Damage 提交、Deferred 和 Restore 边界
确定性失败，不能因 0%、100% 或强制暴击路径而跳过校验。

缺少动作身份的概率暴击必须确定性失败。`ForceCrit` 和 100% 暴击不需要随机样本，但
仍应在正式生产路径携带动作身份以便审计和后续效果扩展。


## 需求演进

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050

