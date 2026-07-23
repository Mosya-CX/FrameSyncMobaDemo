using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public static class UnitTestFactory
    {
        private sealed class WorldFixture
        {
            public readonly List<PrefabEntry> Prefabs = new List<PrefabEntry>();
            public readonly HashSet<int> PrefabIds = new HashSet<int>();
        }

        private static readonly List<GameObject> createdObjects = new List<GameObject>();
        private static readonly List<ScriptableObject> createdAssets = new List<ScriptableObject>();
        private static readonly Dictionary<UnitWorld, WorldFixture> worldFixtures =
            new Dictionary<UnitWorld, WorldFixture>();

        public static Unit CreateUnit(
            UnitUid uid,
            UnitKind kind,
            ushort subKindId,
            TeamId teamId,
            int unitPrototypeId = 0,
            int baseGoldValue = 0,
            int baseExperienceValue = 0)
        {
            GameObject root = CreateComposedUnitObject("TestUnit");
            Unit unit = root.GetComponent<Unit>();
            var prototype = new UnitPrototype
            {
                UnitPrototypeId = unitPrototypeId,
                RuntimeEntityPrefabId = uid.RuntimeEntityPrefabId,
                UnitKind = kind,
                UnitSubKindId = subKindId,
                BaseStats = CreateDefaultPreset(),
                BaseGoldValue = baseGoldValue,
                BaseExperienceValue = baseExperienceValue,
            };

            unit.InitializeForNewRuntime(
                uid,
                default,
                prototype,
                teamId,
                CreateDefaultStatTable(),
                fp.zero,
                fp.zero,
                30,
                fp2.zero);
            unit.PhysicsEntity.SetLogicPose(fp2.zero, new fp2(fp.one, fp.zero));
            unit.PhysicsEntity.SetQueryInfo(new PhysicsEntityQueryInfo(
                new RuntimeUidQueryValue(
                    uid.SpawnLogicTick,
                    uid.RuntimeEntityPrefabId,
                    uid.SpawnSequenceInTick),
                PhysicsEntityKind.Unit,
                teamId.Value,
                unit));
            return unit;
        }

        public static MovementHandler CreateMovementHandler(fp2 position, fp moveSpeed)
        {
            var root = new GameObject("TestMovementHandler")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            createdObjects.Add(root);
            MovementHandler handler = root.AddComponent<MovementHandler>();
            handler.InitializeRuntime(position, moveSpeed);
            return handler;
        }

        public static StatHandler CreateStatHandler(
            StatDefinitionTable table,
            StatPreset preset,
            UnitUid ownerUid,
            int level,
            fp statGrowthC,
            fp statGrowthD)
        {
            var root = new GameObject("TestStatHandler")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            createdObjects.Add(root);
            StatHandler handler = root.AddComponent<StatHandler>();
            handler.InitializeRuntime(table, preset, ownerUid, level, statGrowthC, statGrowthD);
            return handler;
        }

        public static AttackHandler CreateAttackHandler(Unit owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            GameObject child = new GameObject("TestAttackHandler")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            child.transform.SetParent(owner.transform, false);
            createdObjects.Add(child);
            AttackHandler handler = child.AddComponent<AttackHandler>();
            handler.BindOwner(owner);
            handler.InitializeForNewRuntime(30);
            return handler;
        }

        public static Unit SpawnUnit(
            this UnitWorld world,
            UnitPrototype prototype,
            TeamId teamId,
            int currentLogicTick,
            fp statGrowthC,
            fp statGrowthD)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (prototype == null) throw new ArgumentNullException(nameof(prototype));

            ConfigureWorldForPrototype(world, prototype, statGrowthC, statGrowthD);

            SimulationTickContextController tickController = null;
            try
            {
                _ = SimulationTickContext.Current;
            }
            catch (InvalidOperationException)
            {
                tickController = new SimulationTickContextController();
                tickController.BeginTick(currentLogicTick, ExecutionMode.ServerAuthority);
            }

            try
            {
                UnitUid uid = world.SpawnUnit(new UnitSpawnRequest(
                    prototype.UnitPrototypeId,
                    teamId,
                    fp2.zero,
                    new fp2(fp.one, fp.zero)));
                if (!world.TryGetUnit(uid, out Unit unit))
                {
                    throw new InvalidOperationException($"Spawned test Unit {uid} was not registered.");
                }

                return unit;
            }
            finally
            {
                tickController?.EndTick();
            }
        }

        public static void DestroyCreatedObjects()
        {
            for (int i = createdObjects.Count - 1; i >= 0; i--)
            {
                if (createdObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[i]);
                }
            }
            createdObjects.Clear();

            for (int i = createdAssets.Count - 1; i >= 0; i--)
            {
                if (createdAssets[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdAssets[i]);
                }
            }
            createdAssets.Clear();
            worldFixtures.Clear();
        }

        private static void ConfigureWorldForPrototype(
            UnitWorld world,
            UnitPrototype prototype,
            fp statGrowthC,
            fp statGrowthD)
        {
            world.UnitPrototypeTable ??= new GlobalUnitPrototypeTable();
            if (!world.UnitPrototypeTable.TryGet(prototype.UnitPrototypeId, out _))
            {
                world.UnitPrototypeTable.Add(prototype);
            }

            world.PhysicsWorld ??= new PhysicsWorld();
            EnsureDefaultStatDefinitions(world);
            world.StatGrowthC = statGrowthC;
            world.StatGrowthD = statGrowthD;
            world.TickRate = 30;

            if (!worldFixtures.TryGetValue(world, out WorldFixture fixture))
            {
                fixture = new WorldFixture();
                worldFixtures.Add(world, fixture);
            }

            if (!fixture.PrefabIds.Contains(prototype.RuntimeEntityPrefabId))
            {
                GameObject prefab = CreateComposedUnitObject(
                    $"TestUnitPrefab_{prototype.RuntimeEntityPrefabId}");
                fixture.PrefabIds.Add(prototype.RuntimeEntityPrefabId);
                fixture.Prefabs.Add(new PrefabEntry(
                    prototype.RuntimeEntityPrefabId,
                    prefab,
                    prototype.UnitPrototypeId));
            }

            if (world.GlobalPrefabTable == null)
            {
                world.GlobalPrefabTable = ScriptableObject.CreateInstance<GlobalPrefabTable>();
                world.GlobalPrefabTable.hideFlags = HideFlags.HideAndDontSave;
                createdAssets.Add(world.GlobalPrefabTable);
            }

            world.GlobalPrefabTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(PrefabKind.Unit, fixture.Prefabs),
            });
        }

        private static GameObject CreateComposedUnitObject(string objectName)
        {
            var root = new GameObject(objectName)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            createdObjects.Add(root);
            root.AddComponent<PhysicsEntity2D>();
            root.AddComponent<StatHandler>();
            root.AddComponent<MovementHandler>();
            root.AddComponent<AttackHandler>();
            root.AddComponent<AbilityHandler>();
            root.AddComponent<BuffHandler>();
            root.AddComponent<CrowdControlHandler>();
            root.AddComponent<EquipmentHandler>();
            root.AddComponent<Unit>();
            return root;
        }

        private static StatDefinitionTable CreateTableForPreset(StatPreset preset)
        {
            StatDefinitionTable table = CreateDefaultStatTable();
            if (preset == null) return table;

            for (int i = 0; i < preset.Stats.Count; i++)
            {
                StatPresetEntry entry = preset.Stats[i];
                if (table.Contains(entry.StatId)) continue;
                table.Add(new StatDefinition
                {
                    Id = entry.StatId,
                    DebugName = entry.StatId.ToString(),
                    DefaultBaseValue = fp.zero,
                    SupportsLevelGrowth = true,
                });
            }

            return table;
        }

        private static StatDefinitionTable CreateDefaultStatTable()
        {
            var table = new StatDefinitionTable();
            Array values = Enum.GetValues(typeof(StatId));
            for (int i = 0; i < values.Length; i++)
            {
                StatId id = (StatId)values.GetValue(i);
                table.Add(new StatDefinition
                {
                    Id = id,
                    DebugName = id.ToString(),
                    DefaultBaseValue = fp.zero,
                    SupportsLevelGrowth = true,
                });
            }
            return table;
        }

        private static void EnsureDefaultStatDefinitions(UnitWorld world)
        {
            world.StatDefinitionTable ??= new StatDefinitionTable();
            Array values = Enum.GetValues(typeof(StatId));
            for (int i = 0; i < values.Length; i++)
            {
                StatId id = (StatId)values.GetValue(i);
                if (world.StatDefinitionTable.Contains(id)) continue;
                world.StatDefinitionTable.Add(new StatDefinition
                {
                    Id = id,
                    DebugName = id.ToString(),
                    DefaultBaseValue = fp.zero,
                    SupportsLevelGrowth = true,
                });
            }
        }

        private static StatPreset CreateDefaultPreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = (fp)100,
                GrowthValue = fp.zero,
            });
            return preset;
        }
    }

    [SetUpFixture]
    public sealed class UnitTestObjectLifetime
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            UnitTestFactory.DestroyCreatedObjects();
        }
    }
}
