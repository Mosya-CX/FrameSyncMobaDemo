using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Karolina.Testing;
using Karolina.Testing.Pipeline;
using Karolina.Testing.Unity;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Unit.PlayModeTests
{
    public sealed class KarolinaFrameSyncGameplayPipelineTests
    {
        private CancellationTokenSource activeCancellation;
        private Task<TestPipelineExecutionResult> activeExecution;
        private UnitSpawnScenario activeScenario;

        [UnityTest]
        public IEnumerator SpawnedUnitRunsThroughPreparedGameplayPipeline()
        {
            var scenario = new UnitSpawnScenario();
            activeScenario = scenario;
            activeCancellation = new CancellationTokenSource();
            activeExecution = RunPipelineAsync(
                scenario,
                activeCancellation.Token);
            int remainingFrames = 120;
            while (!activeExecution.IsCompleted && remainingFrames-- > 0)
            {
                yield return null;
            }

            if (!activeExecution.IsCompleted)
            {
                activeCancellation.Cancel();
                int cleanupFrames = 30;
                while (!activeExecution.IsCompleted && cleanupFrames-- > 0)
                {
                    yield return null;
                }
            }

            Assert.That(activeExecution.IsCompleted, Is.True,
                "FrameSyncMoba 游戏运行态流水线未在时限内结束。");
            if (activeExecution.IsFaulted)
            {
                Assert.Fail(activeExecution.Exception?.GetBaseException().ToString() ??
                    "FrameSyncMoba 游戏运行态流水线失败。");
                yield break;
            }
            if (activeExecution.IsCanceled)
            {
                Assert.Fail("FrameSyncMoba 游戏运行态流水线被取消。");
                yield break;
            }

            TestPipelineExecutionResult result = activeExecution.GetAwaiter().GetResult();
            yield return null;

            Assert.That(result.Status, Is.EqualTo(TestPipelineStatus.Passed), result.ErrorMessage);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Assertions.Count, Is.EqualTo(5));
            Assert.That(scenario.LastFixture.CleanupCompleted, Is.True);
            Assert.That(scenario.LastFixture.World.GetAllUnits().Count, Is.EqualTo(0),
                "清理阶段未从 UnitWorld 注销测试单位。");
            Assert.That(scenario.LastFixture.Unit == null, Is.True,
                "清理阶段仍遗留测试单位 GameObject。");
            Assert.That(scenario.LastFixture.Template == null, Is.True,
                "清理阶段仍遗留测试 Prefab。");
            Assert.That(scenario.LastFixture.ClockRoot == null, Is.True,
                "清理阶段仍遗留 UnityFrameClock GameObject。");
            Assert.That(scenario.LastFixture.PrefabTable == null, Is.True,
                "清理阶段仍遗留测试 GlobalPrefabTable。");
            Assert.That(scenario.LastFixture.Physics.UnitEntities.Count, Is.Zero, "物理世界仍登记测试单位。");
            Assert.That(scenario.LastFixture.Physics.ProjectileEntities.Count, Is.Zero, "物理世界仍登记测试投射物。");
            Assert.That(scenario.LastFixture.World.PhysicsWorld, Is.Null);
            Assert.That(scenario.LastFixture.World.UnitPrototypeTable, Is.Null);
            Assert.That(scenario.LastFixture.World.StatDefinitionTable, Is.Null);
            Assert.That(scenario.LastFixture.World.GlobalPrefabTable == null, Is.True);
            Assert.That(scenario.LogContents, Does.Contain("\"eventType\":\"gameplay.unit.spawned\""));
            Assert.That(scenario.LogContents, Does.Contain("\"eventType\":\"gameplay.unit.sampled\""));
            Assert.That(scenario.LogContents, Does.Contain("\"domain\":\"SimulationTick\""));
            Assert.That(scenario.LogContents, Does.Contain("\"domain\":\"UnityFrame\""));
        }

        [UnityTearDown]
        public IEnumerator CleanupTimedOutPipeline()
        {
            activeCancellation?.Cancel();
            int remainingFrames = 120;
            while (activeExecution != null &&
                   !activeExecution.IsCompleted &&
                   remainingFrames-- > 0)
            {
                yield return null;
            }

            activeScenario?.CleanupIfNeeded();
            yield return null;
            activeExecution = null;
            activeScenario = null;
            activeCancellation?.Dispose();
            activeCancellation = null;
        }

        private static async Task<TestPipelineExecutionResult> RunPipelineAsync(
            UnitSpawnScenario scenario,
            CancellationToken cancellationToken)
        {
            using (var stream = new MemoryStream())
            {
                var log = new AsyncTestSessionLog(stream, leaveOpen: true);
                try
                {
                    return await new TestPipelineExecutor().RunAsync(
                        scenario,
                        new TestSessionIdentity(
                            Guid.NewGuid().ToString("N"),
                            scenario.Id,
                            "attempt-1",
                            1),
                        log,
                        cancellationToken);
                }
                finally
                {
                    await log.DisposeAsync();
                    scenario.LogContents = Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        private sealed class UnitSpawnScenario :
            ITestPipelineScenario<UnitSpawnFixture, PreparedRuntimeResult<UnitSpawnSample>>
        {
            public string Id => "framesync.unit.spawn-runtime";
            public string Title => "FrameSyncMoba 运行时单位生成与生命周期采样";
            public UnitSpawnFixture LastFixture { get; private set; }
            public string LogContents { get; set; }

            public void CleanupIfNeeded()
            {
                if (LastFixture != null && !LastFixture.CleanupCompleted)
                {
                    DestroyFixture(LastFixture);
                }
            }

            public ValueTask<UnitSpawnFixture> PrepareAsync(
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fixture = new UnitSpawnFixture();
                LastFixture = fixture;
                try
                {
                    fixture.Template = CreateTemplate();
                    fixture.PrefabTable = ScriptableObject.CreateInstance<GlobalPrefabTable>();
                    fixture.PrefabTable.ReplaceGroupsForTests(new[]
                    {
                        new PrefabGroup(PrefabKind.Unit, new[]
                        {
                            new PrefabEntry(8910, fixture.Template, 8910),
                        }),
                    });
                    fixture.PrefabTable.ReplaceRangesForTests(new[]
                    {
                        new PrefabKindRangeConfig
                        {
                            Kind = PrefabKind.Unit,
                            IdRangeStart = 1,
                            IdRangeEnd = 9999,
                        },
                    });

                    var prototype = new UnitPrototype
                    {
                        UnitPrototypeId = 8910,
                        RuntimeEntityPrefabId = 8910,
                        UnitKind = UnitKind.Hero,
                        UnitSubKindId = 0,
                        Loadout = HandlerLoadout.DefaultHero,
                        BaseStats = CreatePreset(),
                    };
                    var prototypes = new GlobalUnitPrototypeTable();
                    prototypes.Add(prototype);
                    fixture.Physics = new PhysicsWorld();
                    fixture.World = new UnitWorld
                    {
                        UnitPrototypeTable = prototypes,
                        GlobalPrefabTable = fixture.PrefabTable,
                        StatDefinitionTable = CreateStatTable(),
                        PhysicsWorld = fixture.Physics,
                        TickRate = 30,
                    };
                    fixture.ClockRoot = new GameObject("KarolinaFrameSyncPipelineClock");
                    fixture.Clock = fixture.ClockRoot.AddComponent<UnityFrameClock>();
                    fixture.StartingFrame = Time.frameCount;
                    context.Record(
                        TestSessionLogLevel.Info,
                        "prepare",
                        "gameplay.runtime.prepared",
                        "已准备 UnitWorld、Prefab、PrototypeTable、PhysicsWorld 与 UnityFrameClock");
                    return new ValueTask<UnitSpawnFixture>(fixture);
                }
                catch
                {
                    DestroyFixture(fixture);
                    throw;
                }
            }

            public ValueTask InjectAsync(
                UnitSpawnFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 条件满足后由预编排运行器触发领域动作，不由模型或鼠标逐帧驱动。
                return new ValueTask();
            }

            private static ValueTask Spawn(UnitSpawnFixture fixture, TestPipelineContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tick = new SimulationTickContextController();
                tick.BeginTick(20, ExecutionMode.ServerAuthority);
                try
                {
                    UnitUid uid = fixture.World.SpawnUnit(new UnitSpawnRequest(
                        8910,
                        GameplayParticipantId.Explicit(8910),
                        TeamId.Neutral,
                        new fp2(3, 8),
                        new fp2(fp.one, fp.zero)));
                    if (!fixture.World.TryGetUnit(uid, out fixture.Unit))
                    {
                        throw new InvalidOperationException("UnitWorld 没有登记预编排生成的测试单位。");
                    }
                    fixture.SpawnFrame = Time.frameCount;
                    context.Record(
                        TestSessionLogLevel.Info,
                        "input",
                        "gameplay.unit.spawned",
                        "在 SimulationTick 20 通过 UnitWorld.SpawnUnit 注入预设单位",
                        new TestTimelinePosition("SimulationTick", 20),
                        new Dictionary<string, string>
                        {
                            ["unitPrototypeId"] = "8910",
                            ["spawnLogicTick"] = uid.SpawnLogicTick.ToString(),
                        });
                }
                finally
                {
                    tick.EndTick();
                }
                return new ValueTask();
            }

            public async ValueTask<PreparedRuntimeResult<UnitSpawnSample>> SampleAsync(
                UnitSpawnFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                var reproduction = new TestReproduction("framesync.unit.spawn.v1", "UnityFrame",
                    new Dictionary<string,string> { ["unitPrototypeId"]="8910", ["spawnTick"]="20", ["position"]="3,8" },
                    new Dictionary<string,string> { ["unityVersion"]=Application.unityVersion,
                        ["testingModule"]=typeof(PreparedRuntimeRunner).Module.ModuleVersionId.ToString(),
                        ["projectTestModule"]=typeof(KarolinaFrameSyncGameplayPipelineTests).Module.ModuleVersionId.ToString() });
                return await new PreparedRuntimeRunner().RunAsync(new UnitRuntimeAdapter(fixture, context),
                    new[] { new PreparedRuntimeAction<UnitSpawnSample>("spawn-unit", sample => !sample.UnitRegistered,
                        ct => Spawn(fixture, context, ct)) },
                    sample => fixture.SpawnFrame >= 0 && sample.FrameCount >= fixture.SpawnFrame + 3 && sample.UnitRegistered,
                    reproduction, context, 30, TimeSpan.FromSeconds(10), cancellationToken);
            }

            public ValueTask<IReadOnlyList<TestAssertionResult>> AnalyzeAsync(
                UnitSpawnFixture fixture,
                PreparedRuntimeResult<UnitSpawnSample> runtime,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // 按实际动作帧划分样本，不能过滤掉未注册的失败帧。
                var post = runtime.Samples.Where(s => fixture.SpawnFrame >= 0 && s.Value.FrameCount > fixture.SpawnFrame).ToArray();
                string[] sampleIds = post.Select(s => s.Id).ToArray();
                bool observed = post.Length >= 3;
                IReadOnlyList<TestAssertionResult> assertions = new[]
                {
                    Assertion("unity-frame-advanced", "预编排完成且动作后三帧均来自真实 Unity Update", runtime.Completed && observed && post.All(s => s.Value.FrameAdvanced) && post.Last().Value.FrameCount >= fixture.SpawnFrame + 3, sampleIds),
                    Assertion("unit-world-registration", "动作后三帧单位持续登记在 UnitWorld", observed && post.All(s => s.Value.UnitRegistered), sampleIds),
                    Assertion("physics-owner-registration", "动作后三帧 PhysicsWorld 所有者持续指向该单位", observed && post.All(s => s.Value.PhysicsRegistered), sampleIds),
                    Assertion("unit-handler-ownership", "动作后三帧 Handler 持续绑定到正确单位", observed && post.All(s => s.Value.HandlersBound), sampleIds),
                    Assertion("unit-spawn-position", "动作后三帧逻辑位置持续为3/8", observed && post.All(s => s.Value.PositionMatches), sampleIds),
                };
                return new ValueTask<IReadOnlyList<TestAssertionResult>>(assertions);
            }

            public async ValueTask CleanupAsync(
                UnitSpawnFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                DestroyFixture(fixture);
                // Destroy在帧末生效；等待Unity主线程实际销毁后再封存清理结果。
                // 此期限只保护测试环境，不作为Gameplay的权威时间。
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (fixture.Unit != null || fixture.Template != null || fixture.ClockRoot != null || fixture.PrefabTable != null)
                {
                    if (timeout.Elapsed > TimeSpan.FromSeconds(2))
                        throw new InvalidOperationException("自建测试对象未在清理期限内销毁，不能封存通过。");
                    await Task.Delay(1);
                }
                bool detached = fixture.World.StatDefinitionTable == null && fixture.World.UnitPrototypeTable == null &&
                    fixture.World.PhysicsWorld == null && fixture.World.GlobalPrefabTable == null;
                bool empty = fixture.World.GetAllUnits().Count == 0 && fixture.Physics.UnitEntities.Count == 0 && fixture.Physics.ProjectileEntities.Count == 0;
                context.Record(TestSessionLogLevel.Info,"cleanup","gameplay.cleanup.checked","实际对象销毁、世界注销、物理登记清空及配置引用解除检查",null,
                    new Dictionary<string,string>{["unitDestroyed"]=(fixture.Unit==null).ToString(),["prefabDestroyed"]=(fixture.Template==null).ToString(),
                        ["clockRootDestroyed"]=(fixture.ClockRoot==null).ToString(),["prefabTableDestroyed"]=(fixture.PrefabTable==null).ToString(),
                        ["worldUnits"]=fixture.World.GetAllUnits().Count.ToString(),["physicsUnits"]=fixture.Physics.UnitEntities.Count.ToString(),
                        ["physicsProjectiles"]=fixture.Physics.ProjectileEntities.Count.ToString(),["managedConfigurationDetached"]=detached.ToString()});
                if (!empty || !detached) throw new InvalidOperationException("测试世界或配置仍有残留，清理检查失败。");
                fixture.CleanupCompleted = true;
            }

            private static TestAssertionResult Assertion(string id, string title, bool passed, IEnumerable<string> sampleIds) =>
                new TestAssertionResult(
                    "gameplay." + id,
                    title,
                    passed ? TestAssertionStatus.Passed : TestAssertionStatus.Failed,
                    "True",
                    passed.ToString(), sampleIds);

            private sealed class UnitRuntimeAdapter : IPreparedRuntimeAdapter<UnitSpawnSample>
            {
                private readonly UnitSpawnFixture fixture;
                private readonly TestPipelineContext context;
                public UnitRuntimeAdapter(UnitSpawnFixture fixture, TestPipelineContext context)
                { this.fixture=fixture; this.context=context; }
                public TestTimelinePosition? Timeline => new TestTimelinePosition("UnityFrame", Time.frameCount);
                public async ValueTask WaitForNextSampleAsync(CancellationToken ct) => await fixture.Clock.WaitForNextFrameAsync(ct);
                public UnitSpawnSample Read()
                {
                    Unit resolved = null;
                    bool registered=fixture.Unit!=null && fixture.World.TryGetUnit(fixture.Unit.UnitUid,out resolved);
                    bool physicsRegistered=registered && fixture.Physics.UnitEntities.Count==1 &&
                        ReferenceEquals(fixture.Physics.UnitEntities[0].QueryInfo.Owner,fixture.Unit);
                    bool handlersBound=registered && ReferenceEquals(resolved.AttackHandler.Owner,resolved) && ReferenceEquals(resolved.BuffHandler.Owner,resolved);
                    bool positionMatches=registered && resolved.PhysicsEntity.Transform2D.Position.Equals(new fp2(3,8)) && resolved.RespawnPosition.Equals(new fp2(3,8));
                    context.Record(TestSessionLogLevel.Info,"sample","gameplay.unit.sampled","采样单位注册、物理所有者和Handler绑定",Timeline);
                    return new UnitSpawnSample(Time.frameCount>fixture.StartingFrame,registered,physicsRegistered,handlersBound,Time.frameCount,positionMatches);
                }
                public IReadOnlyDictionary<string,string> Describe(UnitSpawnSample sample) => new Dictionary<string,string>
                {
                    ["unitRegistered"]=sample.UnitRegistered.ToString(), ["physicsRegistered"]=sample.PhysicsRegistered.ToString(),
                    ["handlersBound"]=sample.HandlersBound.ToString(), ["unityFrame"]=sample.FrameCount.ToString(),
                    ["positionMatches"]=sample.PositionMatches.ToString(), ["phase"]=fixture.SpawnFrame<0 || sample.FrameCount<=fixture.SpawnFrame?"动作前基线":"动作后",
                    ["position"]=sample.UnitRegistered?fixture.Unit.PhysicsEntity.Transform2D.Position.ToString():null
                };
            }

            private static void DestroyFixture(UnitSpawnFixture fixture)
            {
                if (fixture.World != null && fixture.Unit != null &&
                    fixture.World.TryGetUnit(fixture.Unit.UnitUid, out Unit registered) &&
                    ReferenceEquals(registered, fixture.Unit))
                {
                    var tick = new SimulationTickContextController();
                    tick.BeginTick(21, ExecutionMode.ServerAuthority);
                    try
                    {
                        fixture.World.DespawnUnit(new UnitDespawnRequest(
                            fixture.Unit.UnitUid,
                            UnitDespawnReason.ScriptedCleanup,
                            UnitDespawnMode.Destroy));
                    }
                    finally
                    {
                        tick.EndTick();
                    }
                }

                if (fixture.ClockRoot != null) UnityEngine.Object.Destroy(fixture.ClockRoot);
                if (fixture.Template != null) UnityEngine.Object.Destroy(fixture.Template);
                if (fixture.PrefabTable != null) UnityEngine.Object.Destroy(fixture.PrefabTable);
                // 这些表和物理世界是普通托管对象；解除自建世界引用后由GC回收。
                if (fixture.World != null)
                {
                    fixture.World.PhysicsWorld = null;
                    fixture.World.UnitPrototypeTable = null;
                    fixture.World.StatDefinitionTable = null;
                    fixture.World.GlobalPrefabTable = null;
                }
            }

            private static GameObject CreateTemplate()
            {
                var root = new GameObject("KarolinaPipelineUnitTemplate");
                root.AddComponent<PhysicsEntity2D>();
                root.AddComponent<StatHandler>();
                root.AddComponent<MovementHandler>();
                root.AddComponent<AttackHandler>();
                root.AddComponent<AbilityHandler>();
                root.AddComponent<BuffHandler>();
                root.AddComponent<CrowdControlHandler>();
                root.AddComponent<EquipmentHandler>();
                root.AddComponent<FrameSyncMoba.Unit.Unit>();
                return root;
            }

            private static StatPreset CreatePreset()
            {
                var preset = new StatPreset();
                AddStat(preset, StatId.MaxHealth, 100);
                AddStat(preset, StatId.AttackDamage, 100);
                AddStat(preset, StatId.AttackSpeed, 30);
                AddStat(preset, StatId.AttackRange, 200);
                return preset;
            }

            private static StatDefinitionTable CreateStatTable()
            {
                var table = new StatDefinitionTable();
                AddDefinition(table, StatId.MaxHealth);
                AddDefinition(table, StatId.AttackDamage);
                AddDefinition(table, StatId.AttackSpeed);
                AddDefinition(table, StatId.AttackRange);
                return table;
            }

            private static void AddStat(StatPreset preset, StatId id, int value) =>
                preset.Stats.Add(new StatPresetEntry
                {
                    StatId = id,
                    BaseValue = value,
                });

            private static void AddDefinition(StatDefinitionTable table, StatId id) =>
                table.Add(new StatDefinition
                {
                    Id = id,
                    DebugName = id.ToString(),
                    SupportsLevelGrowth = true,
                });
        }

        private sealed class UnitSpawnFixture
        {
            public GameObject Template;
            public GlobalPrefabTable PrefabTable;
            public UnitWorld World;
            public PhysicsWorld Physics;
            public GameObject ClockRoot;
            public UnityFrameClock Clock;
            public Unit Unit;
            public int StartingFrame;
            public int SpawnFrame = -1;
            public bool CleanupCompleted;
        }

        private readonly struct UnitSpawnSample
        {
            public readonly bool FrameAdvanced;
            public readonly bool UnitRegistered;
            public readonly bool PhysicsRegistered;
            public readonly bool HandlersBound;
            public readonly int FrameCount;
            public readonly bool PositionMatches;

            public UnitSpawnSample(
                bool frameAdvanced,
                bool unitRegistered,
                bool physicsRegistered,
                bool handlersBound,
                int frameCount, bool positionMatches)
            {
                FrameAdvanced = frameAdvanced;
                UnitRegistered = unitRegistered;
                PhysicsRegistered = physicsRegistered;
                HandlersBound = handlersBound;
                FrameCount = frameCount;
                PositionMatches = positionMatches;
            }
        }
    }
}
