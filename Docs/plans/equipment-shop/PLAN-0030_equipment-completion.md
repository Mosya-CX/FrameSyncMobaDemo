# 装备交易与效果分发

## 本次执行范围

本计划对应原编码 0030 的一次执行：装备交易与效果分发。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [六格装备配方与唯一标签](../../requirements/equipment-shop/REQ-FEAT-053_equipment-recipes.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：`EquipmentShopRuntime`、`ShopTraderRuntime`、`ShopOperationRecord`、`EquipmentShopOperationType`、`EquipmentShopFailureReason`、`CombatParticipationFlags`、`EquipmentPurchasePlan`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentEffectDispatch.cs`：`EquipmentEffectDispatch`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentDatabase.cs`：`EquipmentDatabase`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Combat/CombatEvents.cs`：`CombatEvents`、`DamageEventData`、`HealEventData`、`ShieldEventData`、`OnHitEventData`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：

```csharp
        public bool TryBuildPurchasePlan(
            int playerSlot, int targetEquipmentId, int currentAvailableGold,
            EquipmentHandler handler, out EquipmentPurchasePlan plan, out EquipmentShopFailureReason failure)
        {
            plan = default;
            failure = EquipmentShopFailureReason.None;

            if (_database == null) { failure = EquipmentShopFailureReason.ItemNotFound; return false; }
            var targetDef = _database.GetDefinition(targetEquipmentId);
            if (targetDef == null) { failure = EquipmentShopFailureReason.ItemNotFound; return false; }

            if (handler == null)
            {
                failure = EquipmentShopFailureReason.ControlledUnitNotFound;
                return false;
            }
            int purchaseCost;
            int[] consumedSlots;
            try
            {
                purchaseCost = SelectRecipeComponents(
                    targetDef,
                    handler,
                    out consumedSlots);
            }
            catch (DeterministicSimulationException)
            {
                failure = EquipmentShopFailureReason.InvalidRecipe;
                return false;
            }

            var before =
                new EquipmentTransactionSlotState[EquipmentHandler.SlotCount];
            var after =
                new EquipmentTransactionSlotState[EquipmentHandler.SlotCount];
            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
            {
                before[slot] = handler.CaptureTransactionSlot(slot);
                after[slot] = CloneSlotState(before[slot]);
            }
            for (int i = 0; i < consumedSlots.Length; i++)
                after[consumedSlots[i]] =
                    EquipmentTransactionSlotState.Empty;

            int destSlot;
            bool mergeIntoExisting = false;
            destSlot = FindStackableSlot(after, targetDef);
            if (destSlot >= 0)
            {
                mergeIntoExisting = true;
                after[destSlot].StackCount++;
            }
            else
            {
                destSlot = -1;
                for (int s = 0; s < EquipmentHandler.SlotCount; s++)
                {
                    if (!after[s].Occupied)
                    { destSlot = s; break; }
                }
                if (destSlot < 0) { failure = EquipmentShopFailureReason.InventoryFull; return false; }
                after[destSlot] =
                    EquipmentHandler.CreateInitialTransactionSlot(
                        targetDef);
            }

            if (!ValidatePostPurchaseState(
                    after,
                    targetDef,
                    out failure))
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：

```csharp
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class CombatSystem : IRollback<CombatSnapshot>
    {
        public MatchEventTracker MatchEventTracker { get; set; }
        private readonly UnitWorld _unitWorld;
        private CombatSnapshot _snapshot;

        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public RespawnTimer RespawnTimer { get; set; }
        public int HeroRespawnBaseTicks { get; }
        public int HeroRespawnPerMinuteTicks { get; }

        private readonly List<ShieldRequest> _shieldQueue = new List<ShieldRequest>();
        private readonly List<DamageRequest> _damageQueue = new List<DamageRequest>();
        private readonly List<HealRequest> _healQueue = new List<HealRequest>();
        private readonly List<DeferredCombatRequest> _deferredBuffer = new List<DeferredCombatRequest>();
        private ushort _nextDeferredSeq;
        private bool _deferredSeqExhausted;
        private ushort _nextSequenceInTick;
        private bool _sequenceExhausted;
        private int _currentSequenceLogicTick = -1;
        private readonly Dictionary<UnitUid, CombatContributionEventLog> _eventLogs = new Dictionary<UnitUid, CombatContributionEventLog>();
        private readonly List<UnitUid> _eventLogVictimScratch = new List<UnitUid>();
        private readonly List<UnitUid> _pendingDying = new List<UnitUid>();
        private readonly List<DeathResult> _deathResults = new List<DeathResult>();
        private readonly List<EvaluatedDamage> _damageBatchScratch =
            new List<EvaluatedDamage>();
        private readonly List<EvaluatedHeal> _healBatchScratch =
            new List<EvaluatedHeal>();
        private readonly List<HeroDamageContribution> _heroDamageScratch =
            new List<HeroDamageContribution>();
        private readonly List<ShieldResultEmission> _shieldEmissionScratch =
            new List<ShieldResultEmission>();
        private readonly List<HealResultEmission> _healEmissionScratch =
            new List<HealResultEmission>();
        private readonly List<DamageResultEmission> _damageEmissionScratch =
            new List<DamageResultEmission>();
        private readonly List<UnitUid> _dyingEmissionScratch =
            new List<UnitUid>();
        private readonly Dictionary<UnitUid, UnitUid> _lethalBatchKillers =
            new Dictionary<UnitUid, UnitUid>();
        private readonly Dictionary<UnitUid, fp> _waveStartHealth =
            new Dictionary<UnitUid, fp>();
        private ushort _nextDeathSeq;
        private bool _deathSeqExhausted;
        private const int MaxSettlementWavesPerTick = 256;
        private const ulong KillerTieDomain = 0x434F4D4241544B49UL;
        private uint _initialMatchSeed;
        private bool _hasConfiguredInitialMatchSeed;
        private bool _isCombatTickActive;

        public int ShieldProcessed { get; private set; }
        public int DamageProcessed { get; private set; }
        public int HealProcessed { get; private set; }
        public uint InitialMatchSeed => _initialMatchSeed;
        /// <summary>
        /// Wall-clock seconds over which the unit's HealthRegeneration /
        /// CastResourceRegeneration stats are fully restored (design v13.2
        /// 5: natural regen, LoL-style per-5s values). Configured from
        /// GlobalGameplayData; defaults to 5.
        /// </summary>
        public int NaturalRegenIntervalMilliseconds { get; set; } =
            5000;

// 方法后续请阅读上述真实源码；这里是节选。
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

- `Assets/Scripts/Gameplay/Tests/EquipmentShopRuntimeSnapshotTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `RestoreResolve_PreservesValidControlledUnitReference`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RestoreResolve_PreservesValidControlledUnitReference()
        {
            UnitWorld world = CreateWorldWithUnit(out Unit unit);
            EquipmentShopRuntime source = CreateRuntime(world);
            source.GetOrCreateTrader(0, unit.UnitUid);
            EquipmentShopRuntimeSnapshot snapshot = default;
            source.Capture(ref snapshot);

            EquipmentShopRuntime restored = CreateRuntime(world);
            restored.Restore(snapshot);
            restored.Resolve(default);

            Assert.AreEqual(
                unit.UnitUid,
                restored.GetTrader(0).ControlledUnitUid);
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
