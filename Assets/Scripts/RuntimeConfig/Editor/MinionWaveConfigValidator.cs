using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig.Editor
{
    /// <summary>
    /// Editor-time validator for MinionWaveConfig assets.
    /// Validates the frozen phase/composition structure.
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

            var phases = config.Phases;
            if (phases == null || phases.Length == 0)
            {
                Debug.LogWarning("[MinionWaveConfig] No phases configured.");
                return true;
            }

            bool allValid = true;
            for (int phaseIndex = 0;
                 phaseIndex < phases.Length;
                 phaseIndex++)
            {
                MinionWavePhase phase = phases[phaseIndex];
                if (phase.StartWaveIndex < 0 ||
                    (phaseIndex > 0 &&
                     phases[phaseIndex - 1].StartWaveIndex >=
                     phase.StartWaveIndex))
                {
                    Debug.LogError(
                        $"[MinionWaveConfig] Phase {phaseIndex} StartWaveIndex must be nonnegative and strictly increasing.");
                    allValid = false;
                }
                var cycle = phase.CompositionCycle;
                if (cycle == null || cycle.Length == 0)
                {
                    Debug.LogError(
                        $"[MinionWaveConfig] Phase {phaseIndex} has no composition cycle.");
                    allValid = false;
                    continue;
                }
                for (int compositionIndex = 0;
                     compositionIndex < cycle.Length;
                     compositionIndex++)
                {
                    var members = cycle[compositionIndex].Members;
                    if (members == null || members.Length == 0)
                    {
                        Debug.LogError(
                            $"[MinionWaveConfig] Phase {phaseIndex} composition {compositionIndex} has no members.");
                        allValid = false;
                        continue;
                    }
                    for (int memberIndex = 0;
                         memberIndex < members.Length;
                         memberIndex++)
                    {
                        MinionWaveMember member =
                            members[memberIndex];
                        if (member.UnitPrototypeId <= 0 ||
                            member.Count <= 0 ||
                            member.FirstSpawnOffsetTicks < 0 ||
                            member.SpawnStepTicks < 0 ||
                            member.FormationGroup < 0)
                        {
                            Debug.LogError(
                                $"[MinionWaveConfig] Phase {phaseIndex}, composition {compositionIndex}, member {memberIndex} is invalid.");
                            allValid = false;
                        }
                    }
                }
            }

            return allValid;
        }
    }
}
