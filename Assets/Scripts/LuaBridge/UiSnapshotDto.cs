using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.LuaBridge
{
    /// <summary>
    /// Per-tick read-only snapshot of UI-relevant Gameplay state.
    /// Populated at tick-end from deterministic state.
    /// Does NOT enter GameplaySnapshot, SharedGameplayChecksum,
    /// or any deterministic path.
    ///
    /// Design: MOBA_UI_Lua_System_Design_v9_1 sections 1.4, 10
    /// </summary>
    public struct UiSnapshotDto
    {
        public fp CurrentHealth;
        public fp MaxHealth;
        public fp CurrentResource;
        public fp MaxResource;
        public int CurrentGold;
        public int ConfirmedGold;
        public int CooldownRemaining0;
        public int CooldownRemaining1;
        public int CooldownRemaining2;
        public int CooldownRemaining3;
        public int CooldownTotal0;
        public int CooldownTotal1;
        public int CooldownTotal2;
        public int CooldownTotal3;
        public int UnitLevel;
        public int CurrentExperience;
        public int ExperienceForNextLevel;

        // Scoreboard fields (populated from MatchStatisticsRuntime)
        public int PlayerCount;
        public int Kills;
        public int Deaths;
        public int Assists;
        // Aggregated all-player stats arrays for Lua scoreboard rendering
        public System.Collections.Generic.List<int> AllPlayerKills;
        public System.Collections.Generic.List<int> AllPlayerDeaths;
        public System.Collections.Generic.List<int> AllPlayerAssists;
        public System.Collections.Generic.List<string> AllPlayerNames;

        public static readonly UiSnapshotDto Empty = default;
    }
}
