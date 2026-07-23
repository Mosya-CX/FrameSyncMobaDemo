using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Global team mapping table that records team identity metadata.
    /// Provides the stable mapping from TeamId (byte) to human-readable
    /// team information for debugging, validation and display.
    /// Not part of deterministic simulation state; it is configuration loaded
    /// at match initialization and read-only during Gameplay.
    /// </summary>
    public sealed class TeamRegistry
    {
        private readonly Dictionary<TeamId, TeamInfo> teams = new Dictionary<TeamId, TeamInfo>();

        /// <summary>
        /// Registers a team. Throws if the same TeamId is already registered
        /// with a different name (deterministic configuration validation).
        /// </summary>
        public void RegisterTeam(TeamId teamId, string name)
        {
            if (teams.TryGetValue(teamId, out TeamInfo existing))
            {
                if (existing.Name != name)
                {
                    throw new InvalidOperationException(
                        $"TeamId {teamId} is already registered as '{existing.Name}', "
                        + $"cannot re-register as '{name}'.");
                }
                return;
            }

            teams[teamId] = new TeamInfo(teamId, name);
        }

        public bool TryGetTeam(TeamId teamId, out TeamInfo info)
        {
            return teams.TryGetValue(teamId, out info);
        }

        public bool IsRegistered(TeamId teamId)
        {
            return teams.ContainsKey(teamId);
        }

        public int Count => teams.Count;
    }

    /// <summary>
    /// Immutable team metadata entry in the global TeamRegistry.
    /// </summary>
    public readonly struct TeamInfo
    {
        public readonly TeamId TeamId;
        public readonly string Name;

        public TeamInfo(TeamId teamId, string name)
        {
            TeamId = teamId;
            Name = name;
        }
    }
}