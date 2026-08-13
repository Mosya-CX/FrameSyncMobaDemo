using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.EditorTools
{
    /// <summary>
    /// Creates an upload-ready archive after a successful UOS Linux server
    /// build. Entries start at the build root because UOS expects the server
    /// executable in the image working-directory root.
    /// </summary>
    public static class UosServerUploadPackager
    {
        public const string DefaultServerRoot = "Builds/UosServer";
        public const string DefaultUploadRoot = "Builds/UosUpload";
        public const string ServerExecutableName =
            "FrameSyncMobaServer.x86_64";
        public const string UnityPlayerName = "UnityPlayer.so";
        public const string ServerDataDirectoryName =
            "FrameSyncMobaServer_Data";

        private const string DoNotShipDirectorySuffix =
            "_BurstDebugInformation_DoNotShip";

        [MenuItem(
            "FrameSyncMoba/Build Local NGO/Package Latest UOS Server")]
        public static void PackageLatestUosServer()
        {
            UosServerUploadPackage result = CreateArchive(
                Path.GetFullPath(DefaultServerRoot),
                Path.GetFullPath(DefaultUploadRoot),
                DateTime.Now);
            Debug.Log(FormatSuccessLog(result));
        }

        public static UosServerUploadPackage CreateArchive(
            string serverRoot,
            string uploadRoot,
            DateTime packageTime)
        {
            string fullServerRoot = Path.GetFullPath(serverRoot);
            string fullUploadRoot = Path.GetFullPath(uploadRoot);
            ValidateServerRoot(fullServerRoot);
            Directory.CreateDirectory(fullUploadRoot);

            string archivePath = CreateUniqueArchivePath(
                fullUploadRoot,
                packageTime);
            string temporaryArchivePath = archivePath + ".tmp";
            string checksumPath = archivePath + ".sha256";
            string temporaryChecksumPath = checksumPath + ".tmp";

            try
            {
                string[] files = Directory.GetFiles(
                    fullServerRoot,
                    "*",
                    SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);

                int entryCount = 0;
                using (FileStream stream = new FileStream(
                           temporaryArchivePath,
                           FileMode.CreateNew,
                           FileAccess.ReadWrite,
                           FileShare.None))
                using (var archive = new ZipArchive(
                           stream,
                           ZipArchiveMode.Create,
                           false))
                {
                    for (int i = 0; i < files.Length; i++)
                    {
                        string relativePath = Path.GetRelativePath(
                            fullServerRoot,
                            files[i]);
                        if (ShouldExclude(relativePath))
                            continue;

                        string entryName = relativePath.Replace(
                            Path.DirectorySeparatorChar,
                            '/');
                        archive.CreateEntryFromFile(
                            files[i],
                            entryName,
                            System.IO.Compression
                                .CompressionLevel.Optimal);
                        entryCount++;
                    }
                }

                ValidateArchive(temporaryArchivePath);
                File.Move(temporaryArchivePath, archivePath);

                string sha256 = ComputeSha256(archivePath);
                File.WriteAllText(
                    temporaryChecksumPath,
                    sha256 + "  " + Path.GetFileName(archivePath) +
                    Environment.NewLine,
                    new UTF8Encoding(false));
                File.Move(temporaryChecksumPath, checksumPath);

                return new UosServerUploadPackage(
                    archivePath,
                    checksumPath,
                    sha256,
                    entryCount,
                    new FileInfo(archivePath).Length);
            }
            catch
            {
                DeleteTemporaryFile(temporaryArchivePath);
                DeleteTemporaryFile(temporaryChecksumPath);
                throw;
            }
        }

        public static string FormatSuccessLog(
            UosServerUploadPackage package)
        {
            return "[Build] UOS server upload archive created: " +
                   package.ArchivePath +
                   $" ({package.EntryCount} files, " +
                   $"{package.ArchiveLength} bytes, SHA-256 " +
                   package.Sha256 + ").";
        }

        private static void ValidateServerRoot(string serverRoot)
        {
            if (!Directory.Exists(serverRoot))
                throw new DirectoryNotFoundException(
                    "UOS server build directory does not exist: " +
                    serverRoot);

            RequireFile(Path.Combine(serverRoot, ServerExecutableName));
            RequireFile(Path.Combine(serverRoot, UnityPlayerName));

            string dataDirectory = Path.Combine(
                serverRoot,
                ServerDataDirectoryName);
            if (!Directory.Exists(dataDirectory))
                throw new DirectoryNotFoundException(
                    "UOS server data directory does not exist: " +
                    dataDirectory);
            if (Directory.GetFiles(
                    dataDirectory,
                    "*",
                    SearchOption.AllDirectories).Length == 0)
                throw new InvalidOperationException(
                    "UOS server data directory is empty: " +
                    dataDirectory);
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Required UOS server build file is missing.",
                    path);
        }

        private static string CreateUniqueArchivePath(
            string uploadRoot,
            DateTime packageTime)
        {
            string baseName = "FrameSyncMobaServer_uos_" +
                              packageTime.ToString("yyyyMMdd-HHmmss");
            for (int suffix = 0; suffix < 1000; suffix++)
            {
                string fileName = suffix == 0
                    ? baseName + ".zip"
                    : baseName + "-" + suffix.ToString("00") + ".zip";
                string candidate = Path.Combine(uploadRoot, fileName);
                if (!File.Exists(candidate) &&
                    !File.Exists(candidate + ".tmp") &&
                    !File.Exists(candidate + ".sha256"))
                    return candidate;
            }

            throw new IOException(
                "Unable to allocate a unique UOS upload archive name.");
        }

        private static bool ShouldExclude(string relativePath)
        {
            string[] segments = relativePath.Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar,
                },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].EndsWith(
                        DoNotShipDirectorySuffix,
                        StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static void ValidateArchive(string archivePath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                RequireArchiveEntry(archive, ServerExecutableName);
                RequireArchiveEntry(archive, UnityPlayerName);

                string dataPrefix = ServerDataDirectoryName + "/";
                bool hasDataEntry = false;
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    string entryName = archive.Entries[i].FullName;
                    if (entryName.StartsWith(
                            dataPrefix,
                            StringComparison.Ordinal))
                        hasDataEntry = true;
                    if (entryName.IndexOf(
                            DoNotShipDirectorySuffix,
                            StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidDataException(
                            "UOS upload archive contains a DoNotShip " +
                            "debug entry: " + entryName);
                }

                if (!hasDataEntry)
                    throw new InvalidDataException(
                        "UOS upload archive has no server data entries.");
            }
        }

        private static void RequireArchiveEntry(
            ZipArchive archive,
            string entryName)
        {
            if (archive.GetEntry(entryName) == null)
                throw new InvalidDataException(
                    "UOS upload archive is missing root entry: " +
                    entryName);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static void DeleteTemporaryFile(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public sealed class UosServerUploadPackage
    {
        public UosServerUploadPackage(
            string archivePath,
            string checksumPath,
            string sha256,
            int entryCount,
            long archiveLength)
        {
            ArchivePath = archivePath;
            ChecksumPath = checksumPath;
            Sha256 = sha256;
            EntryCount = entryCount;
            ArchiveLength = archiveLength;
        }

        public string ArchivePath { get; }
        public string ChecksumPath { get; }
        public string Sha256 { get; }
        public int EntryCount { get; }
        public long ArchiveLength { get; }
    }
}
