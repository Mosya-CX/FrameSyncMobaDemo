using UnityEngine;
using UnityEngine.UI;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Game-view visualization of the camera edge-pan activation bands
    /// (CameraController.edgeSize): translucent strips along the screen
    /// edges where the mouse triggers panning. Debug-scene only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraEdgePanVisualizer :
        MonoBehaviour
    {
        [SerializeField] private CameraController cameraController;
        [SerializeField] private Color edgeColor =
            new Color(1f, 0.8f, 0f, 0.14f);

        private RectTransform left;
        private RectTransform right;
        private RectTransform top;
        private RectTransform bottom;

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController =
                    GetComponentInParent<
                        CameraController>();
            }

            var canvasGo = new GameObject(
                "CameraEdgePanCanvas");
            canvasGo.transform.SetParent(
                transform,
                false);
            var canvas =
                canvasGo.AddComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>()
                .uiScaleMode = CanvasScaler.ScaleMode
                    .ConstantPixelSize;

            left = CreateEdge("EdgeLeft");
            right = CreateEdge("EdgeRight");
            top = CreateEdge("EdgeTop");
            bottom = CreateEdge("EdgeBottom");
        }

        private void LateUpdate()
        {
            if (cameraController == null)
            {
                return;
            }
            float size =
                cameraController.EdgeSize;
            LayoutEdge(
                left,
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, 0.5f),
                new Vector2(size, 0f));
            LayoutEdge(
                right,
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.5f),
                new Vector2(size, 0f));
            LayoutEdge(
                top,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, size));
            LayoutEdge(
                bottom,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, size));
        }

        private RectTransform CreateEdge(
            string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(
                transform.Find(
                    "CameraEdgePanCanvas"),
                false);
            var image = go.AddComponent<Image>();
            image.color = edgeColor;
            return (RectTransform)go.transform;
        }

        private static void LayoutEdge(
            RectTransform edge,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 sizeDelta)
        {
            if (edge == null)
            {
                return;
            }
            edge.anchorMin = anchorMin;
            edge.anchorMax = anchorMax;
            edge.pivot = pivot;
            edge.offsetMin = Vector2.zero;
            edge.offsetMax = Vector2.zero;
            edge.sizeDelta = sizeDelta;
        }
    }
}
