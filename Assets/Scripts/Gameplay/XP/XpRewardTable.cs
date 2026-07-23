namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static lookup for experience rewards on unit kills.
    /// Rewards scale with victim level and killer level differential.
    /// (Combat v13.2 §DeathReward)
    /// </summary>
    public static class XpRewardTable
    {
        /// <summary>Base XP reward for killing a level 1 unit.</summary>
        private const int BaseXpReward = 42;

        /// <summary>Additional XP per victim level above 1.</summary>
        private const int XpPerVictimLevel = 28;

        /// <summary>
        /// XP penalty per level the killer is above the victim (capped at 0).
        /// </summary>
        private const int XpPenaltyPerLevelDiff = 4;

        /// <summary>Minimum XP reward, even when killing much lower-level units.</summary>
        private const int MinXpReward = 10;

        /// <summary>
        /// Compute the XP reward for killing a unit of the given level.
        /// </summary>
        public static int GetKillXpReward(int victimLevel, int killerLevel)
        {
            int baseXp = BaseXpReward + (victimLevel - 1) * XpPerVictimLevel;
            int levelDiff = killerLevel - victimLevel;
            if (levelDiff > 0)
            {
                int penalty = levelDiff * XpPenaltyPerLevelDiff;
                baseXp -= penalty;
                if (baseXp < MinXpReward) baseXp = MinXpReward;
            }
            return baseXp;
        }

        /// <summary>
        /// Assist XP is a fraction of the kill XP, shared among assistants.
        /// </summary>
        public static int GetAssistXpReward(int victimLevel, int killerLevel, int assistantCount)
        {
            if (assistantCount <= 0) return 0;
            int killXp = GetKillXpReward(victimLevel, killerLevel);
            // Assist pool = 50% of kill XP, split among assistants
            int assistPool = killXp / 2;
            return assistPool / assistantCount;
        }
    }
}
