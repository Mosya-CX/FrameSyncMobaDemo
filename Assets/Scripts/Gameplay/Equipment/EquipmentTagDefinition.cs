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
        /// <summary>
        /// Editor-assembly bridge used by the authoring synchronizer. Keeping
        /// the UnityEditor API itself outside this runtime assembly preserves
        /// the deterministic module boundary.
        /// </summary>
        public void AssignUidForEditor(int uidValue)
        {
            if (uid.IsValid)
                return;
            uid = new EquipmentTagUid(uidValue);
        }
#endif
    }
}
