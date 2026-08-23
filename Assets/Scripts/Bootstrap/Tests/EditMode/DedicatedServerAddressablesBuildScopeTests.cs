using System;
using System.Collections;
using System.Reflection;
using FrameSyncMoba.EditorTools;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class DedicatedServerAddressablesBuildScopeTests
    {
        [Test]
        public void ServerScope_RejectsExistingAddressablesBuildPath()
        {
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            AddressableAssetSettings.PlayerBuildOption previous =
                settings.BuildAddressablesWithPlayerBuild;
            MethodInfo getStreamingAssetPaths =
                typeof(AddressablesPlayerBuildProcessor).GetMethod(
                    "GetStreamingAssetPaths",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                getStreamingAssetPaths,
                Is.Not.Null,
                "The test is pinned to Addressables 1.22.3's build processor hook.");

            using (new AddressablesPlayerBuildScope(true))
            {
                Assert.That(
                    settings.BuildAddressablesWithPlayerBuild,
                    Is.EqualTo(
                        AddressableAssetSettings.PlayerBuildOption
                            .DoNotBuildWithPlayer));
                var paths = (ICollection)getStreamingAssetPaths.Invoke(
                    null,
                    null);
                Assert.That(
                    paths,
                    Is.Empty,
                    "Dedicated Server must not copy Addressables build output into StreamingAssets.");
            }

            Assert.That(
                settings.BuildAddressablesWithPlayerBuild,
                Is.EqualTo(previous));
        }
    }
}
