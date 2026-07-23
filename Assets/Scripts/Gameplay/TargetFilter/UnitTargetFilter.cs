using FrameSyncMoba.Physics;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Target filtering criteria for RangeQueryService
    /// (Physics v13.1 section 9.2/9.5).
    /// All conditions are ANDed — a unit must pass all active filters.
    /// </summary>
    public struct UnitTargetFilter
    {
        public TeamQueryRule TeamRule;

        public UnitKindMask UnitKindMask;

        public bool RequireSubKind;
        public ushort UnitSubKindId;

        public bool RequirePrototype;
        public int UnitPrototypeId;

        public UnitLifeStateMask LifeStateMask;

        public bool RequireTargetable;

        /// <summary>
        /// Default filter: all units, all teams, all life states, any capability.
        /// </summary>
        public static readonly UnitTargetFilter Default = new UnitTargetFilter
        {
            TeamRule = TeamQueryRule.Any,
            UnitKindMask = UnitKindMask.All,
            LifeStateMask = UnitLifeStateMask.All,
        };

        /// <summary>
        /// Tests whether a unit passes all active filter conditions
        /// (Physics v13.1 section 9.5 PassUnitTargetFilter pseudocode).
        /// </summary>
        public bool PassFilter(UnitUid requesterUid, TeamId requesterTeam, PhysicsEntity2D entity)
        {
            // Resolve owner Unit from entity
            var unit = entity.QueryInfo.Owner as Unit;
            if (unit == null)
            {
                return false;
            }

            return PassFilter(requesterUid, requesterTeam, unit);
        }

        internal bool PassFilter(UnitUid requesterUid, TeamId requesterTeam, Unit unit)
        {
            if (unit == null)
            {
                return false;
            }

            // Team rule
            if (!PassTeamRule(requesterUid, requesterTeam, unit.UnitUid, unit.TeamId))
            {
                return false;
            }

            // LifeState mask
            if (!LifeStateMask.Contains(unit.LifeState))
            {
                return false;
            }

            // Capability: targetable
            if (RequireTargetable && !unit.CapabilityState.IsTargetable)
            {
                return false;
            }

            // UnitKind mask
            if (!UnitKindMask.Contains(unit.UnitKind))
            {
                return false;
            }

            // SubKind filter
            if (RequireSubKind && unit.UnitSubKindId != UnitSubKindId)
            {
                return false;
            }

            // Prototype filter
            if (RequirePrototype && unit.UnitPrototypeId != UnitPrototypeId)
            {
                return false;
            }

            return true;
        }

        private bool PassTeamRule(
            UnitUid requesterUid,
            TeamId requesterTeam,
            UnitUid targetUid,
            TeamId targetTeam)
        {
            switch (TeamRule)
            {
                case TeamQueryRule.Any:
                    return true;

                case TeamQueryRule.SelfOnly:
                    return targetUid == requesterUid;

                case TeamQueryRule.EnemyOnly:
                    if (requesterTeam == TeamId.Neutral || targetTeam == TeamId.Neutral)
                    {
                        return false;
                    }
                    return requesterTeam != targetTeam;

                case TeamQueryRule.AllyOnly:
                    if (requesterTeam == TeamId.Neutral || targetTeam == TeamId.Neutral)
                    {
                        return false;
                    }
                    return requesterTeam == targetTeam && targetUid != requesterUid;

                case TeamQueryRule.AllyOrSelf:
                    if (requesterTeam == TeamId.Neutral || targetTeam == TeamId.Neutral)
                    {
                        return false;
                    }
                    return requesterTeam == targetTeam;

                default:
                    return false;
            }
        }
    }
}

