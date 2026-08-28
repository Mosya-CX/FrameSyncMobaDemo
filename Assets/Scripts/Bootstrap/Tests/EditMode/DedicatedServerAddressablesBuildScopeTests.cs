using System;
using System.Collections.Generic;
using FrameSyncMoba.EditorTools;
using FrameSyncMoba.EditorTools.Addressables;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class DedicatedServerAddressablesBuildScopeTests
    {
        [Test]
        public void ServerScope_IncludesOnlyLogicAddressablesGroups()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.PlayerBuildOption previous =
                settings.BuildAddressablesWithPlayerBuild;
            var previousGroups = new Dictionary<string, bool>();
            foreach (AddressableAssetGroup group in settings.groups)
            {
                BundledAssetGroupSchema schema =
                    group?.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                    previousGroups.Add(group.Name, schema.IncludeInBuild);
            }

            using (new AddressablesPlayerBuildScope(true))
            {
                Assert.That(
                    settings.BuildAddressablesWithPlayerBuild,
                    Is.EqualTo(
                        AddressableAssetSettings.PlayerBuildOption
                            .BuildWithPlayer));
                foreach (AddressableAssetGroup group in settings.groups)
                {
                    BundledAssetGroupSchema schema =
                        group?.GetSchema<BundledAssetGroupSchema>();
                    if (schema == null)
                        continue;
                    bool isLogic = System.Array.IndexOf(
                        AddressablesProjectConstants.LogicGroups,
                        group.Name) >= 0;
                    Assert.That(schema.IncludeInBuild, Is.EqualTo(isLogic),
                        group.Name);
                }
            }

            Assert.That(
                settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(previous));
            foreach (AddressableAssetGroup group in settings.groups)
            {
                BundledAssetGroupSchema schema =
                    group?.GetSchema<BundledAssetGroupSchema>();
                if (schema != null)
                    Assert.That(schema.IncludeInBuild,
                        Is.EqualTo(previousGroups[group.Name]),
                        group.Name);
            }
        }

        [Test]
        public void LogicGroups_HaveNoClientPresentationDependencies()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            Assert.DoesNotThrow(
                () => AddressablesServerBuildAudit
                    .ValidateLogicGroupDependencies(settings));

            BuildFailedException exception = Assert.Throws<
                BuildFailedException>(
                () => AddressablesServerBuildAudit
                    .ValidateLogicRootDependencies(
                        "Assets/ClientContent/Views/Unit/VarusRuntimeView.prefab",
                        "negative-test"));
            StringAssert.Contains(
                "client presentation",
                exception.Message);
        }

        [Test]
        public void BuildScope_RetainsIndicatorShaderOnlyForClientAndRestores()
        {
            Shader[] expectedProjectShaders =
                ReadAlwaysIncludedShaders();
            Assert.That(
                AddressablesPlayerBuildScope
                    .IsRequiredClientShaderAlwaysIncluded(),
                Is.True,
                "The normal project state is the client-safe default.");

            using (new AddressablesPlayerBuildScope(false))
            {
                Assert.That(
                    AddressablesPlayerBuildScope
                        .IsRequiredClientShaderAlwaysIncluded(),
                    Is.True,
                    "Client builds must retain the Addressables Shader in " +
                    "the Player core.");
            }
            Assert.That(
                AddressablesPlayerBuildScope
                    .IsRequiredClientShaderAlwaysIncluded(),
                Is.True,
                "Client scope disposal must restore project settings.");
            CollectionAssert.AreEqual(
                expectedProjectShaders,
                ReadAlwaysIncludedShaders());

            using (new AddressablesPlayerBuildScope(true))
            {
                Assert.That(
                    AddressablesPlayerBuildScope
                        .IsRequiredClientShaderAlwaysIncluded(),
                    Is.False,
                    "Dedicated Server builds must exclude client shaders.");
            }
            Assert.That(
                AddressablesPlayerBuildScope
                    .IsRequiredClientShaderAlwaysIncluded(),
                Is.True,
                "Server scope disposal must restore the client-safe default.");
            CollectionAssert.AreEqual(
                expectedProjectShaders,
                ReadAlwaysIncludedShaders());

            Shader required = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                AddressablesPlayerBuildScope
                    .RequiredClientIndicatorShaderPath);
            Assert.That(required, Is.Not.Null);
            Assert.That(required.name,
                Is.EqualTo("FrameSyncMoba/SkillIndicatorUnlit"));
        }

        private static Shader[] ReadAlwaysIncludedShaders()
        {
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/GraphicsSettings.asset");
            Assert.That(assets.Length, Is.EqualTo(1));
            var serialized = new SerializedObject(assets[0]);
            SerializedProperty property = serialized.FindProperty(
                "m_AlwaysIncludedShaders");
            Assert.That(property, Is.Not.Null);
            var result = new Shader[property.arraySize];
            for (int i = 0; i < property.arraySize; i++)
            {
                result[i] = property.GetArrayElementAtIndex(i)
                    .objectReferenceValue as Shader;
            }
            return result;
        }
    }
}
