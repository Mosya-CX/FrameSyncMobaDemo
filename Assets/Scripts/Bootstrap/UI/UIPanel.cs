using System;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIPage))]
    public sealed class UIPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        public UIPage Page { get; private set; }
        public bool IsOpen { get; private set; }

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
        }

        public void Close()
        {
            if (!gameObject.activeSelf && !IsOpen)
                return;
            SetCanvasState(false);
            IsOpen = false;
            Closed?.Invoke();
            gameObject.SetActive(false);
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
