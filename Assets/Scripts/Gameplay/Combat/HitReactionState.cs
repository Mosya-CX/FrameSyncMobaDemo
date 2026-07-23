using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public enum HitReactionKind : byte
    {
        None = 0,
        Flinch = 1,
        Stagger = 2,
        Knockback = 3,
        Interrupt = 4,
    }

    public struct HitReactionState
    {
        public HitReactionKind ActiveReaction;
        public int RemainingTicks;
        public int TotalTicks;

        public bool IsActive => ActiveReaction != HitReactionKind.None && RemainingTicks > 0;

        public bool InterruptsMovement =>
            ActiveReaction == HitReactionKind.Stagger
            || ActiveReaction == HitReactionKind.Knockback
            || ActiveReaction == HitReactionKind.Interrupt;

        public bool InterruptsAbility =>
            ActiveReaction == HitReactionKind.Stagger
            || ActiveReaction == HitReactionKind.Knockback
            || ActiveReaction == HitReactionKind.Interrupt;

        public bool InterruptsAttack =>
            ActiveReaction == HitReactionKind.Stagger
            || ActiveReaction == HitReactionKind.Knockback
            || ActiveReaction == HitReactionKind.Interrupt;

        public static readonly HitReactionState None = default;

        public void Trigger(HitReactionKind kind, int durationTicks)
        {
            ActiveReaction = kind;
            RemainingTicks = durationTicks;
            TotalTicks = durationTicks;
        }

        public void TickUpdate()
        {
            if (RemainingTicks > 0)
            {
                int delta = SimulationTickContext.Current.DeltaTick;
                RemainingTicks -= delta;
                if (RemainingTicks <= 0)
                {
                    ActiveReaction = HitReactionKind.None;
                    RemainingTicks = 0;
                    TotalTicks = 0;
                }
            }
        }

        public void Clear()
        {
            ActiveReaction = HitReactionKind.None;
            RemainingTicks = 0;
            TotalTicks = 0;
        }
    }
}
