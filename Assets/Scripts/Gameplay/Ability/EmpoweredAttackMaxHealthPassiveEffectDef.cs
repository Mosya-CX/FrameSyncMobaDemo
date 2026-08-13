using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Fixed passive that owns the empowered-attack ready timing. While
    /// ready it keeps a configurable ready Buff applied (which carries the
    /// AttackRange modifier, the HUD icon and the on-hit strike); a real
    /// basic-attack hit consumes the Buff and commits the passive cooldown.
    /// Healing is based only on the settled actual strike damage.
    /// </summary>
    [System.Serializable]
    public sealed class EmpoweredAttackMaxHealthPassiveEffectDef :
        PassiveAbilityEffectDef
    {
        public override bool EmpowersBasicAttack => true;

        public override bool CanEmpowerBasicAttack(
            Unit owner,
            Unit target,
            in AbilityPassiveRuntimeState state) =>
                target != null &&
                target.UnitKind != UnitKind.Structure;

        public int SourceAbilityId;
        public int RecipeId;
        public BuffConfigId ReadyBuffConfigId;
        public int HeroHealRatioBasisPoints;
        public int NonHeroHealRatioBasisPoints;

        public override void ValidateOrThrow()
        {
            const AbilityPassiveListenerMask expected =
                AbilityPassiveListenerMask.OnHitDealt |
                AbilityPassiveListenerMask.DamageDealt;
            if (ListenerMask != expected ||
                SourceAbilityId <= 0 ||
                RecipeId <= 0 ||
                !ReadyBuffConfigId.IsValid ||
                HeroHealRatioBasisPoints < 0 ||
                NonHeroHealRatioBasisPoints < 0)
            {
                throw new DeterministicSimulationException(
                    "Empowered max-health passive has invalid listeners or authoring values.");
            }
        }

        public override void OnTick(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            if (owner?.BuffHandler == null ||
                !ReadyBuffConfigId.IsValid)
            {
                return;
            }
            bool ready = SimulationTickContext.Current.Tick >=
                state.NextReadyLogicTick;
            bool hasBuff =
                owner.BuffHandler.HasBuff(ReadyBuffConfigId);
            if (ready && !hasBuff)
            {
                ApplyReadyBuff(owner);
            }
            else if (!ready && hasBuff)
            {
                // The empowered strike was consumed: drop the ready Buff.
                owner.BuffHandler.Remove(ReadyBuffConfigId);
            }
        }

        public override bool OnHitDealt(
            Unit owner,
            in OnHitEventData data,
            ref AbilityPassiveRuntimeState state)
        {
            // Consumption signal only: the ready Buff's on-hit effect performs
            // the empowered strike. Returning true lets AbilityHandler commit
            // the passive cooldown on the hit Tick.
            return !data.IsRepeated &&
                SimulationTickContext.Current.Tick >=
                    state.NextReadyLogicTick &&
                owner?.BuffHandler != null &&
                owner.BuffHandler.HasBuff(ReadyBuffConfigId) &&
                owner.World?.TryGetUnit(
                    data.TargetUid,
                    out Unit target) == true &&
                target.UnitKind != UnitKind.Structure;
        }

        public override bool OnDamageDealt(
            Unit owner,
            in DamageEventData data,
            ref AbilityPassiveRuntimeState state)
        {
            if (data.Source.SourceType != CombatSourceType.AttackEffect ||
                data.Source.SourceId != SourceAbilityId ||
                data.ActualDamage <= fp.zero ||
                owner?.World?.CombatSystem == null ||
                !owner.World.TryGetUnit(data.TargetUid, out Unit target))
            {
                return false;
            }
            int basisPoints = target.UnitKind == UnitKind.Hero
                ? HeroHealRatioBasisPoints
                : NonHeroHealRatioBasisPoints;
            fp heal = data.ActualDamage * (fp)basisPoints / (fp)10000;
            if (heal <= fp.zero)
                return false;
            var request = new HealRequest
            {
                Header = CombatRequestHeader.Create(
                    owner.UnitUid,
                    owner.UnitUid,
                    CombatSourceType.Ability,
                    SourceAbilityId,
                    RecipeId),
                SourceUnitUid = owner.UnitUid,
                TargetUnitUid = owner.UnitUid,
                BaseValue = heal,
            };
            owner.World.CombatSystem.SubmitHeal(request);
            return false;
        }

        public override void OnDeactivate(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            RemoveReadyBuff(owner);

        public override void OnUnitDeath(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            RemoveReadyBuff(owner);

        public override void OnRespawn(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            OnTick(owner, ref state);

        public override void Rebuild(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            // Unit v27.3 7.15: the rollback Rebuild phase only rebuilds
            // derived state and must NOT re-attach StatModifiers or re-apply
            // the ready Buff; Restore already brought back both the Buff and
            // the passive runtime state. Life-stage handle reconstruction
            // happens in OnRespawn, not here.
        }

        private void ApplyReadyBuff(Unit owner)
        {
            if (owner?.BuffHandler == null ||
                owner.World?.BuffDefinitions == null ||
                !ReadyBuffConfigId.IsValid)
            {
                return;
            }
            if (!owner.World.BuffDefinitions.TryGet(
                    ReadyBuffConfigId,
                    out BuffDefinition definition))
            {
                return;
            }
            owner.BuffHandler.Apply(
                ReadyBuffConfigId,
                definition,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Script,
                    0));
        }

        private void RemoveReadyBuff(Unit owner)
        {
            if (owner?.BuffHandler == null ||
                !ReadyBuffConfigId.IsValid)
            {
                return;
            }
            owner.BuffHandler.Remove(ReadyBuffConfigId);
        }
    }
}
