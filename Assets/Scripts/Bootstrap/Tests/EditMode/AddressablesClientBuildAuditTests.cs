using System;
using System.IO;
using FrameSyncMoba.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class AddressablesClientBuildAuditTests
    {
        private string testRoot;
        private string playerOutput;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.GetFullPath(
                Path.Combine(
                    "Temp",
                    "AddressablesClientBuildAuditTests",
                    Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(testRoot);
            playerOutput = Path.Combine(
                testRoot,
                "FrameSyncMobaClient.exe");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }

        [Test]
        public void ValidateOutput_AcceptsMatchingWindowsContent()
        {
            CreateAddressablesOutput(
                BuildTarget.StandaloneWindows64);

            Assert.DoesNotThrow(
                () => AddressablesClientBuildAudit.ValidateOutput(
                    playerOutput,
                    BuildTarget.StandaloneWindows64));
        }

        [Test]
        public void ValidateOutput_RejectsLinuxContentInWindowsPlayer()
        {
            CreateAddressablesOutput(
                BuildTarget.StandaloneLinux64);

            BuildFailedException exception = Assert.Throws<
                BuildFailedException>(
                () => AddressablesClientBuildAudit.ValidateOutput(
                    playerOutput,
                    BuildTarget.StandaloneWindows64));
            StringAssert.Contains(
                "platform mismatch",
                exception.Message);
        }

        [Test]
        public void PrepareOutput_RemovesOnlyGeneratedAddressablesDirectory()
        {
            CreateAddressablesOutput(
                BuildTarget.StandaloneLinux64);
            string streamingAssets = Path.Combine(
                testRoot,
                "FrameSyncMobaClient_Data",
                "StreamingAssets");
            string luaDirectory = Path.Combine(
                streamingAssets,
                "Lua");
            Directory.CreateDirectory(luaDirectory);
            string marker = Path.Combine(luaDirectory, "marker.txt");
            File.WriteAllText(marker, "preserve");

            AddressablesClientBuildAudit.PrepareOutput(playerOutput);

            Assert.That(
                Directory.Exists(
                    Path.Combine(streamingAssets, "aa")),
                Is.False);
            Assert.That(File.ReadAllText(marker), Is.EqualTo("preserve"));
        }

        [Test]
        public void ServerStrip_RemovesUrpDependenciesBeforeCameraAndLight()
        {
            var root = new GameObject("ServerStripFixture");
            try
            {
                root.AddComponent<Camera>();
                root.AddComponent<Light>();
                Type cameraDataType = Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime",
                    true);
                Type lightDataType = Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalLightData, Unity.RenderPipelines.Universal.Runtime",
                    true);
                root.AddComponent(cameraDataType);
                root.AddComponent(lightDataType);

                Assert.DoesNotThrow(
                    () => DedicatedServerPresentationStripUtility
                        .StripCamerasAndLights(root));
                Assert.That(root.GetComponent<Camera>(), Is.Null);
                Assert.That(root.GetComponent<Light>(), Is.Null);
                Assert.That(root.GetComponent(cameraDataType), Is.Null);
                Assert.That(root.GetComponent(lightDataType), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ServerAudit_RejectsAnyStreamingAssetsAddressablesDirectory()
        {
            string serverOutput = Path.Combine(testRoot, "Server");
            string addressablesLink = Path.Combine(
                serverOutput,
                "FrameSyncMobaServer_Data",
                "StreamingAssets",
                "aa",
                "AddressablesLink");
            Directory.CreateDirectory(addressablesLink);
            File.WriteAllText(
                Path.Combine(addressablesLink, "link.xml"),
                "<linker />");

            BuildFailedException exception = Assert.Throws<
                BuildFailedException>(
                () => AddressablesServerBuildAudit
                    .ValidateOutputDirectory(serverOutput));
            StringAssert.Contains(
                "Addressables directory",
                exception.Message);
        }

        private void CreateAddressablesOutput(BuildTarget target)
        {
            string addressablesRoot = Path.Combine(
                testRoot,
                "FrameSyncMobaClient_Data",
                "StreamingAssets",
                "aa");
            string platformRoot = Path.Combine(
                addressablesRoot,
                target.ToString());
            Directory.CreateDirectory(platformRoot);
            File.WriteAllText(
                Path.Combine(addressablesRoot, "settings.json"),
                $"{{\"m_buildTarget\":\"{target}\"}}");
            File.WriteAllBytes(
                Path.Combine(platformRoot, "client-test.bundle"),
                new byte[] { 1 });
        }
    }
}
