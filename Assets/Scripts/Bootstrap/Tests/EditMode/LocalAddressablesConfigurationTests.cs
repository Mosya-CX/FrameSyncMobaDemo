using FrameSyncMoba.EditorTools.Addressables;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class LocalAddressablesConfigurationTests
    {
        [Test]
        public void FormalSettingsAreLocalOnlyAndContainAllClientGroups()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.BuildRemoteCatalog, Is.False);
            Assert.That(settings.DisableCatalogUpdateOnStartup, Is.True);

            for (int i = 0;
                 i < AddressablesProjectConstants.ClientGroups.Length;
                 i++)
            {
                AddressableAssetGroup group = settings.FindGroup(
                    AddressablesProjectConstants.ClientGroups[i]);
                Assert.That(group, Is.Not.Null);
                BundledAssetGroupSchema schema =
                    group.GetSchema<BundledAssetGroupSchema>();
                Assert.That(schema, Is.Not.Null);
                Assert.That(
                    schema.BuildPath.GetName(settings),
                    Is.EqualTo(AddressableAssetSettings.kLocalBuildPath));
                Assert.That(
                    schema.LoadPath.GetName(settings),
                    Is.EqualTo(AddressableAssetSettings.kLocalLoadPath));
            }
        }

        [Test]
        public void ClientRootEntries_HaveUniqueAddressesAndExistingAssets()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            int rootCount = 0;

            for (int i = 0;
                 i < AddressablesProjectConstants.ClientGroups.Length;
                 i++)
            {
                AddressableAssetGroup group = settings.FindGroup(
                    AddressablesProjectConstants.ClientGroups[i]);
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    Assert.That(entry.address, Is.Not.Empty, group.Name);
                    Assert.That(
                        addresses.Add(entry.address),
                        Is.True,
                        $"Duplicate Addressables address '{entry.address}'.");
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    Assert.That(path, Is.Not.Empty, entry.address);
                    Assert.That(
                        AssetDatabase.LoadMainAssetAtPath(path),
                        Is.Not.Null,
                        entry.address);
                    rootCount++;
                }
            }

            Assert.That(rootCount, Is.EqualTo(63));
        }

        [TestCase("Assets/Archive/LegacyMonolithicUnitPrefabs/VarusRuntime.prefab", "LegacyMixed")]
        [TestCase("Assets/ClientContent/Animation/Varus/VarusIdle.anim", "ClientPresentation")]
        [TestCase("Assets/Scripts/Gameplay/Unit/Core/UnitWorld.cs", "Logic")]
        public void InventoryClassificationIsStable(string path, string expected)
        {
            Assert.That(AddressableDependencyInventory.Classify(path), Is.EqualTo(expected));
        }
    }
}
