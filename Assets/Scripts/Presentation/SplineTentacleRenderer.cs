using UnityEngine;
using UnityEngine.Splines;

namespace FrameSyncMoba.Presentation
{
    /// <summary>
    /// Builds a procedural tube mesh along a SplineContainer whose radius
    /// shrinks from the first knot to the last knot (a tentacle).
    /// Presentation-only; used by buff VFX prefabs.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    [DisallowMultipleComponent]
    public sealed class SplineTentacleRenderer :
        MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField, Min(4)] private int radialSegments = 10;
        [SerializeField, Min(4)] private int lengthSegments = 48;
        [SerializeField, Min(0.001f)]
        private float startRadius = 0.3f;
        [SerializeField, Min(0.001f)]
        private float endRadius = 0.03f;

        public void Rebuild()
        {
            SplineContainer container =
                GetComponent<SplineContainer>();
            if (container == null)
            {
                return;
            }
            Spline spline = container.Spline;
            if (spline == null || spline.Count < 2)
            {
                return;
            }

            MeshFilter meshFilter =
                GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter =
                    gameObject.AddComponent<
                        MeshFilter>();
            }
            MeshRenderer meshRenderer =
                GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer =
                    gameObject.AddComponent<
                        MeshRenderer>();
            }

            int rings = lengthSegments + 1;
            var vertices =
                new Vector3[rings * radialSegments];
            var normals =
                new Vector3[vertices.Length];
            var uvs =
                new Vector2[vertices.Length];
            var indices =
                new int[
                    lengthSegments *
                    radialSegments * 6];

            for (int i = 0;
                 i <= lengthSegments;
                 i++)
            {
                float t =
                    i / (float)lengthSegments;
                Vector3 position =
                    spline.EvaluatePosition(t);
                Vector3 tangent =
                    spline.EvaluateTangent(t);
                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent = Vector3.up;
                }
                tangent.Normalize();
                Vector3 reference =
                    Mathf.Abs(Vector3.Dot(
                        tangent,
                        Vector3.up)) > 0.99f
                        ? Vector3.right
                        : Vector3.up;
                Vector3 binormal =
                    Vector3.Cross(
                        tangent,
                        reference).normalized;
                Vector3 normal =
                    Vector3.Cross(
                        binormal,
                        tangent).normalized;
                float radius = Mathf.Lerp(
                    startRadius,
                    endRadius,
                    t);

                for (int j = 0;
                     j < radialSegments;
                     j++)
                {
                    float angle =
                        j / (float)radialSegments *
                        Mathf.PI * 2f;
                    Vector3 direction =
                        (normal * Mathf.Cos(angle) +
                         binormal * Mathf.Sin(angle))
                        .normalized;
                    int vertexIndex =
                        i * radialSegments + j;
                    vertices[vertexIndex] =
                        position + direction * radius;
                    normals[vertexIndex] =
                        direction;
                    uvs[vertexIndex] =
                        new Vector2(
                            j / (float)radialSegments,
                            t);
                }
            }

            int index = 0;
            for (int i = 0;
                 i < lengthSegments;
                 i++)
            {
                for (int j = 0;
                     j < radialSegments;
                     j++)
                {
                    int nextJ =
                        (j + 1) % radialSegments;
                    int a = i * radialSegments + j;
                    int b =
                        i * radialSegments + nextJ;
                    int c =
                        (i + 1) * radialSegments + j;
                    int d =
                        (i + 1) * radialSegments +
                        nextJ;
                    indices[index++] = a;
                    indices[index++] = c;
                    indices[index++] = b;
                    indices[index++] = b;
                    indices[index++] = c;
                    indices[index++] = d;
                }
            }

            var mesh = new Mesh
            {
                name = "SplineTentacle",
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            meshFilter.sharedMesh = mesh;
            meshRenderer.sharedMaterial =
                material;
        }

        private void Awake()
        {
            Rebuild();
        }
    }
}
