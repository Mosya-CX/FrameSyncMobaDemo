using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Runtime (Game view) visualization of the CameraController XZ clamp
    /// rectangle. Debug-scene only: draws a colored border on the ground and
    /// an optional translucent fill. Never touches deterministic state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraBoundsVisualizer : MonoBehaviour
    {
        [SerializeField] private CameraController cameraController;
        [SerializeField] private Color borderColor =
            new Color(0f, 1f, 1f, 1f);
        [SerializeField] private Color fillColor =
            new Color(0f, 1f, 1f, 0.16f);
        [SerializeField] private float groundHeight = 0.06f;
        [SerializeField] private float borderWidth = 0.25f;
        [SerializeField] private bool showFill = true;

        private LineRenderer border;
        private Transform fillQuad;
        private Material fillMaterial;

        private void Awake()
        {
            if (cameraController == null)
            {
                cameraController =
                    GetComponentInParent<
                        CameraController>();
            }

            border = gameObject.AddComponent<LineRenderer>();
            Shader lineShader =
                Shader.Find("Sprites/Default");
            if (lineShader != null)
            {
                border.material =
                    new Material(lineShader);
                border.material.color = borderColor;
            }
            border.positionCount = 0;
            border.startWidth = borderWidth;
            border.endWidth = borderWidth;
            border.loop = false;
            border.useWorldSpace = true;

            if (showFill)
            {
                var fillGo = new GameObject(
                    "CameraBoundsFill");
                fillGo.transform.SetParent(
                    transform,
                    false);
                var filter = fillGo.AddComponent<MeshFilter>();
                filter.sharedMesh =
                    Resources.GetBuiltinResource<Mesh>(
                        "Quad.fbx");
                var renderer =
                    fillGo.AddComponent<MeshRenderer>();
                Shader fillShader =
                    Shader.Find("Sprites/Default");
                if (fillShader != null)
                {
                    fillMaterial =
                        new Material(fillShader);
                    fillMaterial.color = fillColor;
                    renderer.sharedMaterial =
                        fillMaterial;
                }
                fillQuad = fillGo.transform;
            }
        }

        private void LateUpdate()
        {
            bool visible =
                cameraController != null &&
                (cameraController.ClampEnabled ||
                 cameraController.MapFitEnabled);
            if (border != null)
            {
                border.enabled = visible;
            }
            if (fillQuad != null)
            {
                fillQuad.gameObject.SetActive(
                    visible);
            }
            if (!visible)
            {
                return;
            }

            Vector2 min =
                cameraController.CurrentClampMin;
            Vector2 max =
                cameraController.CurrentClampMax;
            if (max.x <= min.x ||
                max.y <= min.y)
            {
                return;
            }
            float centerX =
                (min.x + max.x) * 0.5f;
            float centerZ =
                (min.y + max.y) * 0.5f;
            float halfX =
                (max.x - min.x) * 0.5f;
            float halfZ =
                (max.y - min.y) * 0.5f;
            Vector3 c = new Vector3(
                centerX,
                groundHeight,
                centerZ);

            border.positionCount = 5;
            border.SetPosition(
                0,
                c + new Vector3(
                    -halfX, 0f, -halfZ));
            border.SetPosition(
                1,
                c + new Vector3(
                    halfX, 0f, -halfZ));
            border.SetPosition(
                2,
                c + new Vector3(
                    halfX, 0f, halfZ));
            border.SetPosition(
                3,
                c + new Vector3(
                    -halfX, 0f, halfZ));
            border.SetPosition(
                4,
                c + new Vector3(
                    -halfX, 0f, -halfZ));

            if (fillQuad != null)
            {
                fillQuad.position = c;
                fillQuad.localScale = new Vector3(
                    max.x - min.x,
                    1f,
                    max.y - min.y);
            }
        }
    }
}
