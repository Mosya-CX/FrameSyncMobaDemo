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
    public sealed class CriticalDataVersionsAuthoring
    {
        [Min(1)] public uint GameplayDataVersion = 1;
        [Min(1)] public uint MapDataVersion = 1;
        [Min(1)] public uint GlobalPrefabTableVersion = 1;
        [Min(1)] public uint CommandSchemaVersion = 1;
    }

    [Serializable]
    public sealed class GameModeConfigAuthoring
    {
        [Min(1)] public int GameModeId = 1;
        [Min(1)] public int MaxPlayers = 10;
        public DurationAuthoring Countdown;
        [HideInInspector]
        [Min(0)] public float CountdownSeconds = 3f;
        public DurationAuthoring LaunchDelay;
        [HideInInspector]
        [Min(0f)] public float LaunchDelaySeconds = 5f;
        public DurationAuthoring EndingDuration;
        [HideInInspector]
        [Min(0)] public float EndingSeconds = 6f;
        [Min(0)] public int InitialEarnedGold = 1500;
        public DurationAuthoring HeroRespawnBase;
        [HideInInspector]
        [Min(0)] public float HeroRespawnBaseSeconds = 5f;
        public DurationAuthoring HeroRespawnPerMinute;
        [HideInInspector]
        [Min(0)] public float HeroRespawnPerMinuteSeconds = 0.5f;
        public DurationAuthoring MinionWaveInterval;
        [HideInInspector]
        [Min(0.01f)] public float MinionWaveIntervalSeconds = 30f;
        public DurationAuthoring JungleResetTimeout;
        [HideInInspector]
        [Min(0)] public float JungleResetTimeoutSeconds = 5f;
        public DurationAuthoring JungleResetDuration;
        [HideInInspector]
        [Min(0)] public float JungleResetDurationSeconds = 3f;
        public DurationAuthoring JungleRespawnDelay;
        [HideInInspector]
        [Min(0)] public float JungleRespawnDelaySeconds = 60f;
        [Range(0f, 1f)] public float EquipmentSellRate = 0.7f;
        /// <summary>Natural health/cast-resource regen cadence. The unit
        /// stats HealthRegeneration / CastResourceRegeneration express the
        /// amount restored over this many wall-clock seconds (LoL-style
        /// per-5s values).</summary>
        public DurationAuthoring NaturalRegenInterval;
        [HideInInspector, Min(0.1f)]
        public float NaturalRegenIntervalSeconds = 5f;
        [Min(1)] public uint RandomSeed = 12345u;
        public DurationAuthoring PeriodicGoldInterval;
        [HideInInspector, Min(1)]
        public int PeriodicGoldIntervalTicks = 15;
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
        [Min(0.0001f)] public float MoveSpeedToLogicVelocityScale = 0.01f;
        public DurationAuthoring AttackSequenceResetInterval;
        [HideInInspector, Min(1)]
        public int AttackSequenceResetIntervalTicks = 90;
        /// <summary>
        /// Units with AttackRange strictly above this value are treated as
        /// ranged (e.g. melee ~125-225, ranged ~500+). Used by item passives
        /// that scale differently for melee vs ranged owners.
        /// </summary>
        [Min(1)] public int RangedAttackRangeThreshold = 275;
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
        public readonly int LaunchDelayMilliseconds;
        public readonly int MaxPlayers;
        public readonly int CountdownTicks;
        public readonly int EndingDurationTicks;
        public readonly int InitialEarnedGold;
        public readonly fp UnitGridCellSize;
        public readonly fp StatGrowthC;
        public readonly fp StatGrowthD;
        public readonly fp MoveSpeedToLogicVelocityScale;
        public readonly int AttackSequenceResetIntervalTicks;
        public readonly int RangedAttackRangeThreshold;
        public readonly int HeroRespawnBaseTicks;
        public readonly int HeroRespawnPerMinuteTicks;
        public readonly BakedMinionWaveConfig MinionWaveConfig;
        public readonly int JungleResetTimeoutTicks;
        public readonly int JungleResetDurationTicks;
        public readonly int JungleRespawnDelayTicks;
        public readonly fp EquipmentSellRate;
        public readonly int NaturalRegenIntervalMilliseconds;
        public readonly uint RandomSeed;
        public readonly int PeriodicGoldIntervalTicks;
        public readonly int PeriodicGoldAmount;
        public readonly uint GameplayDataVersion;
        public readonly uint MapDataVersion;
        public readonly uint GlobalPrefabTableVersion;
        public readonly uint CommandSchemaVersion;

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
            int launchDelayMilliseconds,
            int maxPlayers,
            int countdownTicks,
            int endingDurationTicks,
            int initialEarnedGold,
            fp unitGridCellSize,
            fp statGrowthC,
            fp statGrowthD,
            fp moveSpeedToLogicVelocityScale,
            int attackSequenceResetIntervalTicks,
            int rangedAttackRangeThreshold,
            int heroRespawnBaseTicks,
            int heroRespawnPerMinuteTicks,
            BakedMinionWaveConfig minionWaveConfig,
            int jungleResetTimeoutTicks,
            int jungleResetDurationTicks,
            int jungleRespawnDelayTicks,
            fp equipmentSellRate,
            int naturalRegenIntervalMilliseconds,
            uint randomSeed,
            int periodicGoldIntervalTicks,
            int periodicGoldAmount,
            uint gameplayDataVersion,
            uint mapDataVersion,
            uint globalPrefabTableVersion,
            uint commandSchemaVersion)
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
            LaunchDelayMilliseconds = launchDelayMilliseconds;
            MaxPlayers = maxPlayers;
            CountdownTicks = countdownTicks;
            EndingDurationTicks = endingDurationTicks;
            InitialEarnedGold = initialEarnedGold;
            UnitGridCellSize = unitGridCellSize;
            StatGrowthC = statGrowthC;
            StatGrowthD = statGrowthD;
            MoveSpeedToLogicVelocityScale = moveSpeedToLogicVelocityScale;
            AttackSequenceResetIntervalTicks = attackSequenceResetIntervalTicks;
            RangedAttackRangeThreshold = rangedAttackRangeThreshold;
            HeroRespawnBaseTicks = heroRespawnBaseTicks;
            HeroRespawnPerMinuteTicks = heroRespawnPerMinuteTicks;
            MinionWaveConfig = minionWaveConfig;
            JungleResetTimeoutTicks = jungleResetTimeoutTicks;
            JungleResetDurationTicks = jungleResetDurationTicks;
            JungleRespawnDelayTicks = jungleRespawnDelayTicks;
            EquipmentSellRate = equipmentSellRate;
            NaturalRegenIntervalMilliseconds =
                naturalRegenIntervalMilliseconds;
            RandomSeed = randomSeed;
            PeriodicGoldIntervalTicks = periodicGoldIntervalTicks;
            PeriodicGoldAmount = periodicGoldAmount;
            GameplayDataVersion = gameplayDataVersion;
            MapDataVersion = mapDataVersion;
            GlobalPrefabTableVersion = globalPrefabTableVersion;
            CommandSchemaVersion = commandSchemaVersion;
        }
    }

    [CreateAssetMenu(
        fileName = "GlobalGameplayData",
        menuName = "FrameSyncMoba/Runtime/Global Gameplay Data")]
    public sealed class GlobalGameplayData : ScriptableObject
    {
        [SerializeField] private GlobalPrefabTable globalPrefabTable;
        [SerializeField] private HeroDisplayTable heroDisplayTable;
        [SerializeField] private FrameSyncSettingsAuthoring frameSync =
            new FrameSyncSettingsAuthoring();
        [SerializeField] private CriticalDataVersionsAuthoring versions =
            new CriticalDataVersionsAuthoring();
        [SerializeField] private GameModeConfigAuthoring gameMode =
            new GameModeConfigAuthoring();
        [SerializeField] private PhysicsSettingsAuthoring physics =
            new PhysicsSettingsAuthoring();
        [SerializeField] private UnitSettingsAuthoring unit =
            new UnitSettingsAuthoring();

        public GlobalPrefabTable GlobalPrefabTable => globalPrefabTable;

        /// <summary>
        /// Hero select presentation data (avatar/name rows auto-synced from
        /// hero prototypes). Presentation-only; never enters Gameplay state.
        /// </summary>
        public HeroDisplayTable HeroDisplayTable =>
            heroDisplayTable;

        public BakedGlobalGameplayData BakeOrThrow()
        {
            if (globalPrefabTable == null)
                throw new InvalidOperationException(
                    "GlobalGameplayData requires a GlobalPrefabTable.");
            globalPrefabTable.ValidateOrThrow();
            if (frameSync == null || versions == null ||
                gameMode == null || physics == null || unit == null)
                throw new InvalidOperationException(
                    "GlobalGameplayData contains a missing authoring section.");
            DeterministicTimeConversion.ValidateSupportedTickRate(
                frameSync.TickRate);
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
                frameSync.StartLeadTicks < 0 ||
                versions.GameplayDataVersion == 0 ||
                versions.MapDataVersion == 0 ||
                versions.GlobalPrefabTableVersion == 0 ||
                versions.CommandSchemaVersion == 0)
                throw new InvalidOperationException(
                    "FrameSync timing, command window, player count, or InitialEarnedGold is invalid.");
            ValidateFiniteNonnegative(gameMode.CountdownSeconds, nameof(gameMode.CountdownSeconds));
            ValidateFiniteNonnegative(gameMode.LaunchDelaySeconds, nameof(gameMode.LaunchDelaySeconds));
            ValidateFiniteNonnegative(gameMode.EndingSeconds, nameof(gameMode.EndingSeconds));
            ValidateFiniteNonnegative(gameMode.HeroRespawnBaseSeconds, nameof(gameMode.HeroRespawnBaseSeconds));
            ValidateFiniteNonnegative(gameMode.HeroRespawnPerMinuteSeconds, nameof(gameMode.HeroRespawnPerMinuteSeconds));
            ValidateFinitePositive(gameMode.MinionWaveIntervalSeconds, nameof(gameMode.MinionWaveIntervalSeconds));
            ValidateFiniteNonnegative(gameMode.JungleResetTimeoutSeconds, nameof(gameMode.JungleResetTimeoutSeconds));
            ValidateFiniteNonnegative(gameMode.JungleResetDurationSeconds, nameof(gameMode.JungleResetDurationSeconds));
            ValidateFiniteNonnegative(gameMode.JungleRespawnDelaySeconds, nameof(gameMode.JungleRespawnDelaySeconds));
            ValidateFiniteNonnegative(gameMode.EquipmentSellRate, nameof(gameMode.EquipmentSellRate));
            if (gameMode.EquipmentSellRate > 1f)
                throw new InvalidOperationException("EquipmentSellRate must not exceed 1.");
            ValidateFinitePositive(
                gameMode.NaturalRegenIntervalSeconds,
                nameof(gameMode.NaturalRegenIntervalSeconds));
            ValidateFinitePositive(physics.UnitGridCellSize, nameof(physics.UnitGridCellSize));
            ValidateFinite(unit.StatGrowthC, nameof(unit.StatGrowthC));
            ValidateFinite(unit.StatGrowthD, nameof(unit.StatGrowthD));
            ValidateFinitePositive(
                unit.MoveSpeedToLogicVelocityScale,
                nameof(unit.MoveSpeedToLogicVelocityScale));
            if (unit.AttackSequenceResetIntervalTicks < 1)
                throw new InvalidOperationException(
                    "AttackSequenceResetIntervalTicks must be at least 1.");
            if (unit.RangedAttackRangeThreshold < 1)
                throw new InvalidOperationException(
                    "RangedAttackRangeThreshold must be at least 1.");

            int countdownTicks = BakeDurationTicks(
                gameMode.Countdown,
                gameMode.CountdownSeconds,
                frameSync.TickRate);
            int endingTicks = BakeDurationTicks(
                gameMode.EndingDuration,
                gameMode.EndingSeconds,
                frameSync.TickRate);
            var bakedMinionWaveConfig = new BakedMinionWaveConfig(
                BakeDurationTicks(
                    gameMode.MinionWaveInterval,
                    gameMode.MinionWaveIntervalSeconds,
                    frameSync.TickRate),
                0,
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
                ResolveMilliseconds(
                    gameMode.LaunchDelay,
                    gameMode.LaunchDelaySeconds),
                gameMode.MaxPlayers,
                countdownTicks,
                endingTicks,
                gameMode.InitialEarnedGold,
                (fp)physics.UnitGridCellSize,
                (fp)unit.StatGrowthC,
                (fp)unit.StatGrowthD,
                (fp)unit.MoveSpeedToLogicVelocityScale,
                unit.AttackSequenceResetInterval.IsAuthored
                    ? unit.AttackSequenceResetInterval
                        .BakeTicks(frameSync.TickRate)
                    : DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(
                            unit.AttackSequenceResetIntervalTicks,
                            frameSync.TickRate),
                unit.RangedAttackRangeThreshold,
                BakeDurationTicks(
                    gameMode.HeroRespawnBase,
                    gameMode.HeroRespawnBaseSeconds,
                    frameSync.TickRate),
                BakeDurationTicks(
                    gameMode.HeroRespawnPerMinute,
                    gameMode.HeroRespawnPerMinuteSeconds,
                    frameSync.TickRate),
                bakedMinionWaveConfig,
                BakeDurationTicks(
                    gameMode.JungleResetTimeout,
                    gameMode.JungleResetTimeoutSeconds,
                    frameSync.TickRate),
                BakeDurationTicks(
                    gameMode.JungleResetDuration,
                    gameMode.JungleResetDurationSeconds,
                    frameSync.TickRate),
                BakeDurationTicks(
                    gameMode.JungleRespawnDelay,
                    gameMode.JungleRespawnDelaySeconds,
                    frameSync.TickRate),
                (fp)gameMode.EquipmentSellRate,
                ResolveMilliseconds(
                    gameMode.NaturalRegenInterval,
                    gameMode.NaturalRegenIntervalSeconds),
                gameMode.RandomSeed,
                gameMode.PeriodicGoldInterval.IsAuthored
                    ? gameMode.PeriodicGoldInterval
                        .BakeTicks(frameSync.TickRate)
                    : DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(
                            gameMode.PeriodicGoldIntervalTicks,
                            frameSync.TickRate),
                gameMode.PeriodicGoldAmount,
                versions.GameplayDataVersion,
                versions.MapDataVersion,
                versions.GlobalPrefabTableVersion,
                versions.CommandSchemaVersion);
        }

        private static int BakeDurationTicks(
            in DurationAuthoring duration,
            float legacySeconds,
            int tickRate)
        {
            return duration.IsAuthored
                ? duration.BakeTicks(tickRate)
                : DeterministicTimeConversion.SecondsToTicks(
                    legacySeconds,
                    tickRate);
        }

        private static int ResolveMilliseconds(
            in DurationAuthoring duration,
            float legacySeconds)
        {
            return duration.IsAuthored
                ? duration.Milliseconds
                : DurationAuthoring.FromSeconds(
                    (decimal)legacySeconds).Milliseconds;
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
