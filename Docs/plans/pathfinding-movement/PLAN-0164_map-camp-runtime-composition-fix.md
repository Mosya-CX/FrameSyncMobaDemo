# 地图营地运行时组合修复

## 本次执行范围

本计划对应原编码 0164 的一次执行：地图营地运行时组合修复。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：`JungleCampSpawnSlot`、`JungleCamp`。
- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。
- `Assets/Scripts/Gameplay/Pathfinding/FlowFieldSceneAuthoring.cs`：`FlowFieldSceneAuthoring`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：

```csharp
        public void Resolve(
            in RollbackContext context)
        {
            for (int i = 0;
                 i < MemberUidsBySlot.Length;
                 i++)
            {
                UnitUid uid = MemberUidsBySlot[i];
                if (MemberAliveBySlot[i] &&
                    uid.IsValid() &&
                    !unitWorld.TryGetUnit(uid, out _))
                    throw new DeterministicSimulationException(
                        $"JungleCamp {campId} live member {uid} is missing.");
            }
            if (PrimaryTargetUid.IsValid() &&
                !unitWorld.TryGetUnit(
                    PrimaryTargetUid,
                    out _))
                throw new DeterministicSimulationException(
                    $"JungleCamp {campId} target {PrimaryTargetUid} is missing.");
        }
```

`Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitWorld
    {
        private readonly UnitRegistry registry = new UnitRegistry();
        private readonly UnitPoolRegistry poolRegistry =
            new UnitPoolRegistry();
        private readonly List<UnitAIController> aiControllers = new List<UnitAIController>();
        private readonly List<JungleCamp> jungleCamps =
            new List<JungleCamp>();
        private bool isTickingAIControllers;
        private int currentSequenceLogicTick = -1;
        private byte nextSpawnSequenceInTick;
        private bool spawnSequenceExhausted;
        private int runtimeRevision;

        public GlobalUnitPrototypeTable UnitPrototypeTable { get; set; }
        public UnitDisposePolicyTable DisposePolicyTable { get; set; }
        public GlobalPrefabTable GlobalPrefabTable { get; set; }
        public StatDefinitionTable StatDefinitionTable { get; set; }
        public EquipmentDatabase EquipmentDatabase { get; set; }
        public AbilityDefinitionRegistry AbilityDefinitions { get; set; }
        public BuffDefinitionRegistry BuffDefinitions { get; set; }
        public CrowdControlDefinitionRegistry CrowdControlDefinitions { get; set; }
        public PhysicsWorld PhysicsWorld { get; set; }
        public fp StatGrowthC { get; set; }
        public fp StatGrowthD { get; set; }
        public fp MoveSpeedToLogicVelocityScale { get; set; } = (fp)0.01m;
        public fp StatDistanceToLogicDistanceScale { get; set; } = (fp)0.01m;
        public int TickRate { get; set; } = 30;
        public int AttackSequenceResetIntervalTicks { get; set; } = 90;
        /// <summary>
        /// Units with AttackRange strictly above this value are treated as
        /// ranged. Baked from GlobalGameplayData.UnitSettings.
        /// </summary>
        public int RangedAttackRangeThreshold { get; set; } = 275;
        public RespawnTimer RespawnTimer { get; set; }
        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public PathGridMap2D PathGrid { get; set; }
        public DynamicNavigationFrame DynamicNavigation { get; private set; }
        public FlowFieldRegistry FlowFieldRegistry { get; set; }
        public IMovementCollisionResolver
            MovementCollisionResolver { get; set; }
        public CombatSystem CombatSystem { get; set; }
        public ProjectileWorld ProjectileWorld { get; set; }
        public RangeQueryService RangeQuery { get; set; }
        public DeterministicRandomService RandomService { get; set; }
        public IReadOnlyList<UnitAIController> AIControllers => aiControllers;
        public IReadOnlyList<JungleCamp> JungleCamps => jungleCamps;
        public MinionSystem MinionSystem { get; set; }
        public int RuntimeRevision => runtimeRevision;
        public UnitPoolRegistry PoolRegistry => poolRegistry;

        public void RebuildDynamicNavigation(
            int logicTick)
        {
            if (PathGrid == null)
            {
                DynamicNavigation = null;
                return;
            }
            if (DynamicNavigation == null ||
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

- `Assets/Scripts/Gameplay/Tests/UnitWorldIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SpawnMultipleKinds_GetByKind`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SpawnMultipleKinds_GetByKind()
        {
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(heroProto, TeamId.Neutral, 1, 0m, 0m);
            world.SpawnUnit(minionProto, TeamId.Neutral, 1, 0m, 0m);

            var heroes = world.GetUnitsByKind(UnitKind.Hero);
            var minions = world.GetUnitsByKind(UnitKind.Minion);
            var all = world.GetAllUnits();

            Assert.AreEqual(2, heroes.Count);
            Assert.AreEqual(1, minions.Count);
            Assert.AreEqual(3, all.Count);
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
- `Assets/Scripts/Gameplay/Tests/NonHeroTopologyTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `MinionWave_ExpandsCanonicalTeamLaneMemberOrder`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void MinionWave_ExpandsCanonicalTeamLaneMemberOrder()
        {
            var schedule = new BakedMinionWaveConfig(
                30,
                0,
                new[]
                {
                    new MinionWavePhase
                    {
                        StartWaveIndex = 0,
                        CompositionCycle = new[]
                        {
                            new MinionWaveComposition
                            {
                                Members = new[]
                                {
                                    new MinionWaveMember
                                    {
                                        UnitPrototypeId = 20,
                                        Count = 2,
                                        FirstSpawnOffsetTicks = 5,
                                        SpawnStepTicks = 1,
                                    },
                                },
                            },
                        },
                    },
                });
            var lane = new LaneRuntimeData(
                3,
                new[]
                {
                    new LaneTeamSpawnData(
                        new TeamId(1),
                        new fp2(1, 2),
                        new fp2(1, 0)),
                    new LaneTeamSpawnData(
                        new TeamId(2),
                        new fp2(9, 2),
                        new fp2(-1, 0)),
                },
                new[] { fp2.zero, new fp2(10, 0) },
                (fp)2m);
            var system = new MinionSystem(
                new UnitWorld(),
                schedule,
                new[] { lane });
            BeginTick(0);

            system.TickLogic();
// 方法后续请阅读上述真实源码；这里是节选。
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
