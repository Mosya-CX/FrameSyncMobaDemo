using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class OnKillStatBuffEffect : BuffEffect
    {
        public StatId StatId = StatId.AttackDamage;
        public StatModifierOperation Operation = StatModifierOperation.FlatAdd;
        public fp ValuePerStack = (fp)5;
        public int MaxStacks = 5;
        public int DurationTicks = 300;

        private const string StackCountKey = "_onkill_stacks";
        private const string HandlePrefix = "_onkill_handle_";

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            runtime.Blackboard.SetNumber(StackCountKey, fp.zero);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner)
        {
            fp countFp = runtime.Blackboard.GetNumberOrDefault(StackCountKey);
            int stacks = (int)countFp;
            for (int i = 0; i < stacks; i++)
            {
                string key = HandlePrefix + i.ToString();
                if (runtime.Blackboard.TryGetStatHandle(key, out var handle) && handle.IsValid)
                {
                    owner?.StatHandler?.RemoveModifier(handle);
                }
            }
            runtime.Blackboard.SetNumber(StackCountKey, fp.zero);
        }

        public override void OnUnitKill(BuffRuntime runtime, Unit owner, Unit victim)
        {
            if (owner?.StatHandler == null || ValuePerStack <= fp.zero)
                return;

            fp countFp = runtime.Blackboard.GetNumberOrDefault(StackCountKey);
            int stacks = (int)countFp;
            if (stacks >= MaxStacks)
                return;

            var handle = owner.StatHandler.AddModifier(StatId, Operation, ValuePerStack);
            runtime.Blackboard.SetStatHandle(HandlePrefix + stacks.ToString(), handle);
            runtime.Blackboard.SetNumber(StackCountKey, (fp)(stacks + 1));
        }

        public override void ClearForDeath(BuffRuntime runtime, Unit owner)
        {
            fp countFp = runtime.Blackboard.GetNumberOrDefault(StackCountKey);
            int stacks = (int)countFp;
            for (int i = 0; i < stacks; i++)
            {
                string key = HandlePrefix + i.ToString();
                if (runtime.Blackboard.TryGetStatHandle(key, out var handle) && handle.IsValid)
                {
                    owner?.StatHandler?.RemoveModifier(handle);
                    runtime.Blackboard.SetStatHandle(key, default);
                }
            }
            runtime.Blackboard.SetNumber(StackCountKey, fp.zero);
        }

        public override void ClearForRespawn(BuffRuntime runtime, Unit owner)
        {
            runtime.Blackboard.SetNumber(StackCountKey, fp.zero);
        }
    }
}
