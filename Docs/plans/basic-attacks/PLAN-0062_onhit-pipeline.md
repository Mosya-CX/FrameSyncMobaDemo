# 攻击特效管线补全

## 本次执行范围

本计划对应原编码 0062 的一次执行：攻击特效管线补全。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [近战远程与攻击特效输出](../../requirements/basic-attacks/REQ-FEAT-038_attack-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

默认普通攻击采用固定来源和配方；远程输出 ProjectileSpawnRequest；AttackSequenceIndex 为确定性 byte，音效通过独立 SfxEvent。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。
- `Assets/Scripts/Gameplay/Buff/BuffEffect.cs`：`BuffEffect`、`StatModifierBuffEffect`、`CombatModifierBuffEffect`。
- `Assets/Scripts/Gameplay/Combat/CombatEvents.cs`：`CombatEvents`、`DamageEventData`、`HealEventData`、`ShieldEventData`、`OnHitEventData`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentEffectDispatch.cs`：`EquipmentEffectDispatch`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitEventBus.cs`：`UnitEventBus`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentEffect.cs`：`EquipmentEffectDef`、`EquipmentActiveSettings`、`EquipmentEffectInvokeTiming`、`EquipmentEffectModule`、`IEmpoweredAttackProvider`、`EquipmentEffectExecutionContext`、`EquipmentEffectRuntime`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：

```csharp
        public void OnHitDealt(OnHitEventData data) => DispatchOnHitReaction(data);
        public void OnDamageDealt(DamageEventData data) =>
            DispatchReaction(BuffReactionKind.DamageDealt, data, default, default, null);
        public void OnHealTaken(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealTaken, default, data, default, null);
        public void OnHealDealt(HealEventData data) =>
            DispatchReaction(BuffReactionKind.HealDealt, default, data, default, null);
        public void OnShieldApplied(ShieldEventData data) =>
            DispatchReaction(BuffReactionKind.ShieldApplied, default, default, data, null);
        public void OnAbilityCast(in AbilityCastEventData data)
        {
            IReadOnlyList<BuffRuntime> runtimes =
                _store.GetAllOrdered();
            for (int i = 0; i < runtimes.Count; i++)
            {
                BuffRuntime runtime = runtimes[i];
                BuffEffect[] effects =
                    runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnAbilityCast(
                        runtime,
                        _owner,
                        data);
                RunEventGroups(
                    runtime.Definition
                        .EventReactions?.AbilityCast,
                    runtime);
            }
        }
```

`Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：

```csharp
        public void OnHitDealt(in OnHitEventData data) => _effectDispatch?.OnHitDealt(data);

        /// <summary>
        /// Resolves an empowered-strike damage recipe that is currently ready
        /// for <paramref name="target"/> (e.g. an equipped Sundered Sky whose
        /// per-target cooldown has expired). Returns false when the attack
        /// should keep the normal basic-attack recipe.
        /// </summary>
        public bool TryResolveEmpoweredAttackRecipe(
            Unit target,
            out int recipeId)
        {
            if (_effectDispatch != null)
            {
                return _effectDispatch
                    .TryResolveEmpoweredAttackRecipe(
                        target,
                        out recipeId);
            }
            recipeId = 0;
            return false;
        }
```

### 输入输出与边界

**近战远程与攻击特效输出**

默认普通攻击采用固定来源和配方；远程输出 ProjectileSpawnRequest；AttackSequenceIndex 为确定性 byte，音效通过独立 SfxEvent。

在途飞弹目标锁定不跟随攻击者换目标；强化攻击、On-Hit 重复必须保留来源与动作身份，防止递归二次触发。

**装备主动被动与可重复命中**

EquipmentEffectDef 内嵌多态 Module，Runtime 持有 EffectUid/Module state；主动一次验证全部模块，再通过仲裁瞬发；On-Hit 重复保留源动作。

每装备最多一个主动 Effect；装备使用使撤销失效；EquipmentTargetPolicy 目前只存在概念提及，待用户确认是否采用及值域。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/BuffEffectLibraryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `PeriodicDamage_DealsDamageEachInterval`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void PeriodicDamage_DealsDamageEachInterval()
        {
            BeginTick(1);
            Unit target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHp = target.StatHandler.CurrentHealth;

            var effect = new PeriodicDamageBuffEffect
            {
                DamagePerTick = 10m,
                DamageType = DamageType.Physical,
            };
            var def = CreateBuffDef(1001, 2, 30, effects: new BuffEffect[] { effect });
            target.BuffHandler.DefinitionRegistry = _buffDefs;
            _buffDefs.Register(def);
            target.BuffHandler.Apply(def.ConfigId, def, target.UnitUid);

            for (int tick = 2; tick <= 5; tick++)
            {
                _combat.BeginTick();
                target.BuffHandler.Advance();
                _combat.SettleActiveRequests();
                _combat.EndTick();
            }

            fp afterHp = target.StatHandler.CurrentHealth;
            Assert.Less(afterHp, initialHp, "Should have taken periodic damage");
        }
```
- `Assets/Scripts/Gameplay/Tests/CombatSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SubmitDamage_ValidRequest_ReducesHealth`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SubmitDamage_ValidRequest_ReducesHealth()
        {
            BeginTick(1);
            var attacker = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            var target = _world.SpawnUnit(_prototype, TeamId.Neutral, 1, 0m, 0m);
            fp initialHealth = target.StatHandler.CurrentHealth;

            _combat.BeginTick();
            _combat.SubmitDamage(UnitTestFactory.CreateDamageRequest(
                attacker.UnitUid, target.UnitUid, (fp)100));
            _combat.SettleActiveRequests();
            _combat.EndTick();

            fp finalHealth = target.StatHandler.CurrentHealth;
            Assert.Less(finalHealth, initialHealth);
            Assert.Greater(finalHealth, fp.zero);
        }
```
- `Assets/Scripts/Gameplay/Tests/CorruptionVineSpreadTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `Spread_InfectsOnlyEnemyHeroesOfOriginalCaster`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Unit.Tests
{
    /// <summary>
    /// Verifies the Corruption Vines (R) spread semantics:
    /// - spread only infects heroes whose team differs from the original
    ///   R caster (never the caster's own camp, never the caster itself);
    /// - a hero that is already infected (vine or permanent marker) is never
    ///   infected a second time;
    /// - Blight stacks are applied at the configured ticks.
    /// </summary>
    [TestFixture]
    public sealed class CorruptionVineSpreadTests
    {
        private const int BlightConfigId = 9001;
        private const int VineConfigId = 9113;
        private const int TimerConfigId = 9115;
        private const string SpreadTagKey =
            "VarusR.Vine";

        private SimulationTickContextController controller;
        private UnitWorld world;
        private PhysicsWorld physicsWorld;
        private BuffDefinitionRegistry buffDefs;
        private CombatSystem combat;
        private BuffDefinition vineDef;
        private UnitType caster;
        private UnitType ally;
        private UnitType enemyA;
        private UnitType enemyB;
        private UnitType farEnemy;
        private int nextTick = 30;

        [SetUp]
        public void SetUp()
        {
            world = new UnitWorld();
            physicsWorld = new PhysicsWorld
            {
                Settings = new PhysicsWorldSettings
                {
                    GridCellSize = (fp)10m,
                },
            };
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
