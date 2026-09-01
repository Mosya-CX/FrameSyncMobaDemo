using System;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Static mapping of VfxDefId to a prefab used by VfxManager.
    /// Authored as a ScriptableObject; never touches Gameplay state.
    /// </summary>
    [CreateAssetMenu(
        menuName = "FrameSyncMoba/VFX Library")]
    public sealed class VfxLibrary : ScriptableObject
    {
        [SerializeField]
        private VfxPrefabEntry[] _entries =
            System.Array.Empty<VfxPrefabEntry>();

        [System.Serializable]
        public struct VfxPrefabEntry
        {
            public int VfxDefId;
            [HideInInspector]
            public GameObject Prefab;
            public string Address;
            [Min(0)]
            public int OwnerHeroConfigId;
        }

        public int Count => _entries?.Length ?? 0;

        public VfxPrefabEntry GetEntry(int index)
        {
            if (_entries == null)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _entries[index];
        }

        public string GetAddress(int vfxDefId)
        {
            if (_entries == null)
            {
                return null;
            }
            for (int i = 0;
                 i < _entries.Length;
                 i++)
            {
                if (_entries[i].VfxDefId ==
                    vfxDefId)
                {
                    return _entries[i].Address ?? string.Empty;
                }
            }
            return string.Empty;
        }
    }
}
