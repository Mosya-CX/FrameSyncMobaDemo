using System;
using System.Threading;
using FrameSyncMoba.Bootstrap;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.PlayerInput;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FrameSyncMoba.ClientContent
{
    /// <summary>
    /// One client-only, cross-scene Addressables owner. It starts before the
    /// first scene, survives Lobby -> GameScene, and rebinds reconstructible
    /// Unit/Projectile views whenever a GameBootstrap becomes available.
    /// </summary>
    public sealed class ClientContentRuntimeHost : MonoBehaviour
    {
        private AddressablesClientContentService contentService;
        private ClientUnitViewBinder unitViewBinder;
        private ClientProjectileViewBinder projectileViewBinder;
        private CancellationTokenSource lifetimeCancellation;
        private GameBootstrap boundBootstrap;
        private IPresentationAssetLease<GameObject> mapViewLease;
        private GameObject mapViewInstance;
        private readonly System.Collections.Generic.Dictionary<
            string,
            IPresentationAssetLease<GameObject>>
            projectileViewLeases =
                new System.Collections.Generic.Dictionary<
                    string,
                    IPresentationAssetLease<GameObject>>();
        private int bindGeneration;
        private readonly System.Collections.Generic.List<
            IPresentationAssetLease<GameObject>> indicatorLeases =
                new System.Collections.Generic.List<
                    IPresentationAssetLease<GameObject>>();

        private async void Awake()
        {
            DontDestroyOnLoad(gameObject);
            lifetimeCancellation = new CancellationTokenSource();
            contentService = new AddressablesClientContentService();
            try
            {
                await contentService.InitializeAsync(
                    lifetimeCancellation.Token);
                if (lifetimeCancellation.IsCancellationRequested)
                    return;
                ClientPresentationServices.Register(contentService);
                SceneManager.sceneLoaded += OnSceneLoaded;
                BindCurrentScene();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ClientContent] Initialization failed: {exception}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            BindCurrentScene();
        }

        private void BindCurrentScene()
        {
            GameBootstrap bootstrap =
                UnityEngine.Object.FindObjectOfType<GameBootstrap>();
            if (!ReferenceEquals(boundBootstrap, bootstrap))
            {
                bindGeneration++;
                unitViewBinder?.Dispose();
                projectileViewBinder?.Dispose();
                if (mapViewInstance != null)
                    Destroy(mapViewInstance);
                mapViewInstance = null;
                mapViewLease?.Dispose();
                mapViewLease = null;
                foreach (System.Collections.Generic.KeyValuePair<string,
                         IPresentationAssetLease<GameObject>> pair
                         in projectileViewLeases)
                {
                    pair.Value.Dispose();
                }
                projectileViewLeases.Clear();
                ReleaseIndicatorLeases();
                unitViewBinder = null;
                projectileViewBinder = null;
                boundBootstrap = bootstrap;

                if (bootstrap != null && bootstrap.UnitWorld != null)
                {
                    unitViewBinder = new ClientUnitViewBinder(
                        bootstrap.UnitWorld,
                        bootstrap.UnitWorld.GlobalPrefabTable,
                        contentService);
                    if (bootstrap.UnitWorld.ProjectileWorld != null)
                    {
                        projectileViewBinder = new ClientProjectileViewBinder(
                            bootstrap.UnitWorld.ProjectileWorld,
                            bootstrap.UnitWorld.GlobalPrefabTable,
                            contentService);
                    }
                    _ = BindMapViewAsync(bootstrap, bindGeneration);
                    _ = PreloadProjectileViewsAsync(
                        bindGeneration);
                }
                _ = BindSkillIndicatorsAsync(bindGeneration);
            }

            VfxManager[] vfxManagers =
                UnityEngine.Object.FindObjectsOfType<VfxManager>(true);
            for (int i = 0; i < vfxManagers.Length; i++)
                vfxManagers[i].SetAssetLoader(contentService);
            AudioManager[] audioManagers =
                UnityEngine.Object.FindObjectsOfType<AudioManager>(true);
            for (int i = 0; i < audioManagers.Length; i++)
                audioManagers[i].SetAssetLoader(contentService);
        }

        private async System.Threading.Tasks.Task BindSkillIndicatorsAsync(
            int generation)
        {
            SkillIndicatorDriver[] drivers =
                UnityEngine.Object.FindObjectsOfType<
                    SkillIndicatorDriver>(true);
            if (drivers.Length == 0)
                return;
            IPresentationAssetLease<GameObject> direction = null;
            IPresentationAssetLease<GameObject> range = null;
            IPresentationAssetLease<GameObject> ground = null;
            try
            {
                direction = await contentService.AcquirePrefabAsync(
                    "ui/indicator/direction",
                    lifetimeCancellation.Token);
                range = await contentService.AcquirePrefabAsync(
                    "ui/indicator/range-circle",
                    lifetimeCancellation.Token);
                ground = await contentService.AcquirePrefabAsync(
                    "ui/indicator/ground-target",
                    lifetimeCancellation.Token);
                if (generation != bindGeneration)
                    return;
                indicatorLeases.Add(direction);
                indicatorLeases.Add(range);
                indicatorLeases.Add(ground);
                direction = null;
                range = null;
                ground = null;
                for (int i = 0; i < drivers.Length; i++)
                {
                    if (drivers[i] != null)
                    {
                        drivers[i].Configure(
                            indicatorLeases[0].Asset,
                            indicatorLeases[1].Asset,
                            indicatorLeases[2].Asset);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ClientContent] Skill indicator load failed: {exception}");
            }
            finally
            {
                direction?.Dispose();
                range?.Dispose();
                ground?.Dispose();
            }
        }

        private void ReleaseIndicatorLeases()
        {
            for (int i = 0; i < indicatorLeases.Count; i++)
                indicatorLeases[i].Dispose();
            indicatorLeases.Clear();
        }

        private async System.Threading.Tasks.Task BindMapViewAsync(
            GameBootstrap bootstrap,
            int generation)
        {
            if (!bootstrap.UnitWorld.GlobalPrefabTable.TryGetEntry(
                    PrefabKind.Misc,
                    5001,
                    out PrefabEntry entry) ||
                string.IsNullOrWhiteSpace(entry.ClientViewAddress))
                return;
            try
            {
                IPresentationAssetLease<GameObject> lease =
                    await contentService.AcquirePrefabAsync(
                        entry.ClientViewAddress,
                        lifetimeCancellation.Token);
                if (generation != bindGeneration ||
                    bootstrap == null ||
                    lifetimeCancellation.IsCancellationRequested)
                {
                    lease.Dispose();
                    return;
                }
                mapViewLease = lease;
                Transform mapViewAnchor = ResolveMapViewAnchor();
                mapViewInstance = mapViewAnchor != null
                    ? Instantiate(
                        lease.Asset,
                        mapViewAnchor,
                        false)
                    : Instantiate(lease.Asset);
                mapViewInstance.name = "ClientMapView";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ClientContent] Map view load failed: {exception}");
            }
        }

        /// <summary>
        /// Warms every projectile view address when GameScene binds so the
        /// projectile bundle is resident before the first missile spawns.
        /// The binders still hold their own leases; keeping one here avoids
        /// ever racing an async reload for short-lived attack missiles.
        /// </summary>
        private async System.Threading.Tasks.Task
            PreloadProjectileViewsAsync(
                int generation)
        {
            GlobalPrefabTable table =
                boundBootstrap?.UnitWorld?.GlobalPrefabTable;
            if (table == null)
            {
                return;
            }
            PrefabGroup projectileGroup = null;
            for (int i = 0;
                 i < table.PrefabGroups.Count;
                 i++)
            {
                if (table.PrefabGroups[i].Kind ==
                    PrefabKind.Projectile)
                {
                    projectileGroup =
                        table.PrefabGroups[i];
                    break;
                }
            }
            if (projectileGroup == null)
            {
                return;
            }
            for (int i = 0;
                 i < projectileGroup.Entries.Count;
                 i++)
            {
                string address =
                    projectileGroup.Entries[i]
                        .ClientViewAddress;
                if (string.IsNullOrWhiteSpace(address) ||
                    projectileViewLeases.ContainsKey(
                        address))
                {
                    continue;
                }
                if (generation != bindGeneration ||
                    lifetimeCancellation
                        .IsCancellationRequested)
                {
                    return;
                }
                try
                {
                    IPresentationAssetLease<GameObject>
                        lease =
                            await contentService
                                .AcquirePrefabAsync(
                                    address,
                                    lifetimeCancellation
                                        .Token);
                    if (generation != bindGeneration ||
                        lifetimeCancellation
                            .IsCancellationRequested)
                    {
                        lease.Dispose();
                        return;
                    }
                    projectileViewLeases.Add(
                        address,
                        lease);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"[ClientContent] Projectile view preload " +
                        $"failed for '{address}': {exception}");
                }
            }
            Debug.Log(
                $"[ClientContent] Projectile views preloaded: " +
                $"{projectileViewLeases.Count}.");
        }

        /// <summary>
        /// The map view must sit at the deterministic world origin and must
        /// never follow the bootstrap root: GameScene's GameBootstrap object
        /// also carries the gameplay Camera, whose CameraController moves that
        /// root every LateUpdate. Anchor to the static deterministic map
        /// topology root when present; otherwise instantiate unparented so
        /// the prefab-authored world position is preserved.
        /// </summary>
        private Transform ResolveMapViewAnchor()
        {
            DeterministicMapSceneAuthoring authoring =
                FindObjectOfType<DeterministicMapSceneAuthoring>();
            return authoring != null
                ? authoring.transform
                : null;
        }

        private void LateUpdate()
        {
            if (boundBootstrap == null)
                BindCurrentScene();
            unitViewBinder?.Reconcile();
            projectileViewBinder?.Reconcile();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            lifetimeCancellation?.Cancel();
            unitViewBinder?.Dispose();
            projectileViewBinder?.Dispose();
            if (mapViewInstance != null)
                Destroy(mapViewInstance);
            mapViewLease?.Dispose();
            foreach (System.Collections.Generic.KeyValuePair<string,
                     IPresentationAssetLease<GameObject>> pair
                     in projectileViewLeases)
            {
                pair.Value.Dispose();
            }
            projectileViewLeases.Clear();
            ReleaseIndicatorLeases();
            ClientSpriteRegistry.Clear();
            if (contentService != null)
                ClientPresentationServices.Unregister(contentService);
            contentService?.Dispose();
            lifetimeCancellation?.Dispose();
        }
    }

    public static class ClientContentRuntimeInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Application.isBatchMode || GameSessionContext.IsDedicatedServer)
                return;
            if (UnityEngine.Object.FindObjectOfType<ClientContentRuntimeHost>() !=
                null)
                return;
            var host = new GameObject("ClientContentRuntime");
            host.AddComponent<ClientContentRuntimeHost>();
        }
    }
}
