using System;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    [CustomPropertyDrawer(typeof(DurationAuthoring))]
    public sealed class DurationAuthoringDrawer : PropertyDrawer
    {
        private const float PolicyWidth = 84f;
        private const float UnitWidth = 24f;
        private const float Gap = 4f;

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty milliseconds =
                property.FindPropertyRelative("milliseconds");
            SerializedProperty policy =
                property.FindPropertyRelative("roundingPolicy");
            SerializedProperty authored =
                property.FindPropertyRelative("authored");
            if (milliseconds == null ||
                policy == null ||
                authored == null)
            {
                EditorGUI.LabelField(
                    position,
                    label,
                    new GUIContent("Invalid DurationAuthoring"));
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            Rect content = EditorGUI.PrefixLabel(position, label);
            Rect millisecondsRect = new Rect(
                content.x,
                content.y,
                Math.Max(
                    0f,
                    content.width - PolicyWidth - UnitWidth - Gap * 2f),
                content.height);
            Rect unitRect = new Rect(
                millisecondsRect.xMax + Gap,
                content.y,
                UnitWidth,
                content.height);
            Rect policyRect = new Rect(
                unitRect.xMax + Gap,
                content.y,
                PolicyWidth,
                content.height);

            EditorGUI.BeginChangeCheck();
            int changed = EditorGUI.IntField(
                millisecondsRect,
                milliseconds.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                milliseconds.intValue = Math.Max(0, changed);
                authored.boolValue = true;
            }
            EditorGUI.LabelField(unitRect, "ms");
            EditorGUI.PropertyField(
                policyRect,
                policy,
                GUIContent.none);
            EditorGUI.EndProperty();
        }
    }
}
