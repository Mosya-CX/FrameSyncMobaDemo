using System;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.Unit.Editor
{
    [CustomEditor(typeof(StatHandler))]
    public sealed class StatHandlerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var handler = (StatHandler)target;
            if (!Application.isPlaying ||
                handler.Owner == null ||
                !handler.OwnerUid.IsValid())
            {
                EditorGUILayout.HelpBox(
                    "Final deterministic stats are available after this Unit is initialized in Play Mode.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Runtime Deterministic Stats",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "UnitUid",
                handler.OwnerUid.ToString());
            EditorGUILayout.LabelField(
                "Current Health",
                handler.CurrentHealth.ToString());
            EditorGUILayout.LabelField(
                "Current Resource",
                handler.CurrentCastResource.ToString());

            Array values = Enum.GetValues(typeof(StatId));
            for (int i = 0; i < values.Length; i++)
            {
                var statId = (StatId)values.GetValue(i);
                EditorGUILayout.LabelField(
                    statId.ToString(),
                    handler.GetStat(statId).ToString());
            }

            if (GUILayout.Button("Refresh Runtime Values"))
                Repaint();
        }
    }
}
