using System;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;

namespace FrameSyncMoba.Bootstrap.Tests
{
    [TestFixture]
    public sealed class UosServerUploadPackagerTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp()
        {
            temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "FrameSyncMoba-UosPackagerTests-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot))
                Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void CreateArchive_UsesRootLayoutAndExcludesDoNotShip()
        {
            string serverRoot = CreateValidServerRoot();
            string debugDirectory = Path.Combine(
                serverRoot,
                "Demo_BurstDebugInformation_DoNotShip");
            Directory.CreateDirectory(debugDirectory);
            File.WriteAllText(
                Path.Combine(debugDirectory, "symbols.txt"),
                "debug");

            EditorTools.UosServerUploadPackage result =
                EditorTools.UosServerUploadPackager.CreateArchive(
                    serverRoot,
                    Path.Combine(temporaryRoot, "upload"),
                    new DateTime(2026, 8, 13, 20, 0, 0));

            Assert.That(File.Exists(result.ArchivePath), Is.True);
            Assert.That(File.Exists(result.ChecksumPath), Is.True);
            Assert.That(result.Sha256, Has.Length.EqualTo(64));
            using (ZipArchive archive = ZipFile.OpenRead(
                       result.ArchivePath))
            {
                Assert.That(
                    archive.GetEntry(
                        EditorTools.UosServerUploadPackager
                            .ServerExecutableName),
                    Is.Not.Null);
                Assert.That(
                    archive.GetEntry(
                        "FrameSyncMobaServer_Data/resources.assets"),
                    Is.Not.Null);
                Assert.That(
                    archive.GetEntry("UosServer/FrameSyncMobaServer.x86_64"),
                    Is.Null);
                Assert.That(
                    archive.GetEntry(
                        "Demo_BurstDebugInformation_DoNotShip/symbols.txt"),
                    Is.Null);
            }
        }

        [Test]
        public void CreateArchive_PreservesExistingArchiveWithUniqueName()
        {
            string serverRoot = CreateValidServerRoot();
            string uploadRoot = Path.Combine(temporaryRoot, "upload");
            var packageTime = new DateTime(2026, 8, 13, 20, 0, 0);

            EditorTools.UosServerUploadPackage first =
                EditorTools.UosServerUploadPackager.CreateArchive(
                    serverRoot,
                    uploadRoot,
                    packageTime);
            EditorTools.UosServerUploadPackage second =
                EditorTools.UosServerUploadPackager.CreateArchive(
                    serverRoot,
                    uploadRoot,
                    packageTime);

            Assert.That(second.ArchivePath, Is.Not.EqualTo(first.ArchivePath));
            Assert.That(File.Exists(first.ArchivePath), Is.True);
            Assert.That(File.Exists(second.ArchivePath), Is.True);
            StringAssert.EndsWith("-01.zip", second.ArchivePath);
        }

        [Test]
        public void CreateArchive_RejectsIncompleteServerBuild()
        {
            string serverRoot = Path.Combine(temporaryRoot, "server");
            Directory.CreateDirectory(serverRoot);
            File.WriteAllText(
                Path.Combine(
                    serverRoot,
                    EditorTools.UosServerUploadPackager
                        .ServerExecutableName),
                "server");
            string uploadRoot = Path.Combine(temporaryRoot, "upload");

            Assert.Throws<FileNotFoundException>(
                () => EditorTools.UosServerUploadPackager.CreateArchive(
                    serverRoot,
                    uploadRoot,
                    DateTime.Now));
            Assert.That(Directory.Exists(uploadRoot), Is.False);
        }

        private string CreateValidServerRoot()
        {
            string serverRoot = Path.Combine(temporaryRoot, "server");
            Directory.CreateDirectory(serverRoot);
            File.WriteAllText(
                Path.Combine(
                    serverRoot,
                    EditorTools.UosServerUploadPackager
                        .ServerExecutableName),
                "server");
            File.WriteAllText(
                Path.Combine(
                    serverRoot,
                    EditorTools.UosServerUploadPackager.UnityPlayerName),
                "player");
            string dataDirectory = Path.Combine(
                serverRoot,
                EditorTools.UosServerUploadPackager
                    .ServerDataDirectoryName);
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(
                Path.Combine(dataDirectory, "resources.assets"),
                "resources");
            return serverRoot;
        }
    }
}
