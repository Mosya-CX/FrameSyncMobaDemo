using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor-time validation for JungleCampConfig ScriptableObject.
    /// Validates: no duplicate campIds, valid prototype references,
    /// non-negative timings, consistent array lengths.
    ///
    /// Design: moba_non_hero_unit_modules_design_v5.md section 4
    /// </summary>
    public static class JungleCampConfigValidator
    {
        /// <summary>
        /// Validate a JungleCampConfig asset and return error messages.
        /// Returns empty list if no errors found.
        /// </summary>
        public static List<string> Validate(JungleCampConfig config)
        {
            var errors = new List<string>();
            if (config == null)
            {
                errors.Add("JungleCampConfig is null.");
                return errors;
            }

            var camps = config.Camps;
            if (camps == null || camps.Count == 0)
            {
                errors.Add("JungleCampConfig has no camp entries.");
                return errors;
            }

            var seenIds = new HashSet<int>();
            for (int i = 0; i < camps.Count; i++)
            {
                JungleCampEntry camp = camps[i];
                if (!seenIds.Add(camp.CampId))
                {
                    errors.Add($"Duplicate CampId {camp.CampId} at entry {i}.");
                }

                if (camp.MonsterPrototypeIds == null || camp.MonsterPrototypeIds.Length == 0)
                {
                    errors.Add($"Camp {camp.CampId} (entry {i}) has no monster prototype IDs.");
                }

                if (camp.RespawnDelaySeconds < 0f)
                {
                    errors.Add($"Camp {camp.CampId} (entry {i}) has negative respawn delay.");
                }

                // Validate reward array lengths match monster count
                int monsterCount = camp.MonsterPrototypeIds?.Length ?? 0;
                if (camp.GoldRewards != null && camp.GoldRewards.Length != monsterCount)
                {
                    errors.Add($"Camp {camp.CampId}: GoldRewards length ({camp.GoldRewards.Length}) != monster count ({monsterCount}).");
                }
                if (camp.XpRewards != null && camp.XpRewards.Length != monsterCount)
                {
                    errors.Add($"Camp {camp.CampId}: XpRewards length ({camp.XpRewards.Length}) != monster count ({monsterCount}).");
                }

                // Validate gold/XP values are non-negative
                if (camp.GoldRewards != null)
                {
                    for (int j = 0; j < camp.GoldRewards.Length; j++)
                    {
                        if (camp.GoldRewards[j] < 0)
                            errors.Add($"Camp {camp.CampId} GoldRewards[{j}] is negative.");
                    }
                }
                if (camp.XpRewards != null)
                {
                    for (int j = 0; j < camp.XpRewards.Length; j++)
                    {
                        if (camp.XpRewards[j] < 0)
                            errors.Add($"Camp {camp.CampId} XpRewards[{j}] is negative.");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Validate and log errors to the Unity Console.
        /// Returns true if validation passes.
        /// </summary>
        public static bool ValidateAndLog(JungleCampConfig config)
        {
            var errors = Validate(config);
            if (errors.Count == 0)
            {
                Debug.Log($"[JungleCampConfigValidator] Config '{config?.name}' is valid ({config?.Camps?.Count ?? 0} camps).");
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[JungleCampConfigValidator] Config '{config?.name}' has {errors.Count} error(s):");
            foreach (var err in errors)
                sb.AppendLine($"  - {err}");
            Debug.LogError(sb.ToString());
            return false;
        }
    }
}
