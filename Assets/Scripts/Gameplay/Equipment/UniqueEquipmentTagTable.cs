using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global configuration for equipment tags that enforce uniqueness.
    /// When two different equipment items share a tag listed here,
    /// they cannot coexist in the same inventory.
    /// (Equipment/Gold v12 §2.10)
    /// </summary>
    [Serializable]
    public sealed class UniqueEquipmentTagTable
    {
        public string[] UniqueTags;

        private HashSet<string> _uniqueSet;

        public void Initialize()
        {
            _uniqueSet = new HashSet<string>();
            if (UniqueTags != null)
            {
                for (int i = 0; i < UniqueTags.Length; i++)
                {
                    if (!string.IsNullOrEmpty(UniqueTags[i]))
                        _uniqueSet.Add(UniqueTags[i]);
                }
            }
        }

        /// <summary>
        /// Returns true if this tag is globally unique — meaning two items
        /// with this tag cannot coexist in the same inventory.
        /// </summary>
        public bool IsUnique(string tag)
        {
            if (_uniqueSet == null) return false;
            return _uniqueSet.Contains(tag);
        }

        /// <summary>
        /// Checks whether a proposed set of tags (from a new item)
        /// would conflict with existing equipment tags.
        /// Returns the first conflicting tag, or null if OK.
        /// </summary>
        public string FindFirstConflict(string[] proposedTags, EquipmentHandler existingHandler)
        {
            if (proposedTags == null || existingHandler == null) return null;

            for (int i = 0; i < proposedTags.Length; i++)
            {
                var tag = proposedTags[i];
                if (!IsUnique(tag)) continue;
                if (existingHandler.HasTag(tag))
                    return tag;
            }
            return null;
        }
    }
}
