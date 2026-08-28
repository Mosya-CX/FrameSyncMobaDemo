using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.EditorTools
{
    public sealed class AddressablesPlayerBuildScope : IDisposable
    {
        public const string RequiredClientIndicatorShaderPath =
            "Assets/ClientContent/Shaders/SkillIndicatorUnlit.shader";

        private readonly AddressableAssetSettings settings;
        private readonly AddressableAssetSettings.PlayerBuildOption previous;
        private readonly bool previousDedicatedServerBuild;
        private readonly AddressableAssetGroup previousDefaultGroup;
        private readonly UnityEngine.Object graphicsSettingsAsset;
        private readonly Shader[] previousAlwaysIncludedShaders;
        private readonly System.Collections.Generic.Dictionary<
            BundledAssetGroupSchema,
            bool> previousIncludeInBuild =
                new System.Collections.Generic.Dictionary<
                    BundledAssetGroupSchema,
                    bool>();
        private bool isDisposed;

        internal static bool IsDedicatedServerBuild { get; private set; }

        public AddressablesPlayerBuildScope(bool dedicatedServer)
        {
            settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                throw new InvalidOperationException(
                    "Addressables settings must exist before a player build.");
            previous = settings.BuildAddressablesWithPlayerBuild;
            previousDefaultGroup = settings.DefaultGroup;
            previousDedicatedServerBuild = IsDedicatedServerBuild;
            graphicsSettingsAsset = LoadGraphicsSettingsAsset();
            previousAlwaysIncludedShaders =
                ReadAlwaysIncludedShaders(graphicsSettingsAsset);
            for (int i = 0; i < settings.groups.Count; i++)
            {
                AddressableAssetGroup group = settings.groups[i];
                if (group == null)
                    continue;
                BundledAssetGroupSchema bundled =
                    group.GetSchema<BundledAssetGroupSchema>();
                if (bundled == null)
                    continue;
                previousIncludeInBuild.Add(
                    bundled,
                    bundled.IncludeInBuild);
            }
            if (dedicatedServer)
                AddressablesServerBuildAudit
                    .ValidateLogicGroupDependencies(settings);
            try
            {
                ConfigureRequiredClientShader(
                    graphicsSettingsAsset,
                    previousAlwaysIncludedShaders,
                    dedicatedServer);
                for (int i = 0; i < settings.groups.Count; i++)
                {
                    AddressableAssetGroup group = settings.groups[i];
                    BundledAssetGroupSchema bundled =
                        group?.GetSchema<BundledAssetGroupSchema>();
                    if (bundled == null)
                        continue;
                    bundled.IncludeInBuild =
                        !dedicatedServer ||
                        Array.IndexOf(
                            Addressables.AddressablesProjectConstants.LogicGroups,
                            group.Name) >= 0;
                    EditorUtility.SetDirty(bundled);
                }
                if (dedicatedServer)
                    settings.DefaultGroup = settings.FindGroup(
                        Addressables.AddressablesProjectConstants.LogicCoreGroup);
                settings.BuildAddressablesWithPlayerBuild =
                    AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                IsDedicatedServerBuild = dedicatedServer;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            Restore();
        }

        private void Restore()
        {
            foreach (System.Collections.Generic.KeyValuePair<
                         BundledAssetGroupSchema,
                         bool> pair in previousIncludeInBuild)
            {
                if (pair.Key != null)
                {
                    pair.Key.IncludeInBuild = pair.Value;
                    EditorUtility.SetDirty(pair.Key);
                }
            }
            settings.DefaultGroup = previousDefaultGroup;
            settings.BuildAddressablesWithPlayerBuild = previous;
            IsDedicatedServerBuild = previousDedicatedServerBuild;
            WriteAlwaysIncludedShaders(
                graphicsSettingsAsset,
                previousAlwaysIncludedShaders);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        public static bool IsRequiredClientShaderAlwaysIncluded()
        {
            UnityEngine.Object asset = LoadGraphicsSettingsAsset();
            Shader required = LoadRequiredClientShader();
            Shader[] shaders = ReadAlwaysIncludedShaders(asset);
            for (int i = 0; i < shaders.Length; i++)
            {
                if (shaders[i] == required)
                    return true;
            }
            return false;
        }

        private static void ConfigureRequiredClientShader(
            UnityEngine.Object asset,
            IReadOnlyList<Shader> current,
            bool dedicatedServer)
        {
            Shader required = LoadRequiredClientShader();
            var next = new List<Shader>(current.Count + 1);
            for (int i = 0; i < current.Count; i++)
            {
                Shader shader = current[i];
                if (shader != required)
                    next.Add(shader);
            }
            if (!dedicatedServer)
                next.Add(required);
            WriteAlwaysIncludedShaders(asset, next);
        }

        private static UnityEngine.Object LoadGraphicsSettingsAsset()
        {
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/GraphicsSettings.asset");
            if (assets == null || assets.Length != 1 || assets[0] == null)
            {
                throw new InvalidOperationException(
                    "Unity GraphicsSettings asset could not be resolved.");
            }
            return assets[0];
        }

        private static Shader LoadRequiredClientShader()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                RequiredClientIndicatorShaderPath);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    $"Required client indicator Shader is missing at " +
                    $"'{RequiredClientIndicatorShaderPath}'.");
            }
            return shader;
        }

        private static Shader[] ReadAlwaysIncludedShaders(
            UnityEngine.Object asset)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(
                "m_AlwaysIncludedShaders");
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    "GraphicsSettings.m_AlwaysIncludedShaders is unavailable.");
            }
            var result = new Shader[property.arraySize];
            for (int i = 0; i < property.arraySize; i++)
            {
                result[i] = property.GetArrayElementAtIndex(i)
                    .objectReferenceValue as Shader;
            }
            return result;
        }

        private static void WriteAlwaysIncludedShaders(
            UnityEngine.Object asset,
            IReadOnlyList<Shader> shaders)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty property = serialized.FindProperty(
                "m_AlwaysIncludedShaders");
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(
                    "GraphicsSettings.m_AlwaysIncludedShaders is unavailable.");
            }
            property.arraySize = shaders.Count;
            for (int i = 0; i < shaders.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue =
                    shaders[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>
    /// Destructively edits only the temporary server build-scene copy. Source
    /// scenes/assets are untouched. Presentation roots and serialized asset
    /// edges are removed before scene serialization.
    /// </summary>
    internal sealed class DedicatedServerScenePresentationStripper :
        IProcessSceneWithReport
    {
        public int callbackOrder => -1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!IsDedicatedServer(report))
                return;
            GameObject[] roots = scene.GetRootGameObjects();
            int destroyedRoots = 0;
            int destroyedComponents = 0;
            for (int i = roots.Length - 1; i >= 0; i--)
            {
                GameObject root = roots[i];
                if (root.GetComponentInChildren<Canvas>(true) != null ||
                    root.GetComponentInChildren<EventSystem>(true) != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    destroyedRoots++;
                    continue;
                }
                destroyedComponents += StripComponents<Renderer>(root);
                destroyedComponents += StripComponents<Animator>(root);
                destroyedComponents += StripComponents<AudioSource>(root);
                destroyedComponents += StripComponents<AudioListener>(root);
                destroyedComponents += StripComponents<ParticleSystem>(root);
                destroyedComponents +=
                    DedicatedServerPresentationStripUtility
                        .StripCamerasAndLights(root);
                ClearPresentationObjectReferences(root);
            }
            Debug.Log(
                $"[ServerBuildStrip] scene={scene.path} roots={destroyedRoots} components={destroyedComponents}");
        }

        private static int StripComponents<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            int count = components.Length;
            for (int i = components.Length - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(components[i]);
            return count;
        }

        private static void ClearPresentationObjectReferences(GameObject root)
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int behaviourIndex = 0;
                 behaviourIndex < behaviours.Length;
                 behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null)
                    continue;
                var serialized = new SerializedObject(behaviour);
                SerializedProperty property = serialized.GetIterator();
                bool changed = false;
                bool enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType !=
                            SerializedPropertyType.ObjectReference ||
                        property.objectReferenceValue == null)
                        continue;
                    UnityEngine.Object referenced =
                        property.objectReferenceValue;
                    string path = AssetDatabase.GetAssetPath(referenced);
                    if (!IsPresentationReference(referenced, path))
                        continue;
                    property.objectReferenceValue = null;
                    changed = true;
                }
                if (changed)
                    serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool IsPresentationReference(
            UnityEngine.Object referenced,
            string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                (path.StartsWith(
                     "Assets/ClientContent/",
                     StringComparison.Ordinal) ||
                 path.StartsWith(
                     "Assets/Art/",
                     StringComparison.Ordinal)))
                return true;
            return referenced is Material ||
                   referenced is Texture ||
                   referenced is Sprite ||
                   referenced is Mesh ||
                   referenced is AnimationClip ||
                   referenced is RuntimeAnimatorController ||
                   referenced is AudioClip ||
                   referenced is RenderTexture ||
                   referenced is Shader;
        }

        internal static bool IsDedicatedServer(BuildReport report) =>
            report != null &&
            AddressablesPlayerBuildScope.IsDedicatedServerBuild;
    }

    public static class DedicatedServerPresentationStripUtility
    {
        public static int StripCamerasAndLights(GameObject root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            int count = 0;
            count += StripComponentsByTypeName(
                root,
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            count += StripComponentsByTypeName(
                root,
                "UnityEngine.Rendering.Universal.UniversalAdditionalLightData");
            count += StripComponents<Camera>(root);
            count += StripComponents<Light>(root);
            return count;
        }

        private static int StripComponents<T>(GameObject root)
            where T : Component
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            int count = components.Length;
            for (int i = components.Length - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(components[i]);
            return count;
        }

        private static int StripComponentsByTypeName(
            GameObject root,
            string fullTypeName)
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);
            int count = 0;
            for (int i = behaviours.Length - 1; i >= 0; i--)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null ||
                    !string.Equals(
                        behaviour.GetType().FullName,
                        fullTypeName,
                        StringComparison.Ordinal))
                    continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
                count++;
            }
            return count;
        }
    }

    internal sealed class DedicatedServerAddressablesAudit :
        IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!DedicatedServerScenePresentationStripper
                    .IsDedicatedServer(report))
                return;
            string outputDirectory = Directory.Exists(
                    report.summary.outputPath)
                ? report.summary.outputPath
                : Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(outputDirectory) ||
                !Directory.Exists(outputDirectory))
                throw new BuildFailedException(
                    "Dedicated Server output directory cannot be audited.");
            AddressablesServerBuildAudit.ValidateOutputDirectory(
                outputDirectory);
            Debug.Log(
                "[ServerBuildAudit] Passed: logic-only Addressables content is present and no client bundle was emitted.");
        }
    }

    public static class AddressablesServerBuildAudit
    {
        public static void ValidateLogicGroupDependencies(
            AddressableAssetSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            for (int groupIndex = 0;
                 groupIndex < Addressables.AddressablesProjectConstants
                     .LogicGroups.Length;
                 groupIndex++)
            {
                string groupName = Addressables
                    .AddressablesProjectConstants.LogicGroups[groupIndex];
                AddressableAssetGroup group = settings.FindGroup(groupName);
                if (group == null)
                    throw new BuildFailedException(
                        $"Dedicated Server logic Addressables group '{groupName}' is missing.");
                foreach (AddressableAssetEntry entry in group.entries)
                    ValidateLogicRootDependencies(
                        entry.AssetPath,
                        groupName);
            }
        }

        public static void ValidateLogicRootDependencies(
            string rootPath,
            string owner)
        {
            if (Addressables.AddressableDependencyInventory.Classify(
                    rootPath) == "ClientPresentation")
                throw new BuildFailedException(
                    $"Dedicated Server logic root '{rootPath}' in '{owner}' is client presentation content.");
            string[] dependencies = AssetDatabase.GetDependencies(
                rootPath,
                true);
            for (int i = 0; i < dependencies.Length; i++)
            {
                string dependency = dependencies[i];
                if (string.Equals(
                        dependency,
                        rootPath,
                        StringComparison.Ordinal))
                    continue;
                if (Addressables.AddressableDependencyInventory.Classify(
                        dependency) == "ClientPresentation")
                    throw new BuildFailedException(
                        $"Dedicated Server logic root '{rootPath}' in '{owner}' reaches client presentation dependency '{dependency}'.");
            }
        }

        public static void ValidateOutputDirectory(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException(
                    "Server output directory is required.",
                    nameof(outputDirectory));
            string fullOutputDirectory = Path.GetFullPath(outputDirectory);
            if (!Directory.Exists(fullOutputDirectory))
                throw new DirectoryNotFoundException(fullOutputDirectory);

            string[] addressablesDirectories = Directory.GetDirectories(
                fullOutputDirectory,
                "aa",
                SearchOption.AllDirectories);
            for (int i = 0; i < addressablesDirectories.Length; i++)
            {
                string parent = Path.GetDirectoryName(
                    addressablesDirectories[i]);
                if (string.Equals(
                        Path.GetFileName(parent),
                        "StreamingAssets",
                        StringComparison.OrdinalIgnoreCase))
                {
                    goto AddressablesFound;
                }
            }

            throw new BuildFailedException(
                "Dedicated Server is missing its logic Addressables directory.");

        AddressablesFound:

            string[] catalogs = Directory.GetFiles(
                fullOutputDirectory,
                "catalog*.json",
                SearchOption.AllDirectories);
            string[] bundles = Directory.GetFiles(
                fullOutputDirectory,
                "*.bundle",
                SearchOption.AllDirectories);
            if (catalogs.Length == 0 || bundles.Length == 0)
            {
                throw new BuildFailedException(
                    $"Dedicated Server logic Addressables content is incomplete: catalogs={catalogs.Length}, bundles={bundles.Length}.");
            }
            for (int i = 0; i < bundles.Length; i++)
            {
                if (Path.GetFileName(bundles[i]).IndexOf(
                        "client-",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new BuildFailedException(
                        $"Dedicated Server contains a client Addressables bundle: '{bundles[i]}'.");
            }
        }
    }

    public static class AddressablesClientBuildAudit
    {
        [Serializable]
        private sealed class RuntimeSettings
        {
            public string m_buildTarget;
        }

        public static void PrepareOutput(string playerOutputPath)
        {
            string addressablesRoot = GetAddressablesRoot(playerOutputPath);
            if (!Directory.Exists(addressablesRoot))
                return;
            Directory.Delete(addressablesRoot, true);
            Debug.Log(
                $"[ClientBuildAudit] Removed stale generated Addressables output '{addressablesRoot}'.");
        }

        public static void ValidateOutput(
            string playerOutputPath,
            BuildTarget expectedTarget)
        {
            string addressablesRoot = GetAddressablesRoot(playerOutputPath);
            string settingsPath = Path.Combine(
                addressablesRoot,
                "settings.json");
            if (!File.Exists(settingsPath))
            {
                throw new BuildFailedException(
                    $"Client Addressables settings are missing: '{settingsPath}'.");
            }

            string expectedTargetName = expectedTarget.ToString();
            string settingsJson = File.ReadAllText(settingsPath);
            RuntimeSettings runtimeSettings =
                JsonUtility.FromJson<RuntimeSettings>(settingsJson);
            if (runtimeSettings == null ||
                !string.Equals(
                    runtimeSettings.m_buildTarget,
                    expectedTargetName,
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    $"Client Addressables platform mismatch. Expected {expectedTargetName}; " +
                    $"settings.json declared '{runtimeSettings?.m_buildTarget ?? "<missing>"}'.");
            }

            string platformRoot = Path.Combine(
                addressablesRoot,
                expectedTargetName);
            if (!Directory.Exists(platformRoot))
            {
                throw new BuildFailedException(
                    $"Client Addressables platform directory is missing: '{platformRoot}'.");
            }

            string[] bundles = Directory.GetFiles(
                platformRoot,
                "*.bundle",
                SearchOption.TopDirectoryOnly);
            if (bundles.Length == 0)
            {
                throw new BuildFailedException(
                    $"Client Addressables platform directory contains no bundles: '{platformRoot}'.");
            }

            string wrongTarget = expectedTarget ==
                    BuildTarget.StandaloneWindows64
                ? BuildTarget.StandaloneLinux64.ToString()
                : BuildTarget.StandaloneWindows64.ToString();
            string wrongPlatformRoot = Path.Combine(
                addressablesRoot,
                wrongTarget);
            if (Directory.Exists(wrongPlatformRoot))
            {
                throw new BuildFailedException(
                    $"Client output contains stale {wrongTarget} Addressables content: '{wrongPlatformRoot}'.");
            }

            Debug.Log(
                $"[ClientBuildAudit] Passed: target={expectedTargetName} bundles={bundles.Length} root='{addressablesRoot}'.");
        }

        private static string GetAddressablesRoot(
            string playerOutputPath)
        {
            if (string.IsNullOrWhiteSpace(playerOutputPath))
                throw new ArgumentException(
                    "Player output path is required.",
                    nameof(playerOutputPath));
            string fullOutputPath = Path.GetFullPath(playerOutputPath);
            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new ArgumentException(
                    "Player output directory is invalid.",
                    nameof(playerOutputPath));
            }
            string dataDirectoryName =
                Path.GetFileNameWithoutExtension(fullOutputPath) +
                "_Data";
            string dataDirectory = Path.GetFullPath(
                Path.Combine(
                    outputDirectory,
                    dataDirectoryName));
            string expectedPrefix =
                Path.GetFullPath(outputDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!dataDirectory.StartsWith(
                    expectedPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Generated Player data directory escaped its output root: '{dataDirectory}'.");
            }
            return Path.Combine(
                dataDirectory,
                "StreamingAssets",
                "aa");
        }
    }
}
