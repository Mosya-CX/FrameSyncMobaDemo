using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    /// <summary>
    /// Hero select display row. The row is auto-created from a hero
    /// UnitPrototype by <c>HeroDisplayTableSync</c>; content authors only fill
    /// the avatar (and may override the display name).
    /// </summary>
    [Serializable]
    public sealed class HeroDisplayEntry
    {
        [Min(1)] public int UnitPrototypeId;
        [Min(1)] public int HeroPrefabId;
        public string DisplayName;
        public Sprite Avatar;
    }

    /// <summary>
    /// Hero avatar/name mapping table (design v10.2 17.x). The prefab table
    /// only stores prefabs; this table stores the hero select presentation
    /// data and is kept in sync with hero prototypes automatically in the
    /// editor.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HeroDisplayTable",
        menuName = "FrameSyncMoba/Runtime/Hero Display Table")]
    public sealed class HeroDisplayTable : ScriptableObject
    {
        [SerializeField] private List<HeroDisplayEntry> entries =
            new List<HeroDisplayEntry>();

        public IReadOnlyList<HeroDisplayEntry> Entries =>
            entries;

        public int Count => entries.Count;

        public HeroDisplayEntry GetEntry(int index)
        {
            return entries[index];
        }

        public bool TryGetByPrototypeId(
            int unitPrototypeId,
            out HeroDisplayEntry entry)
        {
            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                if (entries[i] != null &&
                    entries[i].UnitPrototypeId ==
                    unitPrototypeId)
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = null;
            return false;
        }

        /// <summary>
        /// Adds a mapping row or updates the existing row for the prototype.
        /// Existing display names and avatars are preserved.
        /// </summary>
        public void AddOrReplace(
            HeroDisplayEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(
                    nameof(entry));
            if (entry.UnitPrototypeId <= 0)
                throw new ArgumentException(
                    "UnitPrototypeId must be positive.",
                    nameof(entry));
            for (int i = 0;
                 i < entries.Count;
                 i++)
            {
                if (entries[i] != null &&
                    entries[i].UnitPrototypeId ==
                    entry.UnitPrototypeId)
                {
                    entries[i].HeroPrefabId =
                        entry.HeroPrefabId;
                    if (string.IsNullOrWhiteSpace(
                            entries[i].DisplayName))
                        entries[i].DisplayName =
                            entry.DisplayName;
                    return;
                }
            }
            entries.Add(entry);
        }

        public void RemovePrototype(
            int unitPrototypeId)
        {
            for (int i = entries.Count - 1;
                 i >= 0;
                 i--)
            {
                if (entries[i] != null &&
                    entries[i].UnitPrototypeId ==
                    unitPrototypeId)
                    entries.RemoveAt(i);
            }
        }
    }
}
