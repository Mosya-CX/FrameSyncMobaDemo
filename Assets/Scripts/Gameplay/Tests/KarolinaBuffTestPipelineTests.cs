using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;
using Karolina.Testing;
using Karolina.Testing.Pipeline;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.TestTools;

namespace FrameSyncMoba.Unit.Tests
{
    public sealed class KarolinaBuffTestPipelineTests
    {
        [Test]
        public void DestroyWorldFixtureDoesNotTouchOtherWorld()
        {
            var firstWorld = new UnitWorld
            {
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };
            var secondWorld = new UnitWorld
            {
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };
            var firstPrototype = CreateTestPrototype(8902);
            var secondPrototype = CreateTestPrototype(8903);

            try
            {
                Unit firstUnit = firstWorld.SpawnUnit(
                    firstPrototype,
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);
                Unit secondUnit = secondWorld.SpawnUnit(
                    secondPrototype,
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);

                UnitTestFactory.DestroyWorldFixture(firstWorld);

                Assert.That(firstWorld.GetAllUnits().Count, Is.EqualTo(0));
                Assert.That(firstUnit == null, Is.True);
                Assert.That(secondWorld.GetAllUnits().Count, Is.EqualTo(1));
                Assert.That(secondWorld.TryGetUnit(secondUnit.UnitUid, out Unit registered), Is.True);
                Assert.That(ReferenceEquals(registered, secondUnit), Is.True);
                Assert.That(secondUnit != null, Is.True);
            }
            finally
            {
                UnitTestFactory.DestroyWorldFixture(firstWorld);
                UnitTestFactory.DestroyWorldFixture(secondWorld);
            }
        }

        [Test]
        public void DestroyCreatedObjectsCleansSpawnedUnitsAcrossWorlds()
        {
            var firstWorld = new UnitWorld
            {
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };
            var secondWorld = new UnitWorld
            {
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };

            try
            {
                Unit firstUnit = firstWorld.SpawnUnit(
                    CreateTestPrototype(8905),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);
                Unit secondUnit = secondWorld.SpawnUnit(
                    CreateTestPrototype(8906),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);

                UnitTestFactory.DestroyCreatedObjects();

                Assert.That(firstWorld.GetAllUnits().Count, Is.EqualTo(0));
                Assert.That(secondWorld.GetAllUnits().Count, Is.EqualTo(0));
                Assert.That(firstUnit == null, Is.True);
                Assert.That(secondUnit == null, Is.True);
            }
            finally
            {
                UnitTestFactory.DestroyWorldFixture(firstWorld);
                UnitTestFactory.DestroyWorldFixture(secondWorld);
                UnitTestFactory.DestroyCreatedObjects();
            }
        }

        [Test]
        public void DestroyWorldFixtureRestoresCallerOwnedPrefabTable()
        {
            var originalPrefab = new GameObject("CallerOwnedPrefab");
            GlobalPrefabTable originalTable =
                ScriptableObject.CreateInstance<GlobalPrefabTable>();
            originalTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(PrefabKind.Unit, new[]
                {
                    new PrefabEntry(7801, originalPrefab),
                }),
            });
            originalTable.ReplaceRangesForTests(new[]
            {
                new PrefabKindRangeConfig
                {
                    Kind = PrefabKind.Unit,
                    IdRangeStart = 7801,
                    IdRangeEnd = 7801,
                },
            });
            var world = new UnitWorld
            {
                GlobalPrefabTable = originalTable,
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };

            try
            {
                world.SpawnUnit(
                    CreateTestPrototype(8907),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);

                world.GlobalPrefabTable = null;
                Assert.Throws<InvalidOperationException>(() =>
                    UnitTestFactory.ConfigureWorldForPrototype(
                        world,
                        CreateTestPrototype(8908),
                        fp.zero,
                        fp.zero));

                UnitTestFactory.DestroyWorldFixture(world);

                Assert.That(ReferenceEquals(world.GlobalPrefabTable, originalTable), Is.True);
                Assert.That(originalTable.PrefabGroups.Count, Is.EqualTo(1));
                Assert.That(originalTable.TryGetPrefab(
                    PrefabKind.Unit,
                    7801,
                    out GameObject restoredPrefab), Is.True);
                Assert.That(ReferenceEquals(restoredPrefab, originalPrefab), Is.True);
                Assert.That(originalTable.TryGetPrefab(
                    PrefabKind.Unit,
                    8907,
                    out _), Is.False);
                Assert.That(originalTable.KindRanges.Count, Is.EqualTo(1));
                Assert.That(originalTable.KindRanges[0].IdRangeStart, Is.EqualTo(7801));
                Assert.That(originalTable.KindRanges[0].IdRangeEnd, Is.EqualTo(7801));
            }
            finally
            {
                UnitTestFactory.DestroyWorldFixture(world);
                if (originalPrefab != null)
                {
                    UnityEngine.Object.DestroyImmediate(originalPrefab);
                }
                if (originalTable != null)
                {
                    UnityEngine.Object.DestroyImmediate(originalTable);
                }
            }
        }

        [Test]
        public void DestroyWorldFixtureClearsSurvivingUnitIntentToRemovedTestUnit()
        {
            var world = new UnitWorld
            {
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };
            Unit existingUnit = null;
            UnitUid existingUid = default;
            SimulationTickContextController tickController = null;

            try
            {
                var existingPrototype = CreateTestPrototype(8908);
                UnitTestFactory.ConfigureWorldForPrototype(
                    world,
                    existingPrototype,
                    fp.zero,
                    fp.zero);
                tickController = new SimulationTickContextController();
                tickController.BeginTick(1, ExecutionMode.ServerAuthority);
                existingUid = world.SpawnUnit(new UnitSpawnRequest(
                    existingPrototype.UnitPrototypeId,
                    GameplayParticipantId.Explicit(1),
                    new TeamId(1),
                    fp2.zero,
                    new fp2(fp.one, fp.zero)));
                Assert.That(world.TryGetUnit(existingUid, out existingUnit), Is.True);

                Unit testTarget = world.SpawnUnit(
                    CreateTestPrototype(8909),
                    new TeamId(2),
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);
                var grid = new PathGridMap2D();
                grid.Initialise(
                    new fp2((fp)(-5), (fp)(-5)),
                    new fp2((fp)10, (fp)10),
                    fp.one);
                existingUnit.Locomotion =
                    new UnitLocomotionAgent(existingUnit, grid);
                tickController.EndTick();
                tickController.BeginTick(2, ExecutionMode.ServerAuthority);
                existingUnit.StatHandler.SetStat(
                    StatId.AttackDamage,
                    (fp)100);
                existingUnit.StatHandler.SetStat(
                    StatId.AttackSpeed,
                    (fp)30);
                existingUnit.StatHandler.SetStat(
                    StatId.AttackRange,
                    (fp)200);
                Assert.That(
                    existingUnit.Arbiter.Submit(
                        new AttackActionRequest(testTarget.UnitUid)).IsGranted,
                    Is.True);
                Assert.That(
                    existingUnit.Locomotion.AcceptRouteRequest(
                        RouteMoveRequest.FollowUnit(
                            testTarget.UnitUid,
                            (fp)1m,
                            MovePurpose.ChaseForAttack)),
                    Is.EqualTo(MoveAcceptResult.Accepted));
                existingUnit.Planner.SetIntent(new UnitIntent
                {
                    Kind = IntentKind.AttackTarget,
                    TargetUnit = testTarget.UnitUid,
                });

                UnitTestFactory.DestroyWorldFixture(world);

                Assert.That(world.GetAllUnits().Count, Is.EqualTo(1));
                Assert.That(world.TryGetUnit(existingUid, out Unit stillRegistered), Is.True);
                Assert.That(ReferenceEquals(stillRegistered, existingUnit), Is.True);
                Assert.That(existingUnit.Planner.CurrentIntent.Kind, Is.EqualTo(IntentKind.None));
                Assert.That(
                    existingUnit.AttackHandler.CurrentTargetUid.IsValid(),
                    Is.False);
                Assert.That(existingUnit.ActionRuntimes.Main.IsOccupied, Is.False);
                Assert.That(
                    existingUnit.Locomotion.CurrentTask.State,
                    Is.EqualTo(MovementTaskState.Idle));
                Assert.That(world.TryGetUnit(testTarget.UnitUid, out _), Is.False);
                tickController.EndTick();
                tickController = null;
            }
            finally
            {
                if (tickController == null)
                {
                    UnitTestFactory.DestroyWorldFixture(world);
                }
                else
                {
                    UnitTestFactory.DestroyWorldFixture(world);
                    tickController.EndTick();
                }
                if (existingUnit != null && world.TryGetUnit(existingUid, out _))
                {
                    var cleanupTick = new SimulationTickContextController();
                    cleanupTick.BeginTick(2, ExecutionMode.ServerAuthority);
                    try
                    {
                        world.DespawnUnit(new UnitDespawnRequest(
                            existingUid,
                            UnitDespawnReason.ScriptedCleanup,
                            UnitDespawnMode.Destroy));
                    }
                    finally
                    {
                        cleanupTick.EndTick();
                    }
                }
                UnitTestFactory.DestroyCreatedObjects();
            }
        }

        [Test]
        public void CallerOwnedPrefabTableRejectsConcurrentWorldFixtures()
        {
            var originalPrefab = new GameObject("SharedCallerOwnedPrefab");
            GlobalPrefabTable sharedTable =
                ScriptableObject.CreateInstance<GlobalPrefabTable>();
            sharedTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(PrefabKind.Unit, new[]
                {
                    new PrefabEntry(7810, originalPrefab),
                }),
            });
            sharedTable.ReplaceRangesForTests(new[]
            {
                new PrefabKindRangeConfig
                {
                    Kind = PrefabKind.Unit,
                    IdRangeStart = 7810,
                    IdRangeEnd = 7810,
                },
            });
            var firstWorld = new UnitWorld
            {
                GlobalPrefabTable = sharedTable,
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };
            var secondWorld = new UnitWorld
            {
                GlobalPrefabTable = sharedTable,
                StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
            };

            try
            {
                firstWorld.SpawnUnit(
                    CreateTestPrototype(8910),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);

                Assert.Throws<InvalidOperationException>(() => secondWorld.SpawnUnit(
                    CreateTestPrototype(8911),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero));
                Assert.Throws<InvalidOperationException>(() =>
                    UnitTestFactory.AddProjectilePrefab(secondWorld, 8912));
                Assert.That(sharedTable.PrefabGroups.Count, Is.EqualTo(1));
                Assert.That(sharedTable.TryGetPrefab(
                    PrefabKind.Unit,
                    8910,
                    out _), Is.True);
                Assert.That(sharedTable.TryGetPrefab(
                    PrefabKind.Unit,
                    8911,
                    out _), Is.False);

                UnitTestFactory.DestroyWorldFixture(firstWorld);
                secondWorld.SpawnUnit(
                    CreateTestPrototype(8911),
                    TeamId.Neutral,
                    currentLogicTick: 1,
                    statGrowthC: fp.zero,
                    statGrowthD: fp.zero);
                UnitTestFactory.AddProjectilePrefab(secondWorld, 8912);
                Assert.That(sharedTable.PrefabGroups.Count, Is.EqualTo(2));
                Assert.That(sharedTable.PrefabGroups[1].Kind,
                    Is.EqualTo(PrefabKind.Projectile));
                Assert.That(sharedTable.PrefabGroups[1].Entries[0].PrefabId,
                    Is.EqualTo(8912));
                UnitTestFactory.DestroyWorldFixture(secondWorld);

                Assert.That(sharedTable.TryGetPrefab(
                    PrefabKind.Unit,
                    7810,
                    out GameObject restored), Is.True);
                Assert.That(ReferenceEquals(restored, originalPrefab), Is.True);
                Assert.That(sharedTable.TryGetPrefab(
                    PrefabKind.Unit,
                    8910,
                    out _), Is.False);
                Assert.That(sharedTable.TryGetPrefab(
                    PrefabKind.Unit,
                    8911,
                    out _), Is.False);
            }
            finally
            {
                UnitTestFactory.DestroyWorldFixture(firstWorld);
                UnitTestFactory.DestroyWorldFixture(secondWorld);
                UnitTestFactory.DestroyCreatedObjects();
                if (originalPrefab != null)
                    UnityEngine.Object.DestroyImmediate(originalPrefab);
                if (sharedTable != null)
                    UnityEngine.Object.DestroyImmediate(sharedTable);
            }
        }

        [UnityTest]
        public IEnumerator HealOverTimeScenario_UsesProjectRuntimeAndSealsEvidence()
        {
            Task execution = RunPipelineAndAssertAsync();
            while (!execution.IsCompleted)
                yield return null;

            if (execution.IsFaulted)
            {
                Assert.Fail(execution.Exception?.GetBaseException().ToString() ?? "测试流水线异常结束。");
                yield break;
            }
            if (execution.IsCanceled)
            {
                Assert.Fail("测试流水线被取消。");
                yield break;
            }
            execution.GetAwaiter().GetResult();
        }

        private static UnitPrototype CreateTestPrototype(int id) => new UnitPrototype
        {
            UnitPrototypeId = id,
            RuntimeEntityPrefabId = id,
            UnitKind = UnitKind.Hero,
            UnitSubKindId = 0,
            Loadout = HandlerLoadout.DefaultHero,
            BaseStats = UnitTestFactory.CreateDefaultPreset(),
            BaseGoldValue = 300,
            BaseExperienceValue = 100,
        };

        private static async Task RunPipelineAndAssertAsync()
        {
            using var stream = new MemoryStream();
            var log = new AsyncTestSessionLog(stream, leaveOpen: true);
            var scenario = new HealOverTimeScenario();
            TestPipelineExecutionResult result;
            try
            {
                result = await new TestPipelineExecutor().RunAsync(
                    scenario,
                    new TestSessionIdentity(
                        Guid.NewGuid().ToString("N"),
                        scenario.Id,
                        "attempt-1",
                        1),
                    log);
            }
            finally
            {
                await log.DisposeAsync();
            }

            string diagnostics = Encoding.UTF8.GetString(stream.ToArray());
            Assert.That(result.Status, Is.EqualTo(TestPipelineStatus.Passed), result.ErrorMessage);
            Assert.That(result.Passed, Is.True);
            Assert.That(result.Assertions.Count, Is.EqualTo(6));
            string[] lines = diagnostics.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            Assert.That(CountEvent(lines, "pipeline.started"), Is.EqualTo(1));
            Assert.That(CountEvent(lines, "buff.applied"), Is.EqualTo(1));
            Assert.That(CountEvent(lines, "buff.sample"), Is.EqualTo(4));
            Assert.That(CountEvent(lines, "assertion.passed"), Is.EqualTo(6));
            Assert.That(CountEvent(lines, "pipeline.machine-execution.ended"), Is.EqualTo(1));
            for (int tick = 2; tick <= 5; tick++)
            {
                Assert.That(HasEvent(lines, "buff.sample", e => e.timeline != null &&
                    e.timeline.domain == "SimulationTick" && e.timeline.value == tick), Is.True,
                    "缺少 SimulationTick " + tick + " 的诊断采样记录。");
            }
            Assert.That(HasEvent(
                lines,
                "pipeline.machine-execution.ended",
                e => e.data != null && e.data.cleanupComplete == "True"), Is.True);

            Assert.That(scenario.LastFixture, Is.Not.Null);
            Assert.That(scenario.LastFixture.CleanupCompleted, Is.True);
            Assert.That(scenario.LastFixture.Unit == null, Is.True,
                "清理后测试单位的 Unity 对象仍然存在。");
            Assert.That(scenario.LastFixture.Definition == null, Is.True,
                "清理后测试 Buff 资源仍然存在。");
            Assert.That(scenario.LastFixture.World.GlobalPrefabTable == null, Is.True,
                "清理后测试创建的 Prefab 表仍然存在。");
            Assert.That(scenario.LastFixture.TickContext.IsTickActive, Is.False);
        }

        private static int CountEvent(string[] lines, string eventType)
        {
            int count = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (JsonUtility.FromJson<DiagnosticProbe>(lines[i]).eventType == eventType)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool HasEvent(
            string[] lines,
            string eventType,
            Func<DiagnosticProbe, bool> matches)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var record = JsonUtility.FromJson<DiagnosticProbe>(lines[i]);
                if (record.eventType == eventType && matches(record))
                {
                    return true;
                }
            }
            return false;
        }

        [Serializable]
        private sealed class DiagnosticProbe
        {
            public string eventType = null;
            public TimelineProbe timeline = null;
            public CleanupProbe data = null;
        }
        [Serializable]
        private sealed class TimelineProbe
        {
            public string domain = null;
            public long value = 0;
        }
        [Serializable]
        private sealed class CleanupProbe
        {
            public string cleanupComplete = null;
        }

        private sealed class HealOverTimeScenario : ITestPipelineScenario<BuffFixture, BuffSample>
        {
            public string Id => "framesync.buff.heal-over-time";
            public string Title => "FrameSyncMoba Buff 周期治疗集成验证";
            public BuffFixture LastFixture { get; private set; }

            public ValueTask<BuffFixture> PrepareAsync(
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fixture = new BuffFixture
                {
                    TickContext = new SimulationTickContextController(),
                    World = new UnitWorld
                    {
                        StatDefinitionTable = UnitTestFactory.CreateDefaultStatTable(),
                    },
                };
                LastFixture = fixture;

                try
                {
                    fixture.TickContext.BeginTick(1, ExecutionMode.ServerAuthority);
                    fixture.Combat = new CombatSystem(fixture.World, 300, 60);
                    fixture.World.CombatSystem = fixture.Combat;
                    fixture.Definitions = new BuffDefinitionRegistry();
                    fixture.World.BuffDefinitions = fixture.Definitions;

                    var prototype = new UnitPrototype
                    {
                        UnitPrototypeId = 8901,
                        Name = "KarolinaPipelineTestUnit",
                        RuntimeEntityPrefabId = 8901,
                        UnitKind = UnitKind.Hero,
                        UnitSubKindId = 0,
                        Loadout = HandlerLoadout.DefaultHero,
                        BaseStats = CreateFullStatPreset(),
                        BaseGoldValue = 300,
                        BaseExperienceValue = 100,
                    };
                    fixture.Unit = UnitTestFactory.SpawnUnit(
                        fixture.World,
                        prototype,
                        TeamId.Neutral,
                        currentLogicTick: 1,
                        statGrowthC: fp.zero,
                        statGrowthD: fp.zero);
                    fixture.Unit.StatHandler.SetCurrentHealth((fp)50);
                    fixture.InitialHealth = fixture.Unit.StatHandler.CurrentHealth;
                    fixture.Definition = CreateHealBuff();
                    fixture.Definitions.Register(fixture.Definition);

                    new TestReproduction("framesync.buff.heal.v1", "SimulationTick",
                        new Dictionary<string,string> { ["initialHealth"]="50", ["healPerTick"]="10", ["periodTicks"]="2", ["sampleTicks"]="2,3,4,5" },
                        new Dictionary<string,string> { ["unityVersion"]=Application.unityVersion,
                            ["testingModule"]=typeof(TestPipelineExecutor).Module.ModuleVersionId.ToString(),
                            ["projectTestModule"]=typeof(KarolinaBuffTestPipelineTests).Module.ModuleVersionId.ToString() }).Record(context);

                    context.Record(
                        TestSessionLogLevel.Info,
                        "prepare",
                        "buff.fixture.prepared",
                        "已准备单位、CombatSystem、Buff 注册表和初始生命值",
                        new TestTimelinePosition("SimulationTick", 1),
                        new Dictionary<string, string>
                        {
                            ["initialHealth"] = fixture.InitialHealth.ToString(),
                            ["unitPrototypeId"] = prototype.UnitPrototypeId.ToString(),
                        });
                    return new ValueTask<BuffFixture>(fixture);
                }
                catch
                {
                    CleanupFixture(fixture);
                    throw;
                }
            }

            public ValueTask InjectAsync(
                BuffFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool applied = fixture.Unit.BuffHandler.Apply(
                    fixture.Definition.ConfigId,
                    fixture.Definition,
                    fixture.Unit.UnitUid);
                if (!applied)
                    throw new InvalidOperationException("BuffHandler 拒绝了预设周期治疗 Buff。");

                context.Record(
                    TestSessionLogLevel.Info,
                    "input",
                    "buff.applied",
                    "通过 BuffHandler.Apply 注入周期治疗 Buff",
                    data: new Dictionary<string, string>
                    {
                        ["configId"] = fixture.Definition.ConfigId.Value.ToString(),
                    });
                return new ValueTask();
            }

            public ValueTask<BuffSample> SampleAsync(
                BuffFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (fixture.TickContext.IsTickActive)
                    fixture.TickContext.EndTick();

                var samples = new List<fp>(4);
                for (int tick = 2; tick <= 5; tick++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fixture.TickContext.BeginTick(tick, ExecutionMode.ServerAuthority);
                    try
                    {
                        fixture.Combat.BeginTick();
                        fixture.Unit.BuffHandler.Advance();
                        fixture.Combat.SettleActiveRequests();
                        fixture.Combat.EndTick();

                        fp health = fixture.Unit.StatHandler.CurrentHealth;
                        samples.Add(health);
                        context.Record(
                            TestSessionLogLevel.Info,
                            "sample",
                            "buff.sample",
                            "采集当前生命值",
                            new TestTimelinePosition("SimulationTick", tick),
                            new Dictionary<string, string>
                            {
                                ["health"] = health.ToString(),
                                ["sampleId"] = "health-tick-" + tick,
                            });
                    }
                    finally
                    {
                        if (fixture.TickContext.IsTickActive)
                            fixture.TickContext.EndTick();
                    }
                }

                return new ValueTask<BuffSample>(new BuffSample(
                    fixture.InitialHealth,
                    samples[samples.Count - 1],
                    samples.ToArray()));
            }

            public ValueTask<IReadOnlyList<TestAssertionResult>> AnalyzeAsync(
                BuffFixture fixture,
                BuffSample sample,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var assertions = new List<TestAssertionResult>(new[]
                {
                    new TestAssertionResult(
                        "buff.heal-increased",
                        "周期 Buff 应提高单位生命值",
                        sample.FinalHealth > sample.InitialHealth
                            ? TestAssertionStatus.Passed
                            : TestAssertionStatus.Failed,
                        "最终生命值高于初始生命值",
                        sample.InitialHealth + " -> " + sample.FinalHealth,
                        new[] { "health-tick-2", "health-tick-3", "health-tick-4", "health-tick-5" }),
                    new TestAssertionResult(
                        "buff.sample-count",
                        "采样覆盖预设的四个逻辑 Tick",
                        sample.SampleCount == 4
                            ? TestAssertionStatus.Passed
                            : TestAssertionStatus.Failed,
                        "4 个 Tick 样本",
                        sample.SampleCount.ToString(),
                        new[] { "health-tick-2", "health-tick-3", "health-tick-4", "health-tick-5" }),
                });
                // Tick 1 应用，周期为 2，因此首次治疗 Tick 3，第二次 Tick 5。
                int[] expectedHealth = { 50, 60, 60, 70 };
                for (int i = 0; i < expectedHealth.Length; i++)
                {
                    bool exists = i < sample.HealthSamples.Count;
                    assertions.Add(new TestAssertionResult(
                        "buff.health-tick-" + (i + 2), "Tick " + (i + 2) + " 生命值符合治疗周期",
                        exists && sample.HealthSamples[i] == (fp)expectedHealth[i] ? TestAssertionStatus.Passed : TestAssertionStatus.Failed,
                        expectedHealth[i].ToString(), exists ? sample.HealthSamples[i].ToString() : "样本缺失",
                        new[] { "health-tick-" + (i + 2) }));
                }
                return new ValueTask<IReadOnlyList<TestAssertionResult>>(assertions);
            }

            public ValueTask CleanupAsync(
                BuffFixture fixture,
                TestPipelineContext context,
                CancellationToken cancellationToken)
            {
                CleanupFixture(fixture);
                bool completed = fixture.CleanupCompleted && fixture.Unit == null && fixture.Definition == null &&
                    fixture.World.GetAllUnits().Count == 0 && fixture.World.GlobalPrefabTable == null && !fixture.TickContext.IsTickActive;
                context.Record(TestSessionLogLevel.Info,"cleanup","buff.cleanup.checked","实际核对隔离世界、单位、Buff定义、Prefab表与Tick清理",null,
                    new Dictionary<string,string>{["cleanupCompleted"]=fixture.CleanupCompleted.ToString(),["unitDestroyed"]=(fixture.Unit==null).ToString(),
                        ["definitionDestroyed"]=(fixture.Definition==null).ToString(),["worldUnits"]=fixture.World.GetAllUnits().Count.ToString(),
                        ["prefabTableDestroyed"]=(fixture.World.GlobalPrefabTable==null).ToString(),["tickActive"]=fixture.TickContext.IsTickActive.ToString()});
                if(!completed)throw new InvalidOperationException("Buff测试夹具清理仍有残留，不能封存通过。");
                return new ValueTask();
            }

            private static void CleanupFixture(BuffFixture fixture)
            {
                if (fixture.TickContext != null && fixture.TickContext.IsTickActive)
                    fixture.TickContext.EndTick();
                UnitTestFactory.DestroyWorldFixture(fixture.World);
                if (fixture.Definition != null)
                    UnityEngine.Object.DestroyImmediate(fixture.Definition);
                fixture.CleanupCompleted = true;
            }

            private static BuffDefinition CreateHealBuff()
            {
                var definition = ScriptableObject.CreateInstance<BuffDefinition>();
                definition.ConfigId = new BuffConfigId(8901);
                definition.Life = new BuffLifeRuleConfig
                {
                    Infinite = false,
                    DurationSeconds = 20f / 30f,
                    RefreshMode = BuffRefreshMode.RefreshToFull,
                };
                definition.Stack = new BuffStackRuleConfig
                {
                    MaxStacks = 1,
                    AddMode = BuffAddMode.Ignore,
                    ReduceMode = BuffReduceMode.Reduce,
                };
                definition.PeriodicIntervalTicks = 2;
                definition.InitialStacks = 1;
                definition.Effects = new[]
                {
                    new BuffEffectConfig
                    {
                        Effect = new HealOverTimeBuffEffect
                        {
                            HealPerTick = (fp)10,
                        },
                    },
                };
                return definition;
            }

            private static StatPreset CreateFullStatPreset()
            {
                var preset = new StatPreset();
                Array values = Enum.GetValues(typeof(StatId));
                for (int i = 0; i < values.Length; i++)
                {
                    StatId statId = (StatId)values.GetValue(i);
                    fp baseValue = statId == StatId.MaxHealth
                        ? (fp)100
                        : statId == StatId.HealingReceivedRatio
                            ? fp.one
                            : fp.zero;
                    preset.Stats.Add(new StatPresetEntry
                    {
                        StatId = statId,
                        BaseValue = baseValue,
                        GrowthValue = fp.zero,
                    });
                }
                return preset;
            }
        }

        private sealed class BuffFixture
        {
            public SimulationTickContextController TickContext;
            public UnitWorld World;
            public CombatSystem Combat;
            public BuffDefinitionRegistry Definitions;
            public Unit Unit;
            public BuffDefinition Definition;
            public fp InitialHealth;
            public bool CleanupCompleted;
        }

        private readonly struct BuffSample
        {
            public readonly fp InitialHealth;
            public readonly fp FinalHealth;
            public readonly int SampleCount;
            public readonly IReadOnlyList<fp> HealthSamples;

            public BuffSample(fp initialHealth, fp finalHealth, fp[] samples)
            {
                InitialHealth = initialHealth;
                FinalHealth = finalHealth;
                HealthSamples = Array.AsReadOnly((fp[])samples.Clone());
                SampleCount = samples.Length;
            }
        }
    }
}
