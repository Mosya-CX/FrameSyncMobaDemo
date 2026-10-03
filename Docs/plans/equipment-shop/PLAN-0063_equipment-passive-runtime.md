# 装备被动运行时

## 本次执行范围

本计划对应原编码 0063 的一次执行：装备被动运行时。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [六格装备配方与唯一标签](../../requirements/equipment-shop/REQ-FEAT-053_equipment-recipes.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Equipment/BuffEquipmentModule.cs`：`BuffEquipmentModule`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Equipment/BuffEquipmentModule.cs`：

```csharp
        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            EquipmentInstance instance = context.Instance;
            if (IgnoreRepeatedOnHit &&
                context.Timing == EquipmentEffectInvokeTiming.OnHitDealt &&
                context.OnHit.IsRepeated)
                return;
            if (owner?.BuffHandler == null || !BuffConfigId.IsValid) return;
            if (owner.World?.BuffDefinitions == null) return;
            if (!owner.World.BuffDefinitions.TryGet(BuffConfigId, out BuffDefinition def)) return;

            owner.BuffHandler.Apply(
                BuffConfigId,
                def,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Item,
                    instance?.Definition?.Id ?? 0));
        }
```

`Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：

```csharp
        public override void ClearForDeath()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                var inst = _slots[i];
                if (inst == null) continue;
                ReleaseFixedStats(inst);
            }
        }
```

### 输入输出与边界

**六格装备配方与唯一标签**

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

满栏合成先有确定购买计划；成装重复和跨装备排他显式；固定属性使用本装备持有的句柄。

**装备主动被动与可重复命中**

EquipmentEffectDef 内嵌多态 Module，Runtime 持有 EffectUid/Module state；主动一次验证全部模块，再通过仲裁瞬发；On-Hit 重复保留源动作。

每装备最多一个主动 Effect；装备使用使撤销失效；EquipmentTargetPolicy 目前只存在概念提及，待用户确认是否采用及值域。

**购买合成与 Command 二次校验**

EquipmentShopRuntime 从装备数据库计算 EquipmentPurchasePlan；UI RequestCheck 是预检查，ProcessCommand 再以当前确定性状态验证，组件选择和剩余价格稳定。

不能从 UI 预检查直接扣款；懒创建 Trader 不形成第二金币权威；部分堆叠、重复标签和不足余额明确拒绝。

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
- `Assets/Scripts/Gameplay/Tests/GuinsoosRagebladeEquipmentTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `FormalCatalog_ContainsExpectedStatsRecipeAndModules`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void FormalCatalog_ContainsExpectedStatsRecipeAndModules()
        {
            EquipmentCatalogAsset catalog = LoadCatalog();
            EquipmentDatabase database = catalog.BakeOrThrow();

            Assert.That(database.Count, Is.EqualTo(11));
            Assert.That(database.TryGetDefinition(31004, out EquipmentDefinition recurve), Is.True);
            Assert.That(database.TryGetDefinition(31005, out EquipmentDefinition rageblade), Is.True);
            Assert.That(recurve.Value, Is.EqualTo(700));
            Assert.That(recurve.Recipe.Components.Length, Is.EqualTo(1));
            Assert.That(recurve.Recipe.Components[0].Item.Id, Is.EqualTo(31001));
            Assert.That(rageblade.Value, Is.EqualTo(3000));
            Assert.That(rageblade.Recipe.Components.Length, Is.EqualTo(3));
            Assert.That(rageblade.Recipe.Components[0].Item.Id, Is.EqualTo(31002));
            Assert.That(rageblade.Recipe.Components[1].Item.Id, Is.EqualTo(31004));
            Assert.That(rageblade.Recipe.Components[2].Item.Id, Is.EqualTo(31003));
            Assert.That(rageblade.BakedFixedStats.Length, Is.EqualTo(3));
            Assert.That(rageblade.Effects.Length, Is.EqualTo(2));
            Assert.That(rageblade.Effects[0].Modules[0], Is.TypeOf<OnHitBonusDamageModule>());
            Assert.That(rageblade.Effects[1].Modules[0], Is.TypeOf<BuffEquipmentModule>());
            Assert.That(rageblade.Effects[1].Modules[1], Is.TypeOf<OnHitRepeatModule>());

            BuffDefinition buff = LoadSeethingBuff();
            Assert.That(buff.DurationTicks, Is.EqualTo(90));
            Assert.That(buff.MaxStacks, Is.EqualTo(4));
            Assert.That(buff.GetEffects()[0], Is.TypeOf<StatModifierBuffEffect>());
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
