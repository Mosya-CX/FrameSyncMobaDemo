namespace FrameSyncMoba.Unit
{
    public abstract class StageDef
    {
        public int StageDefId;
        public string DebugName;
        public virtual StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
            => StageResult.Running;
        public virtual StageResult OnTick(AbilitySession session, AbilityRuntime runtime)
            => StageResult.Running;
        public virtual StageResult OnSignal(AbilitySession session, AbilityRuntime runtime,
            AbilitySignal signal) => StageResult.Running;
        public virtual void OnExit(AbilitySession session, AbilityRuntime runtime) { }

        protected static OriginActionId BuildOriginActionId(
            AbilitySession session,
            AbilityRuntime runtime,
            Unit caster)
        {
            if (session == null ||
                runtime?.Definition == null ||
                caster == null)
            {
                return OriginActionId.Invalid;
            }
            return new OriginActionId(
                caster.GameplayParticipantId,
                CombatSourceType.Ability,
                runtime.Definition.AbilityId,
                session.StartLogicTick,
                session.SessionUid);
        }
    }

    public enum StageResult : byte
    {
        Running = 0,
        Completed = 1,
        Failed = 2,
    }
}
