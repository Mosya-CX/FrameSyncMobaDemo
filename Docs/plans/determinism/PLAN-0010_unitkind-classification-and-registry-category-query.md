# 单位分类与子分类查询

## 本次执行范围

本计划对应原编码 0010 的一次执行：单位分类与子分类查询。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [稳定 UID 与参与者身份](../../requirements/determinism/REQ-FEAT-015_stable-unit-identity.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [技能信号与会话状态](../../requirements/abilities/REQ-FEAT-042_ability-signal-session.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：`UnitKind`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitRegistry.cs`：`UnitRegistry`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Prototype/UnitPrototype.cs`：`UnitPrototype`。
- `Assets/Scripts/Gameplay/Unit/Team/TeamId.cs`：`TeamId`。
- `Assets/Scripts/Gameplay/Ability/AbilitySignal.cs`：`AbilitySignal`、`AbilitySignalVerb`、`AimKind`、`AimSnapshot`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：

```csharp
namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stable broad Unit classification frozen by Unit v27.3 section 1.4.
    /// Used for wide queries such as Hero / Minion / Monster / Structure.
    /// Sub-classification within a kind is expressed by <see cref="Unit.UnitSubKindId"/>.
    /// </summary>
    public enum UnitKind : byte
    {
        Hero = 0,
        Minion = 1,
        Monster = 2,
        Structure = 3,
    }
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

**稳定 UID 与参与者身份**

UnitUid、ProjectileUid 使用生成 Tick 与归属系统序列；GameplayParticipantId 从稳定出生来源建立，OriginActionId 由参与者、来源类型/ID、逻辑 Tick 和本地动作序列组成。

参与者缺失或重复可见失败；不得用 PrefabId、对象注册顺序、实例 ID 或队伍侧生成中性随机身份。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

**同步生成死亡复活与回池**

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

**技能信号与会话状态**

AbilityHandler 接受 AbilitySignal，AbilityRuntime 常驻，AbilitySession 只承载本次施法；Session 结束回传执行器，外部只读 AbilityCastView。

HandleSignal 返回是否接受，不让 Planner 私自推进 Session；同 Tick Focus 和 Commit 需正式 CommandSeq 顺序。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/UnitActiveGameplayGateTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `TickEqualToSpawnLogicTick_IsInactive`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void TickEqualToSpawnLogicTick_IsInactive(ExecutionMode executionMode)
        {
            var unit = UnitTestFactory.CreateUnit(new UnitUid(
                spawnLogicTick: 1000,
                runtimeEntityPrefabId: 1,
                spawnSequenceInTick: 0), UnitKind.Hero, 0, TeamId.Neutral);
            controller.BeginTick(1000, executionMode);

            Assert.That(unit.CanRunActiveGameplayThisTick, Is.False);
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitKindQueryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `GetUnitsByKind_ReturnsOnlyMatchingKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GetUnitsByKind_ReturnsOnlyMatchingKind()
        {
            var world = new UnitWorld();
            var hero = UnitTestFactory.CreateUnit(new UnitUid(10, 1, 0), UnitKind.Hero, 1, TeamId.Neutral);
            var minion = UnitTestFactory.CreateUnit(new UnitUid(11, 2, 0), UnitKind.Minion, 2, TeamId.Neutral);
            var monster = UnitTestFactory.CreateUnit(new UnitUid(12, 3, 0), UnitKind.Monster, 3, TeamId.Neutral);
            world.RegisterUnit(hero);
            world.RegisterUnit(minion);
            world.RegisterUnit(monster);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);

            Assert.That(heroes.Count, Is.EqualTo(1));
            Assert.That(heroes[0], Is.SameAs(hero));
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitRegistryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `DuplicateUid_IsRejectedWithoutChangingRegisteredRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void DuplicateUid_IsRejectedWithoutChangingRegisteredRuntime()
        {
            var registry = new UnitRegistry();
            var unitUid = new UnitUid(100, 7, 2);
            var registered = UnitTestFactory.CreateUnit(unitUid, UnitKind.Hero, 1, TeamId.Neutral);
            var duplicate = UnitTestFactory.CreateUnit(unitUid, UnitKind.Hero, 1, TeamId.Neutral);
            registry.Register(registered);

            Assert.Throws<InvalidOperationException>(() => registry.Register(duplicate));

            Assert.That(registry.GetAll().Count, Is.EqualTo(1));
            Assert.That(registry.GetAll()[0], Is.SameAs(registered));
            Assert.That(registry.TryGet(unitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(registered));
        }
```
- `Assets/Scripts/Gameplay/Tests/UnitWorldTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `InternalRegistration_PublicLookupReturnsSameRuntime`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void InternalRegistration_PublicLookupReturnsSameRuntime()
        {
            var world = new UnitWorld();
            var unit = UnitTestFactory.CreateUnit(new UnitUid(300, 9, 1), UnitKind.Hero, 0, TeamId.Neutral);

            world.RegisterUnit(unit);

            Assert.That(world.TryGetUnit(unit.UnitUid, out Unit resolved), Is.True);
            Assert.That(resolved, Is.SameAs(unit));
            Assert.That(world.TryGetUnit(new UnitUid(300, 9, 2), out _), Is.False);
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
