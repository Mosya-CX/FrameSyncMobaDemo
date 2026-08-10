using UnityEngine;
using Sirenix.OdinInspector;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Client-local cartoon outline. At runtime it bakes the unit's own
    /// SkinnedMeshRenderer current pose into a separate inverted-hull outline
    /// renderer (hard outline, ally-green / enemy-red). No extra authoring
    /// mesh/prefab is required; only the outline material needs configuring.
    /// Presentation only, never touches deterministic state.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ClientUnitOutline : MonoBehaviour
    {
        [Tooltip("Renderer whose pose is outlined. Defaults to the first SkinnedMeshRenderer in children.")]
        [SerializeField] private SkinnedMeshRenderer targetRenderer;

        [Tooltip("Static mesh fallback for units without a SkinnedMeshRenderer.")]
        [SerializeField] private MeshRenderer targetMeshRenderer;

        [Tooltip("Outline material (MOBA/UnitOutlineRim).")]
        [SerializeField] private Material outlineMaterial;

        [SerializeField] private Color outlineColor =
            new Color(1f, 0f, 0f, 1f);

        [SerializeField] private float outlineWidth =
            0.05f;

        private GameObject outlineGo;
        private MeshRenderer outlineRenderer;
        private MeshFilter outlineFilter;
        private MeshFilter targetMeshFilter;
        private Material materialInstance;
        private Mesh bakedMesh;
        private bool highlighted;

        public Material OutlineMaterial
        {
            get => outlineMaterial;
            set => outlineMaterial = value;
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = width;
            if (materialInstance != null)
            {
                materialInstance.SetFloat(
                    "_OutlineWidth",
                    outlineWidth);
            }
        }

        /// <summary>
        /// Turns the outline on/off. Only meaningful on the local client.
        /// </summary>
        public void SetHighlighted(
            bool enabled,
            Color color)
        {
            highlighted = enabled;
            outlineColor = color;
            if (!enabled)
            {
                if (outlineGo != null)
                {
                    outlineGo.SetActive(false);
                }
                return;
            }
            EnsureCreated();
            if (outlineGo == null)
            {
                return;
            }
            outlineGo.SetActive(
                true);
            if (materialInstance != null)
            {
                materialInstance.SetColor(
                    "_OutlineColor",
                    outlineColor);
                materialInstance.SetFloat(
                    "_OutlineWidth",
                    outlineWidth);
            }
            BakeOnce();
        }

        public bool IsHighlighted => highlighted;

        private void EnsureCreated()
        {
            if (outlineGo != null)
            {
                return;
            }
            if (outlineMaterial == null)
            {
                return;
            }
            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponentInChildren<
                        SkinnedMeshRenderer>(true);
            }
            if (targetRenderer == null &&
                targetMeshRenderer == null)
            {
                targetMeshRenderer =
                    GetComponentInChildren<
                        MeshRenderer>(true);
                if (targetMeshRenderer != null)
                {
                    targetMeshFilter =
                        targetMeshRenderer
                            .GetComponent<
                                MeshFilter>();
                }
            }
            if (targetRenderer == null)
            {
                if (targetMeshRenderer == null)
                {
                    return;
                }
            }

            outlineGo =
                new GameObject(
                    "UnitOutline");
            if (!Application.isPlaying)
            {
                // Preview object must not be serialized into the prefab /
                // scene when used in prefab mode.
                outlineGo.hideFlags =
                    HideFlags.DontSave;
            }
            outlineGo.transform.SetParent(
                targetRenderer.transform,
                false);
            outlineFilter =
                outlineGo.AddComponent<
                    MeshFilter>();
            outlineRenderer =
                outlineGo.AddComponent<
                    MeshRenderer>();
            materialInstance =
                new Material(
                    outlineMaterial);
            outlineRenderer.sharedMaterial =
                materialInstance;
            if (Application.isPlaying)
            {
                bakedMesh =
                    new Mesh
                    {
                        name =
                            "UnitOutlineBake",
                    };
                outlineFilter.sharedMesh =
                    bakedMesh;
            }
            else
            {
                if (targetRenderer != null)
                {
                    outlineFilter.sharedMesh =
                        targetRenderer.sharedMesh;
                }
                else if (targetMeshRenderer != null)
                {
                    if (targetMeshFilter != null)
                    {
                        outlineFilter.sharedMesh =
                            targetMeshFilter
                                .sharedMesh;
                    }
                }
            }
            if (!Application.isPlaying)
            {
                outlineFilter.hideFlags =
                    HideFlags.DontSave;
                outlineRenderer.hideFlags =
                    HideFlags.DontSave;
                materialInstance.hideFlags =
                    HideFlags.DontSave;
                if (bakedMesh != null)
                {
                    bakedMesh.hideFlags =
                        HideFlags.DontSave;
                }
            }
            outlineGo.SetActive(false);
            outlineGo.transform.SetAsLastSibling();
        }

        private void BakeOnce()
        {
            if (targetRenderer == null)
            {
                // Static mesh path: nothing to re-bake per frame.
                if (targetMeshRenderer != null &&
                    targetMeshFilter != null &&
                    outlineFilter != null &&
                    outlineFilter.sharedMesh !=
                        targetMeshFilter.sharedMesh)
                {
                    outlineFilter.sharedMesh =
                        targetMeshFilter
                            .sharedMesh;
                }
                return;
            }
            if (bakedMesh == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                if (bakedMesh == null)
                {
                    bakedMesh =
                        new Mesh
                        {
                            name =
                                "UnitOutlineBake",
                        };
                    outlineFilter.sharedMesh =
                        bakedMesh;
                }
                targetRenderer.BakeMesh(
                    bakedMesh);
                // BakeMesh outputs vertices already scaled by the
                // renderer's world lossyScale, but the outline object is a
                // child of the renderer and inherits that scale again. Divide
                // back to model space so the final world size is correct.
                Vector3 lossyScale =
                    targetRenderer.transform
                        .lossyScale;
                if (Mathf.Abs(lossyScale.x) >
                        0.0001f &&
                    Mathf.Abs(lossyScale.y) >
                        0.0001f &&
                    Mathf.Abs(lossyScale.z) >
                        0.0001f)
                {
                    Vector3[] vertices =
                        bakedMesh.vertices;
                    for (int i = 0;
                         i < vertices.Length;
                         i++)
                    {
                        vertices[i] =
                            new Vector3(
                                vertices[i].x /
                                    lossyScale.x,
                                vertices[i].y /
                                    lossyScale.y,
                                vertices[i].z /
                                    lossyScale.z);
                    }
                    bakedMesh.vertices =
                        vertices;
                }
                bakedMesh.RecalculateNormals();
                bakedMesh.RecalculateBounds();
            }
            else
            {
                outlineFilter.sharedMesh =
                    targetRenderer.sharedMesh;
            }
        }

        private void LateUpdate()
        {
            if (!highlighted ||
                outlineGo == null ||
                targetRenderer == null)
            {
                return;
            }
            BakeOnce();
        }

        private void OnDestroy()
        {
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(materialInstance);
                }
                else
                {
                    DestroyImmediate(
                        materialInstance);
                }
                materialInstance = null;
            }
            if (bakedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(bakedMesh);
                }
                else
                {
                    DestroyImmediate(
                        bakedMesh);
                }
                bakedMesh = null;
            }
        }

        [Button(
            "Preview Outline",
            ButtonSizes.Medium)]
        private void PreviewOutline()
        {
            SetHighlighted(
                true,
                outlineColor);
        }

        [Button(
            "Stop Preview",
            ButtonSizes.Medium)]
        private void StopPreview()
        {
            SetHighlighted(
                false,
                outlineColor);
        }
    }
}
