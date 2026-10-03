# 命中过滤记忆与等距裁决

## 目标实现

同目标重复命中由策略控制，等距目标有中性选择。

## 技术方案

ProjectileHitQueryService 使用 UnitFinalGrid、扫掠形状和正式 TargetFilter；ProjectileHitMemory 保存跨 Tick 命中事实，命中结果进入 Combat 或所属效果端口。

## 边界情况

距离为主键；等距按动作/参与者纯哈希；记忆不能依赖候选枚举顺序；结构技能效果由中央准入兜底拒绝。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Assets/Scripts/FrameSync/ProjectileHitResolver.cs`：当前关联实现定义 ProjectileHitResult、ProjectileHitResolver（以源码为实际命名）。
- `Assets/Scripts/Gameplay/Projectile/ProjectileRuntime.cs`：当前关联实现定义 ProjectileHitRecord、ProjectileRuntime（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/FrameSync/Tests/ProjectileCombatPipelineTests.cs`：EqualDistanceHits_AreFairnessKeyOrderedAndUseCombat、EndOnFirstHit_RejectsLaterSameTickCandidate、EqualDistanceArbitration_UidRelabelKeepsParticipantWinner、PierceBudget_EndsAtConfiguredHitCount、ProjectileDamage_UsesCombatShieldPipeline、EnemyFilter_ExcludesFriendlyTarget、EnemyFilter_IncludesStructureTarget。
- `Assets/Scripts/FrameSync/Tests/ProjectileFalloffTests.cs`：PiercingFalloff_ReducesDamagePerExtraHit、PiercingFalloff_OverridePath_MatchesStaticConfig、PiercingFalloff_ClampsAtMinDamageRatio。
- `Assets/Scripts/Gameplay/Tests/AatroxFormalContentTests.cs`：CombinedAbilityCatalog_BakesAatroxAndVarus、DarkinBlade_UsesExactThreeZonesAndRecastDelay、DarkinBlade_VfxDurationUsesConfiguredGameplayTickRate、WProjectile_FliesStraightAlongCastDirection_IgnoringTargetPosition、SequentialRecastSession_SnapshotRoundTripPreservesWindowState、SequentialRecastWindow_ExposesShortHudCooldown、FormalCatalogs_RegisterAatroxRuntimeContent。
- `Assets/Scripts/Gameplay/Tests/AbilityAimStageTests.cs`：AreaDamageStage_CentersOnAimTargetPoint、AreaDamageStage_DoesNotDamageEnemyStructure、SpawnProjectileStage_FiresTowardAimDirection。
- `Assets/Scripts/Gameplay/Tests/ChargeAbilityTests.cs`：ChargeStage_RatioIncreasesWithElapsedTicks、ChargeStage_SelfSlowAppliesWhileChargingAndRemovesOnRelease、ChargeStage_TimeoutCancelsAndRefundsHalfCost、ChargeStage_ConsumesActiveToggleAndStartsCooldown、ChargeProjectileStage_InterpolatesDamageRangeAndOverride、ChargeProjectile_SnapshotRoundTrip_PreservesOverride。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

### 投射物完全同距离裁决

移动 Sweep 与 AoE 候选首先继续按正式几何距离升序。距离完全相同时计算：

```text
TargetTieScore64 = StableHash64(
    InitialMatchSeed,
    ProjectileOriginActionId,
    CandidateGameplayParticipantId,
    ProjectileTieDomain)
```

排序键为：

```text
HitDistance
TargetTieScore64
GameplayParticipantId
TargetUnitUid  // 仅完整身份/分值碰撞兜底
```

因此技术 UID 重标不会改变被选中的玩法参与者。哈希只负责中性打散，不消耗全局随机
流；跨固定 seed 语料不得固定偏向 Team、PrefabId 或 UID 较小者。

### 系统边界

```text
ProjectileWorld.ResolveHits
    决定何时查询并组织命中结果。

ProjectileHitQueryService
    根据 PhysicsEntity2D 和 UnitFinalGrid 计算空间候选和精确重叠。

ProjectileTargetFilter
    根据单位业务身份和状态过滤候选。

ProjectileHitMemory
    判断同目标是否允许再次命中。

HitModules
    对已经确认的命中执行 Gameplay 效果。
```

物理系统不执行 HitModules，也不维护投掷物命中次数。

---

### `UnitFinalGrid` 的使用规则

`UnitFinalGrid` 必须包含所有当前有效空间单位。

构建网格时不得提前过滤：

```text
Capability.IsTargetable == false
```

否则投掷物设置：

```text
RequireTargetable = false
```

也无法找到这些单位。

查询时再按具体 `ProjectileTargetFilter` 判断是否允许命中。

---

### `ProjectileTargetFilter`

适配单位框架 v20：

```text
ProjectileTargetFilter
    TeamRule
    UnitKindMask

    IncludeSubKindIds
    ExcludeSubKindIds

    IncludePrototypeIds
    ExcludePrototypeIds

    AllowedLifeStates
    RequireTargetable
```

字段说明：

| 字段 | 说明 |
|---|---|
| `TeamRule` | 敌方、友方、自己或全部 |
| `UnitKindMask` | Hero、Minion、Monster、Structure 等稳定大类 |
| `IncludeSubKindIds` | 只允许指定主要子分类 |
| `ExcludeSubKindIds` | 排除指定主要子分类 |
| `IncludePrototypeIds` | 精确允许指定单位原型 |
| `ExcludePrototypeIds` | 精确排除指定单位原型 |
| `AllowedLifeStates` | 允许 Alive、Dying、Dead、Respawning 中哪些状态 |
| `RequireTargetable` | 是否要求 `Capability.IsTargetable` |

不使用：

```text
UnitTags
UnitQueryTraitMask
运行时任意标签集合
```

权威数据来源：

| 过滤数据 | 来源 |
|---|---|
| 阵营 | `Projectile.Team` 与 `Unit.TeamId` |
| 大类 | `Unit.UnitKind` |
| 子分类 | `Unit.UnitSubKindId` |
| 具体原型 | `Unit.UnitPrototypeId` |
| 生命周期 | `Unit.LifeState` |
| 可命中能力 | `Unit.Capability.IsTargetable` |

`PhysicsEntity2D` 只负责通过 `Owner` 回溯到 Unit，不能成为这些单位业务字段的权威来源。

---

### `ProjectileHitPolicy`

```text
ProjectileHitPolicy
    bool Enabled
    int QueryIntervalTicks

    HitSameTargetPolicy SameTargetPolicy
    int SameTargetCooldownTicks

    int MaxTotalHitCount
    int InitialPierceCount
    int InitialBounceCount

    bool EndOnFirstValidHit
    bool StopResolvingAfterEndRequested
```

说明：

| 字段 | 说明 |
|---|---|
| `Enabled` | 是否参与命中查询 |
| `QueryIntervalTicks` | 区域类投掷物可降低查询频率 |
| `SameTargetPolicy` | 同目标命中规则 |
| `SameTargetCooldownTicks` | 冷却策略的间隔 |
| `MaxTotalHitCount` | 总命中上限 |
| `InitialPierceCount` | 初始穿透次数 |
| `InitialBounceCount` | 初始弹跳次数 |
| `EndOnFirstValidHit` | 首次有效命中后请求结束 |
| `StopResolvingAfterEndRequested` | 请求结束后是否停止处理后续候选 |

---

### 同目标命中策略

```text
HitSameTargetPolicy
    Once
    Cooldown
    Unrestricted
```

| 策略 | 说明 |
|---|---|
| `Once` | 同一目标整个生命周期只命中一次 |
| `Cooldown` | 距上次命中达到指定 Tick 后可再次命中 |
| `Unrestricted` | 不限制同目标次数，由总命中数和生命周期约束 |

不设计 `Stay` 事件。

持续区域要实现周期命中时，使用：

```text
ResolveHits 每 Tick 或按 QueryIntervalTicks 查询
+
SameTargetPolicy.Cooldown
```

无需额外维护 Enter、Stay、Exit 三套命中事件。

---

### `ProjectileHitMemory`

```text
ProjectileHitMemory
    int TotalHitCount
    PerTargetHitRecordSet Records
```

单目标记录：

```text
PerTargetHitRecord
    UnitUid TargetUid
    int HitCount
    int LastHitLogicTick
```

用途：

```text
Once
    判断是否已有记录。

Cooldown
    判断 CurrentLogicTick - LastHitLogicTick。

Unrestricted
    仍可记录命中计数，供模块和快照使用。
```

`HitMemory` 是 `Projectile` 内部运行时数据：

```text
随 Projectile 一起清理复用。
不单独建立对象池。
不存入 PhysicsEntity2D。
不由 ProjectileHitQueryService 维护。
```

---

### 命中结果

```text
ProjectileHitResult
    ProjectileUid ProjectileUid
    UnitUid TargetUnitUid
    fp2 HitPosition
    fp HitDistance
    int CandidateOrder
```

`PendingHitBuffer` 中的结果必须稳定排序。

移动 Sweep 投掷物：

```text
1. HitDistance 升序
2. TargetUnitUid 升序
```

静止区域：

```text
TargetUnitUid 升序
```

`CandidateOrder` 只作为同距离条件下的稳定补充，不应依赖哈希表遍历顺序。

---

### 命中效果边界

HitModules 可以：

```text
提交 DamageRequest
提交 HealRequest
提交 ShieldRequest
提交 Buff 请求
提交 Control 请求
请求生成新投掷物
请求结束当前投掷物
更新 Projectile 自己的模块状态
```

HitModules 不可以：

```text
直接修改 Unit HP
直接写 Unit PhysicsEntity2D 位置
直接调用 Unity Transform
重新执行空间命中查询
修改 ProjectileDef
建立父子投掷物生命周期
```

一个投掷物生成另一个投掷物仅通过：

```text
ProjectileSpawnRequest
```

表达来源关系，不建立父子对象关系。

---

### 最小协作协议

物理系统只依赖以下事实：

```text
Projectile 是纯 C# Gameplay 对象。
Projectile 可以获得并持有一个已经绑定的 PhysicsEntity2D。
ProjectileHit 查询时，调用方能提供：
    PhysicsEntity2D SourceEntity
    ProjectileTargetFilter
    命中排序与形状测试所需输入
```

物理系统不规定 `Projectile` 的 GameObject、Prefab 和对象池结构。

---

### 空间状态

投掷物如何运动由 `Projectile` 自身规则决定。  
当投掷物需要改变位置、朝向或形状时，只需操作其已经绑定的 `PhysicsEntity2D`：

```text
PhysicsEntity2D.SetLogicPosition(...)
PhysicsEntity2D.SetLogicPose(...)
PhysicsEntity2D.ApplyLogicPositionDelta(...)
PhysicsEntity2D.TeleportLogicPosition(...)
PhysicsEntity2D.SetLogicForward(...)
PhysicsEntity2D.SetLogicShape(...)
```

物理系统不规定 `Projectile` 的运动模块、生命周期阶段或调用顺序。

投掷物自身继续拥有：

```text
ProjectileUid
Owner
HitRule
HitMemory
生命周期
运动运行时状态
Pipeline / Effect 提交
```

哪些空间字段进入 `ProjectileWorldSnapshot`，由投掷物系统统一聚合；`PhysicsRuntimeSnapshot` 不重复保存第二份。

### HitCheck 调用

```pseudo
function HitCheck(projectile):
    sourceEntity = projectile.PhysicsEntity

    hits = PhysicsWorld.ProjectileHitQuery.Query(
        sourceEntity,
        projectile.TargetFilter,
        projectile.TempHitBuffer
    )

    projectile.ProcessHitCandidates(hits)
```

`ProjectileHitQueryService` 只返回候选命中。  
以下内容仍属于投掷物系统：

```text
HitEvent
HitMemory
PerTargetCooldown
MaxHitCount
穿透与反弹
命中后结束
效果提交
```

---

### 不写入投掷物系统的实现假设

物理设计案不再出现：

```text
ProjectilePrefabRoot 必须有哪些组件
ProjectileWorld 必须从哪个池取得 PhysicsEntity2D
Projectile.GetComponent
投掷物对象池激活与回收顺序
投掷物 SpawnPipeline 的完整阶段
```

只保留已绑定实体的消费接口。


## 需求演进

### 2026-08-24

变动内容：动作键暴击和等距中性裁决；覆盖先前对 UID 耦合随机样本和目标平局的许可。

legacyDecision：D-050

### 2026-09-01

变动内容：建筑中央准入仅允许规定外源普通攻击，自身效果允许，合法拒绝是成功空操作。

legacyDecision：D-054

