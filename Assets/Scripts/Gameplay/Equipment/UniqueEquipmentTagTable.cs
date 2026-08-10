using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global configuration for equipment tags that enforce uniqueness.
    /// When two different equipment items share a tag listed here,
    /// they cannot coexist in the same inventory.
    /// (Equipment/Gold v12 2.10)
    /// </summary>
    [Serializable]
    public sealed class UniqueEquipmentTagTable
    {
        public EquipmentTagDefinition[] UniqueTags;

        private HashSet<EquipmentTagUid> _uniqueSet;

        public void Initialize()
        {
            _uniqueSet = new HashSet<EquipmentTagUid>();
            if (UniqueTags == null)
                return;
            for (int i = 0;
                 i < UniqueTags.Length;
                 i++)
            {
                EquipmentTagDefinition tag =
                    UniqueTags[i];
                if (tag != null &&
                    tag.Uid.IsValid)
                    _uniqueSet.Add(tag.Uid);
            }
        }

        public bool IsUnique(
            EquipmentTagUid tag)
        {
            if (_uniqueSet == null)
                return false;
            return _uniqueSet.Contains(tag);
        }

        public bool IsUnique(
            EquipmentTagDefinition tag)
        {
            return tag != null &&
                IsUnique(tag.Uid);
        }

        public EquipmentTagUid FindFirstConflict(
            EquipmentTagDefinition[] proposedTags,
            EquipmentHandler existingHandler)
        {
            if (proposedTags == null ||
                existingHandler == null)
                return default;

            for (int i = 0;
                 i < proposedTags.Length;
                 i++)
            {
                EquipmentTagDefinition tag =
                    proposedTags[i];
                if (tag == null ||
                    !IsUnique(tag))
                    continue;
                if (existingHandler.HasTag(tag))
                    return tag.Uid;
            }
            return default;
        }
    }
}
