namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// OnEnter: applies a buff to either the caster or the aim target,
    /// using the BuffDefinitionRegistry and BuffHandler.
    /// </summary>
    public sealed class ApplyBuffStageDef : StageDef
    {
        public BuffConfigId BuffConfigId;
        public BuffTargetRule TargetRule;

        public override StageResult OnEnter(AbilitySession session, AbilityRuntime runtime)
        {
            if (!BuffConfigId.IsValid || runtime.World?.BuffDefinitions == null)
                return StageResult.Failed;
            if (!runtime.World.TryGetUnit(runtime.CasterUnitUid, out Unit caster))
                return StageResult.Failed;

            UnitUid targetUid;
            if (TargetRule == BuffTargetRule.Self)
            {
                targetUid = runtime.CasterUnitUid;
            }
            else
            {
                targetUid = session.Aim.TargetUnitUid;
                if (!targetUid.IsValid())
                    return StageResult.Failed;
            }

            if (!runtime.World.TryGetUnit(targetUid, out Unit target))
                return StageResult.Failed;
            if (!runtime.World.BuffDefinitions.TryGet(BuffConfigId, out BuffDef definition))
                return StageResult.Failed;

            target.BuffHandler.Apply(BuffConfigId, definition, runtime.CasterUnitUid);
            return StageResult.Completed;
        }
    }

    public enum BuffTargetRule : byte
    {
        Self = 0,
        AimTarget = 1,
    }
}
