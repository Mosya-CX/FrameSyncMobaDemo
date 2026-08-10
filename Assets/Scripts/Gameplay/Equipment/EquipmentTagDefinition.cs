using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring ScriptableObject for an equipment tag (design v12 2.9).
    /// The Uid is assigned automatically from the asset GUID when created in
    /// the editor; it is never hand-edited.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EquipmentTag",
        menuName = "MOBA/Equipment Tag")]
    public sealed class EquipmentTagDefinition :
        ScriptableObject
    {
        [SerializeField, HideInInspector]
        private EquipmentTagUid uid;

        public string Name;

        [TextArea]
        public string Description;

        public EquipmentTagUid Uid => uid;

        /// <summary>
        /// Test/runtime factory with an explicit deterministic Uid.
        /// </summary>
        public static EquipmentTagDefinition Create(
            string name,
            int uidValue)
        {
            var tag =
                CreateInstance<EquipmentTagDefinition>();
            tag.name = name;
            tag.uid = new EquipmentTagUid(uidValue);
            return tag;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (uid.IsValid)
                return;
            string path =
                UnityEditor.AssetDatabase
                    .GetAssetPath(this);
            string guid =
                UnityEditor.AssetDatabase
                    .AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                uid = new EquipmentTagUid(
                    StableHash32(guid));
        }

        private void Reset()
        {
            uid = default;
            OnValidate();
        }

        private static int StableHash32(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }
#endif
    }
}
