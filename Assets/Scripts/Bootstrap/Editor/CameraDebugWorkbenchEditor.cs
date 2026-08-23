using FrameSyncMoba.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.EditorTools
{
    [CustomEditor(typeof(CameraDebugWorkbench))]
    public sealed class CameraDebugWorkbenchEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var workbench = (CameraDebugWorkbench)target;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(
                "Camera Debug Actions",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "These proxies only test the formal pointer ray/plane, " +
                "center-radius selection and outline path. They do not " +
                "create Gameplay Units, navigation grids or networking.",
                MessageType.Info);

            if (GUILayout.Button("Switch Blue / Red Preview"))
            {
                Undo.RecordObject(workbench, "Switch Camera Preview Side");
                workbench.TogglePreviewSide();
                EditorUtility.SetDirty(workbench);
            }
            if (GUILayout.Button("Apply Camera + Pointer Draft To Formal"))
            {
                ApplyDraftToFormal(workbench);
            }
            if (GUILayout.Button("Pull Current Formal Configuration"))
            {
                Undo.RecordObject(workbench, "Pull Formal Camera Configuration");
                CameraDebugPointerProbe probe =
                    workbench.GetComponent<CameraDebugPointerProbe>();
                workbench.EditorConfigure(
                    workbench.FormalConfig,
                    FindSceneComponent<CameraController>(workbench.gameObject.scene),
                    probe);
                EditorUtility.SetDirty(workbench);
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Generate 128 Lightweight Stress Proxies"))
                CameraDebugSceneSetup.CreateStressProxies(workbench, 128);
            if (GUILayout.Button("Generate 512 Lightweight Stress Proxies"))
                CameraDebugSceneSetup.CreateStressProxies(workbench, 512);
            if (GUILayout.Button("Remove Stress Proxies"))
                CameraDebugSceneSetup.CreateStressProxies(workbench, 0);
        }

        private static void ApplyDraftToFormal(CameraDebugWorkbench workbench)
        {
            MobaCameraPresentationConfig config = workbench.FormalConfig;
            if (config == null)
            {
                EditorUtility.DisplayDialog(
                    "Camera Debug",
                    "Formal configuration asset is not assigned.",
                    "OK");
                return;
            }
            Undo.RecordObject(config, "Apply Camera Debug Draft");
            config.EditorCopyFrom(
                workbench.BlueSide,
                workbench.RedSide,
                workbench.PointerGroundY,
                workbench.PointerPickRadius,
                workbench.FriendlyOutlineColor,
                workbench.EnemyOutlineColor,
                workbench.OutlineWidth);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[CameraDebug] Applied camera sides, pointer radius and " +
                "outline settings to the formal shared configuration.");
        }

        private static T FindSceneComponent<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }
    }

    public static class CameraDebugSceneSetup
    {
        private const string DebugScenePath =
            "Assets/Scenes/Tests/CameraDebugScene.unity";
        private const string FormalScenePath =
            "Assets/Scenes/GameScene.unity";
        private const string ConfigFolder =
            "Assets/Config/Formal/Presentation";
        private const string ConfigPath =
            ConfigFolder + "/MobaCameraPresentationConfig.asset";
        private const string OutlineMaterialPath =
                "Assets/ClientContent/Materials/UnitOutlineRim.mat";

        [MenuItem("FrameSyncMoba/Camera Debug/Setup Simplified Formal Scene")]
        public static void SetupSimplifiedFormalScene()
        {
            MobaCameraPresentationConfig config = EnsureFormalConfig();
            Scene debugScene = EnsureSceneLoaded(DebugScenePath);
            CameraController controller =
                FindSceneComponent<CameraController>(debugScene);
            Camera camera = FindSceneComponent<Camera>(debugScene);
            if (controller == null || camera == null)
                throw new System.InvalidOperationException(
                    "CameraDebugScene requires a CameraController and Camera.");

            GameObject workbenchObject =
                FindRoot(debugScene, "CameraDebugWorkbench");
            if (workbenchObject == null)
            {
                workbenchObject = new GameObject("CameraDebugWorkbench");
                SceneManager.MoveGameObjectToScene(workbenchObject, debugScene);
            }
            CameraDebugWorkbench workbench =
                GetOrAdd<CameraDebugWorkbench>(workbenchObject);
            CameraDebugPointerProbe probe =
                GetOrAdd<CameraDebugPointerProbe>(workbenchObject);
            workbench.EditorConfigure(config, controller, probe);
            probe.Configure(camera, workbench);
            controller.SetPresentationConfig(config, config.BlueTeamId);

            EnsureBaselineProxies(debugScene, workbench);
            WireFormalScene(config);
            EditorSceneManager.MarkSceneDirty(debugScene);
            EditorSceneManager.SaveScene(debugScene);
            Selection.activeObject = workbenchObject;
            Debug.Log(
                "[CameraDebug] Simplified formal camera/pointer scene ready. " +
                "No grid, navigation, Gameplay Unit, Tick or network runtime was added.");
        }

        public static void CreateStressProxies(
            CameraDebugWorkbench workbench,
            int count)
        {
            if (workbench == null)
                return;
            Scene scene = workbench.gameObject.scene;
            GameObject existing = FindRoot(scene, "PointerStressProxies");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);
            if (count <= 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                return;
            }

            GameObject root = new GameObject("PointerStressProxies");
            Undo.RegisterCreatedObjectUndo(root, "Create Pointer Stress Proxies");
            SceneManager.MoveGameObjectToScene(root, scene);
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            const float spacing = 0.72f;
            for (int i = 0; i < count; i++)
            {
                int x = i % columns;
                int z = i / columns;
                Vector3 position = new Vector3(
                    (x - columns * 0.5f) * spacing,
                    0.45f,
                    (z - columns * 0.5f) * spacing);
                CreateProxy(
                    root.transform,
                    1000 + i,
                    (byte)((i & 1) == 0 ? 1 : 2),
                    position,
                    new Vector3(0.45f, 0.45f, 0.45f));
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void EnsureBaselineProxies(
            Scene scene,
            CameraDebugWorkbench workbench)
        {
            GameObject old = FindRoot(scene, "PointerAccuracyProxies");
            if (old != null)
                Object.DestroyImmediate(old);
            GameObject root = new GameObject("PointerAccuracyProxies");
            SceneManager.MoveGameObjectToScene(root, scene);
            Vector3[] positions =
            {
                new Vector3(-6f, 0.75f, -4f),
                new Vector3(-2f, 0.75f, -4f),
                new Vector3(2f, 0.75f, -4f),
                new Vector3(6f, 0.75f, -4f),
                new Vector3(-6f, 0.75f, 0f),
                new Vector3(-1.1f, 0.75f, 0f),
                new Vector3(1.1f, 0.75f, 0f),
                new Vector3(6f, 0.75f, 0f),
                new Vector3(-6f, 0.75f, 4f),
                new Vector3(-2f, 0.75f, 4f),
                new Vector3(2f, 0.75f, 4f),
                new Vector3(6f, 0.75f, 4f),
            };
            for (int i = 0; i < positions.Length; i++)
            {
                CreateProxy(
                    root.transform,
                    i + 1,
                    (byte)((i & 1) == 0 ? 1 : 2),
                    positions[i],
                    new Vector3(1f, 1.5f, 1f));
            }
        }

        private static void CreateProxy(
            Transform parent,
            int stableId,
            byte teamId,
            Vector3 position,
            Vector3 scale)
        {
            GameObject proxyObject = GameObject.CreatePrimitive(
                PrimitiveType.Capsule);
            proxyObject.name = $"PointerProxy_{stableId:D4}_T{teamId}";
            proxyObject.transform.SetParent(parent, false);
            proxyObject.transform.position = position;
            proxyObject.transform.localScale = scale;
            Collider collider = proxyObject.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            ClientUnitOutline outline =
                proxyObject.AddComponent<ClientUnitOutline>();
            outline.OutlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                OutlineMaterialPath);
            CameraDebugSelectableProxy proxy =
                proxyObject.AddComponent<CameraDebugSelectableProxy>();
            proxy.Configure(stableId, teamId, outline);
        }

        private static MobaCameraPresentationConfig EnsureFormalConfig()
        {
            MobaCameraPresentationConfig config =
                AssetDatabase.LoadAssetAtPath<MobaCameraPresentationConfig>(
                    ConfigPath);
            if (config != null)
                return config;
            if (!AssetDatabase.IsValidFolder(ConfigFolder))
                AssetDatabase.CreateFolder("Assets/Config/Formal", "Presentation");
            config = ScriptableObject.CreateInstance<
                MobaCameraPresentationConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void WireFormalScene(
            MobaCameraPresentationConfig config)
        {
            bool wasLoaded = SceneManager.GetSceneByPath(FormalScenePath).isLoaded;
            Scene formal = EnsureSceneLoaded(FormalScenePath);
            CameraController formalController =
                FindSceneComponent<CameraController>(formal);
            if (formalController == null)
                throw new System.InvalidOperationException(
                    "GameScene has no CameraController.");
            SerializedObject serialized = new SerializedObject(formalController);
            SerializedProperty property =
                serialized.FindProperty("presentationConfig");
            if (property.objectReferenceValue != config)
            {
                property.objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(formalController);
                EditorSceneManager.MarkSceneDirty(formal);
                EditorSceneManager.SaveScene(formal);
            }
            if (!wasLoaded)
                EditorSceneManager.CloseScene(formal, true);
        }

        private static Scene EnsureSceneLoaded(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return scene.isLoaded
                ? scene
                : EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                    return roots[i];
            }
            return null;
        }

        private static T FindSceneComponent<T>(Scene scene)
            where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                    return component;
            }
            return null;
        }

        private static T GetOrAdd<T>(GameObject gameObject)
            where T : Component
        {
            T component = gameObject.GetComponent<T>();
            return component != null
                ? component
                : gameObject.AddComponent<T>();
        }
    }
}
