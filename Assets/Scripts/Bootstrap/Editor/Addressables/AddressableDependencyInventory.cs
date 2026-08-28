using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class AddressableDependencyInventory
    {
        private static readonly string[] RootFolders =
        {
            "Assets/Config/Formal",
            "Assets/Resources/Prefab",
            "Assets/Scenes",
        };

        private static readonly HashSet<string> PresentationExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".anim", ".controller", ".overridecontroller", ".mask",
                ".mat", ".shader", ".shadergraph", ".compute",
                ".fbx", ".obj", ".blend", ".dae",
                ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".tif", ".tiff",
                ".wav", ".mp3", ".ogg", ".aiff",
                ".ttf", ".otf", ".fontsettings",
                ".vfx", ".playable", ".rendertexture",
            };

        [MenuItem("FrameSyncMoba/Addressables/Generate Baseline Dependency Inventory")]
        public static void GenerateBaseline()
        {
            Generate(
                AddressablesProjectConstants.BaselineCsv,
                AddressablesProjectConstants.BaselineMarkdown,
                "Pre-migration baseline",
                false);
        }

        [MenuItem("FrameSyncMoba/Addressables/Generate Current Dependency Inventory")]
        public static void GenerateCurrent()
        {
            Generate(
                AddressablesProjectConstants.CurrentCsv,
                AddressablesProjectConstants.CurrentMarkdown,
                "Current post-migration state",
                true);
        }

        public static IReadOnlyList<DependencyEdge> Collect()
        {
            return Collect(false);
        }

        private static IReadOnlyList<DependencyEdge> Collect(
            bool includeAddressableRoots)
        {
            string[] roots = CollectRoots(includeAddressableRoots);
            var edges = new List<DependencyEdge>();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string source = roots[rootIndex];
                var direct = new HashSet<string>(
                    AssetDatabase.GetDependencies(source, false),
                    StringComparer.Ordinal);
                string[] all = AssetDatabase.GetDependencies(source, true);
                Array.Sort(all, StringComparer.Ordinal);
                for (int dependencyIndex = 0;
                     dependencyIndex < all.Length;
                     dependencyIndex++)
                {
                    string dependency = all[dependencyIndex];
                    if (string.Equals(source, dependency, StringComparison.Ordinal))
                        continue;
                    edges.Add(new DependencyEdge(
                        source,
                        AssetDatabase.AssetPathToGUID(source),
                        dependency,
                        AssetDatabase.AssetPathToGUID(dependency),
                        direct.Contains(dependency),
                        Classify(dependency)));
                }
            }

            edges.Sort(DependencyEdge.Compare);
            return edges;
        }

        public static string Classify(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "Unknown";
            string normalized = assetPath.Replace('\\', '/');
            string extension = Path.GetExtension(normalized);
            if (normalized.StartsWith("Packages/", StringComparison.Ordinal))
                return "ThirdParty";
            if (normalized.StartsWith("Assets/Scripts/Gameplay/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Scripts/Physics/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Scripts/Deterministic/", StringComparison.Ordinal))
                return "Logic";
            if (normalized.Contains("/Editor/") ||
                normalized.Contains("/Tests/") ||
                extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase))
                return "EditorOrCode";
            if (normalized.StartsWith("Assets/Archive/LegacyMonolithic", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Resources/Prefab/Unit/", StringComparison.Ordinal))
                return "LegacyMixed";
            if (normalized.StartsWith("Assets/ClientContent/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Art/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Shader/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Resources/Animation/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Resources/Material/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Resources/Prefab/UI/", StringComparison.Ordinal) ||
                normalized.StartsWith("Assets/Resources/Prefab/VFX/", StringComparison.Ordinal) ||
                PresentationExtensions.Contains(extension))
                return "ClientPresentation";
            if (normalized.StartsWith("Assets/Config/Formal/", StringComparison.Ordinal))
                return "SharedConfig";
            return "Unclassified";
        }

        private static void Generate(
            string csvPath,
            string markdownPath,
            string title,
            bool includeAddressableRoots)
        {
            IReadOnlyList<DependencyEdge> edges = Collect(
                includeAddressableRoots);
            EnsureParentDirectory(csvPath);
            EnsureParentDirectory(markdownPath);
            File.WriteAllText(csvPath, BuildCsv(edges), new UTF8Encoding(false));
            File.WriteAllText(
                markdownPath,
                BuildMarkdown(edges, title),
                new UTF8Encoding(false));
            if (includeAddressableRoots)
                WriteAddressableRoots();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[AddressablesInventory] Wrote {edges.Count} dependency edges to {csvPath} and {markdownPath}.");
        }

        private static string[] CollectRoots(bool includeAddressableRoots)
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, RootFolders);
            var roots = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path))
                    continue;
                string extension = Path.GetExtension(path);
                if (extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".asmdef", StringComparison.OrdinalIgnoreCase))
                    continue;
                roots.Add(path.Replace('\\', '/'));
            }
            if (includeAddressableRoots)
            {
                IReadOnlyList<AddressableRoot> addressableRoots =
                    CollectAddressableRoots();
                for (int i = 0; i < addressableRoots.Count; i++)
                    roots.Add(addressableRoots[i].AssetPath);
            }
            roots.Sort(StringComparer.Ordinal);
            return roots.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static IReadOnlyList<AddressableRoot>
            CollectAddressableRoots()
        {
            var roots = new List<AddressableRoot>();
            AddressableAssetSettings settings =
                AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return roots;

            for (int groupIndex = 0;
                 groupIndex < settings.groups.Count;
                 groupIndex++)
            {
                AddressableAssetGroup group = settings.groups[groupIndex];
                if (group == null ||
                    !AddressablesProjectConstants.ClientGroups.Contains(
                        group.Name,
                        StringComparer.Ordinal))
                    continue;
                foreach (AddressableAssetEntry entry in group.entries)
                {
                    string path = AssetDatabase.GUIDToAssetPath(entry.guid)
                        ?.Replace('\\', '/');
                    if (string.IsNullOrEmpty(path) ||
                        AssetDatabase.IsValidFolder(path))
                        continue;
                    roots.Add(new AddressableRoot(
                        group.Name,
                        entry.address,
                        path,
                        entry.guid));
                }
            }
            roots.Sort(AddressableRoot.Compare);
            return roots;
        }

        private static void WriteAddressableRoots()
        {
            IReadOnlyList<AddressableRoot> roots =
                CollectAddressableRoots();
            var csv = new StringBuilder(roots.Count * 160);
            csv.AppendLine(
                "Group,Address,AssetPath,Guid,DirectDependencyCount,TransitiveDependencyCount,SourceBytes");
            for (int i = 0; i < roots.Count; i++)
            {
                AddressableRoot root = roots[i];
                string[] direct = AssetDatabase.GetDependencies(
                    root.AssetPath,
                    false);
                string[] all = AssetDatabase.GetDependencies(
                    root.AssetPath,
                    true);
                long sourceBytes = File.Exists(root.AssetPath)
                    ? new FileInfo(root.AssetPath).Length
                    : 0L;
                AppendCsv(csv, root.Group);
                AppendCsv(csv, root.Address);
                AppendCsv(csv, root.AssetPath);
                AppendCsv(csv, root.Guid);
                AppendCsv(csv, CountOtherDependencies(
                    direct,
                    root.AssetPath).ToString());
                AppendCsv(csv, CountOtherDependencies(
                    all,
                    root.AssetPath).ToString());
                AppendCsv(csv, sourceBytes.ToString());
                csv.AppendLine();
            }

            var markdown = new StringBuilder();
            markdown.AppendLine("# Addressable client roots");
            markdown.AppendLine();
            markdown.AppendLine(
                "Generated from the formal `Client-*` Addressables groups. The adjacent CSV records every root address, path, GUID and dependency count.");
            markdown.AppendLine();
            markdown.AppendLine($"- Root entries: {roots.Count}");
            markdown.AppendLine("- Remote entries: 0");
            markdown.AppendLine();
            markdown.AppendLine("| Group | Root entries |");
            markdown.AppendLine("|---|---:|");
            foreach (IGrouping<string, AddressableRoot> group in
                     roots.GroupBy(root => root.Group)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
                markdown.AppendLine($"| {group.Key} | {group.Count()} |");

            EnsureParentDirectory(
                AddressablesProjectConstants.AddressableRootsCsv);
            File.WriteAllText(
                AddressablesProjectConstants.AddressableRootsCsv,
                csv.ToString(),
                new UTF8Encoding(false));
            File.WriteAllText(
                AddressablesProjectConstants.AddressableRootsMarkdown,
                markdown.ToString(),
                new UTF8Encoding(false));
        }

        private static int CountOtherDependencies(
            IReadOnlyList<string> dependencies,
            string sourcePath)
        {
            int count = 0;
            for (int i = 0; i < dependencies.Count; i++)
            {
                if (!string.Equals(
                        dependencies[i],
                        sourcePath,
                        StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        private static string BuildCsv(IReadOnlyList<DependencyEdge> edges)
        {
            var builder = new StringBuilder(edges.Count * 180);
            builder.AppendLine("SourcePath,SourceGuid,DependencyPath,DependencyGuid,Relation,Ownership");
            for (int i = 0; i < edges.Count; i++)
            {
                DependencyEdge edge = edges[i];
                AppendCsv(builder, edge.SourcePath);
                AppendCsv(builder, edge.SourceGuid);
                AppendCsv(builder, edge.DependencyPath);
                AppendCsv(builder, edge.DependencyGuid);
                AppendCsv(builder, edge.IsDirect ? "Direct" : "Transitive");
                AppendCsv(builder, edge.Ownership);
                builder.AppendLine();
            }
            return builder.ToString();
        }

        private static string BuildMarkdown(
            IReadOnlyList<DependencyEdge> edges,
            string title)
        {
            int sourceCount = edges.Select(edge => edge.SourcePath)
                .Distinct(StringComparer.Ordinal).Count();
            int dependencyCount = edges.Select(edge => edge.DependencyPath)
                .Distinct(StringComparer.Ordinal).Count();
            var builder = new StringBuilder();
            builder.AppendLine($"# Addressables dependency inventory — {title}");
            builder.AppendLine();
            builder.AppendLine("Generated deterministically with `AssetDatabase.GetDependencies`; no runtime reflection scan is used.");
            builder.AppendLine();
            builder.AppendLine($"- Root assets: {sourceCount}");
            builder.AppendLine($"- Unique dependencies: {dependencyCount}");
            builder.AppendLine($"- Source/dependency edges: {edges.Count}");
            builder.AppendLine();
            builder.AppendLine("## Ownership summary");
            builder.AppendLine();
            builder.AppendLine("| Ownership | Edge count |");
            builder.AppendLine("|---|---:|");
            foreach (IGrouping<string, DependencyEdge> group in
                     edges.GroupBy(edge => edge.Ownership)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
                builder.AppendLine($"| {group.Key} | {group.Count()} |");
            builder.AppendLine();
            builder.AppendLine("The complete path/GUID/direct-or-transitive graph is in the adjacent CSV file.");
            return builder.ToString();
        }

        private static void AppendCsv(StringBuilder builder, string value)
        {
            builder.Append('"');
            builder.Append((value ?? string.Empty).Replace("\"", "\"\""));
            builder.Append("\",");
        }

        private static void EnsureParentDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }

        public readonly struct DependencyEdge
        {
            public readonly string SourcePath;
            public readonly string SourceGuid;
            public readonly string DependencyPath;
            public readonly string DependencyGuid;
            public readonly bool IsDirect;
            public readonly string Ownership;

            public DependencyEdge(
                string sourcePath,
                string sourceGuid,
                string dependencyPath,
                string dependencyGuid,
                bool isDirect,
                string ownership)
            {
                SourcePath = sourcePath;
                SourceGuid = sourceGuid;
                DependencyPath = dependencyPath;
                DependencyGuid = dependencyGuid;
                IsDirect = isDirect;
                Ownership = ownership;
            }

            public static int Compare(DependencyEdge left, DependencyEdge right)
            {
                int source = string.CompareOrdinal(left.SourcePath, right.SourcePath);
                if (source != 0) return source;
                int dependency = string.CompareOrdinal(left.DependencyPath, right.DependencyPath);
                if (dependency != 0) return dependency;
                return right.IsDirect.CompareTo(left.IsDirect);
            }
        }

        private readonly struct AddressableRoot
        {
            public readonly string Group;
            public readonly string Address;
            public readonly string AssetPath;
            public readonly string Guid;

            public AddressableRoot(
                string group,
                string address,
                string assetPath,
                string guid)
            {
                Group = group ?? string.Empty;
                Address = address ?? string.Empty;
                AssetPath = assetPath ?? string.Empty;
                Guid = guid ?? string.Empty;
            }

            public static int Compare(
                AddressableRoot left,
                AddressableRoot right)
            {
                int group = string.CompareOrdinal(
                    left.Group,
                    right.Group);
                if (group != 0)
                    return group;
                int address = string.CompareOrdinal(
                    left.Address,
                    right.Address);
                if (address != 0)
                    return address;
                return string.CompareOrdinal(
                    left.AssetPath,
                    right.AssetPath);
            }
        }
    }
}
