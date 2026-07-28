using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    [Serializable]
    public sealed class FrameSyncSettingsAuthoring
    {
        [Min(1)] public int TickRate = 30;
        [Min(0)] public int MinCommandLeadTicks = 1;
        [Min(1)] public int MaxFutureCommandTicks = 12;
        [Min(2)] public int SnapshotWindowTicks = 180;
        [Min(0)] public int MaxPredictionLeadTicks = 6;
        [Min(1)] public int MaxLogicTicksPerUnityFrame = 4;
        [Min(1)] public int AuthorityRecoveryRetryTicks = 15;
        [Min(1)] public int MaxAuthorityRecoveryAttemptsBeforeDisconnect = 4;
        [Min(0)] public int StartLeadTicks = 3;
    }

    [Serializable]
    public sealed class GameModeConfigAuthoring
    {
        [Min(1)] public int GameModeId = 1;
        [Min(1)] public int MaxPlayers = 10;
        [Min(0)] public float CountdownSeconds = 3f;
        [Min(0)] public float EndingSeconds = 6f;
        [Min(0)] public int InitialEarnedGold = 500;
        [Min(0)] public float HeroRespawnBaseSeconds = 10f;
        [Min(0)] public float HeroRespawnPerLevelSeconds = 2f;
        [Min(0.01f)] public float MinionWaveIntervalSeconds = 30f;
        [Min(0)] public float JungleResetTimeoutSeconds = 5f;
        [Min(0)] public float JungleResetDurationSeconds = 3f;
        [Min(0)] public float JungleRespawnDelaySeconds = 60f;
        [Range(0f, 1f)] public float EquipmentSellRate = 0.7f;
        [Min(1)] public uint RandomSeed = 12345u;
        [Min(1)] public int PeriodicGoldIntervalTicks = 15;
        [Min(0)] public int PeriodicGoldAmount = 2;
    }

    [Serializable]
    public sealed class PhysicsSettingsAuthoring
    {
        [Min(0.01f)] public float UnitGridCellSize = 10f;
    }

    [Serializable]
    public sealed class UnitSettingsAuthoring
    {
        public float StatGrowthC = 0.7025f;
        public float StatGrowthD = 0.0175f;
        [Min(1)] public int AttackSequenceResetIntervalTicks = 90;
    }

    public readonly struct BakedGlobalGameplayData
    {
        public readonly GlobalPrefabTable PrefabTable;
        public readonly int TickRate;
        public readonly fp LogicDelta;
        public readonly int MinCommandLeadTicks;
        public readonly int MaxFutureCommandTicks;
        public readonly int SnapshotWindowTicks;
        public readonly int MaxPredictionLeadTicks;
        public readonly int MaxLogicTicksPerUnityFrame;
        public readonly int AuthorityRecoveryRetryTicks;
        public readonly int MaxAuthorityRecoveryAttemptsBeforeDisconnect;
        public readonly int StartLeadTicks;
        public readonly int MaxPlayers;
        public readonly int CountdownTicks;
        public readonly int EndingDurationTicks;
        public readonly int InitialEarnedGold;
        public readonly fp UnitGridCellSize;
        public readonly fp StatGrowthC;
        public readonly fp StatGrowthD;
        public readonly int AttackSequenceResetIntervalTicks;
        public readonly int HeroRespawnBaseTicks;
        public readonly int HeroRespawnPerLevelTicks;
        public readonly BakedMinionWaveConfig MinionWaveConfig;
        public readonly int JungleResetTimeoutTicks;
        public readonly int JungleResetDurationTicks;
        public readonly int JungleRespawnDelayTicks;
        public readonly fp EquipmentSellRate;
        public readonly uint RandomSeed;
        public readonly int PeriodicGoldIntervalTicks;
        public readonly int PeriodicGoldAmount;

        public BakedGlobalGameplayData(
            GlobalPrefabTable prefabTable,
            int tickRate,
            int minCommandLeadTicks,
            int maxFutureCommandTicks,
            int snapshotWindowTicks,
            int maxPredictionLeadTicks,
            int maxLogicTicksPerUnityFrame,
            int authorityRecoveryRetryTicks,
            int maxAuthorityRecoveryAttemptsBeforeDisconnect,
            int startLeadTicks,
            int maxPlayers,
            int countdownTicks,
            int endingDurationTicks,
            int initialEarnedGold,
            fp unitGridCellSize,
            fp statGrowthC,
            fp statGrowthD,
            int attackSequenceResetIntervalTicks,
            int heroRespawnBaseTicks,
            int heroRespawnPerLevelTicks,
            BakedMinionWaveConfig minionWaveConfig,
            int jungleResetTimeoutTicks,
            int jungleResetDurationTicks,
            int jungleRespawnDelayTicks,
            fp equipmentSellRate,
            uint randomSeed,
            int periodicGoldIntervalTicks,
            int periodicGoldAmount)
        {
            PrefabTable = prefabTable;
            TickRate = tickRate;
            LogicDelta = fp.one / (fp)tickRate;
            MinCommandLeadTicks = minCommandLeadTicks;
            MaxFutureCommandTicks = maxFutureCommandTicks;
            SnapshotWindowTicks = snapshotWindowTicks;
            MaxPredictionLeadTicks = maxPredictionLeadTicks;
            MaxLogicTicksPerUnityFrame = maxLogicTicksPerUnityFrame;
            AuthorityRecoveryRetryTicks = authorityRecoveryRetryTicks;
            MaxAuthorityRecoveryAttemptsBeforeDisconnect =
                maxAuthorityRecoveryAttemptsBeforeDisconnect;
            StartLeadTicks = startLeadTicks;
            MaxPlayers = maxPlayers;
            CountdownTicks = countdownTicks;
            EndingDurationTicks = endingDurationTicks;
            InitialEarnedGold = initialEarnedGold;
            UnitGridCellSize = unitGridCellSize;
            StatGrowthC = statGrowthC;
            StatGrowthD = statGrowthD;
            AttackSequenceResetIntervalTicks = attackSequenceResetIntervalTicks;
            HeroRespawnBaseTicks = heroRespawnBaseTicks;
            HeroRespawnPerLevelTicks = heroRespawnPerLevelTicks;
            MinionWaveConfig = minionWaveConfig;
            JungleResetTimeoutTicks = jungleResetTimeoutTicks;
            JungleResetDurationTicks = jungleResetDurationTicks;
            JungleRespawnDelayTicks = jungleRespawnDelayTicks;
            EquipmentSellRate = equipmentSellRate;
            RandomSeed = randomSeed;
            PeriodicGoldIntervalTicks = periodicGoldIntervalTicks;
            PeriodicGoldAmount = periodicGoldAmount;
        }
    }

    [CreateAssetMenu(
        fileName = "GlobalGameplayData",
        menuName = "FrameSyncMoba/Runtime/Global Gameplay Data")]
    public sealed class GlobalGameplayData : ScriptableObject
    {
        [SerializeField] private GlobalPrefabTable globalPrefabTable;
        [SerializeField] private FrameSyncSettingsAuthoring frameSync =
            new FrameSyncSettingsAuthoring();
        [SerializeField] private GameModeConfigAuthoring gameMode =
            new GameModeConfigAuthoring();
        [SerializeField] private PhysicsSettingsAuthoring physics =
            new PhysicsSettingsAuthoring();
        [SerializeField] private UnitSettingsAuthoring unit =
            new UnitSettingsAuthoring();

        public GlobalPrefabTable GlobalPrefabTable => globalPrefabTable;

        public BakedGlobalGameplayData BakeOrThrow()
        {
            if (globalPrefabTable == null)
                throw new InvalidOperationException(
                    "GlobalGameplayData requires a GlobalPrefabTable.");
            globalPrefabTable.ValidateOrThrow();
            if (frameSync == null || gameMode == null || physics == null || unit == null)
                throw new InvalidOperationException(
                    "GlobalGameplayData contains a missing authoring section.");
            if (frameSync.TickRate <= 0 || gameMode.MaxPlayers <= 0 ||
                gameMode.InitialEarnedGold < 0 ||
                frameSync.MinCommandLeadTicks < 0 ||
                frameSync.MaxFutureCommandTicks <= 0 ||
                frameSync.MinCommandLeadTicks > frameSync.MaxFutureCommandTicks ||
                frameSync.SnapshotWindowTicks < 2 ||
                frameSync.MaxPredictionLeadTicks < 0 ||
                frameSync.MaxPredictionLeadTicks >= frameSync.SnapshotWindowTicks ||
                frameSync.MaxLogicTicksPerUnityFrame <= 0 ||
                frameSync.AuthorityRecoveryRetryTicks <= 0 ||
                frameSync.MaxAuthorityRecoveryAttemptsBeforeDisconnect <= 0 ||
                frameSync.StartLeadTicks < 0)
                throw new InvalidOperationException(
                    "FrameSync timing, command window, player count, or InitialEarnedGold is invalid.");
            ValidateFiniteNonnegative(gameMode.CountdownSeconds, nameof(gameMode.CountdownSeconds));
            ValidateFiniteNonnegative(gameMode.EndingSeconds, nameof(gameMode.EndingSeconds));
            ValidateFiniteNonnegative(gameMode.HeroRespawnBaseSeconds, nameof(gameMode.HeroRespawnBaseSeconds));
            ValidateFiniteNonnegative(gameMode.HeroRespawnPerLevelSeconds, nameof(gameMode.HeroRespawnPerLevelSeconds));
            ValidateFinitePositive(gameMode.MinionWaveIntervalSeconds, nameof(gameMode.MinionWaveIntervalSeconds));
            ValidateFiniteNonnegative(gameMode.JungleResetTimeoutSeconds, nameof(gameMode.JungleResetTimeoutSeconds));
            ValidateFiniteNonnegative(gameMode.JungleResetDurationSeconds, nameof(gameMode.JungleResetDurationSeconds));
            ValidateFiniteNonnegative(gameMode.JungleRespawnDelaySeconds, nameof(gameMode.JungleRespawnDelaySeconds));
            ValidateFiniteNonnegative(gameMode.EquipmentSellRate, nameof(gameMode.EquipmentSellRate));
            if (gameMode.EquipmentSellRate > 1f)
                throw new InvalidOperationException("EquipmentSellRate must not exceed 1.");
            ValidateFinitePositive(physics.UnitGridCellSize, nameof(physics.UnitGridCellSize));
            ValidateFinite(unit.StatGrowthC, nameof(unit.StatGrowthC));
            ValidateFinite(unit.StatGrowthD, nameof(unit.StatGrowthD));
            if (unit.AttackSequenceResetIntervalTicks < 1)
                throw new InvalidOperationException(
                    "AttackSequenceResetIntervalTicks must be at least 1.");

            int countdownTicks = SecondsToTicks(
                gameMode.CountdownSeconds, frameSync.TickRate);
            int endingTicks = SecondsToTicks(
                gameMode.EndingSeconds, frameSync.TickRate);
            var bakedMinionWaveConfig = new BakedMinionWaveConfig(
                SecondsToTicks(gameMode.MinionWaveIntervalSeconds, frameSync.TickRate),
                SecondsToTicks(0f, frameSync.TickRate), // FirstWaveTick: 0 = start immediately
                System.Array.Empty<MinionWavePhase>());
            return new BakedGlobalGameplayData(
                globalPrefabTable,
                frameSync.TickRate,
                frameSync.MinCommandLeadTicks,
                frameSync.MaxFutureCommandTicks,
                frameSync.SnapshotWindowTicks,
                frameSync.MaxPredictionLeadTicks,
                frameSync.MaxLogicTicksPerUnityFrame,
                frameSync.AuthorityRecoveryRetryTicks,
                frameSync.MaxAuthorityRecoveryAttemptsBeforeDisconnect,
                frameSync.StartLeadTicks,
                gameMode.MaxPlayers,
                countdownTicks,
                endingTicks,
                gameMode.InitialEarnedGold,
                (fp)physics.UnitGridCellSize,
                (fp)unit.StatGrowthC,
                (fp)unit.StatGrowthD,
                unit.AttackSequenceResetIntervalTicks,
                SecondsToTicks(gameMode.HeroRespawnBaseSeconds, frameSync.TickRate),
                SecondsToTicks(gameMode.HeroRespawnPerLevelSeconds, frameSync.TickRate),
                bakedMinionWaveConfig,
                SecondsToTicks(gameMode.JungleResetTimeoutSeconds, frameSync.TickRate),
                SecondsToTicks(gameMode.JungleResetDurationSeconds, frameSync.TickRate),
                SecondsToTicks(gameMode.JungleRespawnDelaySeconds, frameSync.TickRate),
                (fp)gameMode.EquipmentSellRate,
                gameMode.RandomSeed,
                gameMode.PeriodicGoldIntervalTicks,
                gameMode.PeriodicGoldAmount);
        }

        private static int SecondsToTicks(float seconds, int tickRate)
        {
            fp exactTicks = (fp)seconds * (fp)tickRate;
            int wholeTicks = (int)exactTicks;
            if ((fp)wholeTicks < exactTicks) wholeTicks = checked(wholeTicks + 1);
            return wholeTicks;
        }

        private static void ValidateFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException($"{name} must be finite.");
        }

        private static void ValidateFiniteNonnegative(float value, string name)
        {
            ValidateFinite(value, name);
            if (value < 0f) throw new InvalidOperationException($"{name} must be nonnegative.");
        }

        private static void ValidateFinitePositive(float value, string name)
        {
            ValidateFinite(value, name);
            if (value <= 0f) throw new InvalidOperationException($"{name} must be positive.");
        }

        private void OnValidate()
        {
            if (globalPrefabTable == null) return;
            try { BakeOrThrow(); }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Invalid GlobalGameplayData '{name}': {exception.Message}", this);
            }
        }
    }
}
