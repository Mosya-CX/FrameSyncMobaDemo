using System;
using System.Collections.Generic;
using System.Linq;
using FrameSyncMoba.Bootstrap;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class MatchContentAddressablesMigration
    {
        public const string RootFolder =
            "Assets/Config/Formal/MatchContent";
        private const string RootTablePath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";
        private const string FullUnitCatalogPath =
            "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset";
        private const string FullAbilityCatalogPath =
            "Assets/Config/Formal/Abilities/FormalHeroAbilityRuntimeCatalog.asset";
        private const string VarusAbilityCatalogPath =
            "Assets/Config/Formal/Abilities/VarusAbilityRuntimeCatalog.asset";
        private const string FullProjectileCatalogPath =
            "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset";
        private const string FullBuffCatalogPath =
            "Assets/Config/Formal/Buffs/FullMatchTestBuffCatalog.asset";
        private const string CrowdControlCatalogPath =
            "Assets/Config/Formal/CrowdControl/CrowdControlCatalog.asset";
        private const string EquipmentCatalogPath =
            "Assets/Config/Formal/Equipment/FormalEquipmentCatalog.asset";
        private const string MapConfigPath =
            "Assets/Config/Formal/FullMatchDeterministicMapConfig.asset";

        [MenuItem("FrameSyncMoba/Addressables/Migrate Match-Scoped Content")]
        public static void Migrate()
        {
            LocalAddressablesConfigurator.Configure();
            EnsureFolder(RootFolder);

            GlobalPrefabTable root = RequireAsset<GlobalPrefabTable>(
                RootTablePath);
            if (root.PrefabGroups.Count == 0)
            {
                ConfigureUnitCatalogAssets();
                RepairPrefabPartitionOwnership();
                AssetDatabase.SaveAssets();
                RefreshPartitionHashes(root);
                ValidateExistingMigration(root);
                ConfigureAddressables();
                ClearGameSceneLegacyCatalogReferences();
                AssetDatabase.SaveAssets();
                return;
            }

            List<PrefabGroup> sourceGroups =
                root.PrefabGroups.ToList();
            var coreGroups = new Dictionary<PrefabKind, List<PrefabEntry>>();
            var mapGroups = new Dictionary<PrefabKind, List<PrefabEntry>>();
            var varusGroups = new Dictionary<PrefabKind, List<PrefabEntry>>();
            var aatroxGroups = new Dictionary<PrefabKind, List<PrefabEntry>>();
            foreach (PrefabGroup group in sourceGroups)
            {
                foreach (PrefabEntry entry in group.Entries)
                {
                    Dictionary<PrefabKind, List<PrefabEntry>> target =
                        SelectPartition(
                            group.Kind,
                            entry.PrefabId,
                            coreGroups,
                            mapGroups,
                            varusGroups,
                            aatroxGroups);
                    Add(
                        target,
                        group.Kind,
                        ToAddressEntry(entry));
                }
            }

            UnitRuntimeCatalogAsset sourceUnits =
                RequireAsset<UnitRuntimeCatalogAsset>(FullUnitCatalogPath);
            UnitRuntimeCatalogAsset coreUnits =
                GetOrCreate<UnitRuntimeCatalogAsset>(
                    RootFolder + "/CoreUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset varusUnits =
                GetOrCreate<UnitRuntimeCatalogAsset>(
                    RootFolder + "/VarusUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset aatroxUnits =
                GetOrCreate<UnitRuntimeCatalogAsset>(
                    RootFolder + "/AatroxUnitRuntimeCatalog.asset");
            coreUnits.ConfigureForEditor(
                sourceUnits.StatDefinitions,
                sourceUnits.UnitPrototypes.Where(
                    value => value.UnitPrototypeId != 1001 &&
                             value.UnitPrototypeId != 1002),
                sourceUnits.DisposePolicyTableForEditor,
                null);
            varusUnits.ConfigureForEditor(
                Array.Empty<StatDefinitionAuthoring>(),
                sourceUnits.UnitPrototypes.Where(
                    value => value.UnitPrototypeId == 1001),
                null,
                null);
            aatroxUnits.ConfigureForEditor(
                Array.Empty<StatDefinitionAuthoring>(),
                sourceUnits.UnitPrototypes.Where(
                    value => value.UnitPrototypeId == 1002),
                null,
                null);
            EditorUtility.SetDirty(coreUnits);
            EditorUtility.SetDirty(varusUnits);
            EditorUtility.SetDirty(aatroxUnits);

            AbilityRuntimeCatalogAsset fullAbilities =
                RequireAsset<AbilityRuntimeCatalogAsset>(
                    FullAbilityCatalogPath);
            AbilityRuntimeCatalogAsset aatroxAbilities =
                GetOrCreate<AbilityRuntimeCatalogAsset>(
                    RootFolder + "/AatroxAbilityRuntimeCatalog.asset");
            ConfigureHeroAbilityCatalog(
                aatroxAbilities,
                fullAbilities,
                10020,
                10021,
                10024);

            ProjectileRuntimeCatalogAsset sourceProjectiles =
                RequireAsset<ProjectileRuntimeCatalogAsset>(
                    FullProjectileCatalogPath);
            ProjectileRuntimeCatalogAsset coreProjectiles =
                CreateProjectileCatalog(
                    "CoreProjectileRuntimeCatalog.asset",
                    sourceProjectiles.Definitions.Where(
                        value => value.RuntimeEntityPrefabId == 2201 ||
                                 value.RuntimeEntityPrefabId == 2202));
            ProjectileRuntimeCatalogAsset varusProjectiles =
                CreateProjectileCatalog(
                    "VarusProjectileRuntimeCatalog.asset",
                    sourceProjectiles.Definitions.Where(
                        value => value.RuntimeEntityPrefabId >= 2101 &&
                                 value.RuntimeEntityPrefabId <= 2104));
            ProjectileRuntimeCatalogAsset aatroxProjectiles =
                CreateProjectileCatalog(
                    "AatroxProjectileRuntimeCatalog.asset",
                    sourceProjectiles.Definitions.Where(
                        value => value.RuntimeEntityPrefabId == 2105 ||
                                 value.RuntimeEntityPrefabId == 2106));

            BuffCatalogAsset sourceBuffs =
                RequireAsset<BuffCatalogAsset>(FullBuffCatalogPath);
            BuffCatalogAsset coreBuffs = CreateBuffCatalog(
                "CoreBuffCatalog.asset",
                sourceBuffs.Definitions.Where(
                    value => ClassifyBuff(value) == 0));
            BuffCatalogAsset varusBuffs = CreateBuffCatalog(
                "VarusBuffCatalog.asset",
                sourceBuffs.Definitions.Where(
                    value => ClassifyBuff(value) == 1001));
            BuffCatalogAsset aatroxBuffs = CreateBuffCatalog(
                "AatroxBuffCatalog.asset",
                sourceBuffs.Definitions.Where(
                    value => ClassifyBuff(value) == 1002));

            AssetDatabase.SaveAssets();

            GlobalPrefabSubTableAsset core = ConfigureSubTable(
                "CoreGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Core,
                0,
                coreGroups,
                new[]
                {
                    Asset(MatchContentAssetKind.UnitRuntimeCatalog, coreUnits),
                    Asset(MatchContentAssetKind.ProjectileRuntimeCatalog, coreProjectiles),
                    Asset(MatchContentAssetKind.BuffCatalog, coreBuffs),
                    Asset(MatchContentAssetKind.CrowdControlCatalog,
                        RequireAsset<CrowdControlCatalogAsset>(CrowdControlCatalogPath)),
                    Asset(MatchContentAssetKind.EquipmentCatalog,
                        RequireAsset<EquipmentCatalogAsset>(EquipmentCatalogPath)),
                });
            GlobalPrefabSubTableAsset map = ConfigureSubTable(
                "Map1GlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Map,
                1,
                mapGroups,
                new[]
                {
                    Asset(MatchContentAssetKind.DeterministicMapConfig,
                        RequireAsset<DeterministicMapConfig>(MapConfigPath)),
                });
            GlobalPrefabSubTableAsset varus = ConfigureSubTable(
                "VarusGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Hero,
                1001,
                varusGroups,
                new[]
                {
                    Asset(MatchContentAssetKind.UnitRuntimeCatalog, varusUnits),
                    Asset(MatchContentAssetKind.AbilityRuntimeCatalog,
                        RequireAsset<AbilityRuntimeCatalogAsset>(VarusAbilityCatalogPath)),
                    Asset(MatchContentAssetKind.ProjectileRuntimeCatalog, varusProjectiles),
                    Asset(MatchContentAssetKind.BuffCatalog, varusBuffs),
                });
            GlobalPrefabSubTableAsset aatrox = ConfigureSubTable(
                "AatroxGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Hero,
                1002,
                aatroxGroups,
                new[]
                {
                    Asset(MatchContentAssetKind.UnitRuntimeCatalog, aatroxUnits),
                    Asset(MatchContentAssetKind.AbilityRuntimeCatalog, aatroxAbilities),
                    Asset(MatchContentAssetKind.ProjectileRuntimeCatalog, aatroxProjectiles),
                    Asset(MatchContentAssetKind.BuffCatalog, aatroxBuffs),
                });

            var references = new[]
            {
                Reference(core, "content/table/core"),
                Reference(map, "content/table/map/1"),
                Reference(varus, "content/table/hero/1001"),
                Reference(aatrox, "content/table/hero/1002"),
            };
            root.ConfigureAddressableRootForEditor(references);
            EditorUtility.SetDirty(root);

            ConfigureAddressables();
            ClearGameSceneLegacyCatalogReferences();
            root.ValidateOrThrow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[MatchContentMigration] Created Core, Map/1, Hero/1001 and Hero/1002 path-only partitions and migrated GameScene to async match-scoped loading.");
        }

        private static Dictionary<PrefabKind, List<PrefabEntry>> SelectPartition(
            PrefabKind kind,
            int prefabId,
            Dictionary<PrefabKind, List<PrefabEntry>> core,
            Dictionary<PrefabKind, List<PrefabEntry>> map,
            Dictionary<PrefabKind, List<PrefabEntry>> varus,
            Dictionary<PrefabKind, List<PrefabEntry>> aatrox)
        {
            if (kind == PrefabKind.Misc && prefabId == 5001)
                return map;
            if ((kind == PrefabKind.Unit && prefabId == 1101) ||
                (kind == PrefabKind.Projectile && prefabId >= 2101 && prefabId <= 2104))
                return varus;
            if ((kind == PrefabKind.Unit && prefabId == 1102) ||
                (kind == PrefabKind.Projectile && (prefabId == 2105 || prefabId == 2106)) ||
                (kind == PrefabKind.ParticleVfx && prefabId >= 3101 && prefabId <= 3103))
                return aatrox;
            return core;
        }

        private static void RepairPrefabPartitionOwnership()
        {
            GlobalPrefabSubTableAsset core =
                RequireAsset<GlobalPrefabSubTableAsset>(
                    RootFolder + "/CoreGlobalPrefabSubTable.asset");
            GlobalPrefabSubTableAsset map =
                RequireAsset<GlobalPrefabSubTableAsset>(
                    RootFolder + "/Map1GlobalPrefabSubTable.asset");
            GlobalPrefabSubTableAsset varus =
                RequireAsset<GlobalPrefabSubTableAsset>(
                    RootFolder + "/VarusGlobalPrefabSubTable.asset");
            GlobalPrefabSubTableAsset aatrox =
                RequireAsset<GlobalPrefabSubTableAsset>(
                    RootFolder + "/AatroxGlobalPrefabSubTable.asset");
            GlobalPrefabSubTableAsset[] sources =
                { core, map, varus, aatrox };
            var coreGroups =
                new Dictionary<PrefabKind, List<PrefabEntry>>();
            var mapGroups =
                new Dictionary<PrefabKind, List<PrefabEntry>>();
            var varusGroups =
                new Dictionary<PrefabKind, List<PrefabEntry>>();
            var aatroxGroups =
                new Dictionary<PrefabKind, List<PrefabEntry>>();
            for (int tableIndex = 0;
                 tableIndex < sources.Length;
                 tableIndex++)
            {
                GlobalPrefabSubTableAsset source = sources[tableIndex];
                for (int groupIndex = 0;
                     groupIndex < source.PrefabGroups.Count;
                     groupIndex++)
                {
                    PrefabGroup group = source.PrefabGroups[groupIndex];
                    for (int entryIndex = 0;
                         entryIndex < group.Entries.Count;
                         entryIndex++)
                    {
                        PrefabEntry entry = group.Entries[entryIndex];
                        Dictionary<PrefabKind, List<PrefabEntry>> target =
                            SelectPartition(
                                group.Kind,
                                entry.PrefabId,
                                coreGroups,
                                mapGroups,
                                varusGroups,
                                aatroxGroups);
                        Add(target, group.Kind, entry);
                    }
                }
            }

            ConfigureSubTable(
                "CoreGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Core,
                0,
                coreGroups,
                core.ContentAssets.ToArray());
            ConfigureSubTable(
                "Map1GlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Map,
                1,
                mapGroups,
                map.ContentAssets.ToArray());
            ConfigureSubTable(
                "VarusGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Hero,
                1001,
                varusGroups,
                varus.ContentAssets.ToArray());
            ConfigureSubTable(
                "AatroxGlobalPrefabSubTable.asset",
                GlobalPrefabPartitionKind.Hero,
                1002,
                aatroxGroups,
                aatrox.ContentAssets.ToArray());
        }

        private static PrefabEntry ToAddressEntry(PrefabEntry source)
        {
            string path = source.UnityPrefab != null
                ? AssetDatabase.GetAssetPath(source.UnityPrefab)
                : source.LogicAssetAddress;
            return new PrefabEntry(
                source.PrefabId,
                path,
                source.GameplayConfigId,
                source.EditorAssetGuid,
                source.ClientViewAddress);
        }

        private static void Add(
            Dictionary<PrefabKind, List<PrefabEntry>> groups,
            PrefabKind kind,
            PrefabEntry entry)
        {
            if (!groups.TryGetValue(kind, out List<PrefabEntry> entries))
            {
                entries = new List<PrefabEntry>();
                groups.Add(kind, entries);
            }
            entries.Add(entry);
        }

        private static GlobalPrefabSubTableAsset ConfigureSubTable(
            string fileName,
            GlobalPrefabPartitionKind kind,
            int ownerId,
            Dictionary<PrefabKind, List<PrefabEntry>> groups,
            MatchContentAssetAddress[] assets)
        {
            GlobalPrefabSubTableAsset value =
                GetOrCreate<GlobalPrefabSubTableAsset>(
                    RootFolder + "/" + fileName);
            PrefabGroup[] prefabGroups = groups
                .OrderBy(pair => (byte)pair.Key)
                .Select(pair => new PrefabGroup(
                    pair.Key,
                    pair.Value.OrderBy(entry => entry.PrefabId)))
                .ToArray();
            ulong hash = ComputeHash(kind, ownerId, prefabGroups, assets);
            value.ConfigureForEditor(
                kind,
                ownerId,
                1,
                hash,
                prefabGroups,
                assets);
            EditorUtility.SetDirty(value);
            value.ValidateOrThrow();
            return value;
        }

        private static GlobalPrefabPartitionReference Reference(
            GlobalPrefabSubTableAsset table,
            string address)
        {
            return new GlobalPrefabPartitionReference(
                table.PartitionKind,
                table.OwnerConfigId,
                address,
                table.ContentVersion,
                table.ContentHash);
        }

        private static MatchContentAssetAddress Asset(
            MatchContentAssetKind kind,
            UnityEngine.Object value)
        {
            return new MatchContentAssetAddress(
                kind,
                AssetDatabase.GetAssetPath(value));
        }

        private static void ConfigureHeroAbilityCatalog(
            AbilityRuntimeCatalogAsset target,
            AbilityRuntimeCatalogAsset source,
            int passiveId,
            int minAbilityId,
            int maxAbilityId)
        {
            AbilityAsset[] abilities = source.Abilities
                .Where(value => value.AbilityId >= minAbilityId &&
                                value.AbilityId <= maxAbilityId)
                .ToArray();
            FixedPassiveDefinitionAsset[] passives = source.FixedPassives
                .Where(value => value.AbilityId == passiveId)
                .ToArray();
            var slots = new List<AbilitySlotDef>();
            foreach (AbilitySlotDef sourceSlot in source.Slots)
            {
                int[] ids = sourceSlot.AbilityIds
                    .Where(value => value >= minAbilityId && value <= maxAbilityId)
                    .ToArray();
                if (ids.Length == 0)
                    continue;
                slots.Add(new AbilitySlotDef
                {
                    SlotId = sourceSlot.SlotId,
                    MaxAllocatedPoints = sourceSlot.MaxAllocatedPoints,
                    RequiredUnitLevelByRank =
                        (int[])sourceSlot.RequiredUnitLevelByRank.Clone(),
                    AbilityIds = ids,
                    InitialActiveAbilityId = ids[0],
                });
            }
            target.ConfigureForEditor(abilities, slots, passives);
            EditorUtility.SetDirty(target);
        }

        private static ProjectileRuntimeCatalogAsset CreateProjectileCatalog(
            string fileName,
            IEnumerable<ProjectileDefinitionAuthoring> definitions)
        {
            ProjectileRuntimeCatalogAsset value =
                GetOrCreate<ProjectileRuntimeCatalogAsset>(
                    RootFolder + "/" + fileName);
            value.ConfigureForEditor(definitions);
            EditorUtility.SetDirty(value);
            return value;
        }

        private static void ConfigureUnitCatalogAssets()
        {
            UnitRuntimeCatalogAsset source =
                RequireAsset<UnitRuntimeCatalogAsset>(FullUnitCatalogPath);
            UnitRuntimeCatalogAsset core =
                RequireAsset<UnitRuntimeCatalogAsset>(
                    RootFolder + "/CoreUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset varus =
                RequireAsset<UnitRuntimeCatalogAsset>(
                    RootFolder + "/VarusUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset aatrox =
                RequireAsset<UnitRuntimeCatalogAsset>(
                    RootFolder + "/AatroxUnitRuntimeCatalog.asset");
            core.ConfigureForEditor(
                source.StatDefinitions,
                source.UnitPrototypes.Where(
                    value => value.UnitPrototypeId != 1001 &&
                             value.UnitPrototypeId != 1002),
                source.DisposePolicyTableForEditor,
                null);
            varus.ConfigureForEditor(
                Array.Empty<StatDefinitionAuthoring>(),
                source.UnitPrototypes.Where(
                    value => value.UnitPrototypeId == 1001),
                null,
                null);
            aatrox.ConfigureForEditor(
                Array.Empty<StatDefinitionAuthoring>(),
                source.UnitPrototypes.Where(
                    value => value.UnitPrototypeId == 1002),
                null,
                null);
            EditorUtility.SetDirty(core);
            EditorUtility.SetDirty(varus);
            EditorUtility.SetDirty(aatrox);
        }

        private static BuffCatalogAsset CreateBuffCatalog(
            string fileName,
            IEnumerable<BuffDefinition> definitions)
        {
            BuffCatalogAsset value = GetOrCreate<BuffCatalogAsset>(
                RootFolder + "/" + fileName);
            value.Definitions = definitions.ToArray();
            EditorUtility.SetDirty(value);
            return value;
        }

        private static int ClassifyBuff(BuffDefinition definition)
        {
            string path = AssetDatabase.GetAssetPath(definition);
            if (path.IndexOf("/Aatrox/", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1002;
            string name = definition.name;
            if (name.IndexOf("Blight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Corruption", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Desecrated", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("MinionMuncher", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("MinionPincushion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("RVine", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1001;
            return 0;
        }

        private static void ConfigureAddressables()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "Addressables settings are missing.");

            MakeAddressable(settings,
                RootFolder + "/CoreGlobalPrefabSubTable.asset",
                AddressablesProjectConstants.LogicCoreGroup,
                "content/table/core");
            MakeAddressable(settings,
                RootFolder + "/Map1GlobalPrefabSubTable.asset",
                AddressablesProjectConstants.LogicMap1Group,
                "content/table/map/1");
            MakeAddressable(settings,
                RootFolder + "/VarusGlobalPrefabSubTable.asset",
                AddressablesProjectConstants.LogicHero1001Group,
                "content/table/hero/1001");
            MakeAddressable(settings,
                RootFolder + "/AatroxGlobalPrefabSubTable.asset",
                AddressablesProjectConstants.LogicHero1002Group,
                "content/table/hero/1002");

            AssignPartitionAssets(settings,
                RequireAsset<GlobalPrefabSubTableAsset>(RootFolder + "/CoreGlobalPrefabSubTable.asset"),
                AddressablesProjectConstants.LogicCoreGroup);
            AssignPartitionAssets(settings,
                RequireAsset<GlobalPrefabSubTableAsset>(RootFolder + "/Map1GlobalPrefabSubTable.asset"),
                AddressablesProjectConstants.LogicMap1Group);
            AssignPartitionAssets(settings,
                RequireAsset<GlobalPrefabSubTableAsset>(RootFolder + "/VarusGlobalPrefabSubTable.asset"),
                AddressablesProjectConstants.LogicHero1001Group);
            AssignPartitionAssets(settings,
                RequireAsset<GlobalPrefabSubTableAsset>(RootFolder + "/AatroxGlobalPrefabSubTable.asset"),
                AddressablesProjectConstants.LogicHero1002Group);

            MoveAddress(settings, "view/unit/1101",
                AddressablesProjectConstants.ClientHero1001Group);
            MoveAddress(settings, "view/unit/1102",
                AddressablesProjectConstants.ClientHero1002Group);
            for (int id = 2101; id <= 2104; id++)
                MoveAddress(settings, $"view/projectile/{id}",
                    AddressablesProjectConstants.ClientHero1001Group);
            for (int id = 2105; id <= 2106; id++)
                MoveAddress(settings, $"view/projectile/{id}",
                    AddressablesProjectConstants.ClientHero1002Group);
            for (int id = 3101; id <= 3103; id++)
                MoveAddress(settings, $"vfx/{id}",
                    AddressablesProjectConstants.ClientHero1002Group);
            for (int id = 4102; id <= 4104; id++)
                MoveAddress(settings, $"vfx/{id}",
                    AddressablesProjectConstants.ClientHero1001Group);
            EditorUtility.SetDirty(settings);
        }

        private static void AssignPartitionAssets(
            AddressableAssetSettings settings,
            GlobalPrefabSubTableAsset table,
            string groupName)
        {
            foreach (PrefabGroup group in table.PrefabGroups)
                foreach (PrefabEntry entry in group.Entries)
                    if (!string.IsNullOrEmpty(entry.LogicAssetAddress))
                        MakeAddressable(settings,
                            entry.LogicAssetAddress,
                            groupName,
                            entry.LogicAssetAddress);
            foreach (MatchContentAssetAddress asset in table.ContentAssets)
                MakeAddressable(settings,
                    asset.Address,
                    groupName,
                    asset.Address);
        }

        private static void MakeAddressable(
            AddressableAssetSettings settings,
            string path,
            string groupName,
            string address)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException(
                    $"Cannot address missing asset '{path}'.");
            AddressableAssetGroup group = settings.FindGroup(groupName) ??
                throw new InvalidOperationException(
                    $"Addressables group '{groupName}' is missing.");
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(
                guid,
                group,
                false,
                false);
            entry.address = address;
            EditorUtility.SetDirty(group);
        }

        private static void MoveAddress(
            AddressableAssetSettings settings,
            string address,
            string groupName)
        {
            AddressableAssetEntry found = null;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;
                found = group.entries.FirstOrDefault(
                    entry => string.Equals(
                        entry.address,
                        address,
                        StringComparison.Ordinal));
                if (found != null)
                    break;
            }
            if (found == null)
                throw new InvalidOperationException(
                    $"Addressable presentation root '{address}' is missing.");
            AddressableAssetGroup target = settings.FindGroup(groupName) ??
                throw new InvalidOperationException(
                    $"Addressables group '{groupName}' is missing.");
            settings.CreateOrMoveEntry(found.guid, target, false, false)
                .address = address;
        }

        private static void ClearGameSceneLegacyCatalogReferences()
        {
            string guid = AssetDatabase.FindAssets("GameScene t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(path =>
                    string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(path),
                        "GameScene",
                        StringComparison.Ordinal));
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("GameScene asset is missing.");
            Scene scene = EditorSceneManager.OpenScene(
                guid,
                OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (GameBootstrap bootstrap in
                         root.GetComponentsInChildren<GameBootstrap>(true))
                {
                    var serialized = new SerializedObject(bootstrap);
                    foreach (string field in new[]
                    {
                        "unitRuntimeCatalog",
                        "abilityRuntimeCatalog",
                        "projectileRuntimeCatalog",
                        "deterministicMapConfig",
                        "equipmentCatalog",
                        "buffCatalog",
                        "crowdControlCatalog",
                    })
                        serialized.FindProperty(field).objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static ulong ComputeHash(
            GlobalPrefabPartitionKind kind,
            int ownerId,
            IEnumerable<PrefabGroup> groups,
            IEnumerable<MatchContentAssetAddress> assets)
        {
            ulong hash = 14695981039346656037UL;
            void AddText(string text)
            {
                foreach (char value in text)
                {
                    hash ^= value;
                    hash *= 1099511628211UL;
                }
            }
            AddText($"{(byte)kind}:{ownerId}:");
            foreach (PrefabGroup group in groups)
            foreach (PrefabEntry entry in group.Entries)
            {
                AddText($"{(byte)group.Kind}:{entry.PrefabId}:{entry.LogicAssetAddress}:{entry.ClientViewAddress};");
                AddDependencyHash(entry.LogicAssetAddress);
            }
            foreach (MatchContentAssetAddress asset in assets.OrderBy(value => (byte)value.AssetKind))
            {
                AddText($"{(byte)asset.AssetKind}:{asset.Address};");
                AddDependencyHash(asset.Address);
            }
            return hash == 0 ? 1UL : hash;

            void AddDependencyHash(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath))
                    return;
                AddText(
                    AssetDatabase.GetAssetDependencyHash(assetPath)
                        .ToString());
            }
        }

        private static void RefreshPartitionHashes(
            GlobalPrefabTable root)
        {
            string[] paths =
            {
                RootFolder + "/CoreGlobalPrefabSubTable.asset",
                RootFolder + "/Map1GlobalPrefabSubTable.asset",
                RootFolder + "/VarusGlobalPrefabSubTable.asset",
                RootFolder + "/AatroxGlobalPrefabSubTable.asset",
            };
            string[] addresses =
            {
                "content/table/core",
                "content/table/map/1",
                "content/table/hero/1001",
                "content/table/hero/1002",
            };
            var references =
                new GlobalPrefabPartitionReference[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                GlobalPrefabSubTableAsset table =
                    RequireAsset<GlobalPrefabSubTableAsset>(paths[i]);
                PrefabGroup[] groups = table.PrefabGroups.ToArray();
                MatchContentAssetAddress[] assets =
                    table.ContentAssets.ToArray();
                ulong hash = ComputeHash(
                    table.PartitionKind,
                    table.OwnerConfigId,
                    groups,
                    assets);
                table.ConfigureForEditor(
                    table.PartitionKind,
                    table.OwnerConfigId,
                    table.ContentVersion,
                    hash,
                    groups,
                    assets);
                EditorUtility.SetDirty(table);
                references[i] = Reference(table, addresses[i]);
            }
            root.ConfigureAddressableRootForEditor(references);
            EditorUtility.SetDirty(root);
        }

        private static T GetOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null)
                return value;
            value = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null)
                throw new InvalidOperationException(
                    $"Required asset '{path}' is missing or is not {typeof(T).Name}.");
            return value;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static void ValidateExistingMigration(
            GlobalPrefabTable root)
        {
            root.ValidateOrThrow();
            if (root.Partitions.Count != 4)
                throw new InvalidOperationException(
                    "Existing match-content migration must contain exactly four formal partitions.");
        }
    }
}
