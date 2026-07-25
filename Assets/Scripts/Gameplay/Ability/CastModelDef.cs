using FrameSyncMoba.Deterministic;

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
        public bool IsValid => Def != null && DurationTicks >= 0;
    }

    public abstract class CastModelDef
    {
        public CastModelKind Kind { get; protected set; }
        public abstract int? HandleSignal(AbilitySignal signal, byte currentStageKey);
        public abstract byte? ResolveIndicatorStage(byte currentStageKey);
        public abstract bool TryInterrupt(byte currentStageKey);
    }

    public sealed class CommitCastModelDef : CastModelDef
    {
        public CastStage Cast;
        public CommitCastModelDef() { Kind = CastModelKind.Commit; }
        public override int? HandleSignal(AbilitySignal signal, byte currentStageKey)
        {
            if (signal.Verb == AbilitySignalVerb.Commit) return 0;
            return null;
        }
        public override byte? ResolveIndicatorStage(byte currentStageKey) => Cast.StageKey;
        public override bool TryInterrupt(byte currentStageKey) => Cast.Interruptible;
    }

    public sealed class HoldReleaseCastModelDef : CastModelDef
    {
        public CastStage Hold;
        public CastStage Release;
        public HoldReleaseCastModelDef() { Kind = CastModelKind.HoldRelease; }
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

    public sealed class ChannelCastModelDef : CastModelDef
    {
        public CastStage Channel;
        public ChannelCastModelDef() { Kind = CastModelKind.Channel; }
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
