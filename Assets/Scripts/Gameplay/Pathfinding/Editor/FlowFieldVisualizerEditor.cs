using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.Unit.Editor
{
    [CustomEditor(typeof(FlowFieldVisualizer))]
    public sealed class FlowFieldVisualizerEditor :
        UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Scene View",
                EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Top"))
                    SetView(
                        Quaternion.Euler(
                            90f,
                            0f,
                            0f),
                        60f);
                if (GUILayout.Button("Isometric"))
                    SetView(
                        Quaternion.Euler(
                            50f,
                            -45f,
                            0f),
                        70f);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Blue Base"))
                    SetView(
                        Quaternion.Euler(
                            35f,
                            45f,
                            0f),
                        75f);
                if (GUILayout.Button("Red Base"))
                    SetView(
                        Quaternion.Euler(
                            35f,
                            -135f,
                            0f),
                        75f);
            }
        }

        private static void SetView(
            Quaternion rotation,
            float size)
        {
            SceneView view =
                SceneView.lastActiveSceneView;
            if (view == null)
                return;
            view.in2DMode = false;
            view.LookAt(
                Vector3.zero,
                rotation,
                size,
                true);
            view.Repaint();
        }
    }
}
