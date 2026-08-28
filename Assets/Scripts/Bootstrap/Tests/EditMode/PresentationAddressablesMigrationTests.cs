using System;
using System.Collections.Generic;
using System.IO;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class PresentationAddressablesMigrationTests
    {
        private const string TablePath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";

        [Test]
        public void ProjectilesAreSplitIntoLogicAndAddressableViews()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            List<PrefabEntry> entries = FindEntries(
                table,
                settings,
                PrefabKind.Projectile);
            Assert.That(entries.Count, Is.EqualTo(8));
            for (int i = 0; i < entries.Count; i++)
            {
                PrefabEntry entry = entries[i];
                AddressableAssetEntry logic =
                    FindEntryByAddress(settings, entry.LogicAssetAddress);
                Assert.That(logic, Is.Not.Null, entry.LogicAssetAddress);
                string logicPath = logic.AssetPath;
                GameObject logicPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(logicPath);
                Assert.That(logicPath,
                    Does.StartWith(
                        "Assets/Config/Formal/Prefabs/Logic/Projectile/"));
                Assert.That(
                    logicPrefab.GetComponent<PhysicsEntity2D>(),
                    Is.Not.Null,
                    logicPath);
                Assert.That(
                    logicPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Empty,
                    logicPath);
                AddressableAssetEntry view =
                    FindEntryByAddress(settings, entry.ClientViewAddress);
                Assert.That(view, Is.Not.Null, entry.ClientViewAddress);
                GameObject viewPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(view.AssetPath);
                Assert.That(viewPrefab.GetComponent<PhysicsEntity2D>(), Is.Null);
                Assert.That(
                    viewPrefab.GetComponentsInChildren<Renderer>(true),
                    Is.Not.Empty,
                    view.AssetPath);
            }
        }

        [Test]
        public void ProjectileViewRootsAreAtWorldOrigin()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/ClientContent/Views/Projectile" });
            Assert.That(guids.Length, Is.EqualTo(8));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                // The view is parented under the projectile physics root
                // (worldPositionStays=false). Any authored root offset
                // displaces the model from the logic point; only child
                // (e.g. Model) sub-offsets are intentional.
                Assert.That(
                    prefab.transform.localPosition,
                    Is.EqualTo(Vector3.zero),
                    $"Projectile view root '{path}' must sit at the world origin.");
            }
        }

        [Test]
        public void MapLogicAndClientViewHaveDisjointResponsibilities()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(
                TryFindEntry(
                    table,
                    settings,
                    PrefabKind.Misc,
                    5001,
                    out PrefabEntry entry),
                Is.True);
            AddressableAssetEntry logic =
                FindEntryByAddress(settings, entry.LogicAssetAddress);
            Assert.That(logic, Is.Not.Null, entry.LogicAssetAddress);
            GameObject logicPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(logic.AssetPath);
            Assert.That(logicPrefab.GetComponent<
                FrameSyncMoba.Unit.FlowFieldSceneAuthoring>(), Is.Not.Null);
            Assert.That(
                logicPrefab.GetComponentsInChildren<Renderer>(true),
                Is.Empty);
            Assert.That(
                logicPrefab.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            AddressableAssetEntry view = FindEntryByAddress(
                settings,
                entry.ClientViewAddress);
            GameObject viewPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(view.AssetPath);
            Assert.That(viewPrefab.GetComponent<
                FrameSyncMoba.Unit.FlowFieldSceneAuthoring>(), Is.Null);
            Assert.That(
                viewPrefab.GetComponentsInChildren<Renderer>(true),
                Is.Not.Empty);
        }

        [Test]
        public void VfxAndAudioLibrariesContainAddressesNotDirectAssets()
        {
            AssertLibrary(
                "Assets/Config/Formal/FullMatchVfxLibrary.asset",
                "_entries",
                "Prefab",
                "Address");
            AssertLibrary(
                "Assets/Config/Formal/AudioLibrary.asset",
                "_entries",
                "Clip",
                "Address");
        }

        [Test]
        public void GameplayConfigurationsHaveNoDirectSpriteDependencies()
        {
            string[] roots =
            {
                "Assets/Config/Formal/Abilities",
                "Assets/Config/Formal/Buffs",
                "Assets/Config/Formal/Equipment",
                "Assets/Config/Formal/HeroDisplayTable.asset",
            };
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string[] guids = AssetDatabase.IsValidFolder(roots[rootIndex])
                    ? AssetDatabase.FindAssets(
                        "t:ScriptableObject",
                        new[] { roots[rootIndex] })
                    : new[] { AssetDatabase.AssetPathToGUID(roots[rootIndex]) };
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string[] dependencies = AssetDatabase.GetDependencies(
                        path,
                        false);
                    for (int dependencyIndex = 0;
                         dependencyIndex < dependencies.Length;
                         dependencyIndex++)
                    {
                        string extension = Path.GetExtension(
                            dependencies[dependencyIndex]);
                        string[] forbidden =
                            { ".png", ".jpg", ".jpeg", ".tga", ".psd" };
                        Assert.That(
                            Array.IndexOf(forbidden, extension.ToLowerInvariant()),
                            Is.LessThan(0),
                            $"'{path}' directly references presentation asset '{dependencies[dependencyIndex]}'.");
                    }
                }
            }
        }

        [Test]
        public void UiPagesAndPresentationRootsAreAddressableAndOutsideResources()
        {
            string managerPath = "Assets/ClientContent/UI/UIManager.prefab";
            GameObject managerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(managerPath);
            var serialized = new SerializedObject(
                managerPrefab.GetComponent<UIManager>());
            SerializedProperty pages = serialized.FindProperty("pages");
            Assert.That(pages.arraySize, Is.EqualTo(7));
            for (int i = 0; i < pages.arraySize; i++)
            {
                SerializedProperty page = pages.GetArrayElementAtIndex(i);
                Assert.That(
                    page.FindPropertyRelative("Prefab").objectReferenceValue,
                    Is.Null);
                string address =
                    page.FindPropertyRelative("Address").stringValue;
                Assert.That(address, Is.Not.Empty);
                Assert.That(
                    FindEntryByAddress(
                        AddressableAssetSettingsDefaultObject.Settings,
                        address),
                    Is.Not.Null);
            }
            string[] required =
            {
                "ui/indicator/direction",
                "ui/indicator/range-circle",
                "ui/indicator/ground-target",
                "view/map/main",
            };
            for (int i = 0; i < required.Length; i++)
            {
                AddressableAssetEntry entry = FindEntryByAddress(
                    AddressableAssetSettingsDefaultObject.Settings,
                    required[i]);
                Assert.That(entry, Is.Not.Null, required[i]);
                Assert.That(
                    entry.AssetPath,
                    Does.Not.StartWith("Assets/Resources/"),
                    required[i]);
            }
            Assert.That(
                AssetDatabase.IsValidFolder("Assets/Resources/Animation"),
                Is.False);
            Assert.That(
                AssetDatabase.IsValidFolder("Assets/Resources/Prefab/UI"),
                Is.False);
            Assert.That(
                AssetDatabase.LoadMainAssetAtPath(
                    "Assets/Resources/MiniMap.renderTexture"),
                Is.Null);
        }

        [Test]
        public void GenericSkillIndicatorsUseSupportedTransparentShader()
        {
            string[] paths =
            {
                "Assets/ClientContent/Indicators/DirectionIndicator.prefab",
                "Assets/ClientContent/Indicators/RangeCircleIndicator.prefab",
                "Assets/ClientContent/Indicators/GroundTargetIndicator.prefab",
            };
            for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(paths[pathIndex]);
                Assert.That(prefab, Is.Not.Null, paths[pathIndex]);
                Renderer[] renderers =
                    prefab.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, paths[pathIndex]);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Material[] materials =
                        renderers[rendererIndex].sharedMaterials;
                    Assert.That(
                        materials,
                        Is.Not.Empty,
                        $"{paths[pathIndex]}:{renderers[rendererIndex].name}");
                    for (int materialIndex = 0;
                         materialIndex < materials.Length;
                         materialIndex++)
                    {
                        Material material = materials[materialIndex];
                        Assert.That(material, Is.Not.Null);
                        Assert.That(
                            material.shader.name,
                            Is.EqualTo(
                                "FrameSyncMoba/SkillIndicatorUnlit"),
                            AssetDatabase.GetAssetPath(material));
                        Assert.That(
                            material.shader.isSupported,
                            Is.True,
                            AssetDatabase.GetAssetPath(material));
                        Assert.That(
                            material.shaderKeywords,
                            Is.Empty,
                            "The dedicated indicator Shader declares no " +
                            "keywords; migrated URP/Sprite keywords would " +
                            "request an unavailable Player variant.");
                        Assert.That(
                            material.color.b,
                            Is.GreaterThan(material.color.r),
                            "Generic indicator tint must remain blue, not the magenta missing-shader fallback.");
                        Assert.That(
                            material.color.a,
                            Is.GreaterThan(0f));
                    }
                }
            }
        }

        private static void AssertLibrary(
            string path,
            string entriesName,
            string legacyName,
            string addressName)
        {
            var serialized = new SerializedObject(
                AssetDatabase.LoadMainAssetAtPath(path));
            SerializedProperty entries =
                serialized.FindProperty(entriesName);
            Assert.That(entries.arraySize, Is.GreaterThan(0));
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                Assert.That(
                    entry.FindPropertyRelative(legacyName).objectReferenceValue,
                    Is.Null,
                    $"{path}:{i}");
                string address =
                    entry.FindPropertyRelative(addressName).stringValue;
                Assert.That(address, Is.Not.Empty, $"{path}:{i}");
                Assert.That(
                    FindEntryByAddress(
                        AddressableAssetSettingsDefaultObject.Settings,
                        address),
                    Is.Not.Null,
                    address);
            }
        }

        private static List<PrefabEntry> FindEntries(
            GlobalPrefabTable table,
            AddressableAssetSettings settings,
            PrefabKind kind)
        {
            Assert.That(table, Is.Not.Null);
            Assert.That(settings, Is.Not.Null);
            table.ValidateOrThrow();
            var result = new List<PrefabEntry>();
            for (int partitionIndex = 0;
                 partitionIndex < table.Partitions.Count;
                 partitionIndex++)
            {
                GlobalPrefabPartitionReference partition =
                    table.Partitions[partitionIndex];
                AddressableAssetEntry childEntry = FindEntryByAddress(
                    settings,
                    partition.SubTableAddress);
                Assert.That(
                    childEntry,
                    Is.Not.Null,
                    partition.SubTableAddress);
                GlobalPrefabSubTableAsset child =
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        childEntry.AssetPath);
                Assert.That(child, Is.Not.Null, childEntry.AssetPath);
                child.ValidateAgainst(partition);
                for (int groupIndex = 0;
                     groupIndex < child.PrefabGroups.Count;
                     groupIndex++)
                {
                    PrefabGroup group = child.PrefabGroups[groupIndex];
                    if (group.Kind != kind)
                        continue;
                    for (int entryIndex = 0;
                         entryIndex < group.Entries.Count;
                         entryIndex++)
                        result.Add(group.Entries[entryIndex]);
                }
            }
            return result;
        }

        private static bool TryFindEntry(
            GlobalPrefabTable table,
            AddressableAssetSettings settings,
            PrefabKind kind,
            int prefabId,
            out PrefabEntry found)
        {
            List<PrefabEntry> entries = FindEntries(table, settings, kind);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].PrefabId != prefabId)
                    continue;
                found = entries[i];
                return true;
            }
            found = null;
            return false;
        }

        private static AddressableAssetEntry FindEntryByAddress(
            AddressableAssetSettings settings,
            string address)
        {
            for (int groupIndex = 0;
                 groupIndex < settings.groups.Count;
                 groupIndex++)
            {
                AddressableAssetGroup group = settings.groups[groupIndex];
                if (group == null)
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                    if (string.Equals(
                            entry.address,
                            address,
                            StringComparison.Ordinal))
                        return entry;
            }
            return null;
        }
    }
}
