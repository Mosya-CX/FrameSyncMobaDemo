# 确定性地图拓扑与目标夹具

## 本次执行范围

本计划对应原编码 0122 的一次执行：确定性地图拓扑与目标夹具。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/FrameSync/MatchRuleRuntime.cs`：`MatchPhase`、`MatchEndReason`、`MatchTopologyRole`、`MatchStatisticsEntry`、`MatchStatisticsRuntimeSnapshot`、`MatchStatisticsRuntime`、`StatisticKind`。
- `Assets/Scripts/Gameplay/Unit/Kind/UnitKind.cs`：`UnitKind`。
- `Assets/Scripts/Gameplay/Pathfinding/PathGridMap2D.cs`：`PathGridMap2D`、`walkability`、`or`、`and`。
- `Assets/Scripts/Gameplay/Unit/Team/TeamId.cs`：`TeamId`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/Physics/Core/PhysicsWorld.cs`：`PhysicsWorld`。

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

`Assets/Scripts/FrameSync/MatchRuleRuntime.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public enum MatchPhase : byte
    {
        Preparing = 0,
        Countdown = 1,
        Running = 2,
        Ending = 3,
        Finished = 4,
    }

    public enum MatchEndReason : byte
    {
        None = 0,
        BaseDestroyed = 1,
        SimultaneousBaseDestruction = 2,
    }

    public enum MatchTopologyRole : byte
    {
        None = 0,
        BlueBase = 1,
        RedBase = 2,
    }

    public struct MatchStatisticsEntry
    {
        public UnitUid HeroUnitUid;
        public int Kills;
        public int Deaths;
        public int Assists;
        /// <summary>Last-hit minion/monster kills (creep score).</summary>
        public int CreepKills;
    }

    public struct MatchStatisticsRuntimeSnapshot
    {
        public System.Collections.Generic.List<MatchStatisticsEntry> Entries;
        public static readonly MatchStatisticsRuntimeSnapshot Empty = default;
    }

    public sealed class MatchStatisticsRuntime
    {
        /// <summary>
        /// Authored/stat-distance radius around a dying minion in which
        /// enemy heroes share the minion's base experience. It is converted
        /// to logic distance through UnitWorld.StatDistanceToLogicDistanceScale.
        /// Minion gold is not shared; only the killer receives gold.
        /// </summary>
        public const int MinionRewardShareRadius = 1200;

        /// <summary>
        /// Killer share of a hero-victim reward (Combat v13.2 11.5); the
        /// remainder is split evenly among valid assisters.
        /// </summary>
        public const int HeroKillerShareNumerator = 3;
        public const int HeroKillerShareDenominator = 5;

        private readonly List<MatchStatisticsEntry> entries =
            new List<MatchStatisticsEntry>();
        private readonly List<GoldAllocation> goldAllocations =
            new List<GoldAllocation>();

// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**旋转网格地图与半径通行**

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

旋转、边界、不可走起终点和过大半径可见处理；NavMask 与内容配置版本有一致来源。

**野怪营地刷新与共享仇恨**

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/TeamIdAndRegistryTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `TeamId_Default_IsNeutral`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void TeamId_Default_IsNeutral()
        {
            Assert.That(TeamId.Neutral.Value, Is.EqualTo(0));
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

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
