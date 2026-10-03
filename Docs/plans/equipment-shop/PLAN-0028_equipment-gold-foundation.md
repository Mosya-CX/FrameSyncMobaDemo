# 装备与金币基础

## 本次执行范围

本计划对应原编码 0028 的一次执行：装备与金币基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [金币批次确认与可用余额](../../requirements/equipment-shop/REQ-FEAT-057_gold-accounting.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [死亡奖励与贡献窗口](../../requirements/combat/REQ-FEAT-035_death-rewards.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [卖出撤销与交易失效](../../requirements/equipment-shop/REQ-FEAT-056_shop-undo.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [六格装备配方与唯一标签](../../requirements/equipment-shop/REQ-FEAT-053_equipment-recipes.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [装备主动被动与可重复命中](../../requirements/equipment-shop/REQ-FEAT-054_equipment-effects.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [购买合成与 Command 二次校验](../../requirements/equipment-shop/REQ-FEAT-055_shop-purchase-validation.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：`EquipmentHandler`、`EquipmentInstance`、`EquipmentUseCheckResult`、`EquipmentFixedStat`、`EquipmentTier`、`EquipmentCooldownGroupId`、`EquipmentSlotSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentShopRuntime.cs`：`EquipmentShopRuntime`、`ShopTraderRuntime`、`ShopOperationRecord`、`EquipmentShopOperationType`、`EquipmentShopFailureReason`、`CombatParticipationFlags`、`EquipmentPurchasePlan`。
- `Assets/Scripts/FrameSync/GoldIncomeRuntime.cs`：`GoldIncomeBatchDigest`、`GoldIncomeReason`、`GoldIncomeRecord`、`GoldIncomeRecordBatch`、`GoldIncomeSnapshot`、`GoldIncomeRuntime`、`BuildState`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentDefinition.cs`：`EquipmentDefinition`、`EquipmentFixedStatAuthoring`、`EquipmentRecipe`、`EquipmentRecipePart`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/Equipment/EquipmentEffect.cs`：`EquipmentEffectDef`、`EquipmentActiveSettings`、`EquipmentEffectInvokeTiming`、`EquipmentEffectModule`、`IEmpoweredAttackProvider`、`EquipmentEffectExecutionContext`、`EquipmentEffectRuntime`。

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

`Assets/Scripts/Gameplay/Equipment/EquipmentHandler.cs`：

```csharp
        public bool Add(EquipmentDefinition definition, int slot)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if ((uint)slot >= SlotCount) return false;
            if (_slots[slot] != null) return false;

            var instance = new EquipmentInstance
            {
                Definition = definition,
                StackCount = 1,
                ChargeCount = ResolveMaxCharge(definition),
            };

            if (!definition.IsBaked)
                throw new InvalidOperationException($"Equipment {definition.Id} must be baked before runtime use.");
            if (_owner.StatHandler != null && definition.BakedFixedStats != null)
            {
                var handles = new List<StatModifierHandle>();
                for (int i = 0; i < definition.BakedFixedStats.Length; i++)
                {
                    var fs = definition.BakedFixedStats[i];
                    var handle = _owner.StatHandler.AddModifier(fs.Stat, StatModifierOperation.FlatAdd, fs.Value);
                    handles.Add(handle);
                }
                instance._fixedStatHandles = handles.ToArray();

                // Items that raise Max Health also raise the current health
                // by the same amount (clamped to the new maximum).
                fp maxHealthDelta =
                    SumMaxHealthDelta(definition);
                if (maxHealthDelta != fp.zero)
                {
                    _owner.StatHandler.SetCurrentHealth(
                        _owner.StatHandler.CurrentHealth +
                        maxHealthDelta);
                }
            }

            _slots[slot] = instance;
            _runtimeRevision++;
            LogReplayMutation("Add", slot, instance);

            // Create EquipmentEffectRuntime instances
            if (definition.Effects != null && definition.Effects.Length > 0)
            {
                instance.EffectRuntimes = new EquipmentEffectRuntime[definition.Effects.Length];
                for (int i = 0; i < definition.Effects.Length; i++)
                {
                    instance.EffectRuntimes[i] = new EquipmentEffectRuntime(definition.Effects[i]);
                }
            }

            // Fire OnEquipped effect modules
            DispatchOnEquipped(instance);
            return true;
        }
```

### 输入输出与边界

**金币批次确认与可用余额**

GoldIncomeRuntime 拥有 builder、未确认批次、digest、confirmed earned total/progress。CurrentAvailableGold=GetConfirmedEarnedGoldTotal(player)+EffectiveShopGoldDelta，只读派生。

Account 不保存第二局内金币累计；派生余额不进 Snapshot；T 收入确认不主动回滚或补造本地已拒绝 Command；金币生产者待确认项单列。

**死亡奖励与贡献窗口**

DeathRewardContext 从正式死亡与贡献日志选收受者，整数稳定分配；经验立即结算，金币统一 RequestGoldIncome，批次摘要参与共享校验。

D-041 的生产者归属与既有复仇/击杀统计日志描述存在冲突，未确认部分不能静默改写；助攻窗口不受杀手修订而丢失。

**卖出撤销与交易失效**

OperationLog 与 UndoableOperationStack 维护 EffectiveShopGoldDelta；出售和撤销出售属于商店增量，不产生 GoldIncome 记录。

离开范围、参与战斗、使用装备等永久失效规则必须准确；金币确认不扫描后续 Purchase/Undo，也不创建金币专用脏 Tick。

**六格装备配方与唯一标签**

EquipmentHandler 保存 EquipmentInstance 和 EffectRuntime；装备本身不用第二个 UID，内部延迟变更通过正式 slot/instance 定位。Recipe 与唯一标签在配置校验。

满栏合成先有确定购买计划；成装重复和跨装备排他显式；固定属性使用本装备持有的句柄。

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
- `Assets/Scripts/FrameSync/Tests/GoldIncomeRuntimeContractTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void EmptyAndNonemptyTicks_SealDigestAndConfirmContinuously()
        {
            var runtime = new GoldIncomeRuntime();
            runtime.Initialize(2, 500);

            runtime.BeginTick(0);
            GoldIncomeRecordBatch empty = runtime.SealTick(0);
            Assert.AreNotEqual(0UL, empty.Digest.Value);
            Assert.AreEqual(0, empty.Records.Length);
            runtime.ConfirmAcceptedTick(0);

            runtime.BeginTick(1);
            runtime.RequestGoldIncome(1, 25, GoldIncomeReason.UnitKill);
            GoldIncomeRecordBatch income = runtime.SealTick(1);
            Assert.AreEqual(0, income.Records[0].IncomeSequenceInTick);
            Assert.Throws<FrameSyncMoba.Deterministic.DeterministicSimulationException>(
                () => runtime.ConfirmAcceptedTick(2));
            runtime.ConfirmAcceptedTick(1);
            Assert.AreEqual(525, runtime.GetConfirmedAvailableGold(1));
        }
```
- `Assets/Scripts/Gameplay/Tests/EquipmentShopRequestTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `RequestPurchase_Allowed_SubmitsCanonicalCommand`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RequestPurchase_Allowed_SubmitsCanonicalCommand()
        {
            EquipmentDefinition item =
                Definition(1, 50, EquipmentTier.Basic);
            RequestContext context =
                CreateContext(item, 100);

            EquipmentShopRequestCheck check =
                context.Shop.RequestPurchase(
                    0,
                    item.Id);

            Assert.That(check.Allowed, Is.True);
            Assert.That(
                context.Submitter.Purchases,
                Is.EqualTo(new[] { item.Id }));
            Assert.That(
                context.Submitter.TotalCalls,
                Is.EqualTo(1));
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
