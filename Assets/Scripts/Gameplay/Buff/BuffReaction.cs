using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Typed ability-cast event routed to BuffHandler (design v14.2 8.3).
    /// </summary>
    public readonly struct AbilityCastEventData
    {
        public readonly UnitUid CasterUid;
        public readonly int AbilityConfigId;
        public readonly byte Slot;
        public readonly int LogicTick;

        public AbilityCastEventData(
            UnitUid casterUid,
            int abilityConfigId,
            byte slot,
            int logicTick)
        {
            CasterUid = casterUid;
            AbilityConfigId = abilityConfigId;
            Slot = slot;
            LogicTick = logicTick;
        }
    }

    /// <summary>
    /// Config-driven reaction condition (design v14.2 7.1).
    /// </summary>
    [Serializable]
    public abstract class BuffConditionConfig
    {
        public abstract bool Passes(
            BuffRuntime runtime,
            Unit owner);
    }

    [Serializable]
    public sealed class BuffAlwaysCondition :
        BuffConditionConfig
    {
        public override bool Passes(
            BuffRuntime runtime,
            Unit owner)
        {
            return true;
        }
    }

    [Serializable]
    public sealed class BuffStackAtLeastCondition :
        BuffConditionConfig
    {
        public int MinStacks = 1;

        public override bool Passes(
            BuffRuntime runtime,
            Unit owner)
        {
            return runtime.CurrentStacks >=
                MinStacks;
        }
    }

    /// <summary>
    /// Config-driven reaction action (design v14.2 7.1). Concrete actions are
    /// deterministic primitives submitted through the owning systems.
    /// </summary>
    [Serializable]
    public abstract class BuffReactionActionConfig
    {
        public abstract void Execute(
            BuffRuntime runtime,
            Unit owner);
    }

    [Serializable]
    public sealed class BuffApplyBuffActionConfig :
        BuffReactionActionConfig
    {
        public BuffConfigId BuffId;

        public override void Execute(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.BuffHandler == null ||
                !BuffId.IsValid ||
                owner.World?.BuffDefinitions == null)
                return;
            if (!owner.World.BuffDefinitions.TryGet(
                    BuffId,
                    out BuffDefinition definition))
                return;
            owner.BuffHandler.Apply(
                BuffId,
                definition,
                runtime.Source);
        }
    }

    [Serializable]
    public sealed class BuffDealDamageActionConfig :
        BuffReactionActionConfig
    {
        public fp DamageAmount;
        public DamageType DamageType =
            DamageType.Magic;

        public override void Execute(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.World?.CombatSystem == null ||
                DamageAmount <= fp.zero)
                return;
            var request = new DamageRequest
            {
                Header = CombatRequestHeader.Create(
                    runtime.SourceUnitUid,
                    owner.UnitUid,
                    CombatSourceType.Buff,
                    runtime.ConfigId.Value,
                    runtime.ConfigId.Value),
                BaseDamage = DamageAmount,
                DamageType = DamageType,
            };
            owner.World.CombatSystem
                .SubmitDamage(request);
        }
    }

    [Serializable]
    public sealed class BuffHealActionConfig :
        BuffReactionActionConfig
    {
        public fp HealAmount;

        public override void Execute(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.World?.CombatSystem == null ||
                HealAmount <= fp.zero)
                return;
            var request = new HealRequest
            {
                TargetUnitUid = owner.UnitUid,
                SourceUnitUid = runtime.SourceUnitUid,
                BaseValue = HealAmount,
            };
            owner.World.CombatSystem
                .SubmitHeal(request);
        }
    }

    [Serializable]
    public sealed class BuffGrantShieldActionConfig :
        BuffReactionActionConfig
    {
        public fp ShieldAmount;
        public int DurationTicks;

        public override void Execute(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.World?.CombatSystem == null ||
                ShieldAmount <= fp.zero)
                return;
            var request = new ShieldRequest
            {
                TargetUnitUid = owner.UnitUid,
                SourceUnitUid = runtime.SourceUnitUid,
                BaseValue = ShieldAmount,
                DurationTicks = DurationTicks,
            };
            owner.World.CombatSystem
                .SubmitShield(request);
        }
    }

    [Serializable]
    public class BuffReactionGroup
    {
        public BuffConditionConfig Condition;
        public BuffReactionActionConfig[] Actions;
    }

    [Serializable]
    public sealed class BuffStackChangedReactionGroup :
        BuffReactionGroup
    {
        public int MinStack;
        public int MaxStack = int.MaxValue;
    }

    [Serializable]
    public sealed class BuffPeriodicReactionGroup :
        BuffReactionGroup
    {
        public float IntervalSeconds;
        public bool TriggerImmediately;
        public BuffStateSlotId NextTriggerTickSlot;
    }

    [Serializable]
    public sealed class BuffLifecycleReactions
    {
        public BuffReactionGroup[] Added;
        public BuffReactionGroup[] Reapplied;
        public BuffReactionGroup[] Removed;
        public BuffStackChangedReactionGroup[]
            StackChanged;
        public BuffPeriodicReactionGroup[] Periodic;
    }

    [Serializable]
    public sealed class BuffEventReactions
    {
        public BuffReactionGroup[] DamageTaken;
        public BuffReactionGroup[] DamageDealt;
        public BuffReactionGroup[] HealTaken;
        public BuffReactionGroup[] HealDealt;
        public BuffReactionGroup[] ShieldApplied;
        public BuffReactionGroup[] AbilityCast;
        public BuffReactionGroup[] LevelUp;
        public BuffReactionGroup[] UnitDying;
        public BuffReactionGroup[] UnitDeath;
        public BuffReactionGroup[] UnitKill;
        public BuffReactionGroup[] OnHitDealt;
        public BuffReactionGroup[] CollisionEnter;
        public BuffReactionGroup[] CollisionExit;
    }
}
