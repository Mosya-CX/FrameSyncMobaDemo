using System;
using System.Collections.Generic;
using System.Linq;
using FrameSyncMoba.EditorTools.Addressables;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class MatchScopedContentConfigurationTests
    {
        private const string RootPath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";

        [Test]
        public void FormalRoot_SelectsOnlyCoreMapAndRequestedHeroes()
        {
            GlobalPrefabTable root = RequireAsset<GlobalPrefabTable>(RootPath);
            var selection = new MatchContentSelection(
                1,
                new[] { 1002, 1001, 1002 });

            IReadOnlyList<GlobalPrefabPartitionReference> both =
                root.SelectPartitions(
                    selection.MapConfigId,
                    selection.HeroConfigIds);

            Assert.That(both.Select(value => value.SubTableAddress), Is.EqualTo(
                new[]
                {
                    "content/table/core",
                    "content/table/map/1",
                    "content/table/hero/1001",
                    "content/table/hero/1002",
                }));
            IReadOnlyList<GlobalPrefabPartitionReference> varusOnly =
                root.SelectPartitions(1, new[] { 1001 });
            Assert.That(
                varusOnly.Any(value => value.OwnerConfigId == 1002),
                Is.False);
            Assert.Throws<InvalidOperationException>(
                () => root.SelectPartitions(1, new[] { 9999 }));
        }

        [Test]
        public void FormalPartitions_ArePathOnlyAndCoverOriginalTwentyEntries()
        {
            GlobalPrefabTable root = RequireAsset<GlobalPrefabTable>(RootPath);
            Assert.That(root.PrefabGroups, Is.Empty);
            Assert.That(root.Partitions.Count, Is.EqualTo(4));
            int entryCount = 0;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (GlobalPrefabPartitionReference reference in root.Partitions)
            {
                GlobalPrefabSubTableAsset table = LoadByAddress<GlobalPrefabSubTableAsset>(
                    reference.SubTableAddress);
                table.ValidateAgainst(reference);
                foreach (PrefabGroup group in table.PrefabGroups)
                foreach (PrefabEntry entry in group.Entries)
                {
                    entryCount++;
                    Assert.That(entry.HasLegacyDirectReference, Is.False);
                    Assert.That(keys.Add($"{group.Kind}/{entry.PrefabId}"), Is.True);
                    if (group.Kind == PrefabKind.Unit ||
                        group.Kind == PrefabKind.Projectile)
                        Assert.That(entry.LogicAssetAddress, Is.Not.Empty);
                    if (group.Kind == PrefabKind.ParticleVfx &&
                        entry.PrefabId >= 3101 &&
                        entry.PrefabId <= 3103)
                    {
                        Assert.That(
                            table.PartitionKind,
                            Is.EqualTo(GlobalPrefabPartitionKind.Hero));
                        Assert.That(
                            table.OwnerConfigId,
                            Is.EqualTo(1002),
                            $"Aatrox Q VFX {entry.PrefabId} belongs to Hero 1002.");
                    }
                }
            }
            Assert.That(entryCount, Is.EqualTo(20));
        }

        [TestCase(1001, 1002, 10011, 10021)]
        [TestCase(1002, 1001, 10021, 10011)]
        public void SelectedHeroCatalogs_BakeWithoutOtherHero(
            int selectedHero,
            int absentHero,
            int selectedAbility,
            int absentAbility)
        {
            GlobalPrefabTable root = RequireAsset<GlobalPrefabTable>(RootPath);
            IReadOnlyList<GlobalPrefabPartitionReference> references =
                root.SelectPartitions(1, new[] { selectedHero });
            GlobalPrefabSubTableAsset[] tables = references
                .Select(value => LoadByAddress<GlobalPrefabSubTableAsset>(
                    value.SubTableAddress))
                .ToArray();
            var resolved = new Dictionary<string, GameObject>(
                StringComparer.Ordinal);
            foreach (GlobalPrefabSubTableAsset table in tables)
            foreach (PrefabGroup group in table.PrefabGroups)
            foreach (PrefabEntry entry in group.Entries)
            {
                if (string.IsNullOrEmpty(entry.LogicAssetAddress) ||
                    resolved.ContainsKey(entry.LogicAssetAddress))
                    continue;
                resolved.Add(
                    entry.LogicAssetAddress,
                    RequireAsset<GameObject>(entry.LogicAssetAddress));
            }
            GlobalPrefabTable runtime = root.CreateResolvedRuntimeTable(
                tables,
                resolved);
            try
            {
                UnitRuntimeCatalogAsset[] units = LoadContent<UnitRuntimeCatalogAsset>(
                    tables,
                    MatchContentAssetKind.UnitRuntimeCatalog);
                AbilityRuntimeCatalogAsset[] abilities =
                    LoadContent<AbilityRuntimeCatalogAsset>(
                        tables,
                        MatchContentAssetKind.AbilityRuntimeCatalog);
                ProjectileRuntimeCatalogAsset[] projectiles =
                    LoadContent<ProjectileRuntimeCatalogAsset>(
                        tables,
                        MatchContentAssetKind.ProjectileRuntimeCatalog);

                BakedUnitRuntimeCatalog bakedUnits =
                    UnitRuntimeCatalogAsset.BakeCombinedOrThrow(
                        units,
                        runtime,
                        30);
                AbilityDefinitionRegistry bakedAbilities =
                    AbilityRuntimeCatalogAsset.BakeCombinedOrThrow(
                        abilities,
                        30);
                ProjectileDefRegistry bakedProjectiles =
                    ProjectileRuntimeCatalogAsset.BakeCombinedOrThrow(
                        projectiles,
                        runtime,
                        30);

                Assert.That(
                    bakedUnits.UnitPrototypes.TryGet(selectedHero, out _),
                    Is.True);
                Assert.That(
                    bakedUnits.UnitPrototypes.TryGet(absentHero, out _),
                    Is.False);
                Assert.That(
                    bakedAbilities.TryGet(selectedAbility, out _),
                    Is.True);
                Assert.That(
                    bakedAbilities.TryGet(absentAbility, out _),
                    Is.False);
                Assert.That(bakedProjectiles.Count, Is.GreaterThan(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtime);
            }
        }

        [Test]
        public void UnitCatalogPartitions_CoreAloneOwnsSharedDisposePolicies()
        {
            UnitRuntimeCatalogAsset core = RequireAsset<UnitRuntimeCatalogAsset>(
                MatchContentAddressablesMigration.RootFolder +
                "/CoreUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset varus = RequireAsset<UnitRuntimeCatalogAsset>(
                MatchContentAddressablesMigration.RootFolder +
                "/VarusUnitRuntimeCatalog.asset");
            UnitRuntimeCatalogAsset aatrox = RequireAsset<UnitRuntimeCatalogAsset>(
                MatchContentAddressablesMigration.RootFolder +
                "/AatroxUnitRuntimeCatalog.asset");

            Assert.That(
                core.DisposePolicyTableForEditor,
                Is.Not.Null,
                "Core is always loaded and owns shared Unit configuration.");
            Assert.That(
                varus.DisposePolicyTableForEditor,
                Is.Null,
                "Hero partitions must not duplicate Core's policy asset into another bundle.");
            Assert.That(
                aatrox.DisposePolicyTableForEditor,
                Is.Null,
                "Hero partitions must not duplicate Core's policy asset into another bundle.");

            string policyPath = AssetDatabase.GetAssetPath(
                core.DisposePolicyTableForEditor);
            Assert.That(policyPath, Is.Not.Empty);
            Assert.That(
                AssetDatabase.GetDependencies(
                    AssetDatabase.GetAssetPath(varus),
                    true),
                Does.Not.Contain(policyPath));
            Assert.That(
                AssetDatabase.GetDependencies(
                    AssetDatabase.GetAssetPath(aatrox),
                    true),
                Does.Not.Contain(policyPath));
        }

        [Test]
        public void LogicAndHeroGroups_AreLocalAndPartitioned()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            foreach (string groupName in AddressablesProjectConstants.LocalGroups)
            {
                AddressableAssetGroup group = settings.FindGroup(groupName);
                Assert.That(group, Is.Not.Null, groupName);
                BundledAssetGroupSchema schema =
                    group.GetSchema<BundledAssetGroupSchema>();
                Assert.That(schema, Is.Not.Null, groupName);
                Assert.That(schema.IncludeInBuild, Is.True, groupName);
                Assert.That(schema.LoadPath.GetValue(settings),
                    Does.Not.Contain("http").IgnoreCase);
            }
            AssertAddressGroup(
                "view/unit/1101",
                AddressablesProjectConstants.ClientHero1001Group);
            AssertAddressGroup(
                "view/unit/1102",
                AddressablesProjectConstants.ClientHero1002Group);
            AssertAddressGroup(
                "vfx/3101",
                AddressablesProjectConstants.ClientHero1002Group);
            AssertAddressGroup(
                "vfx/4102",
                AddressablesProjectConstants.ClientHero1001Group);
            AssertAddressGroup(
                "content/table/hero/1001",
                AddressablesProjectConstants.LogicHero1001Group);
            AssertAddressGroup(
                "content/table/hero/1002",
                AddressablesProjectConstants.LogicHero1002Group);
        }

        [Test]
        public void SessionSelection_IsStableAndResettable()
        {
            var selection = new MatchContentSelection(1, new[] { 1002 });
            Assert.That(selection.ContainsHeroConfigId(1001), Is.False);
            Assert.That(selection.ContainsHeroConfigId(1002), Is.True);

            GameSessionContext.ResetSession();
            GameSessionContext.SetSelectedMatchContent(
                1,
                new[] { 1002, 1001, 1002 });
            Assert.That(GameSessionContext.SelectedMapConfigId, Is.EqualTo(1));
            Assert.That(GameSessionContext.SelectedHeroConfigIds,
                Is.EqualTo(new[] { 1001, 1002 }));
            GameSessionContext.ResetSession();
            Assert.That(GameSessionContext.SelectedMapConfigId, Is.Zero);
            Assert.That(GameSessionContext.SelectedHeroConfigIds, Is.Null);
        }

        [Test]
        public void FormalGameScene_HasNoLegacyDirectCatalogReferences()
        {
            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/GameScene.unity",
                OpenSceneMode.Additive);
            try
            {
                GameBootstrap bootstrap = scene.GetRootGameObjects()
                    .SelectMany(value => value.GetComponentsInChildren<
                        GameBootstrap>(true))
                    .Single();
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
                    Assert.That(
                        serialized.FindProperty(field).objectReferenceValue,
                        Is.Null,
                        field);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static T[] LoadContent<T>(
            IEnumerable<GlobalPrefabSubTableAsset> tables,
            MatchContentAssetKind kind)
            where T : UnityEngine.Object
        {
            return tables
                .SelectMany(value => value.ContentAssets)
                .Where(value => value.AssetKind == kind)
                .Select(value => RequireAsset<T>(value.Address))
                .ToArray();
        }

        private static void AssertAddressGroup(
            string address,
            string expectedGroup)
        {
            AddressableAssetEntry entry = FindEntry(address);
            Assert.That(entry, Is.Not.Null, address);
            Assert.That(entry.parentGroup.Name, Is.EqualTo(expectedGroup));
        }

        private static T LoadByAddress<T>(string address)
            where T : UnityEngine.Object
        {
            AddressableAssetEntry entry = FindEntry(address);
            Assert.That(entry, Is.Not.Null, address);
            return RequireAsset<T>(
                AssetDatabase.GUIDToAssetPath(entry.guid));
        }

        private static AddressableAssetEntry FindEntry(string address)
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;
                AddressableAssetEntry entry = group.entries.FirstOrDefault(
                    value => string.Equals(
                        value.address,
                        address,
                        StringComparison.Ordinal));
                if (entry != null)
                    return entry;
            }
            return null;
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(value, Is.Not.Null, path);
            return value;
        }
    }
}
