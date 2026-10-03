# 属性 Handler 基础

## 本次执行范围

本计划对应原编码 0018 的一次执行：属性 Handler 基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [属性公式与 Modifier 所有权](../../requirements/unit-stats/REQ-FEAT-023_stat-formulas.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [生命资源护盾与自然恢复](../../requirements/unit-stats/REQ-FEAT-024_health-shields-regeneration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

StatHandler 采用正式 Base、Add、Ratio、Final 等槽位和锁定运算顺序；每个来源保有自己的 StatModifierHandle，StatSeq 提供稳定排序。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Stats/StatHandler.cs`：`StatHandler`、`StatConfig`、`ExperienceGainResult`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Stats/StatDefinition.cs`：`StatDefinition`。
- `Assets/Scripts/Gameplay/Stats/StatId.cs`：`StatId`。
- `Assets/Scripts/Gameplay/Stats/StatModifier.cs`：`StatModifier`。
- `Assets/Scripts/Gameplay/Stats/StatPreset.cs`：`StatPreset`。
- `Assets/Scripts/Gameplay/Stats/StatRuntimeEntry.cs`：`StatRuntimeEntry`。
- `Assets/Scripts/Deterministic/Core/DeterministicSimulationException.cs`：`DeterministicSimulationException`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Stats/StatHandler.cs`：

```csharp
        public StatModifierHandle AddModifier(
            StatId statId,
            StatModifierOperation operation,
            fp value)
        {
            if (!configs.ContainsKey(statId))
            {
                if (!definitionTable.TryGet(statId, out StatDefinition def))
                {
                    throw new ArgumentException(
                        $"StatId {statId} is not a valid stat.", nameof(statId));
                }

                var config = new StatConfig
                {
                    BaseValue = def.DefaultBaseValue,
                    GrowthValue = default,
                    Definition = def,
                };
                configs[statId] = config;
                entries[statId] = new StatRuntimeEntry { Dirty = true };
            }
            else if (!entries.ContainsKey(statId))
            {
                // Stale config carried across a rollback Restore: Restore
                // rebuilds entries from the snapshot but intentionally keeps
                // configs (they also hold preset base values). A runtime-added
                // stat (e.g. an ability-passive modifier like Omnivamp) that
                // was created live before the rollback anchor can therefore
                // exist in configs while its entry is absent after restore.
                // Recreate the entry so the modifier can attach during replay.
                entries[statId] = new StatRuntimeEntry { Dirty = true };
            }

            StatRuntimeEntry entry = entries[statId];
            uint seq = nextStatSeq;

            if (nextStatSeq == uint.MaxValue)
            {
                throw new DeterministicSimulationException(
                    "StatSeq overflow: maximum sequence reached.");
            }
            nextStatSeq++;

            entry.Modifiers.Add(new StatModifier
            {
                StatSeq = seq,
                Operation = operation,
                Value = value,
            });
            entry.Dirty = true;

            return new StatModifierHandle(ownerUid, statId, seq);
        }
```

`Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class Unit : MonoBehaviour, IUnitCollisionParticipant
    {
        [Header("Deterministic composition")]
        [Tooltip("Authoritative 2D physics component owned by this Unit prefab.")]
        [SerializeField] private PhysicsEntity2D physicsEntity;
        [SerializeField] private StatHandler statHandler;
        [SerializeField] private MovementHandler movementHandler;
        [SerializeField] private AttackHandler attackHandler;
        [SerializeField] private AbilityHandler abilityHandler;
        [SerializeField] private BuffHandler buffHandler;
        [SerializeField] private CrowdControlHandler crowdControlHandler;
        [SerializeField] private EquipmentHandler equipmentHandler;

        private CapabilityState capabilityState;
        private UnitAbilityMask abilityMask;
        private readonly List<UnitTag> tags =
            new List<UnitTag>();

        /// <summary>Deterministic runtime identity (SpawnLogicTick /
        /// prefab id / spawn sequence). Displayed in the Inspector for
        /// debugging spawned unit instances.</summary>
        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-120)]
        public UnitUid UnitUid { get; private set; }
        public GameplayParticipantId GameplayParticipantId { get; private set; }
        public UnitWorld World { get; internal set; }
        public UnitUid OwnerUid { get; private set; }
        public UnitKind UnitKind { get; private set; }
        public ushort UnitSubKindId { get; private set; }
        public TeamId TeamId { get; private set; }
        public int UnitPrototypeId { get; private set; }
        public int BaseGoldValue { get; private set; }
        public int BaseExperienceValue { get; private set; }
        public int BaseCreepScoreValue { get; private set; }
        public LifeState LifeState { get; private set; }
        public ref readonly CapabilityState CapabilityState => ref capabilityState;
        public UnitAbilityMask AbilityMask => abilityMask;

        public PhysicsEntity2D PhysicsEntity => physicsEntity;
        public StatHandler StatHandler => statHandler;
        public CombatModifierSet CombatModifiers { get; private set; }
        public MovementHandler MovementHandler => movementHandler;
        public AttackHandler AttackHandler => attackHandler;
        public AbilityHandler AbilityHandler => abilityHandler;
        public BuffHandler BuffHandler => buffHandler;
        public CrowdControlHandler CrowdControl => crowdControlHandler;
        public EquipmentHandler EquipmentHandler => equipmentHandler;
        public UnitEventBus EventBus { get; private set; }

        public UnitIntent Intent { get => Planner?.CurrentIntent ?? UnitIntent.None; internal set => Planner?.SetIntent(value); }
        public BehaviorPlanner Planner { get; private set; }
        public ActionArbiter Arbiter { get; private set; }
        public ActionRuntimeSet ActionRuntimes { get; private set; }

        public UnitLocomotionAgent Locomotion { get; internal set; }
        public int Level => statHandler?.Level ?? 1;
        /// <summary>
        /// The deterministic home spawn position captured when this runtime
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**属性公式与 Modifier 所有权**

StatHandler 采用正式 Base、Add、Ratio、Final 等槽位和锁定运算顺序；每个来源保有自己的 StatModifierHandle，StatSeq 提供稳定排序。

死亡不全局清空所有 Modifier；静态配置不含运行 Handle；同值操作不意外改变稳定身份。

**生命资源护盾与自然恢复**

StatHandler 管理当前状态、最大值和护盾实例；Combat 的 Regen、Heal、Shield 管线读取正式数值与修正。

恢复状态后失效 Handle 不能静默丢弃；护盾吸收顺序固定；PendingDying 时治疗和护盾按所属管线合同处理。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/StatHandlerCalculationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `GetStat_NoModifiers_ReturnsLevelBaseValue`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GetStat_NoModifiers_ReturnsLevelBaseValue()
        {
            StatHandler h = CreateHandler(level: 1, growthC: 0.5m);
            Assert.AreEqual((fp)100m, h.GetStat(StatId.AttackDamage));
        }
```
- `Assets/Scripts/Gameplay/Tests/StatHandlerChangeTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `GetChangeThisTick_BeforeFinalize_NoChange`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GetChangeThisTick_BeforeFinalize_NoChange()
        {
            StatHandler h = CreateHandler();
            // Initial entry is Dirty, recompute on query.
            // PreviousLogicTickFinalValue is default (0), so Delta = FinalValue - 0.
            // But this is the first Tick — FinalizeTick hasn't been called yet.
            // For a clean test, call FinalizeTick first to establish baseline.
            h.FinalizeTick();

            StatChange change = h.GetChangeThisTick(StatId.AttackDamage);
            Assert.IsFalse(change.Changed);
            Assert.AreEqual(default(fp), change.Delta);
        }
```
- `Assets/Scripts/Gameplay/Tests/StatHandlerModifierTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `AddModifier_ReturnsValidHandle_WithCorrectStatSeq`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AddModifier_ReturnsValidHandle_WithCorrectStatSeq()
        {
            StatModifierHandle h1 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);
            StatModifierHandle h2 = handler.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)20m);

            Assert.AreEqual(1u, h1.StatSeq);
            Assert.AreEqual(2u, h2.StatSeq);
            Assert.AreEqual(StatId.AttackDamage, h1.StatId);
            Assert.IsTrue(h1.IsValid);
        }
```
- `Assets/Scripts/Gameplay/Tests/StatHandlerSeqTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `StatSeq_StartsAt1`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void StatSeq_StartsAt1()
        {
            StatHandler h = CreateHandler();
            StatModifierHandle handle = h.AddModifier(StatId.AttackDamage, StatModifierOperation.FlatAdd, (fp)10m);

            Assert.AreEqual(1u, handle.StatSeq);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
