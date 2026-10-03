# 单位稳定注册内核

## 本次执行范围

本计划对应原编码 0005 的一次执行：单位稳定注册内核。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitUid.cs`：`UnitUid`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitRegistry.cs`：`UnitRegistry`。
- `Assets/Scripts/Bootstrap/AssemblyInfo.cs`：。
- `Assets/Scripts/Deterministic/AssemblyInfo.cs`：。
- `Assets/Scripts/FrameSync/AssemblyInfo.cs`：。
- `Assets/Scripts/Gameplay/AssemblyInfo.cs`：。

以下代码为当前真实实现节选，完整方法以对应源码为准。

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

`Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：

```csharp
        public IReadOnlyList<Unit> GetAllUnits() => registry.GetAll();
        public IReadOnlyList<Unit> GetUnitsByKind(UnitKind kind) => registry.GetByKind(kind);
        public IReadOnlyList<Unit> GetUnitsBySubKind(UnitKind kind, ushort subKindId) => registry.GetBySubKind(kind, subKindId);
        public IReadOnlyList<Unit> GetUnitsByTeam(TeamId teamId) => registry.GetByTeam(teamId);

        public UnitUid SpawnUnit(in UnitSpawnRequest request)
        {
            RequireSpawnDependencies();

            if (!request.GameplayParticipantId.IsValid)
                throw new DeterministicSimulationException(
                    "UnitSpawnRequest requires a valid GameplayParticipantId.");

            if (!UnitPrototypeTable.TryGet(request.UnitPrototypeId, out UnitPrototype prototype))
            {
                throw new InvalidOperationException(
                    $"No UnitPrototype with id {request.UnitPrototypeId} is registered.");
            }

            byte spawnSequence = AllocateSpawnSequence();
            int spawnTick = SimulationTickContext.Current.Tick;
            var unitUid = new UnitUid(
                spawnTick, prototype.RuntimeEntityPrefabId, spawnSequence);

            GameObject instance = null;
            PhysicsEntity2D physicsEntity = null;
            bool physicsRegistered = false;
            bool unitRegistered = false;

            try
            {
                Unit unit = RentOrInstantiate(prototype, out instance);

                unit.InitializeForNewRuntime(
                    unitUid,
                    request.GameplayParticipantId,
                    request.OwnerUid,
                    prototype,
                    request.TeamId,
                    StatDefinitionTable,
                    StatGrowthC,
                    StatGrowthD,
                    TickRate,
                    AttackSequenceResetIntervalTicks,
                    request.Position);
                unit.MovementHandler?.SetMoveSpeedToLogicVelocityScale(
                    MoveSpeedToLogicVelocityScale);
                unit.MovementHandler?.SetLogicSecondsPerTick(
                    fp.one / (fp)TickRate);
                if (unit.EquipmentHandler != null)
                    unit.EquipmentHandler.DefinitionDatabase = EquipmentDatabase;
                unit.World = this;
                if (unit.AbilityHandler != null)
                {
                    unit.AbilityHandler.DefinitionRegistry = AbilityDefinitions;
                    unit.AbilityHandler.InitializeConfiguredLoadoutOrThrow();
                }
                unit.BuffHandler.DefinitionRegistry = BuffDefinitions;
                unit.BuffHandler.ApplyInitialBuffs();

                physicsEntity = unit.PhysicsEntity;
                physicsEntity.SetLogicPose(request.Position, request.Forward);
                physicsEntity.SetQueryInfo(new PhysicsEntityQueryInfo(
                    new RuntimeUidQueryValue(
                        unitUid.SpawnLogicTick,
                        unitUid.RuntimeEntityPrefabId,
                        unitUid.SpawnSequenceInTick),
                    PhysicsEntityKind.Unit,
                    request.TeamId.Value,
                    unit));
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**同步生成死亡复活与回池**

UnitWorld 同步 Spawn；生成当 Tick 可被动参与，主动工作要求 CurrentTick>SpawnLogicTick。RequestEnterDying、RequestRecoverFromDying、ConfirmUnitDeath 是正式入口。

ClearForDeath、ClearForRespawn、ClearForDespawn 按固定顺序；永久 Buff 与装备跨死亡保留所属状态；回池新生命周期不沿用旧身份。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
