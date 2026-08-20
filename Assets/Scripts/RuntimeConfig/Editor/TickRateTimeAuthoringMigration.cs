using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// One-shot migration from the historical 30 Hz Tick/seconds authoring
    /// fields to integer millisecond authoring. Runtime Tick state is not
    /// touched. The legacy fields remain serialized and hidden for rollback
    /// compatibility, but production Bake reads the migrated source first.
    /// </summary>
    public static class TickRateTimeAuthoringMigration
    {
        private const int LegacyTickRate = 30;

        private static readonly Dictionary<string, string[]>
            LegacyFieldOverrides =
                new Dictionary<string, string[]>(
                    StringComparer.Ordinal)
                {
                    { "firstWaveDelay", new[] { "firstWaveTick" } },
                    { "FirstWaveDelay", new[] { "FirstWaveTick" } },
                    { "maxChargeDuration", new[] { "maxChargeTicks" } },
                    { "MaxChargeDuration", new[] { "MaxChargeTicks" } },
                    { "DecayDuration", new[] { "DecayTicks" } },
                    { "ExtendDuration", new[] { "ExtendTicks" } },
                    { "MaximumRemainingDuration", new[] { "MaximumRemainingTicks" } },
                    { "RestartBurstDuration", new[] { "RestartBurstTicks" } },
                    { "DeathPresentationDuration", new[] { "DeathPresentationTicks" } },
                    { "deathPresentationDuration", new[] { "deathPresentationTicks" } },
                };

        [MenuItem(
            "FrameSyncMoba/Migration/Migrate Legacy Time To Milliseconds")]
        public static void MigrateAllProjectAssets()
        {
            ValidateEditorState();

            int changedObjects = 0;
            int changedFields = 0;
            Debug.Log("[TimeMigration] Migrating formal configuration assets.");
            MigrateScriptableObjects(
                ref changedObjects,
                ref changedFields);
            Debug.Log("[TimeMigration] Migrating known presentation prefabs.");
            MigratePrefabs(
                ref changedObjects,
                ref changedFields);
            Debug.Log("[TimeMigration] Migrating project scenes.");
            MigrateScenes(
                ref changedObjects,
                ref changedFields);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[TimeMigration] Complete. objects={changedObjects} " +
                $"fields={changedFields} legacyTickRate={LegacyTickRate}Hz");
        }

        public static void MigrateFormalConfigAssetsOnly()
        {
            ValidateEditorState();
            int changedObjects = 0;
            int changedFields = 0;
            MigrateScriptableObjects(
                ref changedObjects,
                ref changedFields);
            SaveBatch("Formal", changedObjects, changedFields);
        }

        public static void MigratePresentationPrefabsOnly()
        {
            ValidateEditorState();
            int changedObjects = 0;
            int changedFields = 0;
            MigratePrefabs(
                ref changedObjects,
                ref changedFields);
            SaveBatch("Prefabs", changedObjects, changedFields);
        }

        public static void MigrateScenesOnly()
        {
            ValidateEditorState();
            int changedObjects = 0;
            int changedFields = 0;
            MigrateScenes(
                ref changedObjects,
                ref changedFields);
            SaveBatch("Scenes", changedObjects, changedFields);
        }

        private static void ValidateEditorState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling)
                throw new InvalidOperationException(
                    "Time migration requires an idle Unity Editor.");

            Scene[] openScenes = GetOpenScenes();
            for (int i = 0; i < openScenes.Length; i++)
            {
                if (openScenes[i].isDirty)
                    throw new InvalidOperationException(
                        $"Save dirty scene '{openScenes[i].path}' before time migration.");
            }
        }

        private static void SaveBatch(
            string batch,
            int changedObjects,
            int changedFields)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[TimeMigration] {batch} complete. " +
                $"objects={changedObjects} fields={changedFields}");
        }

        private static void MigrateScriptableObjects(
            ref int changedObjects,
            ref int changedFields)
        {
            var paths = new List<string>();
            AddFoundPaths(
                paths,
                "Assets/Config/Formal/Abilities");
            AddFoundPaths(
                paths,
                "Assets/Config/Formal/Animation");
            AddFoundPaths(
                paths,
                "Assets/Config/Formal/Buffs");
            AddFoundPaths(
                paths,
                "Assets/Config/Formal/Equipment");
            AddFoundPaths(
                paths,
                "Assets/Resources/Animation");
            AddExactPath(paths,
                "Assets/Config/Formal/FullMatchMinionWaveConfig.asset");
            AddExactPath(paths,
                "Assets/Config/Formal/FullMatchProjectileRuntimeCatalog.asset");
            AddExactPath(paths,
                "Assets/Config/Formal/FullMatchUnitDisposePolicyTable.asset");
            AddExactPath(paths,
                "Assets/Config/Formal/FullMatchUnitRuntimeCatalog.asset");
            AddExactPath(paths,
                "Assets/Config/Formal/GlobalGameplayData.asset");
            paths.Sort(StringComparer.Ordinal);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                UnityEngine.Object[] objects =
                    AssetDatabase.LoadAllAssetsAtPath(path);
                for (int objectIndex = 0;
                     objectIndex < objects.Length;
                     objectIndex++)
                {
                    UnityEngine.Object target = objects[objectIndex];
                    if (target == null) continue;
                    int count = MigrateObject(target);
                    if (count <= 0) continue;
                    changedObjects++;
                    changedFields += count;
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private static void AddFoundPaths(
            List<string> paths,
            string root)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:ScriptableObject",
                new[] { root });
            for (int i = 0; i < guids.Length; i++)
                AddExactPath(
                    paths,
                    AssetDatabase.GUIDToAssetPath(guids[i]));
        }

        private static void AddExactPath(
            List<string> paths,
            string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                !paths.Contains(path) &&
                AssetDatabase.LoadMainAssetAtPath(path) != null)
                paths.Add(path);
        }

        private static void MigratePrefabs(
            ref int changedObjects,
            ref int changedFields)
        {
            string[] paths =
            {
                "Assets/Resources/Prefab/VFX/VarusRBuffVFX.prefab",
                "Assets/Resources/Prefab/VFX/VarusSpellEVFX.prefab",
            };
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];
                if (!System.IO.File.Exists(path))
                    continue;
                GameObject root =
                    PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                try
                {
                    Component[] components =
                        root.GetComponentsInChildren<Component>(true);
                    for (int componentIndex = 0;
                         componentIndex < components.Length;
                         componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null) continue;
                        int count = MigrateObject(component);
                        if (count <= 0) continue;
                        changed = true;
                        changedObjects++;
                        changedFields += count;
                    }
                    if (changed)
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void MigrateScenes(
            ref int changedObjects,
            ref int changedFields)
        {
            SceneSetup[] setup =
                EditorSceneManager.GetSceneManagerSetup();
            string[] guids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { "Assets/Scenes" });
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Scene scene = EditorSceneManager.OpenScene(
                        path,
                        OpenSceneMode.Single);
                    bool changed = false;
                    GameObject[] roots = scene.GetRootGameObjects();
                    for (int rootIndex = 0;
                         rootIndex < roots.Length;
                         rootIndex++)
                    {
                        Component[] components = roots[rootIndex]
                            .GetComponentsInChildren<Component>(true);
                        for (int componentIndex = 0;
                             componentIndex < components.Length;
                             componentIndex++)
                        {
                            Component component = components[componentIndex];
                            if (component == null) continue;
                            int count = MigrateObject(component);
                            if (count <= 0) continue;
                            changed = true;
                            changedObjects++;
                            changedFields += count;
                        }
                    }
                    if (changed)
                        EditorSceneManager.SaveScene(scene);
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static int MigrateObject(UnityEngine.Object target)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.UpdateIfRequiredOrScript();
            int changes = MigrateDurationProperties(serializedObject);
            changes += MigrateMillisecondScalars(serializedObject);
            changes += MigrateLevelArrays(serializedObject);
            changes += MigrateGameplayDataVersion(serializedObject);
            if (changes > 0)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            return changes;
        }

        private static int MigrateDurationProperties(
            SerializedObject serializedObject)
        {
            int changes = 0;
            SerializedProperty iterator =
                serializedObject.GetIterator();
            bool enterChildren = true;
            int visited = 0;
            while (iterator.NextVisible(enterChildren))
            {
                EnsureTraversalBound(serializedObject, ref visited);
                enterChildren = true;
                SerializedProperty milliseconds =
                    iterator.FindPropertyRelative("milliseconds");
                SerializedProperty authored =
                    iterator.FindPropertyRelative("authored");
                SerializedProperty rounding =
                    iterator.FindPropertyRelative("roundingPolicy");
                if (milliseconds == null ||
                    authored == null ||
                    rounding == null ||
                    authored.boolValue)
                    continue;

                SerializedProperty legacy = FindLegacyDuration(
                    serializedObject,
                    iterator);
                if (legacy == null) continue;
                milliseconds.intValue = LegacyValueToMilliseconds(legacy);
                rounding.enumValueIndex =
                    (int)DurationRoundingPolicy.Ceil;
                authored.boolValue = true;
                changes++;
                enterChildren = false;
            }
            return changes;
        }

        private static SerializedProperty FindLegacyDuration(
            SerializedObject serializedObject,
            SerializedProperty duration)
        {
            string name = duration.name;
            var candidates = new List<string>();
            if (LegacyFieldOverrides.TryGetValue(
                    name,
                    out string[] special))
                candidates.AddRange(special);

            AddCandidate(candidates, name + "Ticks");
            AddCandidate(candidates, name + "Seconds");
            if (name.EndsWith("Duration", StringComparison.Ordinal))
            {
                string stem = name.Substring(
                    0,
                    name.Length - "Duration".Length);
                AddCandidate(candidates, stem + "Ticks");
                AddCandidate(candidates, stem + "Seconds");
            }
            if (name.Length > 0)
            {
                string lower = char.ToLowerInvariant(name[0]) +
                    name.Substring(1);
                string upper = char.ToUpperInvariant(name[0]) +
                    name.Substring(1);
                AddCandidate(candidates, lower + "Ticks");
                AddCandidate(candidates, lower + "Seconds");
                AddCandidate(candidates, upper + "Ticks");
                AddCandidate(candidates, upper + "Seconds");
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                SerializedProperty sibling = FindSibling(
                    serializedObject,
                    duration.propertyPath,
                    candidates[i]);
                if (sibling != null &&
                    (sibling.propertyType ==
                        SerializedPropertyType.Integer ||
                     sibling.propertyType ==
                        SerializedPropertyType.Float))
                    return sibling;
            }
            return null;
        }

        private static void AddCandidate(
            List<string> candidates,
            string value)
        {
            if (!candidates.Contains(value))
                candidates.Add(value);
        }

        private static SerializedProperty FindSibling(
            SerializedObject serializedObject,
            string propertyPath,
            string siblingName)
        {
            int separator = propertyPath.LastIndexOf('.');
            string siblingPath = separator < 0
                ? siblingName
                : propertyPath.Substring(0, separator + 1) +
                  siblingName;
            return serializedObject.FindProperty(siblingPath);
        }

        private static int LegacyValueToMilliseconds(
            SerializedProperty legacy)
        {
            bool isSeconds = legacy.name.EndsWith(
                "Seconds",
                StringComparison.OrdinalIgnoreCase);
            if (legacy.propertyType ==
                SerializedPropertyType.Float || isSeconds)
            {
                double seconds = legacy.doubleValue;
                return checked((int)Math.Round(
                    seconds * 1000.0,
                    MidpointRounding.AwayFromZero));
            }

            long ticks = legacy.longValue;
            if (ticks <= 0L) return 0;
            return checked((int)(
                ticks * 1000L / LegacyTickRate));
        }

        private static int MigrateMillisecondScalars(
            SerializedObject serializedObject)
        {
            int changes = 0;
            SerializedProperty iterator =
                serializedObject.GetIterator();
            bool enterChildren = true;
            int visited = 0;
            while (iterator.NextVisible(enterChildren))
            {
                EnsureTraversalBound(serializedObject, ref visited);
                enterChildren = true;
                if (iterator.propertyType !=
                        SerializedPropertyType.Integer ||
                    !iterator.name.EndsWith(
                        "Milliseconds",
                        StringComparison.Ordinal) ||
                    iterator.intValue != 0)
                    continue;

                string stem = iterator.name.Substring(
                    0,
                    iterator.name.Length -
                    "Milliseconds".Length);
                string capitalizedStem = stem.Length == 0
                    ? stem
                    : char.ToUpperInvariant(stem[0]) +
                      stem.Substring(1);
                string[] candidates =
                {
                    stem + "Seconds",
                    "legacy" + capitalizedStem + "Seconds",
                    capitalizedStem + "Seconds",
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    SerializedProperty legacy = FindSibling(
                        serializedObject,
                        iterator.propertyPath,
                        candidates[i]);
                    if (legacy == null ||
                        legacy.propertyType !=
                            SerializedPropertyType.Float)
                        continue;
                    iterator.intValue = checked((int)Math.Round(
                        legacy.doubleValue * 1000.0,
                        MidpointRounding.AwayFromZero));
                    changes++;
                    break;
                }
            }
            return changes;
        }

        private static void EnsureTraversalBound(
            SerializedObject serializedObject,
            ref int visited)
        {
            visited++;
            if (visited <= 100000) return;
            throw new InvalidOperationException(
                $"Serialized time migration exceeded 100000 properties on " +
                $"'{serializedObject.targetObject.name}' " +
                $"({serializedObject.targetObject.GetType().FullName}).");
        }

        private static int MigrateLevelArrays(
            SerializedObject serializedObject)
        {
            int changes = MigrateArray(
                serializedObject,
                "cooldownMillisecondsByLevel",
                "cooldownTicksByLevel",
                false);
            changes += MigrateArray(
                serializedObject,
                "DurationMillisecondsByUnitLevel",
                "DurationSecondsByUnitLevel",
                true);
            return changes;
        }

        private static int MigrateArray(
            SerializedObject serializedObject,
            string destinationPath,
            string legacyPath,
            bool legacyIsSeconds)
        {
            SerializedProperty destination =
                serializedObject.FindProperty(destinationPath);
            SerializedProperty legacy =
                serializedObject.FindProperty(legacyPath);
            if (destination == null ||
                legacy == null ||
                !destination.isArray ||
                !legacy.isArray ||
                destination.arraySize > 0 ||
                legacy.arraySize == 0)
                return 0;

            destination.arraySize = legacy.arraySize;
            for (int i = 0; i < legacy.arraySize; i++)
            {
                SerializedProperty source =
                    legacy.GetArrayElementAtIndex(i);
                int milliseconds = legacyIsSeconds
                    ? checked((int)Math.Round(
                        source.doubleValue * 1000.0,
                        MidpointRounding.AwayFromZero))
                    : source.doubleValue <= 0.0
                        ? 0
                        : checked((int)Math.Floor(
                            source.doubleValue * 1000.0 /
                            LegacyTickRate));
                destination.GetArrayElementAtIndex(i)
                    .intValue = milliseconds;
            }
            return 1;
        }

        private static int MigrateGameplayDataVersion(
            SerializedObject serializedObject)
        {
            SerializedProperty version = serializedObject.FindProperty(
                "versions.GameplayDataVersion");
            if (version == null || version.longValue >= 3L)
                return 0;
            version.longValue = 3L;
            return 1;
        }

        private static Scene[] GetOpenScenes()
        {
            var scenes = new Scene[SceneManager.sceneCount];
            for (int i = 0; i < scenes.Length; i++)
                scenes[i] = SceneManager.GetSceneAt(i);
            return scenes;
        }
    }
}
