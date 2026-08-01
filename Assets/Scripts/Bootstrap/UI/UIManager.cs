using System;
using System.Collections.Generic;
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
        private bool initialized;

        public bool IsInitialized => initialized;

        private void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (initialized)
                return;
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
                    panel.Open();
                else
                    panel.Close();
            }
        }

        public UIPanel OpenPage(UIPageId pageId)
        {
            Initialize();
            UIPanel panel = GetOrCreate(pageId);
            panel.Open();
            return panel;
        }

        public bool ClosePage(UIPageId pageId)
        {
            if (!instances.TryGetValue(
                    pageId,
                    out UIPanel panel))
                return false;
            panel.Close();
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
            return panel;
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

        private Transform GetLayer(UIPageLayer layer)
        {
            switch (layer)
            {
                case UIPageLayer.Popup:
                    return popupLayer;
                case UIPageLayer.Overlay:
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
