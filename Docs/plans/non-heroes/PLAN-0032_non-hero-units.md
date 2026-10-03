# 非英雄单位基础

## 本次执行范围

本计划对应原编码 0032 的一次执行：非英雄单位基础。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [AI 注册调度与多态快照](../../requirements/non-heroes/REQ-FEAT-068_ai-scheduling.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [全局阶段与同步 Tick 管线](../../requirements/frame-sync/REQ-FEAT-006_simulation-pipeline.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [快照树与字段归属](../../requirements/frame-sync/REQ-FEAT-012_snapshot-ownership.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [单位根与能力装配](../../requirements/units/REQ-FEAT-020_unit-capability-composition.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [同步生成死亡复活与回池](../../requirements/units/REQ-FEAT-028_unit-lifecycle.md)：目标实现、技术方案、边界情况与附录。引用版本 1。
- [小兵波次与兵线 AI](../../requirements/non-heroes/REQ-FEAT-069_minion-waves.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：`UnitWorld`。
- `Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：`MinionSystem`。
- `Assets/Scripts/FrameSync/GameplaySnapshot.cs`：`UnitSnapshot`、`UnitWorldSnapshot`、`GameplaySnapshot`、`RollbackFrameSnapshot`。
- `Assets/Scripts/FrameSync/SimulationTickPipeline.cs`：`SimulationTickPipeline`、`InitialSpawnEntry`。
- `Assets/Scripts/Gameplay/NonHero/NonHeroSnapshot.cs`：`MinionSystemSnapshot`、`MinionTicket`、`JungleCampSnapshot`、`JungleCampState`、`UnitAIControllerSnapshot`、`MinionThreatSnapshotEntry`、`UnitAIControllerKind`。
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs`：`UnitAIController`、`MinionAIController`、`ThreatEntry`、`MonsterAIController`、`TowerAIController`。
- `Assets/Scripts/Gameplay/Unit/Core/Unit.cs`：`Unit`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs`：

```csharp
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

                PhysicsWorld.RegisterUnit(physicsEntity);
                physicsRegistered = true;
                RegisterUnit(unit);
                unitRegistered = true;
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/Gameplay/NonHero/MinionSystem.cs`：

```csharp
using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class MinionSystem
    {
        private readonly UnitWorld unitWorld;
        private readonly BakedMinionWaveConfig schedule;
        private readonly LaneRuntimeData[] lanes;
        private readonly List<MinionTicket> pendingTickets =
            new List<MinionTicket>(64);
        private readonly List<UnitUid> managedMinionUids =
            new List<UnitUid>(128);
        private int waveIndex;
        private int nextWaveLogicTick;
        private int nextTicketCursor;

        public int WaveIndex => waveIndex;
        public int NextWaveLogicTick => nextWaveLogicTick;
        public IReadOnlyList<MinionTicket> PendingTickets =>
            pendingTickets;
        public IReadOnlyList<UnitUid> ManagedMinionUids =>
            managedMinionUids;

        public bool TryGetLane(
            int laneId,
            out LaneRuntimeData lane)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].LaneId != laneId)
                    continue;
                lane = lanes[i];
                return true;
            }
            lane = null;
            return false;
        }

        public MinionSystem(
            UnitWorld unitWorld,
            in BakedMinionWaveConfig schedule,
            LaneRuntimeData[] lanes)
        {
            this.unitWorld = unitWorld ??
                throw new ArgumentNullException(nameof(unitWorld));
            if (schedule.WaveIntervalTicks <= 0 ||
                schedule.FirstWaveTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(schedule));
            this.schedule = schedule;
            this.lanes = lanes ??
                Array.Empty<LaneRuntimeData>();
            ValidateStaticTopology();
            nextWaveLogicTick = schedule.FirstWaveTick;
        }

        public void TickLogic()
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            while (currentTick >= nextWaveLogicTick)
            {
                ExpandWave(nextWaveLogicTick);
                waveIndex++;
                nextWaveLogicTick = checked(
                    nextWaveLogicTick +
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**AI 注册调度与多态快照**

UnitAIController 子类提供决策与专有快照；按稳定 Uid 调度，但主动生效晚于出生 Tick。

不增加通用模拟按键层；AI Runtime 与管理者状态区分；死亡注销不能在恢复时静默漏建。

**全局阶段与同步 Tick 管线**

SimulationTickPipeline 按全局 Handler 子阶段推进 Tag、Buff、Equipment、HitReaction、Ability、Movement、Attack，然后封存并结算战斗波次。

UnitUid 只用于稳定遍历，不能通过先处理整只单位制造跨 Handler 优势；捕获前瞬态队列必须清空。

**快照树与字段归属**

GameplaySnapshot 聚合 UnitWorld、Combat、Projectile、EquipmentShop、Physics、MatchRule 和随机状态；Snapshot 间隔一 Tick。技术 UID、Participant 和 OriginAction 均按所属模块保存。

Tick 内工作缓存不进入快照；恢复不能猜测缺字段；版本与 GameplayDataVersion 同步推进。

**单位根与能力装配**

Unit 是唯一逻辑根，UnitKind、UnitSubKindId、UnitTag 和 CapabilityState 各有含义；Handler 能力决定可支持动作。

不重复 UID 或空间状态；轻量隐形标记不是另一套可见性模拟；不能由表现组件装配顺序决定能力。

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
