# 单位预制体界面生命周期与寻路

## 本次执行范围

本计划对应原编码 0125 的一次执行：单位预制体界面生命周期与寻路。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [旋转网格地图与半径通行](../../requirements/pathfinding-movement/REQ-FEAT-062_pathfinding-grid.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [路线选择与跟随状态](../../requirements/pathfinding-movement/REQ-FEAT-063_path-following.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [A 星路径搜索与简化](../../requirements/pathfinding-movement/REQ-FEAT-064_astar-search.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [页面层级与 Lua 实例生命周期](../../requirements/presentation-ui/REQ-FEAT-075_lua-ui-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [HUD 数值技能与小地图](../../requirements/presentation-ui/REQ-FEAT-076_hud-minimap.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [按对局加载内容闭包](../../requirements/configuration-content/REQ-FEAT-018_match-content-closure.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位视图绑定与语义挂点](../../requirements/presentation-ui/REQ-FEAT-072_unit-view-binding.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。
- `Assets/Scripts/Bootstrap/UI/UIManager.cs`：`UIManager`、`PageRegistration`。
- `Assets/Scripts/Bootstrap/ClientBootstrap.cs`：`ClientBootstrap`。
- `Assets/Scripts/Bootstrap/UI/UIPage.cs`：`name`、`UIPageId`、`UIPageLayer`、`UIPage`。
- `Assets/Scripts/Bootstrap/UI/UIPanel.cs`：`UIPanel`。
- `Assets/Scripts/Physics/Core/PhysicsEntity2D.cs`：`PhysicsEntity2D`。
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs`：`UnitAnimationDriver`。
- `Assets/Scripts/Bootstrap/GameBootstrap.cs`：`InitialUnitSpawnAuthoring`、`GameBootstrap`、`ScoreboardBuffer`。

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

`Assets/Scripts/Bootstrap/UI/UIManager.cs`：

```csharp
        public bool ClosePage(UIPageId pageId)
        {
            if (GetRegistration(pageId).Layer ==
                UIPageLayer.BattleOverlay)
            {
                if (_currentOverlay != pageId)
                    return false;
                HideOverlay(pageId);
                return true;
            }
            if (_currentMainPage != pageId)
                return false;
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.Close();
            _currentMainPage = UIPageId.None;
            return true;
        }
```

### 输入输出与边界

**旋转网格地图与半径通行**

PathGridMap2D 明确世界到局部、格坐标与格中心变换，RadiusClass 参与 IsWalkableForAgent 与 IsCircleWalkable。

旋转、边界、不可走起终点和过大半径可见处理；NavMask 与内容配置版本有一致来源。

**路线选择与跟随状态**

UnitLocomotionAgent/RouteResolver 按 MovePurpose、目标变化和路径偏离决策；PathFollower2D 输出 LocomotionResult，不直接写空间。

控制打断不重写 Order；恢复保留正式路线状态或明确重建可派生结果；追踪目标失效按正式边界返回。

**A 星路径搜索与简化**

AStarPathService 采用显式 OpenSet 排序、固定邻居访问与启发式成本；候选终点和路径简化仍受半径通行验证。

相同代价稳定平局；不可达不伪造直达路线；禁止随机/集合枚举顺序影响路径。

**页面层级与 Lua 实例生命周期**

UIManager、UIPanel/UIPage 管理页面，LuaManager 管理环境，LuaHost 管理实例；UIList/UICell 复用格子，显式绑定与解绑。

不由 UI 决定预测或回滚；页面关闭移除观察者；主机单元和页面实例不共用意外 mutable 状态。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

- `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `AStar_OpenGrid_ReturnsCorrectPath`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void AStar_OpenGrid_ReturnsCorrectPath()
        {
            var grid = CreateOpenGrid();
            var aStar = new AStarPathService(grid);

            fp2 start = grid.CellToWorld(1, 1);
            fp2 target = grid.CellToWorld(10, 10);

            PathResult result = aStar.FindPath(start, target);

            Assert.That(result.Success, Is.True, "A* should find a path on an open grid.");
            Assert.That(result.PathCellIndices, Is.Not.Null);
            Assert.That(result.PathCellIndices.Length, Is.GreaterThan(0));
        }
```
- `Assets/Scripts/Gameplay/Tests/FlowFieldBuildTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void BuildLaneCostField_SingleTarget_RadialCostsIncreaseOutward()
        {
            var grid = CreateOpenGrid();
            var service = new TeamFlowFieldService(grid);
            var laneConfig = new LaneTargetConfig
            {
                LaneIndex = 0,
                Targets = new fp2[] { new fp2((fp)8m, (fp)8m) },
            };

            int[] cost = service.BuildLaneCostField(laneConfig, RadiusClass.Medium);

            (int tx, int ty) = grid.WorldToCell(new fp2((fp)8m, (fp)8m));
            int targetIdx = ty * GridWidth + tx;
            Assert.That(cost[targetIdx], Is.EqualTo(0), "Target should have cost 0.");

            (int fx, int fy) = grid.WorldToCell(new fp2((fp)14m, (fp)14m));
            int farIdx = fy * GridWidth + fx;
            Assert.That(cost[farIdx], Is.GreaterThan(0), "Far cell should have positive cost.");
            Assert.That(cost[farIdx], Is.LessThan(int.MaxValue), "Far cell should be reachable.");
        }
```
- `Assets/Scripts/Gameplay/Tests/PathfindingIntegrationTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `FiveAgents_ConvergingOnCenter_NoDeadlock`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
using FrameSyncMoba.Unit;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public class RVOIntegrationTests
    {
        private DeterministicRVOSystem _rvo;
        [SetUp] public void SetUp() => _rvo = new DeterministicRVOSystem(RVOConfig.Default);

        private static RVOInput MakeInput(int id, fp2 pos, fp2 desiredVel, fp maxSpeed = default, fp radius = default)
        {
            if (maxSpeed <= fp.zero) maxSpeed = (fp)3m;
            if (radius <= fp.zero) radius = (fp)0.5m;
            return new RVOInput { SelfUid = new UnitUid(1, (byte)id, (byte)id), Position = pos, DesiredVelocity = desiredVel, Radius = radius, MaxSpeed = maxSpeed };
        }

        [Test] public void FiveAgents_ConvergingOnCenter_NoDeadlock()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2((fp)0m,(fp)5m),new fp2((fp)0m,-(fp)1m)), MakeInput(2,new fp2((fp)0m,-(fp)5m),new fp2((fp)0m,(fp)1m)), MakeInput(3,new fp2(-(fp)5m,(fp)0m),new fp2((fp)1m,(fp)0m)), MakeInput(4,new fp2((fp)5m,(fp)0m),new fp2(-(fp)1m,(fp)0m)), MakeInput(5,new fp2(-(fp)3m,(fp)3m),new fp2((fp)1m,-(fp)1m)) };
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(5));
            for (int i = 0; i < r.Length; i++) Assert.That(fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity), Is.GreaterThanOrEqualTo(fp.zero));
        }

        [Test] public void TwoUnitsHeadOn_VelocitiesDiverge()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2(fp.zero,(fp)8m),new fp2(fp.zero,-fp.one)), MakeInput(2,new fp2(fp.zero,(fp)3m),new fp2(fp.zero,fp.one)) };
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(2));
            for (int i = 0; i < r.Length; i++) { fp lenSq = fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity); Assert.That(lenSq, Is.GreaterThanOrEqualTo(fp.zero)); Assert.That(lenSq, Is.LessThanOrEqualTo(inputs[i].MaxSpeed*inputs[i].MaxSpeed+(fp)0.1m)); }
        }

        [Test] public void TenAgents_RandomSpread_CompletesWithoutError()
        {
            var inputs = new RVOInput[10];
            for (int i = 0; i < 10; i++) { fp a = (fp)((i*36.0m)*3.14159265m/180.0m); inputs[i] = MakeInput(i+1,new fp2(fpmath.cos(a)*(fp)5m,fpmath.sin(a)*(fp)5m),new fp2(-fpmath.cos(a),-fpmath.sin(a))); }
            RvoResult[] r = _rvo.Step(inputs); Assert.That(r.Length, Is.EqualTo(10));
            for (int i = 0; i < r.Length; i++) Assert.That(fpmath.dot(r[i].FinalVelocity,r[i].FinalVelocity), Is.GreaterThanOrEqualTo(fp.zero));
        }

        [Test] public void RVO_Deterministic_SameInputProducesSameOutput()
        {
            var inputs = new RVOInput[] { MakeInput(1,new fp2((fp)0m,(fp)5m),new fp2((fp)0m,-(fp)1m)), MakeInput(2,new fp2((fp)0m,(fp)6m),new fp2((fp)0m,(fp)1m)), MakeInput(3,new fp2((fp)1m,(fp)5m),new fp2((fp)0m,-(fp)1m)) };
            var a = new DeterministicRVOSystem(RVOConfig.Default); var b = new DeterministicRVOSystem(RVOConfig.Default);
            RvoResult[] r1 = a.Step(inputs); RvoResult[] r2 = b.Step(inputs);
            for (int i = 0; i < r1.Length; i++) { Assert.That(r1[i].FinalVelocity.x, Is.EqualTo(r2[i].FinalVelocity.x)); Assert.That(r1[i].FinalVelocity.y, Is.EqualTo(r2[i].FinalVelocity.y)); }
        }

        [Test] public void RVO_ZeroDesiredVelocity_ReturnsZero()
// 方法后续请阅读上述真实源码；这里是节选。
```
- `Assets/Scripts/Gameplay/Tests/RVOSystemTests.cs`：EditMode，程序集 `FrameSyncMoba.Unit.Tests`，函数 `SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void SolveAvoidance_TwoUnitsHeadOn_VelocitiesDiverge()
        {
            var inputs = new RVOInput[]
            {
                MakeInput(1, new fp2(fp.zero, (fp)5m), new fp2(fp.zero, -fp.one)),
                MakeInput(2, new fp2(fp.zero, (fp)6m), new fp2(fp.zero, fp.one)),
            };

            RvoResult[] results = _rvo.Step(inputs);

            Assert.That(results.Length, Is.EqualTo(2));
            // Units should not both move directly toward each other
            fp2 v1 = results[0].FinalVelocity;
            fp2 v2 = results[1].FinalVelocity;

            // At least one unit should have a non-zero velocity
            bool hasMovement = (v1.x != fp.zero || v1.y != fp.zero)
                || (v2.x != fp.zero || v2.y != fp.zero);
            Assert.That(hasMovement, Is.True, "At least one unit should have movement.");
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
