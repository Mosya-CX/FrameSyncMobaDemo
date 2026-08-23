using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FrameSyncMoba.RuntimeConfig;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FrameSyncMoba.EditorTools.Addressables
{
    public static class ProjectilePrefabAddressablesMigration
    {
        private const string LegacyRoot = "Assets/Resources/Prefab/Missle";
        private const string ArchiveRoot = "Assets/Archive/LegacyMonolithicProjectilePrefabs";
        private const string LogicRoot = "Assets/Config/Formal/Prefabs/Logic/Projectile";
        private const string ViewRoot = "Assets/ClientContent/Views/Projectile";
        private const string PrefabTablePath = "Assets/Config/Formal/GlobalPrefabTable.asset";
        private const string CompositionReport =
            "Docs/Implementation/Addressables/PROJECTILE_PREFAB_COMPOSITION_BASELINE.md";
        private const string MigrationReport =
            "Docs/Implementation/Addressables/PROJECTILE_PREFAB_MIGRATION.md";

        private static readonly Spec[] Specs =
        {
            new Spec(2101, "VarusAttackMissle.prefab"),
            new Spec(2102, "VarusSpellQMissle.prefab"),
            new Spec(2103, "VarusSpellRMissle.prefab"),
            new Spec(2104, "VarusEMissle_DesecratedGround.prefab"),
            new Spec(2105, "AatroxSpellWMissle.prefab"),
            new Spec(2201, "BlueTeamGeneralAttackMissle.prefab"),
            new Spec(2202, "RedTeamGeneralAttackMissle.prefab"),
            new Spec(2106, "InfernalChainsArea.prefab"),
        };

        private static readonly HashSet<string> LogicAssemblies =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "FrameSyncMoba.Unit", "FrameSyncMoba.Physics",
                "FrameSyncMoba.Deterministic", "FrameSyncMoba.RuntimeConfig",
            };

        private static readonly HashSet<string> PresentationAssemblies =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "FrameSyncMoba.FrameSync", "FrameSyncMoba.Bootstrap",
                "FrameSyncMoba.PlayerInput", "FrameSyncMoba.LuaBridge",
            };

        [MenuItem("FrameSyncMoba/Addressables/Report Legacy Projectile Prefab Composition")]
        public static void GenerateCompositionReport()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CompositionReport));
            var builder = new StringBuilder();
            builder.AppendLine("# Legacy projectile prefab composition baseline");
            builder.AppendLine();
            builder.AppendLine("Targeted prefab traversal; Transform-only nodes are omitted and no serialized-field reflection scan is used.");
            builder.AppendLine();
            for (int i = 0; i < Specs.Length; i++)
            {
                Spec spec = Specs[i];
                string path = ResolveSource(spec);
                builder.AppendLine($"## {spec.PrefabId} — {spec.FileName}");
                builder.AppendLine();
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                    for (int j = 0; j < transforms.Length; j++)
                    {
                        string[] components = transforms[j].GetComponents<Component>()
                            .Where(component => component != null && !(component is Transform))
                            .Select(component => component.GetType().FullName)
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToArray();
                        if (components.Length > 0)
                            builder.AppendLine($"- `{Relative(root.transform, transforms[j])}`: {string.Join(", ", components)}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                builder.AppendLine();
            }
            File.WriteAllText(CompositionReport, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        [MenuItem("FrameSyncMoba/Addressables/Migrate All Projectile Logic and Views")]
        public static void MigrateAll()
        {
            EnsureFolder(ArchiveRoot);
            EnsureFolder(LogicRoot);
            EnsureFolder(ViewRoot);
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException("Addressables settings are required.");
            AddressableAssetGroup group = LocalAddressablesConfigurator.EnsureLocalGroup(
                settings, AddressablesProjectConstants.ProjectileViewsGroup);
            var results = new List<Result>(Specs.Length);
            for (int i = 0; i < Specs.Length; i++)
                results.Add(MigrateOne(Specs[i], settings, group));
            UpdateTable(results);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteReport(results);
        }

        private static Result MigrateOne(
            Spec spec,
            AddressableAssetSettings settings,
            AddressableAssetGroup group)
        {
            string legacy = $"{LegacyRoot}/{spec.FileName}";
            string archive = $"{ArchiveRoot}/{spec.FileName}";
            string logic = $"{LogicRoot}/{spec.FileName}";
            string view = $"{ViewRoot}/{Path.GetFileNameWithoutExtension(spec.FileName)}View.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(archive) == null)
                Copy(AssetDatabase.LoadAssetAtPath<GameObject>(legacy) != null ? legacy : logic, archive);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(view) == null)
                Copy(archive, view);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(logic) == null)
            {
                string error = AssetDatabase.MoveAsset(legacy, logic);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException(error);
            }
            Strip(logic, false);
            Strip(view, true);
            string viewGuid = AssetDatabase.AssetPathToGUID(view);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(viewGuid, group, false, false);
            string address = $"view/projectile/{spec.PrefabId}";
            entry.SetAddress(address, false);
            entry.SetLabel("client-projectile-view", true, false, false);
            return new Result(
                spec, archive, logic, view, address,
                AssetDatabase.AssetPathToGUID(logic), viewGuid);
        }

        private static void Strip(string path, bool view)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var removeChildren = new List<GameObject>();
                for (int i = 0; i < root.transform.childCount; i++)
                {
                    Transform child = root.transform.GetChild(i);
                    if (!(view ? ContainsPresentation(child) : ContainsLogic(child)))
                        removeChildren.Add(child.gameObject);
                }
                for (int i = 0; i < removeChildren.Count; i++)
                    UnityEngine.Object.DestroyImmediate(removeChildren[i], true);

                Component[] components = root.GetComponentsInChildren<Component>(true);
                for (int i = components.Length - 1; i >= 0; i--)
                {
                    Component component = components[i];
                    if (component == null || component is Transform)
                        continue;
                    if (view ? IsLogic(component) : IsPresentation(component))
                        UnityEngine.Object.DestroyImmediate(component, true);
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool ContainsLogic(Transform root) =>
            root.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && IsLogic(component));

        private static bool ContainsPresentation(Transform root) =>
            root.GetComponentsInChildren<Component>(true).Any(component =>
                component != null && IsPresentation(component));

        private static bool IsLogic(Component component)
        {
            if (component is Collider || component is Rigidbody ||
                component is CharacterController)
                return true;
            return LogicAssemblies.Contains(component.GetType().Assembly.GetName().Name);
        }

        private static bool IsPresentation(Component component)
        {
            if (component is Animator || component is Renderer ||
                component is MeshFilter || component is ParticleSystem ||
                component is AudioSource || component is AudioListener ||
                component is Light || component is LODGroup)
                return true;
            return PresentationAssemblies.Contains(component.GetType().Assembly.GetName().Name);
        }

        private static void UpdateTable(IReadOnlyList<Result> results)
        {
            GlobalPrefabTable table = AssetDatabase.LoadAssetAtPath<GlobalPrefabTable>(PrefabTablePath);
            var byId = results.ToDictionary(result => result.Spec.PrefabId);
            var serialized = new SerializedObject(table);
            SerializedProperty groups = serialized.FindProperty("prefabGroups");
            for (int gi = 0; gi < groups.arraySize; gi++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(gi);
                if (group.FindPropertyRelative("kind").intValue != (int)PrefabKind.Projectile)
                    continue;
                SerializedProperty entries = group.FindPropertyRelative("entries");
                for (int ei = 0; ei < entries.arraySize; ei++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(ei);
                    int id = entry.FindPropertyRelative("prefabId").intValue;
                    if (!byId.TryGetValue(id, out Result result))
                        continue;
                    entry.FindPropertyRelative("unityPrefab").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<GameObject>(result.LogicPath);
                    entry.FindPropertyRelative("editorAssetGuid").stringValue = result.LogicGuid;
                    entry.FindPropertyRelative("clientViewAddress").stringValue = result.Address;
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            table.ValidateOrThrow();
            EditorUtility.SetDirty(table);
        }

        private static void WriteReport(IReadOnlyList<Result> results)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Projectile prefab Addressables migration");
            builder.AppendLine();
            builder.AppendLine("| PrefabId | Archive | Logic | View | Address | Logic GUID | View GUID |");
            builder.AppendLine("|---:|---|---|---|---|---|---|");
            for (int i = 0; i < results.Count; i++)
            {
                Result result = results[i];
                builder.AppendLine($"| {result.Spec.PrefabId} | `{result.ArchivePath}` | `{result.LogicPath}` | `{result.ViewPath}` | `{result.Address}` | `{result.LogicGuid}` | `{result.ViewGuid}` |");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MigrationReport));
            File.WriteAllText(MigrationReport, builder.ToString(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static string ResolveSource(Spec spec)
        {
            string[] candidates =
            {
                $"{LegacyRoot}/{spec.FileName}", $"{ArchiveRoot}/{spec.FileName}",
                $"{LogicRoot}/{spec.FileName}",
            };
            for (int i = 0; i < candidates.Length; i++)
                if (AssetDatabase.LoadAssetAtPath<GameObject>(candidates[i]) != null)
                    return candidates[i];
            throw new InvalidOperationException(spec.FileName);
        }

        private static string Relative(Transform root, Transform current)
        {
            if (root == current) return root.name;
            var names = new Stack<string>();
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return $"{root.name}/{string.Join("/", names)}";
        }

        private static void Copy(string source, string destination)
        {
            if (!AssetDatabase.CopyAsset(source, destination))
                throw new InvalidOperationException($"Could not copy '{source}' to '{destination}'.");
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private readonly struct Spec
        {
            public readonly int PrefabId;
            public readonly string FileName;
            public Spec(int prefabId, string fileName)
            {
                PrefabId = prefabId;
                FileName = fileName;
            }
        }

        private readonly struct Result
        {
            public readonly Spec Spec;
            public readonly string ArchivePath;
            public readonly string LogicPath;
            public readonly string ViewPath;
            public readonly string Address;
            public readonly string LogicGuid;
            public readonly string ViewGuid;
            public Result(
                Spec spec, string archivePath, string logicPath, string viewPath,
                string address, string logicGuid, string viewGuid)
            {
                Spec = spec;
                ArchivePath = archivePath;
                LogicPath = logicPath;
                ViewPath = viewPath;
                Address = address;
                LogicGuid = logicGuid;
                ViewGuid = viewGuid;
            }
        }
    }
}
