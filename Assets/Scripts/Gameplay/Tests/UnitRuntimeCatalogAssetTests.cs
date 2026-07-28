using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit.Tests
{
    [TestFixture]
    public sealed class UnitRuntimeCatalogAssetTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = cleanup.Count - 1; i >= 0; i--)
            {
                if (cleanup[i] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[i]);
            }
            cleanup.Clear();
        }

        [Test]
        public void Bake_ConvertsFloatAuthoringToExistingRuntimeContracts()
        {
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                CreateDefinitions(),
                new[] { CreatePrototype() },
                out GlobalPrefabTable prefabTable);

            BakedUnitRuntimeCatalog baked = catalog.BakeOrThrow(prefabTable);

            Assert.That(baked.StatDefinitions.Count, Is.EqualTo(2));
            Assert.That(baked.UnitPrototypes.Count, Is.EqualTo(1));
            Assert.That(
                baked.StatDefinitions.TryGet(StatId.MaxHealth, out StatDefinition health),
                Is.True);
            Assert.That(health.DefaultBaseValue, Is.EqualTo((fp)100f));
            Assert.That(
                baked.UnitPrototypes.TryGet(1001, out UnitPrototype prototype),
                Is.True);
            Assert.That(
                prototype.LocomotionProfile.BaseMoveSpeed,
                Is.EqualTo((fp)3.5f));
            Assert.That(prototype.PhysicsProfile.ShapeParam, Is.EqualTo((fp)0.5f));
            Assert.That(prototype.BaseStats.Stats[0].StatId, Is.EqualTo(StatId.MaxHealth));
            Assert.That(prototype.BaseStats.Stats[1].StatId, Is.EqualTo(StatId.MoveSpeed));
        }

        [Test]
        public void Bake_ReversedAuthoringOrderProducesSameStableTables()
        {
            StatDefinitionAuthoring[] definitions = CreateDefinitions();
            UnitPrototypeAuthoring prototype = CreatePrototype();
            prototype.BaseStats.Reverse();
            Array.Reverse(definitions);
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                definitions,
                new[] { prototype },
                out GlobalPrefabTable prefabTable);

            BakedUnitRuntimeCatalog baked = catalog.BakeOrThrow(prefabTable);

            Assert.That(baked.StatDefinitions.Contains(StatId.MaxHealth), Is.True);
            Assert.That(baked.StatDefinitions.Contains(StatId.MoveSpeed), Is.True);
            Assert.That(baked.UnitPrototypes.TryGet(1001, out UnitPrototype result), Is.True);
            Assert.That(result.BaseStats.Stats[0].StatId, Is.EqualTo(StatId.MaxHealth));
            Assert.That(result.BaseStats.Stats[1].StatId, Is.EqualTo(StatId.MoveSpeed));
        }

        [Test]
        public void Bake_DuplicateStatDefinitionFailsInsteadOfOverwriting()
        {
            StatDefinitionAuthoring duplicate = CreateDefinitions()[0];
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                new[] { duplicate, duplicate },
                new[] { CreatePrototype() },
                out GlobalPrefabTable prefabTable);

            Assert.Throws<ArgumentException>(() => catalog.BakeOrThrow(prefabTable));
        }

        [Test]
        public void Bake_DuplicatePrototypePresetStatFails()
        {
            UnitPrototypeAuthoring prototype = CreatePrototype();
            prototype.BaseStats.Add(prototype.BaseStats[0]);
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                CreateDefinitions(),
                new[] { prototype },
                out GlobalPrefabTable prefabTable);

            Assert.Throws<InvalidOperationException>(
                () => catalog.BakeOrThrow(prefabTable));
        }

        [Test]
        public void SpawnUnit_UsesBakedAbilityMovementAndPhysicsProfiles()
        {
            UnitRuntimeCatalogAsset catalog = CreateCatalog(
                CreateDefinitions(),
                new[] { CreatePrototype() },
                out GlobalPrefabTable prefabTable);
            BakedUnitRuntimeCatalog baked = catalog.BakeOrThrow(prefabTable);
            var physicsWorld = new PhysicsWorld();
            var unitWorld = new UnitWorld
            {
                PhysicsWorld = physicsWorld,
                GlobalPrefabTable = prefabTable,
                UnitPrototypeTable = baked.UnitPrototypes,
                StatDefinitionTable = baked.StatDefinitions,
                EquipmentDatabase = new EquipmentDatabase(),
                AbilityDefinitions = new AbilityDefinitionRegistry(),
                BuffDefinitions = new BuffDefinitionRegistry(),
                TickRate = 30,
                StatGrowthC = (fp)0.7025f,
                StatGrowthD = (fp)0.0175f,
            };
            var controller = new SimulationTickContextController();
            UnitUid uid;
            try
            {
                controller.BeginTick(0, ExecutionMode.ServerAuthority);
                uid = unitWorld.SpawnUnit(new UnitSpawnRequest(
                    1001,
                    new TeamId(1),
                    new fp2((fp)2, (fp)3),
                    new fp2(fp.zero, fp.one)));
            }
            finally
            {
                if (controller.IsTickActive) controller.EndTick();
            }

            Assert.That(uid.IsValid(), Is.True);
            Assert.That(unitWorld.TryGetUnit(uid, out Unit unit), Is.True);
            cleanup.Add(unit.gameObject);
            Assert.That(unit.AbilityMask.HasMovement, Is.True);
            Assert.That(unit.AbilityMask.HasAttack, Is.True);
            Assert.That(unit.AbilityMask.HasAbility, Is.True);
            Assert.That(unit.MovementHandler.MoveSpeed, Is.EqualTo((fp)3.5f));
            Assert.That(unit.PhysicsEntity.Shape.Kind, Is.EqualTo(PhysicsShapeKind.Circle));
            Assert.That(unit.PhysicsEntity.Shape.Radius, Is.EqualTo((fp)0.5f));
            Assert.That(unit.PhysicsEntity.Transform2D.Position, Is.EqualTo(
                new fp2((fp)2, (fp)3)));
            Assert.That(physicsWorld.UnitEntities.Count, Is.EqualTo(1));
        }

        private UnitRuntimeCatalogAsset CreateCatalog(
            IEnumerable<StatDefinitionAuthoring> definitions,
            IEnumerable<UnitPrototypeAuthoring> prototypes,
            out GlobalPrefabTable prefabTable)
        {
            GameObject prefab = CreateComposedUnit("CatalogTestUnit");
            cleanup.Add(prefab);
            prefabTable = ScriptableObject.CreateInstance<GlobalPrefabTable>();
            cleanup.Add(prefabTable);
            prefabTable.ReplaceGroupsForTests(new[]
            {
                new PrefabGroup(
                    PrefabKind.Unit,
                    new[] { new PrefabEntry(1001, prefab) }),
            });
            UnitRuntimeCatalogAsset catalog =
                ScriptableObject.CreateInstance<UnitRuntimeCatalogAsset>();
            cleanup.Add(catalog);
            catalog.ReplaceForTests(definitions, prototypes);
            return catalog;
        }

        private static GameObject CreateComposedUnit(string name)
        {
            var root = new GameObject(name);
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

        private static StatDefinitionAuthoring[] CreateDefinitions() => new[]
        {
            new StatDefinitionAuthoring
            {
                Id = StatId.MaxHealth,
                DebugName = "Health",
                DefaultBaseValue = 100f,
                HasMinValue = true,
                MinValue = 0f,
            },
            new StatDefinitionAuthoring
            {
                Id = StatId.MoveSpeed,
                DebugName = "MoveSpeed",
                DefaultBaseValue = 3.5f,
                HasMinValue = true,
                MinValue = 0f,
            },
        };

        private static UnitPrototypeAuthoring CreatePrototype() =>
            new UnitPrototypeAuthoring
            {
                UnitPrototypeId = 1001,
                Name = "Neutral Test Unit",
                RuntimeEntityPrefabId = 1001,
                UnitKind = UnitKind.Hero,
                UnitSubKindId = 1,
                Loadout = HandlerLoadout.DefaultHero,
                Locomotion = new LocomotionProfileAuthoring
                {
                    BaseMoveSpeed = 3.5f,
                    CollisionRadius = 0.5f,
                    RadiusClass = RadiusClass.Medium,
                    ArriveDistance = 0.05f,
                },
                Physics = new PhysicsProfile2DAuthoring
                {
                    DefaultShape = PhysicsShapeKind.Circle,
                    ShapeParam = 0.5f,
                    InitialForward = Vector2.up,
                    RegisterForSpatialQuery = true,
                },
                LevelExperience = new LevelExperienceConfig
                {
                    CanLevelUp = false,
                    InitialLevel = 1,
                    MaxLevel = 1,
                },
                BaseStats = new List<StatPresetEntryAuthoring>
                {
                    new StatPresetEntryAuthoring
                    {
                        StatId = StatId.MoveSpeed,
                        BaseValue = 3.5f,
                    },
                    new StatPresetEntryAuthoring
                    {
                        StatId = StatId.MaxHealth,
                        BaseValue = 100f,
                    },
                },
            };
    }
}
