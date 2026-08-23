using System;
using FrameSyncMoba.Deterministic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Flags]
    public enum AbilityPassiveListenerMask : ushort
    {
        None = 0,
        DamageTaken = 1 << 0,
        DamageDealt = 1 << 1,
        HealTaken = 1 << 2,
        HealDealt = 1 << 3,
        UnitDying = 1 << 4,
        UnitDeath = 1 << 5,
        UnitKill = 1 << 6,
        LevelUp = 1 << 7,
        OnHitDealt = 1 << 8,
        UnitAssist = 1 << 9,
    }

    public abstract class AbilityPassiveEffectDefBase
    {
        public AbilityPassiveListenerMask ListenerMask;

        public virtual void ValidateOrThrow() { }
        public virtual void OnActivate(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual void OnDeactivate(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual void OnAbilityRankChanged(Unit owner, int level, ref AbilityPassiveRuntimeState state) { }
        public virtual void OnUnitDeath(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual void OnRespawn(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual void Rebuild(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual void OnTick(Unit owner, ref AbilityPassiveRuntimeState state) { }
        public virtual bool OnDamageTaken(Unit owner, in DamageEventData data, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnDamageDealt(Unit owner, in DamageEventData data, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnHealTaken(Unit owner, in HealEventData data, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnHealDealt(Unit owner, in HealEventData data, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnUnitDying(Unit owner, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnUnitKill(Unit owner, Unit victim, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnLevelUp(Unit owner, int previousLevel, int newLevel, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnHitDealt(Unit owner, in OnHitEventData data, ref AbilityPassiveRuntimeState state) => false;
        public virtual bool OnUnitAssist(Unit owner, Unit victim, ref AbilityPassiveRuntimeState state) => false;

        internal bool ListensTo(AbilityPassiveListenerMask eventMask) =>
            (ListenerMask & eventMask) != 0;
    }

    public abstract class ActiveAbilityPassiveEffectDef : AbilityPassiveEffectDefBase
    {
        public override void ValidateOrThrow()
        {
            ushort mask = (ushort)ListenerMask;
            if (mask != 0 && (mask & (mask - 1)) != 0)
                throw new InvalidOperationException(
                    "An active Ability passive may listen to at most one Unit event.");
        }
    }

    public abstract class PassiveAbilityEffectDef : AbilityPassiveEffectDefBase
    {
        /// <summary>
        /// True when a ready instance of this passive changes the next basic
        /// attack into an empowered attack. AttackHandler reads this semantic
        /// once at BeginAttack and locks the result into AttackSnapshot;
        /// presentation never infers empowered attacks from passive state.
        /// </summary>
        public virtual bool EmpowersBasicAttack => false;

        /// <summary>
        /// Target-sensitive eligibility evaluated only by AttackHandler while
        /// beginning a real basic attack. Content can exclude target kinds
        /// without putting hero-specific rules into AttackHandler.
        /// </summary>
        public virtual bool CanEmpowerBasicAttack(
            Unit owner,
            Unit target,
            in AbilityPassiveRuntimeState state) =>
                EmpowersBasicAttack;
    }

    public sealed class PassiveAbilityDef
    {
        public int AbilityId;
        public string Name;
        /// <summary>Stable client Addressables icon address.</summary>
        public string IconAddress;
        public PassiveAbilityEffectDef PassiveEffect;
        public int[] CooldownByUnitLevel;
        public bool IsValid => AbilityId > 0 && PassiveEffect != null;

        public int GetCooldownTicks(int unitLevel)
        {
            if (CooldownByUnitLevel == null || CooldownByUnitLevel.Length == 0) return 0;
            int index = unitLevel <= 1 ? 0 : unitLevel - 1;
            if (index >= CooldownByUnitLevel.Length) index = CooldownByUnitLevel.Length - 1;
            int value = CooldownByUnitLevel[index];
            if (value < 0)
                throw new DeterministicSimulationException(
                    $"Passive Ability {AbilityId} has a negative cooldown.");
            return value;
        }
    }

    public struct AbilityPassiveRuntimeState
    {
        public int AbilityLevel;
        public int StackCount;
        public int TriggerCount;
        public int LastTriggerLogicTick;
        public int NextReadyLogicTick;
        public UnitUid TargetUnitUid;
        public StatModifierHandle StatModifierHandle;
        public CombatModifierHandle CombatModifierHandle;
    }

    public sealed class AbilityPassiveEffectRuntime
    {
        public readonly AbilityPassiveEffectDefBase Definition;
        public AbilityPassiveRuntimeState State;

        public AbilityPassiveEffectRuntime(AbilityPassiveEffectDefBase definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Definition.ValidateOrThrow();
        }

        public void SetAbilityLevel(int level)
        {
            State.AbilityLevel = level;
        }

        public void Activate(Unit owner) => Definition.OnActivate(owner, ref State);
        public void Deactivate(Unit owner) => Definition.OnDeactivate(owner, ref State);
        public void RankChanged(Unit owner, int level) => Definition.OnAbilityRankChanged(owner, level, ref State);
        public void Death(Unit owner) => Definition.OnUnitDeath(owner, ref State);
        public void Respawn(Unit owner) => Definition.OnRespawn(owner, ref State);
        public void Rebuild(Unit owner) => Definition.Rebuild(owner, ref State);
        public void Tick(Unit owner) => Definition.OnTick(owner, ref State);
        public bool DamageTaken(Unit owner, in DamageEventData data) => Definition.ListensTo(AbilityPassiveListenerMask.DamageTaken) && Definition.OnDamageTaken(owner, data, ref State);
        public bool DamageDealt(Unit owner, in DamageEventData data) => Definition.ListensTo(AbilityPassiveListenerMask.DamageDealt) && Definition.OnDamageDealt(owner, data, ref State);
        public bool HealTaken(Unit owner, in HealEventData data) => Definition.ListensTo(AbilityPassiveListenerMask.HealTaken) && Definition.OnHealTaken(owner, data, ref State);
        public bool HealDealt(Unit owner, in HealEventData data) => Definition.ListensTo(AbilityPassiveListenerMask.HealDealt) && Definition.OnHealDealt(owner, data, ref State);
        public bool UnitDying(Unit owner) => Definition.ListensTo(AbilityPassiveListenerMask.UnitDying) && Definition.OnUnitDying(owner, ref State);
        public bool UnitKill(Unit owner, Unit victim) => Definition.ListensTo(AbilityPassiveListenerMask.UnitKill) && Definition.OnUnitKill(owner, victim, ref State);
        public bool LevelUp(Unit owner, int previousLevel, int newLevel) => Definition.ListensTo(AbilityPassiveListenerMask.LevelUp) && Definition.OnLevelUp(owner, previousLevel, newLevel, ref State);
        public bool OnHitDealt(Unit owner, in OnHitEventData data) => Definition.ListensTo(AbilityPassiveListenerMask.OnHitDealt) && Definition.OnHitDealt(owner, data, ref State);
        public bool UnitAssist(Unit owner, Unit victim) => Definition.ListensTo(AbilityPassiveListenerMask.UnitAssist) && Definition.OnUnitAssist(owner, victim, ref State);

        public void Resolve(UnitWorld world)
        {
            if (State.TargetUnitUid.IsValid() && !world.TryGetUnit(State.TargetUnitUid, out _))
                throw new DeterministicSimulationException(
                    $"Ability passive references missing Unit {State.TargetUnitUid}.");
        }
    }

    public sealed class PassiveAbilityRuntime
    {
        public readonly PassiveAbilityDef Definition;
        public readonly AbilityPassiveEffectRuntime EffectRuntime;

        public string GetCurrentIconAddress() =>
            Definition?.IconAddress;

        public PassiveAbilityRuntime(PassiveAbilityDef definition)
        {
            if (definition == null || !definition.IsValid)
                throw new ArgumentException("Fixed passive definition is invalid.", nameof(definition));
            Definition = definition;
            EffectRuntime = new AbilityPassiveEffectRuntime(definition.PassiveEffect);
        }

        public bool IsReady(int tick) => tick >= EffectRuntime.State.NextReadyLogicTick;

        public void CommitTrigger(Unit owner)
        {
            AbilityPassiveRuntimeState state = EffectRuntime.State;
            state.TriggerCount++;
            state.LastTriggerLogicTick = SimulationTickContext.Current.Tick;
            state.NextReadyLogicTick = checked(
                state.LastTriggerLogicTick + Definition.GetCooldownTicks(owner.Level));
            EffectRuntime.State = state;
        }
    }
}
