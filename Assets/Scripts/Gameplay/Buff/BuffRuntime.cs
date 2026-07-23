using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffRuntime
    {
        public BuffConfigId ConfigId { get; }
        public BuffDef Definition { get; }
        public UnitUid SourceUnitUid { get; private set; }
        public int RemainingTicks { get; private set; }
        public int CurrentStacks { get; private set; }
        public int ElapsedTicks { get; private set; }
        public RemovalReason RemovalReason { get; private set; }
        public bool IsRemoving { get; private set; }
        public bool IsPermanent => Definition != null && Definition.LifeRule == BuffLifeRule.Infinite;
        public BuffBlackboard Blackboard { get; }
        internal int PeriodicTimer => _periodicTimer;

        private readonly BuffEffect[] _effects;
        private int _periodicTimer;

        internal BuffRuntime(BuffConfigId configId, BuffDef definition, UnitUid sourceUnitUid)
        {
            ConfigId = configId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            SourceUnitUid = sourceUnitUid;
            RemainingTicks = definition.DurationTicks;
            CurrentStacks = definition.InitialStacks > 0 ? definition.InitialStacks : 1;
            Blackboard = new BuffBlackboard();

            if (definition.Effects != null && definition.Effects.Length > 0)
            {
                _effects = new BuffEffect[definition.Effects.Length];
                for (int i = 0; i < definition.Effects.Length; i++)
                {
                    _effects[i] = definition.Effects[i];
                }
            }
            else
            {
                _effects = Array.Empty<BuffEffect>();
            }
        }

        public void Tick(int deltaTicks)
        {
            if (IsRemoving) return;

            ElapsedTicks += deltaTicks;

            if (!IsPermanent)
            {
                RemainingTicks -= deltaTicks;
            }

            if (Definition.PeriodicIntervalTicks > 0)
            {
                _periodicTimer += deltaTicks;
            }
        }

        public bool ShouldExecutePeriodic()
        {
            if (Definition.PeriodicIntervalTicks <= 0) return false;
            if (_periodicTimer < Definition.PeriodicIntervalTicks) return false;
            _periodicTimer = 0;
            return true;
        }

        public bool IsExpired()
        {
            if (IsPermanent) return false;
            return RemainingTicks <= 0;
        }

        public bool IsStackExhausted()
        {
            return CurrentStacks <= 0;
        }

        public void BeginRemoval(RemovalReason reason)
        {
            if (IsRemoving) return;
            IsRemoving = true;
            RemovalReason = reason;
        }

        public void SetRemainingTicks(int ticks)
        {
            RemainingTicks = ticks;
        }

        public void SetStacks(int stacks)
        {
            CurrentStacks = stacks;
        }

        public void ReduceStacks(int count)
        {
            CurrentStacks -= count;
            if (CurrentStacks < 0) CurrentStacks = 0;
        }

        public void SetSource(UnitUid sourceUnitUid)
        {
            SourceUnitUid = sourceUnitUid;
        }

        public BuffEffect[] GetEffects()
        {
            return _effects;
        }

        internal void Restore(in BuffRuntimeSnapshot state)
        {
            if (state.ConfigId != ConfigId)
                throw new DeterministicSimulationException("Buff snapshot ConfigId mismatch.");
            SourceUnitUid = state.SourceUnitUid;
            RemainingTicks = state.RemainingTicks;
            CurrentStacks = state.CurrentStacks;
            ElapsedTicks = state.ElapsedTicks;
            _periodicTimer = state.PeriodicTimer;
            RemovalReason = state.RemovalReason;
            IsRemoving = state.IsRemoving;
            Blackboard.Restore(state.Blackboard);
        }
    }
}
