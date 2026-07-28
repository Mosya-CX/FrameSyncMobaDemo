using System;
using System.Collections.Generic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class StatDefinitionAuthoring
    {
        public StatId Id;
        public string DebugName;
        public float DefaultBaseValue;
        public bool SupportsLevelGrowth;
        public bool HasMinValue;
        public float MinValue;
        public bool HasMaxValue;
        public float MaxValue;

        internal StatDefinition BakeOrThrow()
        {
            ValidateFinite(DefaultBaseValue, nameof(DefaultBaseValue));
            ValidateFinite(MinValue, nameof(MinValue));
            ValidateFinite(MaxValue, nameof(MaxValue));
            if (!Enum.IsDefined(typeof(StatId), Id))
                throw new InvalidOperationException($"Undefined StatId {(ushort)Id}.");
            if (HasMinValue && HasMaxValue && MinValue > MaxValue)
                throw new InvalidOperationException(
                    $"Stat {Id} has MinValue greater than MaxValue.");

            return new StatDefinition
            {
                Id = Id,
                DebugName = string.IsNullOrWhiteSpace(DebugName)
                    ? Id.ToString()
                    : DebugName.Trim(),
                DefaultBaseValue = (fp)DefaultBaseValue,
                SupportsLevelGrowth = SupportsLevelGrowth,
                HasMinValue = HasMinValue,
                MinValue = (fp)MinValue,
                HasMaxValue = HasMaxValue,
                MaxValue = (fp)MaxValue,
            };
        }

        internal static void ValidateFinite(float value, string field)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException($"{field} must be finite.");
        }
    }

    [Serializable]
    public struct StatPresetEntryAuthoring
    {
        public StatId StatId;
        public float BaseValue;
        public float GrowthValue;

        internal StatPresetEntry BakeOrThrow()
        {
            StatDefinitionAuthoring.ValidateFinite(BaseValue, nameof(BaseValue));
            StatDefinitionAuthoring.ValidateFinite(GrowthValue, nameof(GrowthValue));
            if (!Enum.IsDefined(typeof(StatId), StatId))
                throw new InvalidOperationException(
                    $"Undefined StatId {(ushort)StatId} in Unit stat preset.");
            return new StatPresetEntry
            {
                StatId = StatId,
                BaseValue = (fp)BaseValue,
                GrowthValue = (fp)GrowthValue,
            };
        }
    }

    [Serializable]
    public struct LocomotionProfileAuthoring
    {
        [Min(0f)] public float BaseMoveSpeed;
        [Min(0f)] public float CollisionRadius;
        public RadiusClass RadiusClass;
        [Min(0f)] public float MaxTurnRate;
        [Min(0f)] public float ArriveDistance;

        internal LocomotionProfile BakeOrThrow()
        {
            ValidateNonnegativeFinite(BaseMoveSpeed, nameof(BaseMoveSpeed));
            ValidateNonnegativeFinite(CollisionRadius, nameof(CollisionRadius));
            ValidateNonnegativeFinite(MaxTurnRate, nameof(MaxTurnRate));
            ValidateNonnegativeFinite(ArriveDistance, nameof(ArriveDistance));
            if (!Enum.IsDefined(typeof(RadiusClass), RadiusClass))
                throw new InvalidOperationException($"Undefined RadiusClass {RadiusClass}.");
            return new LocomotionProfile
            {
                BaseMoveSpeed = (fp)BaseMoveSpeed,
                CollisionRadius = (fp)CollisionRadius,
                RadiusClass = RadiusClass,
                MaxTurnRate = (fp)MaxTurnRate,
                ArriveDistance = (fp)ArriveDistance,
            };
        }

        internal static void ValidateNonnegativeFinite(float value, string field)
        {
            StatDefinitionAuthoring.ValidateFinite(value, field);
            if (value < 0f)
                throw new InvalidOperationException($"{field} must be nonnegative.");
        }
    }

    [Serializable]
    public struct PhysicsProfile2DAuthoring
    {
        public PhysicsShapeKind DefaultShape;
        [Min(0f)] public float ShapeParam;
        public Vector2 InitialForward;
        public bool RegisterForSpatialQuery;

        internal PhysicsProfile2D BakeOrThrow()
        {
            LocomotionProfileAuthoring.ValidateNonnegativeFinite(
                ShapeParam, nameof(ShapeParam));
            if (DefaultShape != PhysicsShapeKind.Point &&
                DefaultShape != PhysicsShapeKind.Circle)
                throw new InvalidOperationException(
                    $"Unit authoring supports Point or Circle, got {DefaultShape}.");
            if (DefaultShape == PhysicsShapeKind.Circle && ShapeParam <= 0f)
                throw new InvalidOperationException("Circle ShapeParam must be positive.");
            StatDefinitionAuthoring.ValidateFinite(InitialForward.x, "InitialForward.x");
            StatDefinitionAuthoring.ValidateFinite(InitialForward.y, "InitialForward.y");
            var forward = new fp2((fp)InitialForward.x, (fp)InitialForward.y);
            if (!PhysicsGeometry2D.TryCreateFacing(forward, out fp2 normalized, out _))
                throw new InvalidOperationException("InitialForward must be non-zero.");
            return new PhysicsProfile2D
            {
                DefaultShape = DefaultShape,
                ShapeParam = (fp)ShapeParam,
                InitialForward = normalized,
                RegisterForSpatialQuery = RegisterForSpatialQuery,
            };
        }
    }

    [Serializable]
    public sealed class UnitPrototypeAuthoring
    {
        [Min(1)] public int UnitPrototypeId;
        public string Name;
        [Min(1)] public int RuntimeEntityPrefabId;
        public UnitKind UnitKind;
        public ushort UnitSubKindId;
        [Min(0)] public int BaseGoldValue;
        [Min(0)] public int BaseExperienceValue;
        public ushort UnitDisposePolicyId;
        public UnitRespawnConfig RespawnConfig = UnitRespawnConfig.CannotRespawn;
        public HandlerLoadout Loadout = HandlerLoadout.DefaultHero;
        public LocomotionProfileAuthoring Locomotion;
        public PhysicsProfile2DAuthoring Physics;
        public LevelExperienceConfig LevelExperience = LevelExperienceConfig.Disabled;
        public List<StatPresetEntryAuthoring> BaseStats =
            new List<StatPresetEntryAuthoring>();

        internal UnitPrototype BakeOrThrow()
        {
            if (UnitPrototypeId <= 0)
                throw new InvalidOperationException("UnitPrototypeId must be positive.");
            if (RuntimeEntityPrefabId <= 0)
                throw new InvalidOperationException(
                    $"Prototype {UnitPrototypeId} RuntimeEntityPrefabId must be positive.");
            if (!Enum.IsDefined(typeof(UnitKind), UnitKind))
                throw new InvalidOperationException(
                    $"Prototype {UnitPrototypeId} has undefined UnitKind {UnitKind}.");
            if (BaseGoldValue < 0 || BaseExperienceValue < 0)
                throw new InvalidOperationException(
                    $"Prototype {UnitPrototypeId} reward values must be nonnegative.");

            LevelExperienceConfig levelConfig =
                LevelExperience ?? LevelExperienceConfig.Disabled;
            ValidateLevelExperience(levelConfig);

            var entries = new List<StatPresetEntry>(BaseStats?.Count ?? 0);
            if (BaseStats != null)
            {
                for (int i = 0; i < BaseStats.Count; i++)
                    entries.Add(BaseStats[i].BakeOrThrow());
            }
            entries.Sort((left, right) => left.StatId.CompareTo(right.StatId));
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i - 1].StatId == entries[i].StatId)
                    throw new InvalidOperationException(
                        $"Prototype {UnitPrototypeId} has duplicate StatId {entries[i].StatId}.");
            }

            LocomotionProfile locomotion = Locomotion.BakeOrThrow();
            PhysicsProfile2D physics = Physics.BakeOrThrow();
            if (physics.DefaultShape == PhysicsShapeKind.Circle &&
                locomotion.CollisionRadius != physics.ShapeParam)
                throw new InvalidOperationException(
                    $"Prototype {UnitPrototypeId} locomotion and physics radii must match.");

            return new UnitPrototype
            {
                UnitPrototypeId = UnitPrototypeId,
                Name = string.IsNullOrWhiteSpace(Name)
                    ? $"UnitPrototype_{UnitPrototypeId}"
                    : Name.Trim(),
                RuntimeEntityPrefabId = RuntimeEntityPrefabId,
                UnitKind = UnitKind,
                UnitSubKindId = UnitSubKindId,
                BaseStats = new StatPreset
                {
                    LevelExperience = levelConfig,
                    Stats = entries,
                },
                BaseGoldValue = BaseGoldValue,
                BaseExperienceValue = BaseExperienceValue,
                UnitDisposePolicyId = UnitDisposePolicyId,
                RespawnConfig = RespawnConfig,
                Loadout = Loadout,
                LocomotionProfile = locomotion,
                PhysicsProfile = physics,
            };
        }

        private static void ValidateLevelExperience(LevelExperienceConfig config)
        {
            if (config.InitialLevel < 1 || config.MaxLevel < config.InitialLevel)
                throw new InvalidOperationException(
                    "LevelExperience initial/max range is invalid.");
            if (config.InitialExperience < 0)
                throw new InvalidOperationException(
                    "LevelExperience InitialExperience must be nonnegative.");
            int expected = config.CanLevelUp ? config.MaxLevel - 1 : 0;
            int actual = config.RequiredExperiencePerLevel?.Count ?? 0;
            if (actual != expected)
                throw new InvalidOperationException(
                    $"LevelExperience requires {expected} XP entries, got {actual}.");
            for (int i = 0; i < actual; i++)
            {
                if (config.RequiredExperiencePerLevel[i] <= 0)
                    throw new InvalidOperationException(
                        $"LevelExperience XP entry {i} must be positive.");
            }
        }
    }

    public sealed class BakedUnitRuntimeCatalog
    {
        public BakedUnitRuntimeCatalog(
            StatDefinitionTable statDefinitions,
            GlobalUnitPrototypeTable unitPrototypes)
        {
            StatDefinitions = statDefinitions ??
                throw new ArgumentNullException(nameof(statDefinitions));
            UnitPrototypes = unitPrototypes ??
                throw new ArgumentNullException(nameof(unitPrototypes));
        }

        public StatDefinitionTable StatDefinitions { get; }
        public GlobalUnitPrototypeTable UnitPrototypes { get; }
    }

    [CreateAssetMenu(
        fileName = "UnitRuntimeCatalog",
        menuName = "FrameSyncMoba/Unit/Runtime Catalog")]
    public sealed class UnitRuntimeCatalogAsset : ScriptableObject
    {
        [SerializeField] private List<StatDefinitionAuthoring> statDefinitions =
            new List<StatDefinitionAuthoring>();
        [SerializeField] private List<UnitPrototypeAuthoring> unitPrototypes =
            new List<UnitPrototypeAuthoring>();

        public IReadOnlyList<StatDefinitionAuthoring> StatDefinitions => statDefinitions;
        public IReadOnlyList<UnitPrototypeAuthoring> UnitPrototypes => unitPrototypes;

        public BakedUnitRuntimeCatalog BakeOrThrow(GlobalPrefabTable prefabTable)
        {
            if (prefabTable == null)
                throw new ArgumentNullException(nameof(prefabTable));
            prefabTable.ValidateOrThrow();

            var sortedDefinitions = new List<StatDefinitionAuthoring>(
                statDefinitions ?? new List<StatDefinitionAuthoring>());
            if (sortedDefinitions.Count == 0)
                throw new InvalidOperationException(
                    "UnitRuntimeCatalog requires at least one StatDefinition.");
            sortedDefinitions.Sort(CompareDefinitions);
            var definitionTable = new StatDefinitionTable();
            for (int i = 0; i < sortedDefinitions.Count; i++)
            {
                if (sortedDefinitions[i] == null)
                    throw new InvalidOperationException($"Stat definition {i} is null.");
                definitionTable.Add(sortedDefinitions[i].BakeOrThrow());
            }

            var sortedPrototypes = new List<UnitPrototypeAuthoring>(
                unitPrototypes ?? new List<UnitPrototypeAuthoring>());
            if (sortedPrototypes.Count == 0)
                throw new InvalidOperationException(
                    "UnitRuntimeCatalog requires at least one UnitPrototype.");
            sortedPrototypes.Sort(ComparePrototypes);
            var prototypeTable = new GlobalUnitPrototypeTable();
            for (int i = 0; i < sortedPrototypes.Count; i++)
            {
                UnitPrototypeAuthoring authoring = sortedPrototypes[i];
                if (authoring == null)
                    throw new InvalidOperationException($"Unit prototype {i} is null.");
                UnitPrototype prototype = authoring.BakeOrThrow();
                GameObject prefab = prefabTable.GetRequiredPrefab(
                    PrefabKind.Unit, prototype.RuntimeEntityPrefabId);
                ValidatePrefabComposition(prefab, prototype);
                prototypeTable.Add(prototype);
            }

            prototypeTable.ValidateAll(definitionTable);
            return new BakedUnitRuntimeCatalog(definitionTable, prototypeTable);
        }

        internal void ReplaceForTests(
            IEnumerable<StatDefinitionAuthoring> definitions,
            IEnumerable<UnitPrototypeAuthoring> prototypes)
        {
            statDefinitions = definitions == null
                ? new List<StatDefinitionAuthoring>()
                : new List<StatDefinitionAuthoring>(definitions);
            unitPrototypes = prototypes == null
                ? new List<UnitPrototypeAuthoring>()
                : new List<UnitPrototypeAuthoring>(prototypes);
        }

        private static int CompareDefinitions(
            StatDefinitionAuthoring left,
            StatDefinitionAuthoring right)
        {
            if (left == null) return right == null ? 0 : -1;
            if (right == null) return 1;
            return left.Id.CompareTo(right.Id);
        }

        private static int ComparePrototypes(
            UnitPrototypeAuthoring left,
            UnitPrototypeAuthoring right)
        {
            if (left == null) return right == null ? 0 : -1;
            if (right == null) return 1;
            return left.UnitPrototypeId.CompareTo(right.UnitPrototypeId);
        }

        private static void ValidatePrefabComposition(
            GameObject prefab,
            UnitPrototype prototype)
        {
            if (prefab.GetComponent<Unit>() == null)
                throw new InvalidOperationException(
                    $"Unit prefab {prototype.RuntimeEntityPrefabId} needs Unit on its root.");
            RequireExactlyOne<PhysicsEntity2D>(prefab, prototype);
            RequireExactlyOne<StatHandler>(prefab, prototype);
            RequireExactlyOne<MovementHandler>(prefab, prototype);
            RequireExactlyOne<AttackHandler>(prefab, prototype);
            RequireExactlyOne<AbilityHandler>(prefab, prototype);
            RequireExactlyOne<BuffHandler>(prefab, prototype);
            RequireExactlyOne<CrowdControlHandler>(prefab, prototype);
            RequireExactlyOne<EquipmentHandler>(prefab, prototype);
        }

        private static void RequireExactlyOne<T>(
            GameObject prefab,
            UnitPrototype prototype) where T : Component
        {
            int count = prefab.GetComponentsInChildren<T>(true).Length;
            if (count != 1)
                throw new InvalidOperationException(
                    $"Unit prefab {prototype.RuntimeEntityPrefabId} requires exactly one {typeof(T).Name}; found {count}.");
        }
    }
}
