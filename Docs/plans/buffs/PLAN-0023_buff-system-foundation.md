# Buff 系统基础

## 本次执行范围

本计划对应原编码 0023 的一次执行：Buff 系统基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [Buff 施加覆写与运行身份](../../requirements/buffs/REQ-FEAT-048_buff-application.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [Buff 反应与句柄生命周期](../../requirements/buffs/REQ-FEAT-049_buff-reaction-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [Buff 上限优先级与驱逐](../../requirements/buffs/REQ-FEAT-050_buff-capacity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

BuffHandler 保存按 ConfigId 稳定的 BuffRuntime，重复 Apply 覆写、更新 stack/duration；BuffSource 与黑板只承载正式数据。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Gameplay/Buff/BuffBlackboard.cs`：`BuffBlackboard`、`BuffValueSnapshot`、`BuffBlackboardSnapshot`。
- `Assets/Scripts/Gameplay/Buff/BuffConfigId.cs`：`BuffConfigId`。
- `Assets/Scripts/Gameplay/Buff/BuffEffect.cs`：`BuffEffect`、`StatModifierBuffEffect`、`CombatModifierBuffEffect`。
- `Assets/Scripts/Gameplay/Buff/BuffLifeRule.cs`：`BuffLifeRule`。
- `Assets/Scripts/Gameplay/Buff/BuffRuntime.cs`：`BuffRuntime`。
- `Assets/Scripts/Gameplay/Buff/BuffSnapshot.cs`：`BuffRuntimeSnapshot`、`BuffHandlerSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
        internal void ClearForDeath()
        {
            // D-009: StatHandler and CombatModifiers survive ordinary death.
            // Stat modifiers survive, while StatHandler-owned shields do not.
            statHandler.ClearForDeath();
            movementHandler?.ClearForDeath();
            attackHandler?.ClearForDeath();
            abilityHandler?.ClearForDeath();
            buffHandler.ClearForDeath();
            crowdControlHandler?.ClearForDeath();
            equipmentHandler?.ClearForDeath();
            ClearTags();
            Locomotion?.CancelRoute(MoveCancelReason.Death);
            Planner?.ClearForDeath();
            ActionRuntimes?.ClearWithoutCancel();
            Intent = UnitIntent.None;
        }
```

`Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：

```csharp
        public void Advance()
        {
            int deltaTicks = SimulationTickContext.Current.DeltaTick;
            var ordered = _store.GetAllOrdered();

            for (int i = 0; i < ordered.Count; i++)
            {
                var runtime = ordered[i];
                if (runtime.IsRemoving) continue;
                runtime.Tick(deltaTicks);
                var effects = runtime.GetEffects();
                for (int j = 0; j < effects.Length; j++)
                    effects[j].OnTick(runtime, _owner);
                RunPeriodicReactions(runtime);
                if (runtime.IsExpired())
                    _removalPending.Add(runtime);
            }

            for (int i = 0; i < _removalPending.Count; i++)
                ExecuteRemoval(_removalPending[i], RemovalReason.DurationExpired);
            _removalPending.Clear();
        }
```

### 输入输出与边界

**Buff 施加覆写与运行身份**

BuffHandler 保存按 ConfigId 稳定的 BuffRuntime，重复 Apply 覆写、更新 stack/duration；BuffSource 与黑板只承载正式数据。

不新增平行 Buff 身份；Added、Reapplied、StackChanged、Removed 顺序明确；结构外源 Buff 先拒绝再创建。

**Buff 反应与句柄生命周期**

静态 BuffEffect/ReactionConfig + Runtime Blackboard；创建者持有 StatModifierHandle/CombatModifierHandle，BuffHandler 不扫描黑板兜底释放。

RemovedComplete 在 store 移除后运行；死亡回调不自行清空 Buff；普通死亡保留永久 Runtime，复活重建本生命 Handle。

**Buff 上限优先级与驱逐**

MaxBuffs 为 byte 默认 255；Priority 0 最高。仅新 ConfigId 首次施加检查：最低优先级非永久为候选，同级按 ConfigId 稳定顺序末项；新 Priority<=候选才驱逐。

否则允许超过软上限；驱逐使用 ManualRemove 正常移除流程；重施已有 Buff 不重复驱逐。

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
- `Assets/Scripts/Gameplay/Tests/BlightDetonationAndOnHitPassiveTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `AbilityHit_DetonatesAndConsumesAllStacks`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AbilityHit_DetonatesAndConsumesAllStacks()
        {
            ApplyBlightTwice();
            combat.BeginTick();

            DealAbilityDamage((fp)50);
            combat.SettleActiveRequests();

            // 50 physical + 2 * (1000 * 3%) = 60 magic.
            Assert.AreEqual(
                (double)((fp)890),
                (double)target.StatHandler.CurrentHealth,
                0.01);
            Assert.IsFalse(
                target.BuffHandler.HasBuff(
                    new BuffConfigId(BlightConfigId)),
                "All blight stacks must be consumed.");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
