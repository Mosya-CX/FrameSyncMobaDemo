# 普攻战斗来源合同恢复

## 本次执行范围

本计划对应原编码 0113 的一次执行：普攻战斗来源合同恢复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [战斗请求封存与因果波次](../../requirements/combat/REQ-FEAT-029_combat-causal-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [伤害配方抗性与吸血](../../requirements/combat/REQ-FEAT-030_damage-resistance.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [濒死批次与公平击杀归属](../../requirements/combat/REQ-FEAT-034_death-attribution.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [攻击周期规划与 Commit](../../requirements/basic-attacks/REQ-FEAT-037_attack-cycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [近战远程与攻击特效输出](../../requirements/basic-attacks/REQ-FEAT-038_attack-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Combat/DamageRequest.cs`：`DamageRequest`。
- `Assets/Scripts/Gameplay/Attack/AttackSnapshot.cs`：`AttackSnapshot`、`AttackAnimationSnapshot`、`HitReactionAnimationInfo`。
- `Assets/Scripts/Gameplay/Attack/AttackHandler.cs`：`AttackPlanStatus`、`AttackTimerResetReason`、`AttackHandler`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Combat/CombatRequestHeader.cs`：`CombatSourceType`、`SourceDescriptor`、`CombatBuiltinSourceId`、`CombatBuiltinRecipeId`、`CombatRequestHeader`。
- `Assets/Scripts/Gameplay/Projectile/ProjectileWorld.cs`：`PendingSpawnEntry`、`ProjectileWorld`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Combat/DamageRequest.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public struct DamageRequest
    {
        public CombatRequestHeader Header;

        public UnitUid SourceUnitUid
        {
            get => Header.SourceUnitUid;
            set => Header.SourceUnitUid = value;
        }

        public UnitUid TargetUnitUid
        {
            get => Header.TargetUnitUid;
            set => Header.TargetUnitUid = value;
        }

        public DamageType DamageType;
        public fp BaseDamage;
        public ProjectileUid? ProjectileSourceUid;

        public bool IsValid =>
            SourceUnitUid.IsValid() &&
            TargetUnitUid.IsValid() &&
            Header.SourceDescriptor.IsValid &&
            Header.RecipeId > 0 &&
            BaseDamage > fp.zero;

        public static readonly DamageRequest None = default;
    }
}
```

`Assets/Scripts/Gameplay/Attack/AttackSnapshot.cs`：

```csharp
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Cross-Tick attack state captured at Tick end for snapshot/rollback
    /// (Attack v6.2 section 2.3; Snapshot Appendix v7.2).
    /// </summary>
    public struct AttackSnapshot
    {
        /// <summary>Target identity, or default when idle.</summary>
        public UnitUid CurrentTargetUid;

        /// <summary>Logic Tick when the current attack cycle started.</summary>
        public int AttackStartLogicTick;

        /// <summary>Logic Tick when Impact (damage/projectile) fires.</summary>
        public int ImpactLogicTick;

        /// <summary>Logic Tick when the next attack may begin.</summary>
        public int NextAttackReadyLogicTick;

        /// <summary>True if Impact has already fired this cycle.</summary>
        public bool ImpactCommitted;

        public bool IsEmpoweredAttack;

        /// <summary>
        /// Monotonically increasing attack animation sequence index
        /// (Attack v6.2 section 2.3, adjust #14).
        /// </summary>
        public byte AttackSequenceIndex;

        public int LastSuccessfulAttackLogicTick;

        public int ResolvedAttackDurationTicks;

        public int ResolvedWindupTicks;

        /// <summary>Tower ramp state (TowerAttackHandler): target whose hero
        /// hits are being ramped, hits so far, in-flight projectile and the
        /// projectile-locked target. Zero/default when not a tower.</summary>
        public UnitUid RampTargetUnitUid;
        public int RampHitCount;
        public ProjectileUid PendingProjectileUid;
        public UnitUid LockedTargetUnitUid;

        public static readonly AttackSnapshot Default = new AttackSnapshot
        {
            CurrentTargetUid = default,
            AttackStartLogicTick = -1,
            ImpactLogicTick = -1,
            NextAttackReadyLogicTick = 0,
            ImpactCommitted = false,
            IsEmpoweredAttack = false,
            AttackSequenceIndex = 0,
            LastSuccessfulAttackLogicTick = -1,
            ResolvedAttackDurationTicks = 0,
            ResolvedWindupTicks = 0,
        };
    }

    /// <summary>
    /// Animation-facing snapshot of the current attack state for presentation.
    /// Computed during presentation Update by reading deterministic
    /// AttackHandler state.
    /// </summary>
    public struct AttackAnimationSnapshot
    {
        public bool IsAttacking;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**战斗请求封存与因果波次**

收集强类型 Shield、Damage、Heal 请求；封存成因果波次后才分配最终 SequenceInTick；同目标基于批次开始时冻结状态结算。

同批治疗封顶、盾参与吸收、总生命伤害一次提交；由结果产生的新反应进下一波；死亡反应产生的普通请求进下一 Tick。

**伤害配方抗性与吸血**

DamageRecipe/FormulaTerm 生成伤害，固定槽位 Modifier 合并后进入暴击、抗性、盾、生命、偷取及反应；超额伤害按定点权重分摊 ActualLifeDamage。

免疫、零伤害、纯盾伤害和纯过量不计击杀优势；非法请求仍报错；结构拒绝政策合法拒绝是成功空操作。

**濒死批次与公平击杀归属**

Combat 同步请求 UnitWorld 更新 Dying/Dead；致死批次按有效敌方英雄 ActualLifeDamage 总和取最大，纯中性分数处理最高伤害并列。

旧末次伤害者方案已被修订；队伍、Prefab、提交序列不决定平局；FormalDeathResult 唯一输出给统计和奖励。

**死亡奖励与贡献窗口**

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/AttackHandlerTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FormalDeathInvalidation_AtomicallyClearsWindupAndMainRuntime()
        {
            ActionSubmitResult started = attacker.Arbiter.Submit(
                new AttackActionRequest(target.UnitUid));
            Assert.That(started.IsGranted, Is.True);
            Assert.That(attacker.AttackHandler.CurrentTargetUid,
                Is.EqualTo(target.UnitUid));
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.True);
            Assert.That(attacker.ActionRuntimes.Main.Kind,
                Is.EqualTo(ActionKind.Attack));

            world.RequestEnterDying(target);
            world.ConfirmUnitDeath(target);
            world.ApplyFormalDeathActionInvalidations(new[]
            {
                new DeathResult
                {
                    VictimUid = target.UnitUid,
                    DeathSequenceInTick = 0,
                    DeathLogicTick = 10,
                },
            });

            Assert.That(attacker.AttackHandler.CurrentTargetUid.IsValid(),
                Is.False);
            Assert.That(attacker.ActionRuntimes.Main.IsOccupied, Is.False);

            var attackSnapshot = default(AttackSnapshot);
            var runtimeSnapshot = default(ActionRuntimeSetSnapshot);
            attacker.AttackHandler.Capture(ref attackSnapshot);
            attacker.ActionRuntimes.Capture(ref runtimeSnapshot);
            attacker.AttackHandler.Restore(attackSnapshot);
            attacker.ActionRuntimes.Restore(runtimeSnapshot);

            Assert.DoesNotThrow(() =>
            {
                attacker.AttackHandler.Resolve(new RollbackContext(
                    10,
                    ExecutionMode.ClientReplay));
                attacker.ActionRuntimes.Resolve();
            });
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
