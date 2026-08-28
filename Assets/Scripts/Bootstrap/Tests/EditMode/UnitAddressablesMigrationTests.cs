using System;
using System.Collections.Generic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Physics;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using GameplayUnit = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class UnitAddressablesMigrationTests
    {
        private const string TablePath =
            "Assets/Config/Formal/GlobalPrefabTable.asset";

        [Test]
        public void AllFormalUnitEntriesResolveLogicPrefabAndAddressableView()
        {
            GlobalPrefabTable table =
                AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(TablePath);
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(table, Is.Not.Null);
            Assert.That(settings, Is.Not.Null);
            table.ValidateOrThrow();

            List<PrefabEntry> unitEntries = LoadEntries(
                table,
                settings,
                PrefabKind.Unit);
            Assert.That(unitEntries.Count, Is.EqualTo(8));

            for (int i = 0; i < unitEntries.Count; i++)
            {
                PrefabEntry entry = unitEntries[i];
                AddressableAssetEntry logicEntry =
                    FindEntryByAddress(settings, entry.LogicAssetAddress);
                Assert.That(logicEntry, Is.Not.Null, entry.LogicAssetAddress);
                string logicPath = logicEntry.AssetPath;
                Assert.That(
                    logicPath,
                    Does.StartWith("Assets/Config/Formal/Prefabs/Logic/Unit/"),
                    $"PrefabId {entry.PrefabId}");
                Assert.That(entry.ClientViewAddress, Is.Not.Empty);

                GameObject logicPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(logicPath);
                string viewGuid = AssetDatabase.AssetPathToGUID(
                    $"Assets/ClientContent/Views/Unit/{logicPrefab.name}View.prefab");
                AddressableAssetEntry viewEntry =
                    settings.FindAssetEntry(viewGuid);
                Assert.That(viewEntry, Is.Not.Null, $"PrefabId {entry.PrefabId}");
                Assert.That(
                    viewEntry.address,
                    Is.EqualTo(entry.ClientViewAddress),
                    $"PrefabId {entry.PrefabId}");
            }
        }

        [Test]
        public void LogicPrefabsContainNoPresentationComponentsOrAssets()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Config/Formal/Prefabs/Logic/Unit" });
            Assert.That(guids.Length, Is.EqualTo(8));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponent<GameplayUnit>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Empty, path);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Empty, path);
                Assert.That(prefab.GetComponentsInChildren<UnitPresentationHost>(true), Is.Empty, path);
                Assert.That(prefab.GetComponentsInChildren<UnitAnimationDriver>(true), Is.Empty, path);

                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                for (int dependencyIndex = 0;
                     dependencyIndex < dependencies.Length;
                     dependencyIndex++)
                {
                    string extension = System.IO.Path.GetExtension(
                        dependencies[dependencyIndex]);
                    string[] forbidden =
                    {
                        ".anim", ".controller", ".mat", ".shader",
                        ".fbx", ".png", ".wav", ".mp3", ".ogg",
                    };
                    Assert.That(
                        Array.IndexOf(forbidden, extension.ToLowerInvariant()),
                        Is.LessThan(0),
                        $"Logic prefab '{path}' reaches presentation dependency '{dependencies[dependencyIndex]}'.");
                }
            }
        }

        [Test]
        public void ClientViewsContainPresentationHostButNoGameplayRoot()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/ClientContent/Views/Unit" });
            Assert.That(guids.Length, Is.EqualTo(8));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponent<UnitPresentationHost>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<GameplayUnit>(), Is.Null, path);
                Assert.That(prefab.GetComponent<PhysicsEntity2D>(), Is.Null, path);
                Assert.That(prefab.GetComponentsInChildren<Animator>(true), Is.Not.Empty, path);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, path);
            }
        }

        [Test]
        public void ClientViewRootsAreAtWorldOrigin()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/ClientContent/Views/Unit" });
            Assert.That(guids.Length, Is.EqualTo(8));
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                // The view is parented under the deterministic unit root
                // (worldPositionStays=false), so any authored root offset
                // displaces the model from the logic position.
                Assert.That(
                    prefab.transform.localPosition,
                    Is.EqualTo(Vector3.zero),
                    $"Client view root '{path}' must sit at the world origin.");
                Assert.That(
                    prefab.transform.localRotation,
                    Is.EqualTo(Quaternion.identity),
                    $"Client view root '{path}' must keep identity rotation.");
                Assert.That(
                    prefab.transform.localScale,
                    Is.EqualTo(Vector3.one),
                    $"Client view root '{path}' must keep unit scale.");
            }
        }

        [Test]
        public void LegacyArchivesAreNotReachableFromFormalAssets()
        {
            string[] formalGuids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { "Assets/Config/Formal", "Assets/Scenes" });
            for (int i = 0; i < formalGuids.Length; i++)
            {
                string source = AssetDatabase.GUIDToAssetPath(formalGuids[i]);
                if (AssetDatabase.IsValidFolder(source))
                    continue;
                string[] dependencies = AssetDatabase.GetDependencies(source, true);
                for (int dependencyIndex = 0;
                     dependencyIndex < dependencies.Length;
                     dependencyIndex++)
                    Assert.That(
                        dependencies[dependencyIndex],
                        Does.Not.StartWith("Assets/Archive/"),
                        source);
            }
        }

        private static List<PrefabEntry> LoadEntries(
            GlobalPrefabTable table,
            AddressableAssetSettings settings,
            PrefabKind kind)
        {
            var result = new List<PrefabEntry>();
            for (int partitionIndex = 0;
                 partitionIndex < table.Partitions.Count;
                 partitionIndex++)
            {
                GlobalPrefabPartitionReference partition =
                    table.Partitions[partitionIndex];
                AddressableAssetEntry tableEntry = FindEntryByAddress(
                    settings,
                    partition.SubTableAddress);
                Assert.That(
                    tableEntry,
                    Is.Not.Null,
                    partition.SubTableAddress);
                GlobalPrefabSubTableAsset child =
                    AssetDatabase.LoadAssetAtPath<GlobalPrefabSubTableAsset>(
                        tableEntry.AssetPath);
                Assert.That(child, Is.Not.Null, tableEntry.AssetPath);
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
