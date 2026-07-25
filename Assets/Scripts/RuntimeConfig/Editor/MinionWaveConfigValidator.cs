using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor-time validator for MinionWaveConfig assets.
    /// Validates that all referenced prototype IDs exist and lane IDs are valid.
    /// </summary>
    public static class MinionWaveConfigValidator
    {
        /// <summary>
        /// Validate structural integrity of the config.
        /// Prototype ID validation happens at runtime via UnitPrototypeTable.
        /// </summary>
        public static bool Validate(MinionWaveConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[MinionWaveConfig] Config is null.");
                return false;
            }

            if (config.WaveIntervalTicks < 1)
            {
                Debug.LogError("[MinionWaveConfig] WaveIntervalTicks must be >= 1.");
                return false;
            }

            if (config.FirstWaveTick < 0)
            {
                Debug.LogError("[MinionWaveConfig] FirstWaveTick must be >= 0.");
                return false;
            }

            var waves = config.Waves;
            if (waves == null || waves.Length == 0)
            {
                Debug.LogWarning("[MinionWaveConfig] No waves configured.");
                return true;
            }

            bool allValid = true;

            for (int i = 0; i < waves.Length; i++)
            {
                var entry = waves[i];

                if (entry.LaneId > 2)
                {
                    Debug.LogError($"[MinionWaveConfig] Wave {i}: LaneId {entry.LaneId} is invalid (must be 0-2).");
                    allValid = false;
                }

                if (!entry.IsValid)
                {
                    Debug.LogError($"[MinionWaveConfig] Wave {i}: No minion types configured (total count = 0).");
                    allValid = false;
                }

                if (entry.MeleeCount < 0)
                {
                    Debug.LogError($"[MinionWaveConfig] Wave {i}: MeleeCount cannot be negative.");
                    allValid = false;
                }

                if (entry.RangedCount < 0)
                {
                    Debug.LogError($"[MinionWaveConfig] Wave {i}: RangedCount cannot be negative.");
                    allValid = false;
                }

                if (entry.SiegeCount < 0)
                {
                    Debug.LogError($"[MinionWaveConfig] Wave {i}: SiegeCount cannot be negative.");
                    allValid = false;
                }
            }

            return allValid;
        }
    }
}
