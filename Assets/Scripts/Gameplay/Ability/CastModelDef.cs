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
        public abstract byte? ResolveIndicatorStage(byte currentStageKey);
        public abstract bool TryInterrupt(byte currentStageKey);
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
}
