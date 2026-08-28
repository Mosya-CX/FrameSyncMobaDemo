using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEditor;
using Unity.Mathematics.FixedPoint;
using Object = UnityEngine.Object;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.Bootstrap.Tests
{
    public sealed class GameBootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator ClientComposition_InitializesFromProjectAssets()
        {
            var root = new GameObject("TestClientBootstrap");
            root.SetActive(false);
            try
            {
                var camera = root.AddComponent<Camera>();
                var input = root.AddComponent<PlayerInputController>();
                var bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(
                    bootstrap,
                    "globalGameplayData",
                    LoadAsset<GlobalGameplayData>(
                        "8b0cdcd39dbb2964baebdd8475f1e60e"));
                SetReference(
                    bootstrap,
                    "unitRuntimeCatalog",
                    LoadAsset<UnitRuntimeCatalogAsset>(
                        "cf6a213803fa81b4cb7ac2699f40045b"));
                SetReference(
                    bootstrap,
                    "abilityRuntimeCatalog",
                    LoadAsset<AbilityRuntimeCatalogAsset>(
                        "e09025f013ae7a8449335c6356fee5fb"));
                SetReference(
                    bootstrap,
                    "projectileRuntimeCatalog",
                    LoadAsset<ProjectileRuntimeCatalogAsset>(
                        "e548718fd0a6b7d4b87db7539574720f"));
                SetReference(
                    bootstrap,
                    "equipmentCatalog",
                    LoadAsset<EquipmentCatalogAsset>(
                        "eb9d7cfdf62385847aa5e2480b266dae"));
                SetReference(bootstrap, "playerInputController", input);
                SetReference(bootstrap, "gameplayCamera", camera);
                SetReference(
                    input,
                    "inputActions",
                    UnityEditor.AssetDatabase.LoadAssetAtPath<
                        InputActionAsset>(
                        "Assets/Input/PlayerInputActions.inputactions"));
                GameSessionContext.SetSelectedMatchContent(
                    1,
                    new[] { 1001 });
                root.SetActive(true);
                int waitFrames = 0;
                while (!bootstrap.IsInitialized && waitFrames < 600)
                {
                    waitFrames++;
                    yield return null;
                }

                Assert.IsTrue(bootstrap.IsInitialized);
                Assert.NotNull(bootstrap.Runtime);
                Assert.NotNull(bootstrap.UnitWorld);
                Assert.NotNull(bootstrap.PhysicsWorld);
                Assert.That(
                    bootstrap.UnitWorld.EquipmentDatabase.Count,
                    Is.EqualTo(11),
                    "The formal composition root must bake the global equipment catalog into UnitWorld for the shop runtime.");

                int unitsBeforeMismatch =
                    bootstrap.UnitWorld.GetAllUnits().Count;
                var mismatchedContent = new GameStartConfig(
                    "mismatched-content",
                    1,
                    1,
                    1,
                    1,
                    new[]
                    {
                        new PlayerSlotConfig(
                            0,
                            "AatroxOnly",
                            1,
                            new TeamId(1),
                            1002,
                            0),
                    },
                    0,
                    123u,
                    bootstrap.LocalVersions.GameplayDataVersion);
                InvalidOperationException mismatch =
                    Assert.Throws<InvalidOperationException>(
                        () => bootstrap.BuildAuthoritativeBootstrapPayload(
                            mismatchedContent));
                StringAssert.Contains(
                    "does not match",
                    mismatch.Message);
                Assert.That(
                    bootstrap.UnitWorld.GetAllUnits().Count,
                    Is.EqualTo(unitsBeforeMismatch),
                    "Content mismatch must fail before initial spawn materialization.");
                Assert.That(bootstrap.Runtime.CurrentTick, Is.Zero);

                var tickController =
                    new SimulationTickContextController();
                UnitUid controlledUid;
                tickController.BeginTick(
                    bootstrap.Runtime.CurrentTick,
                    ExecutionMode.ServerAuthority);
                try
                {
                    controlledUid = bootstrap.UnitWorld.SpawnUnit(
                        new UnitSpawnRequest(
                            1001,
                            GameplayParticipantId.Explicit(1001),
                            new TeamId(1),
                            fp2.zero,
                            new fp2(fp.one, fp.zero)));
                }
                finally
                {
                    tickController.EndTick();
                }
                Assert.That(
                    bootstrap.UnitWorld.TryGetUnit(
                        controlledUid,
                        out UnitType controlledUnit),
                    Is.True,
                    "Formal hero spawn must register the controlled unit.");

                bootstrap.BindLocalPlayer(
                    controlledUnit,
                    playerSlot: 0,
                    clientId: 1);

                EquipmentDefinition firstEquipment =
                    bootstrap.UnitWorld.EquipmentDatabase
                        .AllDefinitions[0];
                EquipmentShopRequestCheck purchaseCheck = default;
                Assert.DoesNotThrow(
                    () => purchaseCheck =
                        bootstrap.Runtime.EquipmentShop
                            .RequestPurchase(
                                playerSlot: 0,
                                firstEquipment.Id),
                    "Binding the formal local player must inject the canonical shop command submitter.");
                Assert.IsTrue(purchaseCheck.Allowed);
                Assert.That(
                    bootstrap.Runtime.CommandCollector.CommandCount,
                    Is.EqualTo(1));
                GameplayCommand submittedCommand =
                    bootstrap.Runtime.CommandCollector
                        .GetCanonicalCommands()[0];
                Assert.That(
                    submittedCommand.Kind,
                    Is.EqualTo(GameplayCommandKind.EquipmentShop));
                Assert.That(
                    submittedCommand.ShopOperationType,
                    Is.EqualTo(
                        EquipmentShopCommandOperationType.Purchase));
                Assert.That(
                    submittedCommand.EquipmentId,
                    Is.EqualTo(firstEquipment.Id));
            }
            finally
            {
                GameSessionContext.ResetSession();
                Object.Destroy(root);
            }
        }

        [UnityTest]
        public IEnumerator DestroyDuringContentLoad_ReleasesTransferredScope()
        {
            Assert.That(
                AddressableMatchContentScope.ActiveScopeCount,
                Is.Zero);
            var root = new GameObject("DestroyDuringContentLoad");
            root.SetActive(false);
            Task initialization = null;
            try
            {
                var bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(
                    bootstrap,
                    "globalGameplayData",
                    LoadAsset<GlobalGameplayData>(
                        "8b0cdcd39dbb2964baebdd8475f1e60e"));
                GameSessionContext.SetSelectedMatchContent(
                    1,
                    new[] { 1001 });
                root.SetActive(true);
                initialization = bootstrap.InitializationTask;
                Object.DestroyImmediate(root);
                root = null;
                int frames = 0;
                while (!initialization.IsCompleted && frames < 600)
                {
                    frames++;
                    yield return null;
                }
                Assert.That(initialization.IsCompleted, Is.True);
                Assert.That(
                    AddressableMatchContentScope.ActiveScopeCount,
                    Is.Zero,
                    "A destroyed bootstrap must not retain a completed match scope.");
            }
            finally
            {
                GameSessionContext.ResetSession();
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ExternalFlow_PrimesLoadingBeforeContentInitialization()
        {
            var root = new GameObject("ExternalLoadingHandoff");
            root.SetActive(false);
            try
            {
                UIManager manager = root.AddComponent<UIManager>();
                ConfigureLoadingTestPages(manager);
                GameBootstrap bootstrap = root.AddComponent<GameBootstrap>();
                SetReference(bootstrap, "uiManager", manager);
                GameSessionContext.FlowManagedExternally = true;
                GameSessionContext.FlowMode = FrameFlowMode.LocalDirect;

                MethodInfo prime = typeof(GameBootstrap).GetMethod(
                    "PrimeExternalLoadingPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(prime, Is.Not.Null);
                prime.Invoke(bootstrap, null);

                FieldInfo pendingPage = typeof(UIManager).GetField(
                    "pendingMainPage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(pendingPage, Is.Not.Null);
                if (!manager.IsInitialized)
                {
                    Assert.That(
                        (UIPageId)pendingPage.GetValue(manager),
                        Is.EqualTo(UIPageId.Load),
                        "External GameScene flow must replace the serialized Main fallback before awaiting match content.");
                }
                else
                {
                    Assert.That(manager.IsOpen(UIPageId.Load), Is.True);
                    Assert.That(manager.IsOpen(UIPageId.Main), Is.False);
                }

                int frames = 0;
                while (!manager.IsInitialized && frames < 600)
                {
                    frames++;
                    yield return null;
                }
                Assert.That(manager.IsInitialized, Is.True);
                Assert.That(manager.IsOpen(UIPageId.Load), Is.True);
                Assert.That(manager.IsOpen(UIPageId.Main), Is.False);
            }
            finally
            {
                GameSessionContext.ResetSession();
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator GenericSkillIndicators_BindDedicatedRuntimeMaterials()
        {
            Task<IClientPresentationAssetLoader> loaderTask =
                ClientPresentationServices.GetLoaderAsync();
            while (!loaderTask.IsCompleted)
                yield return null;
            Assert.That(loaderTask.IsCompletedSuccessfully, Is.True);
            IClientPresentationAssetLoader loader = loaderTask.Result;

            IPresentationAssetLease<GameObject> direction = null;
            IPresentationAssetLease<GameObject> range = null;
            IPresentationAssetLease<GameObject> ground = null;
            GameObject root = null;
            try
            {
                Task<IPresentationAssetLease<GameObject>> directionTask =
                    loader.AcquirePrefabAsync(
                        "ui/indicator/direction",
                        CancellationToken.None);
                Task<IPresentationAssetLease<GameObject>> rangeTask =
                    loader.AcquirePrefabAsync(
                        "ui/indicator/range-circle",
                        CancellationToken.None);
                Task<IPresentationAssetLease<GameObject>> groundTask =
                    loader.AcquirePrefabAsync(
                        "ui/indicator/ground-target",
                        CancellationToken.None);
                while (!directionTask.IsCompleted ||
                       !rangeTask.IsCompleted ||
                       !groundTask.IsCompleted)
                    yield return null;
                Assert.That(directionTask.IsCompletedSuccessfully, Is.True);
                Assert.That(rangeTask.IsCompletedSuccessfully, Is.True);
                Assert.That(groundTask.IsCompletedSuccessfully, Is.True);
                direction = directionTask.Result;
                range = rangeTask.Result;
                ground = groundTask.Result;

                root = new GameObject("GenericIndicatorRuntimeMaterials");
                SkillIndicatorDriver driver =
                    root.AddComponent<SkillIndicatorDriver>();
                driver.Configure(
                    direction.Asset,
                    range.Asset,
                    ground.Asset);

                Renderer[] renderers =
                    root.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(4));
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Material material =
                        renderers[rendererIndex].sharedMaterial;
                    Assert.That(material, Is.Not.Null);
                    Material sourceMaterial = FindSourceMaterial(
                        direction.Asset,
                        range.Asset,
                        ground.Asset,
                        material.name);
                    Assert.That(
                        material.name,
                        Does.EndWith(" (Runtime)"));
                    Assert.That(
                        material.shader.name,
                        Is.EqualTo(
                            "FrameSyncMoba/SkillIndicatorUnlit"));
                    Assert.That(material.shader.isSupported, Is.True);
                    Assert.That(material.mainTexture, Is.Not.Null);
                    Assert.That(material.color.b,
                        Is.GreaterThan(material.color.r));
                    Assert.That(
                        material.hideFlags,
                        Is.EqualTo(HideFlags.HideAndDontSave));
                    Assert.That(
                        material,
                        Is.Not.SameAs(sourceMaterial));
                    Assert.That(
                        material.shader,
                        Is.SameAs(sourceMaterial.shader),
                        "Runtime binding must inherit the Shader object " +
                        "resolved by the loaded Addressables material, not " +
                        "perform a global name lookup.");
                    Assert.That(
                        material.mainTexture,
                        Is.SameAs(sourceMaterial.mainTexture));
                    Assert.That(
                        material.color,
                        Is.EqualTo(sourceMaterial.color));
                }

                driver.Show(
                    AimKind.Direction,
                    (fp)3,
                    fp2.zero,
                    new fp2(fp.one, fp.zero));
                AssertGenericIndicatorFrameIsBlueNotMagenta();

                driver.ForceClear();
                yield return null;
                Assert.That(
                    root.GetComponentsInChildren<Renderer>(true),
                    Is.Empty);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
                direction?.Dispose();
                range?.Dispose();
                ground?.Dispose();
            }
        }

        private static T LoadAsset<T>(
            string guid)
            where T : Object
        {
            string path =
                UnityEditor.AssetDatabase.GUIDToAssetPath(
                    guid);
            T asset =
                UnityEditor.AssetDatabase
                    .LoadAssetAtPath<T>(path);
            Assert.NotNull(
                asset,
                $"Project asset {guid} must exist.");
            return asset;
        }

        private static void SetReference(
            Object target,
            string field,
            Object value)
        {
            FieldInfo fieldInfo = target.GetType().GetField(
                field,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(fieldInfo, $"Missing serialized field {field}.");
            fieldInfo.SetValue(target, value);
        }

        private static void ConfigureLoadingTestPages(UIManager manager)
        {
            var serialized = new SerializedObject(manager);
            SerializedProperty pages = serialized.FindProperty("pages");
            pages.arraySize = 2;
            ConfigurePage(
                pages.GetArrayElementAtIndex(0),
                UIPageId.Main,
                "ui/page/main",
                true);
            ConfigurePage(
                pages.GetArrayElementAtIndex(1),
                UIPageId.Load,
                "ui/page/load",
                false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigurePage(
            SerializedProperty page,
            UIPageId id,
            string address,
            bool openOnStart)
        {
            page.FindPropertyRelative("PageId").intValue = (int)id;
            page.FindPropertyRelative("Prefab").objectReferenceValue = null;
            page.FindPropertyRelative("Address").stringValue = address;
            page.FindPropertyRelative("Layer").intValue =
                (int)UIPageLayer.Main;
            page.FindPropertyRelative("Preload").boolValue = false;
            page.FindPropertyRelative("OpenOnStart").boolValue = openOnStart;
        }

        private static Material FindSourceMaterial(
            GameObject direction,
            GameObject range,
            GameObject ground,
            string runtimeName)
        {
            string sourceName = runtimeName.Substring(
                0,
                runtimeName.Length - " (Runtime)".Length);
            GameObject[] prefabs = { direction, range, ground };
            for (int prefabIndex = 0;
                 prefabIndex < prefabs.Length;
                 prefabIndex++)
            {
                Renderer[] renderers = prefabs[prefabIndex]
                    .GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0;
                     rendererIndex < renderers.Length;
                     rendererIndex++)
                {
                    Material material = renderers[rendererIndex]
                        .sharedMaterial;
                    if (material != null && material.name == sourceName)
                        return material;
                }
            }
            Assert.Fail($"Missing source material '{sourceName}'.");
            return null;
        }

        private static void AssertGenericIndicatorFrameIsBlueNotMagenta()
        {
            var cameraObject = new GameObject("IndicatorRenderCamera");
            var target = new RenderTexture(256, 256, 24);
            var readback = new Texture2D(
                256,
                256,
                TextureFormat.RGBA32,
                false);
            Camera camera = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera = cameraObject.AddComponent<Camera>();
                camera.transform.SetPositionAndRotation(
                    new Vector3(0f, 10f, 0f),
                    Quaternion.Euler(90f, 0f, 0f));
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                readback.ReadPixels(
                    new Rect(0f, 0f, 256f, 256f),
                    0,
                    0);
                readback.Apply();
                Color32[] pixels = readback.GetPixels32();
                int bluePixels = 0;
                int magentaPixels = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    if (pixel.b > 50 &&
                        pixel.b > pixel.r + 30 &&
                        pixel.g > pixel.r)
                        bluePixels++;
                    if (pixel.r > 180 &&
                        pixel.b > 180 &&
                        pixel.g < 100)
                        magentaPixels++;
                }
                Assert.That(
                    bluePixels,
                    Is.GreaterThan(50),
                    "The generic indicator must produce visible blue pixels.");
                Assert.That(
                    magentaPixels,
                    Is.Zero,
                    "The generic indicator must not render Unity's missing-shader magenta fallback.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null)
                    camera.targetTexture = null;
                Object.DestroyImmediate(readback);
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
