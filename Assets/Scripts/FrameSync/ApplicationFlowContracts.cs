using System;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public readonly struct PlayerSlotConfig
    {
        public readonly int PlayerSlot;
        public readonly string AccountId;
        public readonly ulong ControllerClientId;
        public readonly TeamId TeamId;
        public readonly int HeroConfigId;
        public readonly int SpawnPointId;

        public PlayerSlotConfig(
            int playerSlot,
            string accountId,
            ulong controllerClientId,
            TeamId teamId,
            int heroConfigId,
            int spawnPointId)
        {
            if (playerSlot < 0 || playerSlot >= 10)
                throw new ArgumentOutOfRangeException(nameof(playerSlot));
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException(
                    "AccountId is required.",
                    nameof(accountId));
            if (teamId == TeamId.Neutral)
                throw new ArgumentException(
                    "A player slot requires a non-neutral TeamId.",
                    nameof(teamId));
            if (heroConfigId <= 0)
                throw new ArgumentOutOfRangeException(nameof(heroConfigId));
            if (spawnPointId < 0)
                throw new ArgumentOutOfRangeException(nameof(spawnPointId));
            PlayerSlot = playerSlot;
            AccountId = accountId;
            ControllerClientId = controllerClientId;
            TeamId = teamId;
            HeroConfigId = heroConfigId;
            SpawnPointId = spawnPointId;
        }
    }

    public readonly struct GameStartConfig
    {
        private readonly PlayerSlotConfig[] playerSlots;

        public readonly string MatchId;
        public readonly int GameModeId;
        public readonly int MapConfigId;
        public readonly int GameStartPlayerCount;
        public readonly int TeamCount;
        public readonly int StartTick;
        public readonly uint InitialRandomSeed;
        public readonly uint GameplayDataVersion;

        public PlayerSlotConfig[] PlayerSlots =>
            playerSlots == null
                ? Array.Empty<PlayerSlotConfig>()
                : (PlayerSlotConfig[])playerSlots.Clone();

        public GameStartConfig(
            string matchId,
            int gameModeId,
            int mapConfigId,
            int gameStartPlayerCount,
            int teamCount,
            PlayerSlotConfig[] playerSlots,
            int startTick,
            uint initialRandomSeed,
            uint gameplayDataVersion)
        {
            MatchId = matchId;
            GameModeId = gameModeId;
            MapConfigId = mapConfigId;
            GameStartPlayerCount = gameStartPlayerCount;
            TeamCount = teamCount;
            this.playerSlots = playerSlots == null
                ? Array.Empty<PlayerSlotConfig>()
                : (PlayerSlotConfig[])playerSlots.Clone();
            StartTick = startTick;
            InitialRandomSeed = initialRandomSeed;
            GameplayDataVersion = gameplayDataVersion;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(MatchId))
                throw new DeterministicSimulationException(
                    "GameStartConfig requires MatchId.");
            if (GameModeId <= 0 || MapConfigId <= 0)
                throw new DeterministicSimulationException(
                    "GameStartConfig requires positive mode and map IDs.");
            if (GameStartPlayerCount < 1 ||
                GameStartPlayerCount > 10 ||
                playerSlots == null ||
                playerSlots.Length != GameStartPlayerCount)
                throw new DeterministicSimulationException(
                    "GameStartPlayerCount must be 1-10 and equal PlayerSlots.Length.");
            if (TeamCount < 1 || TeamCount > GameStartPlayerCount)
                throw new DeterministicSimulationException(
                    "TeamCount is outside the player-count range.");
            if (StartTick < 0 || InitialRandomSeed == 0 ||
                GameplayDataVersion == 0)
                throw new DeterministicSimulationException(
                    "StartTick, random seed or GameplayDataVersion is invalid.");

            for (int i = 0; i < playerSlots.Length; i++)
            {
                PlayerSlotConfig slot = playerSlots[i];
                if (slot.PlayerSlot != i)
                    throw new DeterministicSimulationException(
                        "PlayerSlots must be unique and stored in ascending PlayerSlot order.");
                for (int j = 0; j < i; j++)
                {
                    if (playerSlots[j].AccountId == slot.AccountId)
                        throw new DeterministicSimulationException(
                            "GameStartConfig contains a duplicate AccountId.");
                    if (playerSlots[j].ControllerClientId ==
                        slot.ControllerClientId)
                        throw new DeterministicSimulationException(
                            "GameStartConfig contains a duplicate ControllerClientId.");
                }
            }
        }
    }

    public readonly struct FrameSyncVersionHandshake
    {
        public readonly uint GameplayDataVersion;
        public readonly uint MapDataVersion;
        public readonly uint GlobalPrefabTableVersion;
        public readonly uint CommandSchemaVersion;
        public readonly uint SnapshotSchemaVersion;

        public FrameSyncVersionHandshake(
            uint gameplayDataVersion,
            uint mapDataVersion,
            uint globalPrefabTableVersion,
            uint commandSchemaVersion,
            uint snapshotSchemaVersion)
        {
            if (gameplayDataVersion == 0 ||
                mapDataVersion == 0 ||
                globalPrefabTableVersion == 0 ||
                commandSchemaVersion == 0 ||
                snapshotSchemaVersion == 0)
                throw new ArgumentOutOfRangeException(
                    nameof(gameplayDataVersion),
                    "All critical versions must be nonzero.");
            GameplayDataVersion = gameplayDataVersion;
            MapDataVersion = mapDataVersion;
            GlobalPrefabTableVersion = globalPrefabTableVersion;
            CommandSchemaVersion = commandSchemaVersion;
            SnapshotSchemaVersion = snapshotSchemaVersion;
        }

        public void RequireExactMatch(
            in FrameSyncVersionHandshake remote)
        {
            if (GameplayDataVersion != remote.GameplayDataVersion ||
                MapDataVersion != remote.MapDataVersion ||
                GlobalPrefabTableVersion != remote.GlobalPrefabTableVersion ||
                CommandSchemaVersion != remote.CommandSchemaVersion ||
                SnapshotSchemaVersion != remote.SnapshotSchemaVersion)
                throw new DeterministicSimulationException(
                    "Critical FrameSync version handshake mismatch.");
        }
    }

    /// <summary>
    /// Frozen match-start ownership mapping required by FrameSync v10.2
    /// section 3.5. It is application configuration, not mutable Gameplay
    /// snapshot state.
    /// </summary>
    public readonly struct PlayerSlotUnitMapping
    {
        public readonly int PlayerSlot;
        public readonly UnitUid ControlledUnitUid;

        public PlayerSlotUnitMapping(
            int playerSlot,
            UnitUid controlledUnitUid)
        {
            if (playerSlot < 0 || playerSlot >= 10)
                throw new ArgumentOutOfRangeException(
                    nameof(playerSlot));
            if (!controlledUnitUid.IsValid())
                throw new ArgumentException(
                    "Controlled UnitUid must be valid.",
                    nameof(controlledUnitUid));
            PlayerSlot = playerSlot;
            ControlledUnitUid = controlledUnitUid;
        }
    }

    public readonly struct GameBootstrapPayload
    {
        private readonly PlayerSlotUnitMapping[] playerSlotMappings;

        public readonly GameStartConfig GameStartConfig;
        public readonly FrameSyncVersionHandshake Versions;
        public readonly GameplaySnapshot InitialGameplaySnapshot;
        public readonly int InitialSnapshotTick;
        public readonly int StartTick;
        public readonly uint InitialRandomSeed;
        /// <summary>
        /// Legacy wire-layout field. The two-phase startup contract requires
        /// this to be 0; MatchLaunchCommit exclusively authorizes simulation.
        /// </summary>
        public readonly long LaunchUtcTicks;

        public PlayerSlotUnitMapping[] PlayerSlotMappings =>
            playerSlotMappings == null
                ? Array.Empty<PlayerSlotUnitMapping>()
                : (PlayerSlotUnitMapping[])playerSlotMappings.Clone();

        public GameBootstrapPayload(
            in GameStartConfig gameStartConfig,
            in FrameSyncVersionHandshake versions,
            in GameplaySnapshot initialGameplaySnapshot,
            int initialSnapshotTick,
            int startTick,
            uint initialRandomSeed,
            PlayerSlotUnitMapping[] playerSlotMappings,
            long launchUtcTicks = 0)
        {
            gameStartConfig.ValidateOrThrow();
            if (!initialGameplaySnapshot.IsValid ||
                initialGameplaySnapshot.SchemaVersion !=
                    versions.SnapshotSchemaVersion)
                throw new DeterministicSimulationException(
                    "Initial GameplaySnapshot does not match the version handshake.");
            if (initialSnapshotTick != startTick ||
                startTick != gameStartConfig.StartTick ||
                initialRandomSeed != gameStartConfig.InitialRandomSeed)
                throw new DeterministicSimulationException(
                    "Bootstrap Tick or random seed disagrees with GameStartConfig.");
            if (launchUtcTicks != 0)
                throw new DeterministicSimulationException(
                    "GameBootstrapPayload cannot authorize simulation launch.");

            PlayerSlotConfig[] slots = gameStartConfig.PlayerSlots;
            if (playerSlotMappings == null ||
                playerSlotMappings.Length != slots.Length)
                throw new DeterministicSimulationException(
                    "PlayerSlotMappings must match GameStartPlayerCount.");
            UnitSnapshot[] units =
                initialGameplaySnapshot.UnitWorldState.Units ??
                Array.Empty<UnitSnapshot>();
            for (int i = 0; i < playerSlotMappings.Length; i++)
            {
                PlayerSlotUnitMapping mapping =
                    playerSlotMappings[i];
                if (mapping.PlayerSlot != i)
                    throw new DeterministicSimulationException(
                        "PlayerSlotMappings must be stored in ascending PlayerSlot order.");
                int unitIndex = FindUnit(
                    units,
                    mapping.ControlledUnitUid);
                if (unitIndex < 0)
                    throw new DeterministicSimulationException(
                        "PlayerSlotMappings references a Unit missing from the initial snapshot.");
                if (units[unitIndex].TeamId !=
                    slots[i].TeamId)
                    throw new DeterministicSimulationException(
                        "PlayerSlotMappings Unit team disagrees with GameStartConfig.");
            }

            GameStartConfig = gameStartConfig;
            Versions = versions;
            InitialGameplaySnapshot = initialGameplaySnapshot;
            InitialSnapshotTick = initialSnapshotTick;
            StartTick = startTick;
            InitialRandomSeed = initialRandomSeed;
            LaunchUtcTicks = launchUtcTicks;
            this.playerSlotMappings =
                (PlayerSlotUnitMapping[])playerSlotMappings.Clone();
        }

        private static int FindUnit(
            UnitSnapshot[] units,
            UnitUid uid)
        {
            int low = 0;
            int high = units.Length;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                int comparison =
                    units[middle].UnitUid.CompareTo(uid);
                if (comparison < 0)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low < units.Length &&
                   units[low].UnitUid == uid
                ? low
                : -1;
        }
    }

    public readonly struct MatchResultState
    {
        public readonly string MatchId;
        public readonly uint ResultRevision;
        public readonly int GameOverTick;
        public readonly TeamId WinningTeamId;
        public readonly MatchEndReason EndReason;

        public MatchResultState(
            string matchId,
            uint resultRevision,
            int gameOverTick,
            TeamId winningTeamId,
            MatchEndReason endReason)
        {
            if (string.IsNullOrWhiteSpace(matchId) ||
                resultRevision == 0 ||
                gameOverTick < 0 ||
                endReason == MatchEndReason.None)
                throw new ArgumentException(
                    "MatchResultState is incomplete.");
            MatchId = matchId;
            ResultRevision = resultRevision;
            GameOverTick = gameOverTick;
            WinningTeamId = winningTeamId;
            EndReason = endReason;
        }

        public void ValidateAgainst(
            MatchRuleRuntime matchRule,
            int latestAuthorityFrameTick)
        {
            if (matchRule == null)
                throw new ArgumentNullException(nameof(matchRule));
            if (latestAuthorityFrameTick < GameOverTick)
                throw new InvalidOperationException(
                    "The matching AuthorityFrame is not continuously accepted yet.");
            if (matchRule.GameOverTick != GameOverTick ||
                matchRule.WinningTeamId != WinningTeamId ||
                matchRule.EndReason != EndReason)
                throw new DeterministicSimulationException(
                    "MatchResultState disagrees with authority-replayed Gameplay.");
        }
    }
}
