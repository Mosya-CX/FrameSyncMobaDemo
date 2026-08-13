using FrameSyncMoba.Deterministic;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum CastModelKind : byte
    {
        Commit = 0,
        HoldRelease = 1,
        Channel = 2,
        ActiveSignal = 3,
        Toggle = 4,
        GroundTarget = 5,
        VectorTarget = 6,
        SequentialRecast = 7,
    }

    public struct CastStage
    {
        public byte StageKey;
        public StageDef Def;
        public int DurationTicks;
        public bool NotifyAbilityCastOnEnter;
        public bool Interruptible;
        /// <summary>
        /// When true, the caster cannot issue voluntary Move / Attack while
        /// this cast stage is active (cast windup lock). Movable-cast stages
        /// (e.g. a charge Hold) set this to false.
        /// </summary>
        public bool LockMovement;
        /// <summary>Per-cast-stage UI icon override (design v15.2).
        /// Serialized asset reference; never created at runtime.</summary>
        public Sprite IconOverride;
        public bool IsValid => Def != null && DurationTicks >= 0;
    }

    public abstract class CastModelDef
    {
        public CastModelKind Kind { get; protected set; }
        public abstract CastStage? GetStage(byte stageKey);
        public abstract int? HandleSignal(AbilitySignal signal, byte currentStageKey);

        public virtual bool CanHandleSignal(
            AbilitySignal signal,
            byte currentStageKey,
            int stageElapsedTicks) => true;
        public abstract byte? ResolveIndicatorStage(byte currentStageKey);
        public abstract bool TryInterrupt(byte currentStageKey);

        /// <summary>
        /// Resolves the deterministic transition produced by a completed or
        /// timed-out stage. A null result ends the session and starts its
        /// normal cooldown. Existing cast models preserve their historical
        /// completion-as-Commit behavior; models with explicit recast
        /// windows override this method so a timeout never invents input.
        /// </summary>
        public virtual int? ResolveStageEnd(
            byte currentStageKey,
            bool timedOut)
        {
            return HandleSignal(
                new AbilitySignal
                {
                    Verb = AbilitySignalVerb.Commit,
                },
                currentStageKey);
        }
    }

    public sealed class CommitCastModelDef : CastModelDef
    {
        public CastStage Cast;
        public CommitCastModelDef() { Kind = CastModelKind.Commit; }
        public override CastStage? GetStage(byte stageKey)
            => stageKey == Cast.StageKey ? Cast : (CastStage?)null;
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            if (signal.Verb == AbilitySignalVerb.Commit)
                return Cast.StageKey;
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Cast.StageKey;
        public override bool TryInterrupt(byte currentStageKey) => Cast.Interruptible;
    }

    public sealed class HoldReleaseCastModelDef : CastModelDef
    {
        public CastStage Hold;
        public CastStage Release;
        /// <summary>What happens when the Hold stage reaches its duration
        /// (design v15.2 3.10): auto-release or cancel.</summary>
        public HoldTimeoutPolicy HoldTimeoutPolicy =
            HoldTimeoutPolicy.AutoRelease;
        /// <summary>Fraction of the already-paid cost refunded when the hold
        /// times out and the policy is Cancel (0.5 = half).</summary>
        public Unity.Mathematics.FixedPoint.fp
            RefundCostPercentOnTimeout;
        public HoldReleaseCastModelDef() { Kind = CastModelKind.HoldRelease; }
        public override CastStage? GetStage(byte stageKey)
        {
            if (stageKey == Hold.StageKey) return Hold;
            if (stageKey == Release.StageKey) return Release;
            return null;
        }
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            switch (signal.Verb)
            {
                case AbilitySignalVerb.Focus: return Hold.StageKey;
                case AbilitySignalVerb.Commit:
                    if (currentStageKey == Hold.StageKey) return Release.StageKey;
                    break;
            }
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Release.StageKey;
        public override bool TryInterrupt(byte currentStageKey)
            => (currentStageKey == Hold.StageKey) ? Hold.Interruptible : Release.Interruptible;
    }

    public enum HoldTimeoutPolicy : byte
    {
        AutoRelease = 0,
        Cancel = 1,
    }

    public sealed class ChannelCastModelDef : CastModelDef
    {
        public CastStage Channel;
        public ChannelCastModelDef() { Kind = CastModelKind.Channel; }
        public override CastStage? GetStage(byte stageKey)
            => stageKey == Channel.StageKey ? Channel : (CastStage?)null;
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            if (signal.Verb == AbilitySignalVerb.Commit) return Channel.StageKey;
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Channel.StageKey;
        public override bool TryInterrupt(byte currentStageKey) => Channel.Interruptible;
    }

    public sealed class ActiveSignalCastModelDef : CastModelDef
    {
        public CastStage Active;
        public ActiveSignalCastModelDef() { Kind = CastModelKind.ActiveSignal; }
        public override CastStage? GetStage(byte stageKey)
            => stageKey == Active.StageKey ? Active : (CastStage?)null;
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            if (signal.Verb == AbilitySignalVerb.Commit)
            {
                if (currentStageKey == byte.MaxValue) return Active.StageKey;
                return currentStageKey;
            }
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Active.StageKey;
        public override bool TryInterrupt(byte currentStageKey) => Active.Interruptible;
    }

    public sealed class ToggleCastModelDef : CastModelDef
    {
        public CastStage Active;
        public Unity.Mathematics.FixedPoint.fp ResourcePerTick;
        public ToggleCastModelDef() { Kind = CastModelKind.Toggle; }
        public override CastStage? GetStage(byte stageKey)
            => stageKey == Active.StageKey ? Active : (CastStage?)null;
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            if (signal.Verb == AbilitySignalVerb.Commit)
            {
                if (currentStageKey == byte.MaxValue) return Active.StageKey;
                return null;
            }
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Active.StageKey;
        public override bool TryInterrupt(byte currentStageKey) => Active.Interruptible;
    }

    public sealed class GroundTargetCastModelDef : CastModelDef
    {
        public CastStage Aim;
        public CastStage Execute;
        public Unity.Mathematics.FixedPoint.fp MaxRange;
        public Unity.Mathematics.FixedPoint.fp Radius;
        public GroundTargetCastModelDef() { Kind = CastModelKind.GroundTarget; }
        public override CastStage? GetStage(byte stageKey)
        {
            if (stageKey == Aim.StageKey) return Aim;
            if (stageKey == Execute.StageKey) return Execute;
            return null;
        }
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            switch (signal.Verb)
            {
                case AbilitySignalVerb.Focus:
                    if (currentStageKey == byte.MaxValue) return Aim.StageKey;
                    return currentStageKey;
                case AbilitySignalVerb.Commit:
                    if (currentStageKey == byte.MaxValue) return Aim.StageKey;
                    if (currentStageKey == Aim.StageKey && signal.Aim.Kind == AimKind.Point)
                        return Execute.StageKey;
                    return null;
                default: return null;
            }
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Aim.StageKey;
        public override bool TryInterrupt(byte currentStageKey)
            => (currentStageKey == Aim.StageKey) ? Aim.Interruptible : Execute.Interruptible;
    }

    public sealed class VectorTargetCastModelDef : CastModelDef
    {
        public CastStage Aim;
        public CastStage Execute;
        public Unity.Mathematics.FixedPoint.fp MaxRange;
        public Unity.Mathematics.FixedPoint.fp MinRange;
        public VectorTargetCastModelDef() { Kind = CastModelKind.VectorTarget; }
        public override CastStage? GetStage(byte stageKey)
        {
            if (stageKey == Aim.StageKey) return Aim;
            if (stageKey == Execute.StageKey) return Execute;
            return null;
        }
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            switch (signal.Verb)
            {
                case AbilitySignalVerb.Focus:
                    if (currentStageKey == byte.MaxValue) return Aim.StageKey;
                    return currentStageKey;
                case AbilitySignalVerb.Commit:
                    if (currentStageKey == byte.MaxValue) return Aim.StageKey;
                    if (currentStageKey == Aim.StageKey && signal.Aim.Kind == AimKind.Direction)
                        return Execute.StageKey;
                    return null;
                default: return null;
            }
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Aim.StageKey;
        public override bool TryInterrupt(byte currentStageKey)
            => (currentStageKey == Aim.StageKey) ? Aim.Interruptible : Execute.Interruptible;
    }

    /// <summary>
    /// Reusable three-impact cast model separated by two finite recast
    /// windows. Impact completion advances to the matching window; only a
    /// real Commit advances a window to the next impact. A window timeout or
    /// final-impact completion ends the session.
    /// </summary>
    public sealed class SequentialRecastCastModelDef : CastModelDef
    {
        public CastStage FirstImpact;
        public CastStage FirstRecastWindow;
        public CastStage SecondImpact;
        public CastStage SecondRecastWindow;
        public CastStage FinalImpact;
        public int FirstMinimumRecastDelayTicks;
        public int SecondMinimumRecastDelayTicks;

        public SequentialRecastCastModelDef()
        {
            Kind = CastModelKind.SequentialRecast;
        }

        public override CastStage? GetStage(byte stageKey)
        {
            if (stageKey == FirstImpact.StageKey) return FirstImpact;
            if (stageKey == FirstRecastWindow.StageKey) return FirstRecastWindow;
            if (stageKey == SecondImpact.StageKey) return SecondImpact;
            if (stageKey == SecondRecastWindow.StageKey) return SecondRecastWindow;
            if (stageKey == FinalImpact.StageKey) return FinalImpact;
            return null;
        }

        public override int? HandleSignal(
            AbilitySignal signal,
            byte currentStageKey)
        {
            if (signal.Verb != AbilitySignalVerb.Commit)
                return null;
            if (currentStageKey == byte.MaxValue)
                return FirstImpact.StageKey;
            if (currentStageKey == FirstRecastWindow.StageKey)
                return SecondImpact.StageKey;
            if (currentStageKey == SecondRecastWindow.StageKey)
                return FinalImpact.StageKey;
            return null;
        }

        public override bool CanHandleSignal(
            AbilitySignal signal,
            byte currentStageKey,
            int stageElapsedTicks)
        {
            if (signal.Verb != AbilitySignalVerb.Commit)
                return true;
            if (currentStageKey == FirstRecastWindow.StageKey)
                return stageElapsedTicks >= FirstMinimumRecastDelayTicks;
            if (currentStageKey == SecondRecastWindow.StageKey)
                return stageElapsedTicks >= SecondMinimumRecastDelayTicks;
            return true;
        }

        public override int? ResolveStageEnd(
            byte currentStageKey,
            bool timedOut)
        {
            if (currentStageKey == FirstImpact.StageKey)
                return FirstRecastWindow.StageKey;
            if (currentStageKey == SecondImpact.StageKey)
                return SecondRecastWindow.StageKey;
            return null;
        }

        public override byte? ResolveIndicatorStage(byte currentStageKey)
        {
            if (currentStageKey == FirstRecastWindow.StageKey)
                return SecondImpact.StageKey;
            if (currentStageKey == SecondRecastWindow.StageKey)
                return FinalImpact.StageKey;
            return currentStageKey == byte.MaxValue
                ? FirstImpact.StageKey
                : currentStageKey;
        }

        public override bool TryInterrupt(byte currentStageKey)
        {
            CastStage? stage = GetStage(currentStageKey);
            return stage.HasValue && stage.Value.Interruptible;
        }
    }
}
