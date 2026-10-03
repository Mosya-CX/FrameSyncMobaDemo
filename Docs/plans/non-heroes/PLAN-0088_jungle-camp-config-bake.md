# 野怪营地配置烘焙

## 本次执行范围

本计划对应原编码 0088 的一次执行：野怪营地配置烘焙。同功能后续改造由后续独立计划承接，关闭记录不重新进入执行。

## 参考需求

- [野怪营地刷新与共享仇恨](../../requirements/non-heroes/REQ-FEAT-070_jungle-camps.md)：目标实现、技术方案、边界情况与附录。引用版本 1。

## 实施技术细节

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

### 当前类型、核心算法与数据流

本节按原执行批次实际点名的文件和类型核对，不能用邻近功能的共享基础类型代替。历史合同的后续扩展不属于本批次当时的新增范围。

- `Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：`FrameSyncGameRuntime`。
- `Assets/Scripts/RuntimeConfig/MinionWaveConfig.cs`：`MinionTeamPrototypeOverride`、`MinionWaveMember`、`MinionWaveComposition`、`MinionWavePhase`、`BakedMinionWaveConfig`、`MinionWaveConfig`。
- `Assets/Scripts/Gameplay/NonHero/JungleCamp.cs`：`JungleCampSpawnSlot`、`JungleCamp`。
- `Assets/Scripts/RuntimeConfig/Editor/MinionWaveBakeMenuItem.cs`：`MinionWaveBakeMenuItem`。
- `Assets/Scripts/RuntimeConfig/Editor/MinionWaveConfigValidator.cs`：`MinionWaveConfigValidator`。
- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs`：`FrameSyncSettingsAuthoring`、`CriticalDataVersionsAuthoring`、`GameModeConfigAuthoring`、`PhysicsSettingsAuthoring`、`UnitSettingsAuthoring`、`BakedGlobalGameplayData`、`GlobalGameplayData`。

以下代码为当前真实实现节选，完整方法以对应源码为准。

`Assets/Scripts/FrameSync/FrameSyncGameRuntime.cs`：

```csharp
using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.FrameSync
{
    public sealed class FrameSyncGameRuntime
    {
        private readonly SimulationTickPipeline _pipeline;
        private readonly SimulationTickContextController _tickController;
        private readonly PredictionRollbackCoordinator _rollbackCoordinator;
        private readonly CommandRelayBuffer _commandRelayBuffer;
        private readonly AuthorityRecoveryArchive _authorityRecoveryArchive;
        private readonly AuthorityFrameReplicator _authorityFrameReplicator;
        private readonly AuthorityRecoveryCoordinator _authorityRecoveryCoordinator;
        private PlayerSlotUnitMapping[] _playerSlotMappings =
            Array.Empty<PlayerSlotUnitMapping>();
        private int _naturalGoldIntervalTicks = 15;
        private int _naturalGoldAmount = 2;

        public Unit.UnitWorld UnitWorld { get; }
        public PhysicsWorld PhysicsWorld { get; }
        public Unit.CombatSystem CombatSystem { get; }
        public MatchRuleRuntime MatchRule { get; }
        public GoldIncomeRuntime GoldIncome { get; }

        public Unit.IEquipmentShopView
            CreateEquipmentShopView(
                int playerSlot)
        {
            return new Unit.EquipmentShopView(
                _pipeline.EquipmentShop,
                GoldIncome,
                playerSlot);
        }
        public CommandCollector CommandCollector => _pipeline.CommandCollector;
        public int CurrentTick => _pipeline.LocalSimulationTick;
        public int LastCompletedTick => _pipeline.LocalSimulationTick - 1;
        public int LatestSynchronizedServerTick { get; private set; } = -1;
        public int MinCommandLeadTicks { get; private set; } = 1;
        public int MaxFutureCommandTicks => _pipeline.MaxFutureCommandTicks;
        public uint LastChecksum => _pipeline.LastChecksum;
        public SimulationTickPipeline TickPipeline => _pipeline;
        public PredictionRollbackCoordinator Prediction =>
            _rollbackCoordinator;
        public AuthorityFrameReplicator AuthorityFrames =>
            _authorityFrameReplicator;
        public AuthorityRecoveryCoordinator AuthorityRecovery =>
            _authorityRecoveryCoordinator;

        /// <summary>
        /// Active composition-root runtime that Lua UI pages query. It is set by
        /// the application layer only; deterministic simulation never depends on it.
        /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 5.3, 10.11.
        /// </summary>
        public static FrameSyncGameRuntime Instance { get; private set; }

        public static void RegisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            Instance = runtime;
        }

        public static void UnregisterActiveInstance(
            FrameSyncGameRuntime runtime)
        {
// 方法后续请阅读上述真实源码；这里是节选。
```

`Assets/Scripts/RuntimeConfig/MinionWaveConfig.cs`：

```csharp
using System;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    [Serializable]
    public struct MinionTeamPrototypeOverride
    {
        [Range(1, byte.MaxValue)] public int TeamId;
        [Min(1)] public int UnitPrototypeId;
    }

    [Serializable]
    public struct MinionWaveMember
    {
        [Min(1)] public int UnitPrototypeId;
        [Tooltip("Optional team-specific runtime prototypes. Entries must be sorted by TeamId.")]
        public MinionTeamPrototypeOverride[] TeamPrototypeOverrides;
        [Min(1)] public int Count;
        public DurationAuthoring FirstSpawnOffset;
        [HideInInspector]
        [Min(0)] public int FirstSpawnOffsetTicks;
        public DurationAuthoring SpawnStep;
        [HideInInspector]
        [Min(0)] public int SpawnStepTicks;
        [Min(0)] public int FormationGroup;

        public int ResolveUnitPrototypeId(int teamId)
        {
            MinionTeamPrototypeOverride[] overrides =
                TeamPrototypeOverrides ??
                Array.Empty<MinionTeamPrototypeOverride>();
            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].TeamId == teamId)
                    return overrides[i].UnitPrototypeId;
            }
            return UnitPrototypeId;
        }
    }

    [Serializable]
    public struct MinionWaveComposition
    {
        public MinionWaveMember[] Members;
    }

    [Serializable]
    public struct MinionWavePhase
    {
        [Min(0)] public int StartWaveIndex;
        public MinionWaveComposition[] CompositionCycle;
    }

    public readonly struct BakedMinionWaveConfig
    {
        public readonly int WaveIntervalTicks;
        public readonly int FirstWaveTick;
        public readonly MinionWavePhase[] Phases;

        public BakedMinionWaveConfig(
            int waveIntervalTicks,
            int firstWaveTick,
            MinionWavePhase[] phases)
        {
            if (waveIntervalTicks <= 0 || firstWaveTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(waveIntervalTicks),
                    "Wave interval must be positive and first wave Tick nonnegative.");
            WaveIntervalTicks = waveIntervalTicks;
// 方法后续请阅读上述真实源码；这里是节选。
```

### 输入输出与边界

**野怪营地刷新与共享仇恨**

JungleCamp 拥有营地、respawn 和 member slot；MonsterAIController 管理战斗、共享目标、追击与回营，复用普通行为链。

主怪/小怪死亡、营地清空、目标远离和不可达分别处理；三狼回营/寻路旧计划未完成，不能标成已验收。

## 执行结果

用户本轮确认功能手动测试正常；当前对应文件、类型、入口与已有测试断言已核对。按本轮人工确认关闭历史执行记录；没有重跑 Unity 行为测试，源码定位不等于自动化验收通过。

## Agent 测试与验收

本期以用户人工功能确认和当前源码复核记录结果，未重跑 Unity 行为测试。下面是原切片可关联的实际测试输入、函数与期望断言，不宣称本期自动化通过。

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
- `Assets/Scripts/Bootstrap/Tests/EditMode/MurkWolfFormalContentTests.cs`：EditMode，程序集 `FrameSyncMoba.Bootstrap.EditModeTests`，函数 `UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void UnitCatalog_ContainsAuthoredGreaterAndMiniWolfValues()
        {
            UnitRuntimeCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<UnitRuntimeCatalogAsset>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            UnitPrototypeAuthoring greater = FindPrototype(
                catalog,
                MurkWolfContentIds.GreaterPrototype);
            UnitPrototypeAuthoring mini = FindPrototype(
                catalog,
                MurkWolfContentIds.MiniPrototype);

            AssertPrototype(
                greater,
                MurkWolfContentIds.GreaterPrefab,
                NonHeroUnitSubKindId.GreaterMurkWolf,
                1600f,
                30f,
                42f,
                42f,
                0.625f,
                175f,
                525f,
                0.8f,
                55,
                50,
                2);
            CollectionAssert.AreEqual(
                new[] { MurkWolfContentIds.GreaterCurrentHealthOnHitBuff },
                greater.InitialBuffConfigIds);

            AssertPrototype(
                mini,
                MurkWolfContentIds.MiniPrefab,
                NonHeroUnitSubKindId.MurkWolf,
                630f,
                10f,
                20f,
                20f,
                0.625f,
                125f,
                525f,
                0.5f,
                13,
                15,
                1);
            Assert.That(mini.InitialBuffConfigIds, Is.Empty);
        }
```
- `Assets/Scripts/FrameSync/Tests/FrameSyncPipelineTests.cs`：EditMode，程序集 `FrameSyncMoba.FrameSync.Tests`，函数 `GameplayCommand_CreateMove_WritesCanonicalBytes`；输入/夹具与期望见真实断言，失败保留回执与 Console。

```csharp
        public void GameplayCommand_CreateMove_WritesCanonicalBytes()
        {
            var controller = new SimulationTickContextController();
            controller.BeginTick(10, ExecutionMode.ServerAuthority);

            try
            {
                var unit = _world.SpawnUnit(_prototype, TeamId.Neutral, 10, 0m, 0m);
                var cmd = GameplayCommand.CreateMove(
                    CreateHeader(unit.UnitUid, 11, 1),
                    new fp2(fp.one, fp.zero));

                var buffer = new byte[256];
                var writer = new CanonicalByteWriter(buffer);
                cmd.WriteCanonicalBytes(writer);

                Assert.Greater(writer.WrittenCount, 0);
            }
            finally
            {
                controller.EndTick();
            }
        }
```

## 恢复与限制

不撤销已有工作区，不改动其他批次源码和资产。历史接口已重构时以当前具体需求为准；工程宪法中的确定性、快照和资源 GUID 约束仍适用。源码存在和用户人工测试不能替代尚未完成的配套构建或专项测试。
