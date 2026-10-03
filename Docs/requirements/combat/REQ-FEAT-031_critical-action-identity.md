# 动作键暴击与中性平局

## 目标实现

技术 UID 重新标记不改变暴击样本和完全等距命中选择。

## 技术方案

暴击纯 64 位 hash 输入 InitialMatchSeed、OriginActionId、目标 ParticipantId、EffectOrdinal 和固定域；投射物等距排序先中性分数再完整参与者身份。

## 边界情况

不消耗共享随机流；只有完整身份/分数碰撞才用 TargetUnitUid；跨固定种子集检查不永久偏向阵营或 Prefab。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/Gameplay/Combat/CombatActionIdentity.cs`：当前关联实现定义 OriginActionId、CombatActionIdentityFactory、CombatFairnessKey（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Unit/Core/GameplayParticipantId.cs`：当前关联实现定义 GameplayParticipantDomain、GameplayParticipantId（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/ClientContent/Tests/PlayMode/ProjectileViewBinderPlayModeTests.cs`：ProjectileViewLeaseStaysResidentAcrossLifetimes。
- `Assets/Scripts/FrameSync/Tests/ChecksumNewStateCoverageTests.cs`：Checksum_ChangesWhenProjectileOnHitOverrideDiffers、Checksum_ChangesWhenPendingLifetimeOverrideDiffers、Checksum_ChangesWhenPassiveAbilityLevelDiffers、Checksum_ChangesWhenMinionThreatRefreshTickDiffers、Checksum_ChangesWhenMinionThreatTableDiffers、Checksum_ChangesWhenEquipmentTriggerCountDiffers、Checksum_ChangesWhenGameplayParticipantIdDiffers。
- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/ProjectileFalloffTests.cs`：PiercingFalloff_ReducesDamagePerExtraHit、PiercingFalloff_OverridePath_MatchesStaticConfig、PiercingFalloff_ClampsAtMinDamageRatio。
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 动作级确定性暴击

概率暴击分值：

```text
CritRoll64 = StableHash64(
    InitialMatchSeed,
    OriginActionId,
    TargetGameplayParticipantId,
    EffectOrdinal,
    CombatCritDomain)
```

将固定 32 位映射为 `[0, 1)` 固定点值，与当前暴击概率比较。该运算不调用
`DeterministicRandomService.Next*`，因此其它系统随机调用数量和 Damage 请求规范排序
不会重新分配 Crit 样本。同一动作、目标和效果序号重复求值必须得到同一结果。

### 验收

必须覆盖：

- 同一动作键重复求值、Snapshot/Restore/Replay 逐位等价；
- 交换技术 UnitUid/PrefabId/注册顺序但保持 Participant/Action 身份后，Crit 归属不变；
- 在 Crit 请求前后插入其它全局随机调用不改变该 Crit；
- 投射物完全同距、最大命中数、穿透和衰减在 UID 重标后选择相同 Participant；
- 固定 seed 语料不永久偏向 Team、PrefabId 或 UID 升序；
- Deferred Damage 和 Pending/Active Projectile 身份快照/校验覆盖；
- 缺失/重复 Participant、缺失概率 Crit ActionId、非法 EffectOrdinal 确定性失败。


## 需求演进

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050

