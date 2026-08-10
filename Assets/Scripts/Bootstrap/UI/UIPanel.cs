using System;
using FrameSyncMoba.LuaBridge;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIPage))]
    public sealed class UIPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private string luaModule;
        [SerializeField] private UIRef[] refs =
            Array.Empty<UIRef>();

        private LuaHost host;

        public UIPage Page { get; private set; }
        public bool IsOpen { get; private set; }
        public bool HasLuaHost => host != null;

        public event Action Opened;
        public event Action Closed;

        private void Awake()
        {
            Page = GetComponent<UIPage>();
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            SetCanvasState(true);
            if (IsOpen)
                return;
            IsOpen = true;
            Opened?.Invoke();
            host?.Show();
        }

        public void Close()
        {
            if (!gameObject.activeSelf && !IsOpen)
                return;
            SetCanvasState(false);
            IsOpen = false;
            Closed?.Invoke();
            host?.Hide();
            gameObject.SetActive(false);
        }

        public void Build(LuaManager luaManager)
        {
            var lists =
                GetComponentsInChildren<UIList>(true);
            for (int i = 0; i < lists.Length; i++)
                lists[i].SetManager(luaManager);
            if (string.IsNullOrEmpty(luaModule))
                return;
            host?.Dispose();
            host = null;
            host = luaManager.CreatePageHost(
                luaModule,
                refs);
        }

        public void Refresh()
        {
            if (!IsOpen)
                return;
            host?.Refresh();
        }

        public void RefreshLuaHost()
        {
            host?.Refresh();
        }

        public void DisposeLuaHost()
        {
            host?.Dispose();
            host = null;
        }

        private void OnDestroy()
        {
            DisposeLuaHost();
        }

        private void SetCanvasState(bool visible)
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
