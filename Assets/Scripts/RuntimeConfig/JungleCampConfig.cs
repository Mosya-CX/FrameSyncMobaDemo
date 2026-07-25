using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    /// <summary>
    /// Data-driven jungle camp configuration.
    /// Each entry defines a camp's monster composition, respawn delay,
    /// and reward data. Used at match start to initialize JungleCampSystem.
    ///
    /// Design: moba_non_hero_unit_modules_design_v5.md section 4
    /// </summary>
    [CreateAssetMenu(
        fileName = "JungleCampConfig",
        menuName = "FrameSyncMoba/Config/Jungle Camp Config")]
    public sealed class JungleCampConfig : ScriptableObject
    {
        [SerializeField] private List<JungleCampEntry> camps = new List<JungleCampEntry>();
        public IReadOnlyList<JungleCampEntry> Camps => camps;

        public void SetEntries(IReadOnlyList<JungleCampEntry> entries)
        {
            camps.Clear();
            if (entries != null)
                camps.AddRange(entries);
        }
    }

    [Serializable]
    public struct JungleCampEntry
    {
        [Tooltip("Unique camp identifier. Must match scene/Map data.")]
        [Min(0)] public int CampId;

        [Tooltip("Unit prototype IDs for each monster slot in this camp.")]
        public int[] MonsterPrototypeIds;

        [Tooltip("Respawn delay in seconds after main monster death.")]
        [Min(0f)] public float RespawnDelaySeconds;

        [Tooltip("Gold reward per monster slot on kill.")]
        public int[] GoldRewards;

        [Tooltip("XP reward per monster slot on kill.")]
        public int[] XpRewards;
    }
}
