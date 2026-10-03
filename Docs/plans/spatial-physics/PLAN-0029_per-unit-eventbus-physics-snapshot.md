# 单位事件总线与空间快照

## 本次执行范围

本计划对应原编码 0029 的一次执行：单位事件总线与空间快照。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [空间实体注册与写入](../../requirements/spatial-physics/REQ-FEAT-058_spatial-registration.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [定点形状与范围查询](../../requirements/spatial-physics/REQ-FEAT-059_spatial-geometry.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [移动前后网格与碰撞事实](../../requirements/spatial-physics/REQ-FEAT-060_collision-facts.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [预测回滚与逐帧重演](../../requirements/frame-sync/REQ-FEAT-011_rollback-replay.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PhysicsEntity2D 拥有位置、朝向、形状和稳定查询信息；PhysicsWorld 注册/反注册，Movement 与投射物通过正式写入接口修改逻辑。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Physics/Core/PhysicsRuntimeSnapshot.cs`：`UnitContactPair`、`UnitCollisionEventBufferSnapshot`、`PhysicsRuntimeSnapshot`、`IUnitCollisionParticipant`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitEventBus.cs`：`UnitEventBus`。
- `Assets/Scripts/Gameplay/Combat/CombatEvents.cs`：`CombatEvents`、`DamageEventData`、`HealEventData`、`ShieldEventData`、`OnHitEventData`。
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs`：`CombatSystem`、`ShieldRequestComparer`、`HealRequestComparer`、`DamageRequestComparer`、`DamageAllocationGroup`、`EvaluatedDamage`、`HeroDamageContribution`。
- `Assets/Scripts/Gameplay/Buff/BuffHandler.cs`：`BuffHandler`、`BuffReactionKind`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。

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

`Assets/Scripts/Physics/Core/PhysicsRuntimeSnapshot.cs`：

```csharp
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Physics
{
    public readonly struct UnitContactPair :
        IEquatable<UnitContactPair>, IComparable<UnitContactPair>
    {
        public readonly RuntimeUidQueryValue MinUid;
        public readonly RuntimeUidQueryValue MaxUid;

        public UnitContactPair(
            RuntimeUidQueryValue first,
            RuntimeUidQueryValue second)
        {
            if (!first.IsValid || !second.IsValid || first == second)
                throw new ArgumentException(
                    "A Unit contact pair requires two distinct valid runtime UIDs.");
            if (first.CompareTo(second) < 0)
            {
                MinUid = first;
                MaxUid = second;
            }
            else
            {
                MinUid = second;
                MaxUid = first;
            }
        }

        public int CompareTo(UnitContactPair other)
        {
            int min = MinUid.CompareTo(other.MinUid);
            return min != 0 ? min : MaxUid.CompareTo(other.MaxUid);
        }

        public bool Equals(UnitContactPair other) =>
            MinUid == other.MinUid && MaxUid == other.MaxUid;

        public override bool Equals(object obj) =>
            obj is UnitContactPair other && Equals(other);

        public override int GetHashCode()
        {
            unchecked { return (MinUid.GetHashCode() * 397) ^ MaxUid.GetHashCode(); }
        }
    }

    public struct UnitCollisionEventBufferSnapshot
    {
        public System.Collections.Generic.List<UnitContactPair> PreviousPairs;
        public static readonly UnitCollisionEventBufferSnapshot Empty = default;
    }

    public struct PhysicsRuntimeSnapshot
    {
        public UnitCollisionEventBufferSnapshot CollisionBuffer;
        public static readonly PhysicsRuntimeSnapshot Empty = default;
    }

    public interface IUnitCollisionParticipant
    {
        bool CanParticipateInUnitCollision { get; }
        void PublishUnitCollisionEnter(
            RuntimeUidQueryValue otherUid,
            fp2 contactNormal);
        void PublishUnitCollisionExit(RuntimeUidQueryValue otherUid);
    }
}
```

### 输入输出与边界

**空间实体注册与写入**

PhysicsEntity2D 拥有位置、朝向、形状和稳定查询信息；PhysicsWorld 注册/反注册，Movement 与投射物通过正式写入接口修改逻辑。

不新增 PhysicsEntityHandle；Physics 不执行 Combat；PrevPosition 每 Tick 冻结，传送和恢复有明确语义；Unity Transform 为派生表现。

**定点形状与范围查询**

PhysicsShape2D 及 geometry 定点窄相位；RangeQueryService 使用 TeamQueryRule、分类和完整目标过滤，再按指定正式键排序。

不是 Unity 物理权威；边缘接触、零半径、退化线段、旋转矩形和候选去重都有明确结果。

**移动前后网格与碰撞事实**

RvoGrid 使用移动前位置，UnitFinalGrid 在全部移动提交后构建；UnitCollisionEventBuffer 使用稳定 PairKey 发布轻量接触事实。

网格不提前按业务存活状态删候选；碰撞缓存跨 Tick 部分按快照合同；恢复后 Rebuild 派生网格。

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldRegistrationTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `RegisterUnit_AddsToUnitEntities`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void RegisterUnit_AddsToUnitEntities()
        {
            var world = new PhysicsWorld();
            var entity = CreateEntity();

            world.RegisterUnit(entity);

            Assert.That(world.UnitEntities.Count, Is.EqualTo(1));
            Assert.That(world.UnitEntities[0], Is.SameAs(entity));
            Assert.That(world.ProjectileEntities.Count, Is.EqualTo(0));
        }
```
- `Assets/Scripts/Physics/Tests/PlayMode/PhysicsWorldBuildFinalGridTests.cs`：PlayMode，程序集 `FrameSyncMoba.Physics.PlayModeTests`，函数 `BuildUnitFinalGrid_AllRegisteredUnits_Inserted`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void BuildUnitFinalGrid_AllRegisteredUnits_Inserted()
        {
            var world = new PhysicsWorld();
            world.Settings.GridCellSize = (fp)10m;

            var e1 = CreateUnitEntity(100, 1, 0, (fp)5m, (fp)5m);
            var e2 = CreateUnitEntity(100, 2, 0, (fp)15m, (fp)15m);

            world.RegisterUnit(e1);
            world.RegisterUnit(e2);

            world.BuildUnitFinalGrid();

            var results = new List<PhysicsEntity2D>();
            var queryBounds = new PhysicsBounds2D(new fp2(0, 0), new fp2(20, 20));
            world.UnitFinalGrid.CollectCandidates(queryBounds, results);

            Assert.AreEqual(2, results.Count);
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
