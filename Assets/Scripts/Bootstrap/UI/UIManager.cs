using System;
using System.Collections.Generic;
using FrameSyncMoba.LuaBridge;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        [Serializable]
        private struct PageRegistration
        {
            public UIPageId PageId;
            public GameObject Prefab;
            public UIPageLayer Layer;
            public bool Preload;
            public bool OpenOnStart;
        }

        [SerializeField] private Transform pageLayer;
        [SerializeField] private Transform popupLayer;
        [SerializeField] private Transform overlayLayer;
        [SerializeField] private PageRegistration[] pages =
            Array.Empty<PageRegistration>();

        private readonly Dictionary<UIPageId, UIPanel> instances =
            new Dictionary<UIPageId, UIPanel>();
        private LuaManager luaManager;
        private bool initialized;
        private UIPageId _currentMainPage =
            UIPageId.None;
        private UIPageId _currentOverlay =
            UIPageId.None;

        public bool IsInitialized => initialized;
        public LuaManager Lua => luaManager;
        public static UIManager Instance { get; private set; }

        /// <summary>
        /// HUD owned-equipment focus event (UI design v9.1 2.5): slot +
        /// equipment id, forwarded to the Shop overlay.
        /// </summary>
        public event Action<int, int> ShopOwnedEquipmentFocused;

        private void Awake()
        {
            Instance = this;
            if (luaManager == null)
                luaManager =
                    LuaManager.CreateDefault();
            Initialize();
        }

        private void Update()
        {
            luaManager?.Tick();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this))
                Instance = null;
            if (ReferenceEquals(
                    GameFlowLuaBridge.UiManager,
                    this))
                GameFlowLuaBridge.UiManager = null;
            foreach (UIPanel panel in
                     instances.Values)
                panel.DisposeLuaHost();
            instances.Clear();
            luaManager?.Dispose();
            luaManager = null;
        }

        public void Initialize()
        {
            if (initialized)
                return;
            // GameBootstrap can reach Initialize() before this component's
            // Awake runs (component Awake order is not guaranteed). Create the
            // manager lazily so panel Build never sees a null LuaManager.
            if (luaManager == null)
                luaManager =
                    LuaManager.CreateDefault();
            ValidateRegistrations();
            EnsureLayers();
            initialized = true;

            for (int i = 0; i < pages.Length; i++)
            {
                if (!pages[i].Preload &&
                    !pages[i].OpenOnStart)
                    continue;
                UIPanel panel =
                    GetOrCreate(pages[i].PageId);
                if (pages[i].OpenOnStart)
                {
                    panel.Open();
                    // Register the opened main page so CloseAll/ShowPage can
                    // close it later (OpenOnStart pages are not tracked by
                    // ShowPage's _currentMainPage otherwise).
                    if (pages[i].Layer ==
                            UIPageLayer.Main &&
                        _currentMainPage ==
                        UIPageId.None)
                        _currentMainPage =
                            pages[i].PageId;
                }
                else
                    panel.Close();
            }
        }

        public void ShowPage(UIPageId pageId)
        {
            Initialize();
            PageRegistration registration =
                GetRegistration(pageId);
            if (registration.Layer !=
                UIPageLayer.Main)
                throw new InvalidOperationException(
                    $"ShowPage requires a Main-layer page, got {pageId}.");

            if (_currentOverlay != UIPageId.None)
            {
                HideOverlay(_currentOverlay);
            }
            if (_currentMainPage == pageId)
            {
                Refresh(pageId);
                return;
            }
            if (_currentMainPage != UIPageId.None)
            {
                if (instances.TryGetValue(
                        _currentMainPage,
                        out UIPanel previous))
                    previous.Close();
            }
            UIPanel panel = GetOrCreate(pageId);
            panel.Open();
            _currentMainPage = pageId;
            panel.Refresh();
        }

        public void ShowOverlay(UIPageId pageId)
        {
            Initialize();
            PageRegistration registration =
                GetRegistration(pageId);
            if (registration.Layer !=
                UIPageLayer.BattleOverlay)
                throw new InvalidOperationException(
                    $"ShowOverlay requires a BattleOverlay page, got {pageId}.");
            if (_currentMainPage != UIPageId.HUD)
                throw new InvalidOperationException(
                    "ShowOverlay requires the current main page to be HUD.");
            if (_currentOverlay != UIPageId.None &&
                _currentOverlay != pageId)
            {
                HideOverlay(_currentOverlay);
            }
            UIPanel panel = GetOrCreate(pageId);
            panel.Open();
            _currentOverlay = pageId;
            panel.Refresh();
        }

        public void HideOverlay(UIPageId pageId)
        {
            if (_currentOverlay != pageId)
                return;
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.Close();
            _currentOverlay = UIPageId.None;
        }

        public bool IsOpen(UIPageId pageId)
        {
            return instances.TryGetValue(
                    pageId,
                    out UIPanel panel) &&
                panel.IsOpen;
        }

        public void CloseAll()
        {
            if (_currentOverlay != UIPageId.None)
                HideOverlay(_currentOverlay);
            if (_currentMainPage != UIPageId.None)
            {
                if (instances.TryGetValue(
                        _currentMainPage,
                        out UIPanel panel))
                    panel.Close();
                _currentMainPage = UIPageId.None;
            }
        }

        public void FocusShopOwnedEquipment(
            int slot,
            int equipmentId)
        {
            ShopOwnedEquipmentFocused?.Invoke(
                slot,
                equipmentId);
        }

        public UIPanel OpenPage(UIPageId pageId)
        {
            Initialize();
            if (GetRegistration(pageId).Layer ==
                UIPageLayer.BattleOverlay)
            {
                ShowOverlay(pageId);
            }
            else
            {
                ShowPage(pageId);
            }
            return instances.TryGetValue(
                pageId,
                out UIPanel panel)
                ? panel
                : null;
        }

        public bool ClosePage(UIPageId pageId)
        {
            if (GetRegistration(pageId).Layer ==
                UIPageLayer.BattleOverlay)
            {
                if (_currentOverlay != pageId)
                    return false;
                HideOverlay(pageId);
                return true;
            }
            if (_currentMainPage != pageId)
                return false;
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.Close();
            _currentMainPage = UIPageId.None;
            return true;
        }

        public bool TryGetPage(
            UIPageId pageId,
            out UIPanel panel)
        {
            Initialize();
            if (instances.TryGetValue(pageId, out panel))
                return true;
            if (!HasRegistration(pageId))
                return false;
            panel = GetOrCreate(pageId);
            return true;
        }

        public T GetPageComponent<T>(
            UIPageId pageId)
            where T : Component
        {
            return TryGetPage(pageId, out UIPanel panel)
                ? panel.GetComponentInChildren<T>(true)
                : null;
        }

        private UIPanel GetOrCreate(UIPageId pageId)
        {
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel existing))
                return existing;

            int index = FindRegistration(pageId);
            if (index < 0)
                throw new InvalidOperationException(
                    $"No UI prefab is registered for {pageId}.");
            PageRegistration registration = pages[index];
            Transform parent =
                GetLayer(registration.Layer);
            GameObject instance =
                Instantiate(
                    registration.Prefab,
                    parent,
                    false);
            if (instance.scene != gameObject.scene)
                UnityEngine.SceneManagement
                    .SceneManager.MoveGameObjectToScene(
                        instance,
                        gameObject.scene);
            instance.name = registration.Prefab.name;
            UIPage page = instance.GetComponent<UIPage>();
            UIPanel panel = instance.GetComponent<UIPanel>();
            if (page == null || panel == null)
                throw new InvalidOperationException(
                    $"UI prefab {registration.Prefab.name} requires UIPage and UIPanel.");
            if (page.PageId != pageId)
                throw new InvalidOperationException(
                    $"UI prefab {registration.Prefab.name} declares {page.PageId} but is registered as {pageId}.");
            instances.Add(pageId, panel);
            panel.Build(luaManager);
            return panel;
        }

        public void Refresh(UIPageId pageId)
        {
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.Refresh();
        }

        public void RefreshLuaHost(UIPageId pageId)
        {
            if (instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                panel.RefreshLuaHost();
        }

        private void ValidateRegistrations()
        {
            var ids = new HashSet<UIPageId>();
            for (int i = 0; i < pages.Length; i++)
            {
                PageRegistration entry = pages[i];
                if (entry.PageId == UIPageId.None)
                    throw new InvalidOperationException(
                        $"UI registration {i} has no PageId.");
                if (entry.Prefab == null)
                    throw new InvalidOperationException(
                        $"UI registration {entry.PageId} has no prefab.");
                if (!ids.Add(entry.PageId))
                    throw new InvalidOperationException(
                        $"Duplicate UI registration {entry.PageId}.");
            }
        }

        private bool HasRegistration(UIPageId pageId)
        {
            return FindRegistration(pageId) >= 0;
        }

        private int FindRegistration(UIPageId pageId)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i].PageId == pageId)
                    return i;
            }
            return -1;
        }

        private PageRegistration GetRegistration(
            UIPageId pageId)
        {
            int index = FindRegistration(pageId);
            if (index < 0)
                throw new InvalidOperationException(
                    $"No UI prefab is registered for {pageId}.");
            return pages[index];
        }

        private Transform GetLayer(UIPageLayer layer)
        {
            switch (layer)
            {
                case UIPageLayer.BattleOverlay:
                    return overlayLayer;
                default:
                    return pageLayer;
            }
        }

        private void EnsureLayers()
        {
            pageLayer = EnsureLayer(
                pageLayer,
                "Pages");
            popupLayer = EnsureLayer(
                popupLayer,
                "Popups");
            overlayLayer = EnsureLayer(
                overlayLayer,
                "Overlay");
        }

        private Transform EnsureLayer(
            Transform current,
            string layerName)
        {
            if (current != null)
                return current;
            Transform existing = transform.Find(layerName);
            if (existing != null)
                return existing;
            var layerObject =
                new GameObject(
                    layerName,
                    typeof(RectTransform));
            layerObject.transform.SetParent(
                transform,
                false);
            var rect =
                (RectTransform)layerObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
