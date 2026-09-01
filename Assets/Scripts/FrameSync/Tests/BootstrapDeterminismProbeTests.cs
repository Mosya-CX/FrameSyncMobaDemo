using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;
using UnityEditor;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync.Tests
{
    /// <summary>
    /// Reproduces the packaged-client checksum divergence in the editor:
    /// builds two identical worlds from the real FullMatchTest configuration,
    /// runs the server path (authoritative build + first tick) against the
    /// client path (restore initial snapshot + prediction tick) and compares
    /// the UnitWorld checksum segments down to per-unit Stats sub-segments.
    /// </summary>
    [TestFixture]
    public sealed class BootstrapDeterminismProbeTests
    {
        private const string GlobalDataPath =
            "Assets/Config/Formal/GlobalGameplayData.asset";
        private const string UnitCatalogPath =
            "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset";
        private const string AbilityCatalogPath =
            "Assets/Config/Formal/Abilities/VarusAbilityRuntimeCatalog.asset";
        private const string BuffCatalogPath =
            "Assets/Config/Formal/Buffs/FullMatchTestBuffCatalog.asset";
        private const string MatchContentRoot =
            "Assets/Config/Formal/MatchContent/";

        private readonly System.Collections.Generic.List<UnitType>
            spawnedUnits =
                new System.Collections.Generic.List<UnitType>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0;
                 i < spawnedUnits.Count;
                 i++)
            {
                if (spawnedUnits[i] != null &&
                    spawnedUnits[i].gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        spawnedUnits[i].gameObject);
                }
            }
            spawnedUnits.Clear();
        }

        [Test]
        public void ServerFirstTick_MatchesClientPredictionFirstTick()
        {
            BakedGlobalGameplayData baked =
                AssetDatabase
                    .LoadAssetAtPath<GlobalGameplayData>(
                        GlobalDataPath)
                    .BakeOrThrow();
            UnitRuntimeCatalogAsset unitCatalog =
                AssetDatabase.LoadAssetAtPath<
                    UnitRuntimeCatalogAsset>(
                    UnitCatalogPath);
            AbilityRuntimeCatalogAsset abilityCatalog =
                AssetDatabase.LoadAssetAtPath<
                    AbilityRuntimeCatalogAsset>(
                    AbilityCatalogPath);
            BuffCatalogAsset buffCatalog =
                AssetDatabase.LoadAssetAtPath<
                    BuffCatalogAsset>(
                    BuffCatalogPath);
            GlobalPrefabTable resolvedPrefabTable =
                BuildResolvedFormalPrefabTable(
                    baked.PrefabTable);

            // Server: authoritative build then execute first tick.
            UnitWorld server = CreateWorld(
                baked,
                resolvedPrefabTable,
                unitCatalog,
                abilityCatalog,
                buffCatalog);
            var serverRuntime =
                new FrameSyncGameRuntime(
                    server,
                    server.PhysicsWorld,
                    baked);
            QueueHeroes(serverRuntime);
            serverRuntime.ConfigureMatchStart(
                3,
                12345u,
                2,
                baked.InitialEarnedGold);
            UnitUid[] serverSpawned =
                serverRuntime
                    .MaterializeInitialSpawnsForBootstrap(
                        3);
            serverRuntime.ConfigurePlayerSlotMappings(
                new[]
                {
                    new PlayerSlotUnitMapping(
                        0,
                        serverSpawned[0]),
                    new PlayerSlotUnitMapping(
                        1,
                        serverSpawned[1]),
                });
            GameplaySnapshot initialSnapshot =
                serverRuntime.TickPipeline
                    .CaptureAggregateSnapshot();

            var controller =
                new SimulationTickContextController();
            serverRuntime.TickPipeline.ExecuteTick(
                controller,
                ExecutionMode.ServerAuthority);
            GameplaySnapshot serverAfterFirstTick =
                serverRuntime.TickPipeline
                    .CaptureAggregateSnapshot();

            // Client: restore the authoritative initial snapshot, then run
            // the same first tick under ClientPrediction.
            UnitWorld client = CreateWorld(
                baked,
                resolvedPrefabTable,
                unitCatalog,
                abilityCatalog,
                buffCatalog);
            var clientRuntime =
                new FrameSyncGameRuntime(
                    client,
                    client.PhysicsWorld,
                    baked);
            clientRuntime.ConfigureMatchStart(
                3,
                12345u,
                2,
                baked.InitialEarnedGold);
            clientRuntime.RestoreInitialSnapshot(
                initialSnapshot,
                3,
                ExecutionMode.ClientPrediction);
            clientRuntime.TickPipeline.ExecuteTick(
                controller,
                ExecutionMode.ClientPrediction);
            GameplaySnapshot clientAfterFirstTick =
                clientRuntime.TickPipeline
                    .CaptureAggregateSnapshot();

            CompareUnitWorld(
                serverAfterFirstTick,
                clientAfterFirstTick,
                serverRuntime,
                clientRuntime);
        }

        private static void CompareUnitWorld(
            GameplaySnapshot server,
            GameplaySnapshot client,
            FrameSyncGameRuntime serverRuntime,
            FrameSyncGameRuntime clientRuntime)
        {
            SharedGameplayChecksum.ChecksumSegment[] serverSegments =
                SharedGameplayChecksum.ComputeSegmentHashes(
                    server,
                    serverRuntime.TickPipeline.GoldIncome?
                        .GetBatchDigest(3) ??
                    new GoldIncomeBatchDigest(0));
            SharedGameplayChecksum.ChecksumSegment[] clientSegments =
                SharedGameplayChecksum.ComputeSegmentHashes(
                    client,
                    clientRuntime.TickPipeline.GoldIncome?
                        .GetBatchDigest(3) ??
                    new GoldIncomeBatchDigest(0));

            uint serverUnitWorld = 0;
            uint clientUnitWorld = 0;
            for (int i = 0;
                 i < serverSegments.Length;
                 i++)
            {
                if (serverSegments[i].Label == "UnitWorld")
                    serverUnitWorld =
                        serverSegments[i].Hash;
            }
            for (int i = 0;
                 i < clientSegments.Length;
                 i++)
            {
                if (clientSegments[i].Label == "UnitWorld")
                    clientUnitWorld =
                        clientSegments[i].Hash;
            }

            if (serverUnitWorld == clientUnitWorld)
                return;

            var detail =
                new System.Text.StringBuilder();
            detail.AppendLine(
                "UnitWorld checksum diverged. " +
                "Per-unit Stats sub-segments (server | client):");
            UnitSnapshot[] serverUnits =
                server.UnitWorldState.Units ??
                System.Array.Empty<UnitSnapshot>();
            UnitSnapshot[] clientUnits =
                client.UnitWorldState.Units ??
                System.Array.Empty<UnitSnapshot>();
            for (int u = 0;
                 u < serverUnits.Length &&
                 u < clientUnits.Length;
                 u++)
            {
                SharedGameplayChecksum.ChecksumSegment[]
                    serverHandlers =
                        SharedGameplayChecksum
                            .ComputeUnitHandlerHashes(
                                serverUnits[u]);
                SharedGameplayChecksum.ChecksumSegment[]
                    clientHandlers =
                        SharedGameplayChecksum
                            .ComputeUnitHandlerHashes(
                                clientUnits[u]);
                for (int h = 0;
                     h < serverHandlers.Length &&
                     h < clientHandlers.Length;
                     h++)
                {
                    if (serverHandlers[h].Hash ==
                        clientHandlers[h].Hash)
                        continue;
                    detail.AppendLine(
                        $"  Unit{u} {serverHandlers[h].Label}: " +
                        $"{serverHandlers[h].Hash} | " +
                        $"{clientHandlers[h].Hash}");
                }
            }
            Assert.Fail(detail.ToString());
        }

        private static void QueueHeroes(
            FrameSyncGameRuntime runtime)
        {
            runtime.TickPipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    1001,
                    GameplayParticipantId.InitialSpawn(1),
                    new TeamId(1),
                    fp2.zero,
                    new fp2(fp.zero, fp.one)));
            runtime.TickPipeline.QueueInitialSpawn(
                new UnitSpawnRequest(
                    1001,
                    GameplayParticipantId.InitialSpawn(2),
                    new TeamId(2),
                    new fp2((fp)5, fp.zero),
                    new fp2(fp.zero, fp.one)));
        }

        private UnitWorld CreateWorld(
            BakedGlobalGameplayData baked,
            GlobalPrefabTable resolvedPrefabTable,
            UnitRuntimeCatalogAsset unitCatalog,
            AbilityRuntimeCatalogAsset abilityCatalog,
            BuffCatalogAsset buffCatalog)
        {
            BakedUnitRuntimeCatalog bakedUnits =
                unitCatalog.BakeOrThrow(
                    resolvedPrefabTable);
            var world = new UnitWorld
            {
                PhysicsWorld = new PhysicsWorld(),
                GlobalPrefabTable = resolvedPrefabTable,
                UnitPrototypeTable =
                    bakedUnits.UnitPrototypes,
                DisposePolicyTable =
                    bakedUnits.DisposePolicies,
                StatDefinitionTable =
                    bakedUnits.StatDefinitions,
                AbilityDefinitions =
                    abilityCatalog.BakeOrThrow(),
                BuffDefinitions =
                    new BuffDefinitionRegistry(),
                TickRate = baked.TickRate,
                AttackSequenceResetIntervalTicks =
                    baked.AttackSequenceResetIntervalTicks,
                RangedAttackRangeThreshold =
                    baked.RangedAttackRangeThreshold,
                StatGrowthC = baked.StatGrowthC,
                StatGrowthD = baked.StatGrowthD,
            };
            buffCatalog.RegisterAll(
                world.BuffDefinitions);
            return world;
        }

        private static GlobalPrefabTable BuildResolvedFormalPrefabTable(
            GlobalPrefabTable root)
        {
            string[] paths =
            {
                MatchContentRoot + "CoreGlobalPrefabSubTable.asset",
                MatchContentRoot + "Map1GlobalPrefabSubTable.asset",
                MatchContentRoot + "VarusGlobalPrefabSubTable.asset",
                MatchContentRoot + "AatroxGlobalPrefabSubTable.asset",
            };
            var children = new List<GlobalPrefabSubTableAsset>(
                paths.Length);
            var resolved = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            for (int pathIndex = 0;
                 pathIndex < paths.Length;
                 pathIndex++)
            {
                GlobalPrefabSubTableAsset child =
                    AssetDatabase.LoadAssetAtPath<
                        GlobalPrefabSubTableAsset>(
                        paths[pathIndex]);
                Assert.That(child, Is.Not.Null, paths[pathIndex]);
                child.ValidateOrThrow();
                children.Add(child);
                for (int groupIndex = 0;
                     groupIndex < child.PrefabGroups.Count;
                     groupIndex++)
                {
                    PrefabGroup group =
                        child.PrefabGroups[groupIndex];
                    for (int entryIndex = 0;
                         entryIndex < group.Entries.Count;
                         entryIndex++)
                    {
                        PrefabEntry entry =
                            group.Entries[entryIndex];
                        if (string.IsNullOrEmpty(
                                entry.LogicAssetAddress) ||
                            resolved.ContainsKey(
                                entry.LogicAssetAddress))
                            continue;
                        GameObject prefab =
                            AssetDatabase.LoadAssetAtPath<
                                GameObject>(
                                entry.LogicAssetAddress);
                        Assert.That(
                            prefab,
                            Is.Not.Null,
                            entry.LogicAssetAddress);
                        resolved.Add(
                            entry.LogicAssetAddress,
                            prefab);
                    }
                }
            }
            return root.CreateResolvedRuntimeTable(
                children,
                resolved);
        }
    }
}
