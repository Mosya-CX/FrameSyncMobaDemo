using System;
using System.Collections.Generic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public int[] InitialBuffConfigIds =
            Array.Empty<int>();
        public ushort UnitDisposePolicyId;
        public UnitRespawnConfig RespawnConfig = UnitRespawnConfig.CannotRespawn;
        public UnitPoolConfig PoolConfig = UnitPoolConfig.Default;
        public HandlerLoadout Loadout = HandlerLoadout.DefaultHero;
        public LocomotionProfileAuthoring Locomotion;
        public PhysicsProfile2DAuthoring Physics;
        public LevelExperienceConfig LevelExperience = LevelExperienceConfig.Disabled;
        public List<StatPresetEntryAuthoring> BaseStats =
            new List<StatPresetEntryAuthoring>();

        internal UnitPrototype BakeOrThrow(
            int tickRate = 30)
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
                InitialBuffConfigIds =
                    BakeInitialBuffConfigs(),
                UnitDisposePolicyId = UnitDisposePolicyId,
                RespawnConfig = RespawnConfig.BakeTime(tickRate),
                PoolConfig = PoolConfig,
                Loadout = Loadout,
                LocomotionProfile = locomotion,
                PhysicsProfile = physics,
            };
        }

        private BuffConfigId[] BakeInitialBuffConfigs()
        {
            if (InitialBuffConfigIds == null ||
                InitialBuffConfigIds.Length == 0)
            {
                return Array.Empty<BuffConfigId>();
            }
            var result =
                new BuffConfigId[
                    InitialBuffConfigIds.Length];
            for (int i = 0;
                 i < InitialBuffConfigIds.Length;
                 i++)
            {
                if (InitialBuffConfigIds[i] <= 0)
                {
                    throw new InvalidOperationException(
                        $"Prototype {UnitPrototypeId} initial BuffConfigId must be positive.");
                }
                result[i] =
                    new BuffConfigId(
                        InitialBuffConfigIds[i]);
            }
            return result;
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
            GlobalUnitPrototypeTable unitPrototypes,
            UnitDisposePolicyTable disposePolicies)
        {
            StatDefinitions = statDefinitions ??
                throw new ArgumentNullException(nameof(statDefinitions));
            UnitPrototypes = unitPrototypes ??
                throw new ArgumentNullException(nameof(unitPrototypes));
            DisposePolicies = disposePolicies;
        }

        public StatDefinitionTable StatDefinitions { get; }
        public GlobalUnitPrototypeTable UnitPrototypes { get; }
        public UnitDisposePolicyTable DisposePolicies { get; }
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
        [SerializeField] private UnitDisposePolicyTable disposePolicyTable;
#if UNITY_EDITOR
        [Header("Editor hero display auto-sync (design v10.2 17.x)")]
        [Tooltip("Hero prototypes automatically create matching avatar/name rows in this table.")]
        [SerializeField] private HeroDisplayTable heroDisplayTable;
#endif

        public IReadOnlyList<StatDefinitionAuthoring> StatDefinitions => statDefinitions;
        public IReadOnlyList<UnitPrototypeAuthoring> UnitPrototypes => unitPrototypes;
#if UNITY_EDITOR
        public UnitDisposePolicyTable DisposePolicyTableForEditor =>
            disposePolicyTable;

        public void ConfigureForEditor(
            IEnumerable<StatDefinitionAuthoring> definitions,
            IEnumerable<UnitPrototypeAuthoring> prototypes,
            UnitDisposePolicyTable disposePolicies,
            HeroDisplayTable displayTable)
        {
            statDefinitions = definitions != null
                ? new List<StatDefinitionAuthoring>(definitions)
                : new List<StatDefinitionAuthoring>();
            unitPrototypes = prototypes != null
                ? new List<UnitPrototypeAuthoring>(prototypes)
                : new List<UnitPrototypeAuthoring>();
            disposePolicyTable = disposePolicies;
            heroDisplayTable = displayTable;
        }

        public HeroDisplayTable HeroDisplayTableForSync =>
            heroDisplayTable;
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (heroDisplayTable == null)
                return;
            EditorApplication.delayCall -=
                DelaySyncHeroDisplay;
            EditorApplication.delayCall +=
                DelaySyncHeroDisplay;
        }

        private void DelaySyncHeroDisplay()
        {
            if (this == null ||
                heroDisplayTable == null)
                return;
            HeroDisplayTableSync.Sync(
                heroDisplayTable,
                this);
        }
#endif

        public BakedUnitRuntimeCatalog BakeOrThrow(
            GlobalPrefabTable prefabTable,
            int tickRate = 30)
        {
            return BakeCombinedOrThrow(
                new[] { this },
                prefabTable,
                tickRate);
        }

        public static BakedUnitRuntimeCatalog
            BakeCombinedOrThrow(
                IReadOnlyList<UnitRuntimeCatalogAsset> catalogs,
                GlobalPrefabTable prefabTable,
                int tickRate = 30)
        {
            if (catalogs == null || catalogs.Count == 0)
                throw new InvalidOperationException(
                    "Combined Unit catalog requires at least one partition.");
            if (prefabTable == null)
                throw new ArgumentNullException(nameof(prefabTable));
            prefabTable.ValidateOrThrow();
            DeterministicTimeConversion.ValidateSupportedTickRate(
                tickRate);

            var combinedDefinitions =
                new List<StatDefinitionAuthoring>();
            var combinedPrototypes =
                new List<UnitPrototypeAuthoring>();
            UnitDisposePolicyTable combinedDisposePolicies = null;
            for (int catalogIndex = 0;
                 catalogIndex < catalogs.Count;
                 catalogIndex++)
            {
                UnitRuntimeCatalogAsset catalog =
                    catalogs[catalogIndex] ??
                    throw new InvalidOperationException(
                        $"Unit catalog partition {catalogIndex} is null.");
                if (catalog.statDefinitions == null ||
                    catalog.unitPrototypes == null)
                    throw new InvalidOperationException(
                        $"Unit catalog partition '{catalog.name}' contains a null collection.");
                combinedDefinitions.AddRange(
                    catalog.statDefinitions);
                combinedPrototypes.AddRange(
                    catalog.unitPrototypes);
                if (catalog.disposePolicyTable != null)
                {
                    if (combinedDisposePolicies != null &&
                        combinedDisposePolicies !=
                            catalog.disposePolicyTable)
                        throw new InvalidOperationException(
                            "Unit content partitions reference different dispose-policy tables.");
                    combinedDisposePolicies =
                        catalog.disposePolicyTable;
                }
            }
            combinedDisposePolicies?.BakeTime(tickRate);

            var sortedDefinitions = new List<StatDefinitionAuthoring>(
                combinedDefinitions);
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
                combinedPrototypes);
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
                UnitPrototype prototype = authoring.BakeOrThrow(
                    tickRate);
                if (combinedDisposePolicies != null)
                {
                    if (!combinedDisposePolicies.TryGet(
                            prototype.UnitDisposePolicyId,
                            out UnitDisposePolicyEntry disposePolicy))
                        throw new InvalidOperationException(
                            $"Unit prototype {prototype.UnitPrototypeId} references missing dispose policy {prototype.UnitDisposePolicyId}.");
                    ValidateLifecycleConfiguration(
                        prototype,
                        disposePolicy);
                }
                GameObject prefab = prefabTable.GetRequiredPrefab(
                    PrefabKind.Unit, prototype.RuntimeEntityPrefabId);
                ValidatePrefabComposition(prefab, prototype);
                prototypeTable.Add(prototype);
            }

            prototypeTable.ValidateAll(definitionTable);
            return new BakedUnitRuntimeCatalog(
                definitionTable,
                prototypeTable,
                combinedDisposePolicies);
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
            // Unit Framework v27.3 1.7: only Physics/Stat/Buff are universal.
            // Movement / Attack / Ability / CrowdControl / Equipment presence
            // must match the authored HandlerLoadout (towers have no
            // Movement/Ability/Equipment; minions have no Ability/Equipment).
            RequireExactlyOne<PhysicsEntity2D>(prefab, prototype);
            RequireExactlyOne<StatHandler>(prefab, prototype);
            RequireExactlyOne<BuffHandler>(prefab, prototype);
            RequireLoadoutPresence<MovementHandler>(
                prefab, prototype, "MovementHandler",
                prototype.Loadout.HasMovement);
            RequireLoadoutPresence<AttackHandler>(
                prefab, prototype, "AttackHandler",
                prototype.Loadout.HasAttack);
            RequireLoadoutPresence<AbilityHandler>(
                prefab, prototype, "AbilityHandler",
                prototype.Loadout.HasAbility);
            RequireLoadoutPresence<CrowdControlHandler>(
                prefab, prototype, "CrowdControlHandler",
                prototype.Loadout.HasCrowdControl);
            RequireLoadoutPresence<EquipmentHandler>(
                prefab, prototype, "EquipmentHandler",
                prototype.Loadout.HasEquipment);
        }

        private static void RequireLoadoutPresence<T>(
            GameObject prefab,
            UnitPrototype prototype,
            string label,
            bool expected) where T : Component
        {
            int count =
                prefab.GetComponentsInChildren<T>(
                    true).Length;
            if (count > 1)
            {
                throw new InvalidOperationException(
                    $"Unit prefab {prototype.RuntimeEntityPrefabId} " +
                    $"has more than one {typeof(T).Name}.");
            }
            bool present = count == 1;
            if (present != expected)
            {
                throw new InvalidOperationException(
                    $"Unit prefab {prototype.RuntimeEntityPrefabId} " +
                    $"{label} presence ({present}) disagrees with " +
                    $"its HandlerLoadout ({expected}).");
            }
        }

        private static void ValidateLifecycleConfiguration(
            UnitPrototype prototype,
            in UnitDisposePolicyEntry disposePolicy)
        {
            if (disposePolicy.DeathPresentationTicks < 0)
                throw new InvalidOperationException(
                    $"Dispose policy {disposePolicy.Id} has a negative death presentation duration.");
            if (disposePolicy.Kind == UnitDisposePolicyKind.Pool &&
                (prototype.PoolConfig.PrewarmCount < 0 ||
                 prototype.PoolConfig.MaxCapacity <= 0))
                throw new InvalidOperationException(
                    $"Pooled Unit prototype {prototype.UnitPrototypeId} requires a valid PoolConfig.");
            if (disposePolicy.Kind == UnitDisposePolicyKind.SpawnRuin &&
                disposePolicy.RuinUnitPrototypeId <= 0)
                throw new InvalidOperationException(
                    $"Dispose policy {disposePolicy.Id} requires a ruin prototype.");
            if (disposePolicy.Kind != UnitDisposePolicyKind.KeepAlive &&
                prototype.RespawnConfig.CanRespawn)
                throw new InvalidOperationException(
                    $"Disposable Unit prototype {prototype.UnitPrototypeId} cannot use UnitWorld respawn.");
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
