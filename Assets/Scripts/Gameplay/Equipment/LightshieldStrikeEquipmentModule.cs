using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Sundered Sky's "Lightshield Strike" passive (equipment passive, NOT an
    /// on-hit effect): while the item is equipped it upgrades the damage
    /// recipe of the next basic attack against an eligible enemy hero into an
    /// empowered strike (Combat v13.2 4.2/7.7). The upgrade is expressed as
    /// a permanent CombatModifierRecord gated on the empowered recipe -
    /// ForceCrit plus a CoreValue multiplier of EmpoweredDamageMultiplier
    /// (160%), i.e. the attack damage is multiplied by 1.6 first and then
    /// critically strikes for the default crit multiplier (2x) - damage
    /// settlement resolves the heal and per-target cooldown.
    ///
    /// Per-enemy-hero cooldown uses the lightweight Unit tag system: the
    /// target carries a tag keyed by the item owner so each enemy hero has
    /// an independent 10s window (like the Varus R spread markers).
    /// </summary>
    [System.Serializable]
    public sealed class LightshieldStrikeEquipmentModule :
        EquipmentEffectModule,
        IEmpoweredAttackProvider
    {
        private const ulong RecordIdBase =
            0x5353_0000_0000_0000UL;

        /// <summary>Recipe used by the empowered strike.</summary>
        public int EmpoweredRecipeId =
            CombatBuiltinRecipeId.EmpoweredAttackDamage;

        /// <summary>
        /// Multiplier applied to the attack damage request BEFORE the
        /// guaranteed crit (1.6 = 160%). Final damage = attack * 1.6 * crit.
        /// </summary>
        public fp EmpoweredDamageMultiplier =
            (fp)1.6m;

        /// <summary>Per-target cooldown in Ticks (300 = 10s at 30tps).</summary>
        public int CooldownTicks = 300;

        /// <summary>Heal base-AD ratio for melee strikes.</summary>
        public fp MeleeBaseAttackDamageHealRatio =
            (fp)0.9m;

        /// <summary>Heal base-AD ratio for ranged strikes.</summary>
        public fp RangedBaseAttackDamageHealRatio =
            (fp)0.45m;

        /// <summary>Heal ratio of the owner's missing health.</summary>
        public fp MissingHealthHealRatio =
            (fp)0.04m;

        /// <summary>Overheal -> temporary bonus max-health buff.</summary>
        public BuffConfigId OverhealBuffConfigId;

        /// <summary>Fp slot that receives the overheal amount.</summary>
        public BuffStateSlotId OverhealValueSlot =
            new BuffStateSlotId(1);

        /// <summary>Handle slot used by the overheal buff effect.</summary>
        public BuffStateSlotId OverhealHandleSlot =
            new BuffStateSlotId(2);

        /// <summary>
        /// Prefix of the per-target cooldown tag key; the owner UnitUid is
        /// appended so two Sundered Sky holders are independent.
        /// </summary>
        public string CooldownTagKeyPrefix =
            "SunderedSky.Cooldown";

        public LightshieldStrikeEquipmentModule()
        {
            InvokeTimings = new[]
            {
                EquipmentEffectInvokeTiming.OnEquipped,
                EquipmentEffectInvokeTiming.OnUnequipped,
                EquipmentEffectInvokeTiming.DamageDealt,
            };
        }

        int IEmpoweredAttackProvider.EmpoweredRecipeId =>
            EmpoweredRecipeId;

        public bool IsReadyForTarget(
            Unit owner,
            Unit target)
        {
            if (owner == null ||
                target == null ||
                target.UnitKind != UnitKind.Hero ||
                target.TeamId == owner.TeamId ||
                owner.LifeState != LifeState.Alive ||
                target.LifeState != LifeState.Alive)
            {
                return false;
            }
            return !target.HasTag(
                CooldownTagKey(owner));
        }

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            EquipmentInstance instance =
                context.Instance;
            if (owner == null ||
                instance?.Definition == null)
            {
                return;
            }
            switch (context.Timing)
            {
                case EquipmentEffectInvokeTiming.OnEquipped:
                    AttachRecord(owner, instance);
                    break;
                case EquipmentEffectInvokeTiming.OnUnequipped:
                    DetachRecord(owner, instance);
                    break;
                case EquipmentEffectInvokeTiming.DamageDealt:
                    ResolveStrike(
                        owner,
                        instance,
                        context.Dispatch.LastDamageDealt);
                    break;
            }
        }

        private void AttachRecord(
            Unit owner,
            EquipmentInstance instance)
        {
            if (owner.CombatModifiers == null)
            {
                return;
            }
            ulong recordId = ComputeRecordId(
                instance.Definition.Id);
            // Idempotent: a re-equip must not duplicate the record.
            owner.CombatModifiers.Detach(
                new CombatModifierHandle(
                    owner.UnitUid,
                    recordId));
            owner.CombatModifiers.Attach(
                new CombatModifierRecord
                {
                    Id = recordId,
                    Domain = CombatDomain.Damage,
                    Scope = CombatModifierScope.Outgoing,
                    Match = new CombatModifierMatch(
                        SourceTypeMask.Attack,
                        sourceId: 0,
                        EmpoweredRecipeId,
                        DamageTypeMask.None,
                        targetKinds:
                            1UL << (int)UnitKind.Hero),
                    ValuePatches = new[]
                    {
                        new CombatFormulaPatch(
                            CombatFormulaSlot.CoreValue,
                            CombatModifierOperation.Multiply,
                            new CombatOperand(
                                EmpoweredDamageMultiplier)),
                    },
                    PolicyPatches = new[]
                    {
                        new CombatPolicyPatch(
                            CombatPolicyKind.ForceCrit),
                    },
                });
        }

        private void DetachRecord(
            Unit owner,
            EquipmentInstance instance)
        {
            if (owner.CombatModifiers == null)
            {
                return;
            }
            owner.CombatModifiers.Detach(
                new CombatModifierHandle(
                    owner.UnitUid,
                    ComputeRecordId(
                        instance.Definition.Id)));
        }

        private void ResolveStrike(
            Unit owner,
            EquipmentInstance instance,
            in DamageEventData data)
        {
            if (data.Source.SourceType !=
                    CombatSourceType.Attack ||
                data.RecipeId != EmpoweredRecipeId ||
                data.ActualDamage <= fp.zero ||
                owner.World == null ||
                owner.LifeState != LifeState.Alive)
            {
                return;
            }
            if (!owner.World.TryGetUnit(
                    data.TargetUid,
                    out Unit target) ||
                target.UnitKind != UnitKind.Hero ||
                target.TeamId == owner.TeamId)
            {
                return;
            }

            int tick =
                SimulationTickContext.Current.Tick;
            // Start the independent per-enemy-hero cooldown.
            target.AddTag(
                CooldownTagKey(owner),
                CooldownTicks,
                new UnitTagUid(
                    owner.UnitUid,
                    (byte)BuffSourceType.Item,
                    instance.Definition.Id,
                    tick));

            ResolveHeal(owner, instance);
        }

        private void ResolveHeal(
            Unit owner,
            EquipmentInstance instance)
        {
            StatHandler stats = owner.StatHandler;
            if (stats == null)
            {
                return;
            }
            fp maxHp =
                stats.GetStat(StatId.MaxHealth);
            fp current =
                stats.CurrentHealth;
            fp missing = maxHp - current;
            if (missing < fp.zero)
            {
                missing = fp.zero;
            }
            // Melee/ranged is decided by the AttackRange stat, not by the
            // AttackHandler projectile id: melee heroes may still author a
            // projectile (e.g. Aatrox projectileDefId 101 with range 175).
            bool ranged = IsRanged(owner);
            fp baseRatio = ranged
                ? RangedBaseAttackDamageHealRatio
                : MeleeBaseAttackDamageHealRatio;
            // Heal scales with BASE Attack Damage only; equipment/buff bonus
            // Attack Damage (e.g. this item's +45) must not inflate it.
            fp baseAd =
                stats.GetBaseStat(StatId.AttackDamage);
            fp heal =
                baseAd * baseRatio +
                missing * MissingHealthHealRatio;
            if (heal <= fp.zero)
            {
                return;
            }

            fp overheal =
                heal > missing ? heal - missing : fp.zero;
            fp applied = heal - overheal;
            if (applied > fp.zero &&
                owner.World?.CombatSystem != null)
            {
                owner.World.CombatSystem.SubmitHeal(
                    new HealRequest
                    {
                        SourceUnitUid =
                            owner.UnitUid,
                        TargetUnitUid =
                            owner.UnitUid,
                        BaseValue = applied,
                    });
            }
            if (overheal > fp.zero)
            {
                ApplyOverhealBuff(
                    owner,
                    instance,
                    overheal);
            }
        }

        private void ApplyOverhealBuff(
            Unit owner,
            EquipmentInstance instance,
            fp amount)
        {
            if (!OverhealBuffConfigId.IsValid ||
                owner.BuffHandler == null ||
                owner.World?.BuffDefinitions == null)
            {
                return;
            }
            if (!owner.World.BuffDefinitions.TryGet(
                    OverhealBuffConfigId,
                    out BuffDefinition definition))
            {
                return;
            }
            owner.BuffHandler.Apply(
                OverhealBuffConfigId,
                definition,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Item,
                    instance.Definition.Id));
            if (OverhealValueSlot.IsValid &&
                owner.BuffHandler.TryGetRuntime(
                    OverhealBuffConfigId,
                    out BuffRuntime runtime))
            {
                runtime.Blackboard.WriteFp(
                    OverhealValueSlot,
                    amount);
            }
        }

        private string CooldownTagKey(Unit owner) =>
            CooldownTagKeyPrefix +
            "." +
            owner.UnitUid;

        private bool IsRanged(Unit owner)
        {
            fp range =
                owner?.StatHandler != null
                    ? owner.StatHandler.GetStat(
                        StatId.AttackRange)
                    : fp.zero;
            int threshold =
                owner?.World
                    ?.RangedAttackRangeThreshold ??
                275;
            return range > (fp)threshold;
        }

        private static ulong ComputeRecordId(
            int equipmentId) =>
            RecordIdBase |
            (uint)equipmentId;
    }
}
