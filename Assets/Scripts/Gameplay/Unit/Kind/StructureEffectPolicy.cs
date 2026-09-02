using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Canonical admission rule for effects targeting structures. A structure
    /// may keep its own authored/runtime effects, but an external unit may
    /// affect it only through the base damage of an ordinary attack.
    /// </summary>
    public static class StructureEffectPolicy
    {
        public static bool AllowsDamage(
            Unit target,
            in CombatRequestHeader header)
        {
            if (target == null ||
                target.UnitKind != UnitKind.Structure)
            {
                return true;
            }
            if (IsSelfSource(target, header.SourceUnitUid) ||
                IsSelfSource(
                    target,
                    header.SourceDescriptor.OwnerUnitUid))
            {
                return true;
            }
            return header.SourceDescriptor.SourceType ==
                    CombatSourceType.Attack &&
                header.SourceDescriptor.SourceId ==
                    CombatBuiltinSourceId.BasicAttack;
        }

        public static bool AllowsExternalEffect(
            Unit target,
            UnitUid sourceUnitUid)
        {
            return target == null ||
                target.UnitKind != UnitKind.Structure ||
                IsSelfSource(target, sourceUnitUid);
        }

        public static bool AllowsBuff(
            Unit target,
            in BuffSource source)
        {
            return AllowsExternalEffect(
                target,
                source.CasterUid);
        }

        /// <summary>
        /// Canonical Buff admission boundary for producers that may target a
        /// Unit without a BuffHandler (notably formal structures).
        /// </summary>
        public static bool TryApplyBuff(
            Unit target,
            BuffConfigId configId,
            BuffDefinition definition,
            in BuffSource source)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (!configId.IsValid)
                return false;
            if (!AllowsBuff(target, source))
                return false;
            if (target.BuffHandler == null)
            {
                throw new DeterministicSimulationException(
                    $"Buff target {target.UnitUid} has no BuffHandler.");
            }
            return target.BuffHandler.Apply(
                configId,
                definition,
                source);
        }

        /// <summary>
        /// Canonical crowd-control admission boundary. It rejects external
        /// structure control before touching an optional CrowdControlHandler,
        /// so a content-mask mistake cannot become a null-handler exception.
        /// </summary>
        public static CrowdControlAddResult TryApplyCrowdControl(
            Unit target,
            UnitUid sourceUnitUid,
            CrowdControlId controlId,
            int durationTicks,
            in CrowdControlParamWriter parameters)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (!AllowsExternalEffect(target, sourceUnitUid))
            {
                CrowdControlAddStatus validationStatus =
                    ValidateCrowdControlRequest(
                        target,
                        controlId,
                        durationTicks,
                        parameters);
                if (validationStatus != CrowdControlAddStatus.Added)
                {
                    return new CrowdControlAddResult(
                        validationStatus,
                        default);
                }
                return new CrowdControlAddResult(
                    CrowdControlAddStatus.OwnerRejected,
                    default);
            }
            if (target.CrowdControl == null)
            {
                throw new DeterministicSimulationException(
                    $"Crowd-control target {target.UnitUid} has no CrowdControlHandler.");
            }
            return target.CrowdControl.Add(
                controlId,
                durationTicks,
                parameters);
        }

        private static CrowdControlAddStatus
            ValidateCrowdControlRequest(
                Unit target,
                CrowdControlId controlId,
                int durationTicks,
                in CrowdControlParamWriter parameters)
        {
            CrowdControlDefinitionRegistry registry =
                target.World?.CrowdControlDefinitions;
            if (registry == null ||
                !registry.TryGet(
                    controlId,
                    out CrowdControlDefinition definition) ||
                definition == null ||
                !definition.IsValid)
            {
                return CrowdControlAddStatus.InvalidDefinition;
            }
            if (!parameters.Materialize(
                    definition.ParamLayout,
                    out _))
            {
                return CrowdControlAddStatus.InvalidParams;
            }
            if (durationTicks != CrowdControlHandler.InfiniteTicks &&
                durationTicks <= 0)
            {
                return CrowdControlAddStatus.InvalidDuration;
            }
            return CrowdControlAddStatus.Added;
        }

        private static bool IsSelfSource(
            Unit target,
            UnitUid sourceUnitUid)
        {
            return target != null &&
                sourceUnitUid.IsValid() &&
                sourceUnitUid == target.UnitUid;
        }
    }
}
