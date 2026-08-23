using System;
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
            PrefabGroup group = FindGroup(table, PrefabKind.Projectile);
            Assert.That(group.Entries.Count, Is.EqualTo(8));
            for (int i = 0; i < group.Entries.Count; i++)
            {
                PrefabEntry entry = group.Entries[i];
                string logicPath = AssetDatabase.GetAssetPath(entry.UnityPrefab);
                Assert.That(logicPath,
                    Does.StartWith(
                        "Assets/Config/Formal/Prefabs/Logic/Projectile/"));
                Assert.That(
                    entry.UnityPrefab.GetComponent<PhysicsEntity2D>(),
                    Is.Not.Null,
                    logicPath);
                Assert.That(
                    entry.UnityPrefab.GetComponentsInChildren<Renderer>(true),
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
            Assert.That(
                table.TryGetEntry(PrefabKind.Misc, 5001, out PrefabEntry entry),
                Is.True);
            Assert.That(entry.UnityPrefab.GetComponent<
                FrameSyncMoba.Unit.FlowFieldSceneAuthoring>(), Is.Not.Null);
            Assert.That(
                entry.UnityPrefab.GetComponentsInChildren<Renderer>(true),
                Is.Empty);
            Assert.That(
                entry.UnityPrefab.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            AddressableAssetEntry view = FindEntryByAddress(
                AddressableAssetSettingsDefaultObject.Settings,
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

        private static PrefabGroup FindGroup(
            GlobalPrefabTable table,
            PrefabKind kind)
        {
            for (int i = 0; i < table.PrefabGroups.Count; i++)
                if (table.PrefabGroups[i].Kind == kind)
                    return table.PrefabGroups[i];
            Assert.Fail($"Missing {kind} prefab group.");
            return null;
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
