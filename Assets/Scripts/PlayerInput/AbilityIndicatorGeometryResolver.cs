using FrameSyncMoba.Unit;

namespace FrameSyncMoba.PlayerInput
{
    /// <summary>
    /// Resolves presentation geometry from the currently active formal
    /// ability stage. The result is read-only and never participates in
    /// deterministic simulation.
    /// </summary>
    public static class AbilityIndicatorGeometryResolver
    {
        public static bool TryResolveDirectionalZone(
            AbilityHandler handler,
            byte slot,
            out DirectionalMultiZoneDamageStageDef zone)
        {
            zone = null;
            AbilityRuntime runtime =
                handler?.GetActiveRuntime(slot);
            CastModelDef model =
                runtime?.Definition?.CastModel;
            if (model == null)
            {
                return false;
            }

            byte currentStageKey =
                runtime.ActiveSession != null
                    ? runtime.ActiveSession.CurrentStageKey
                    : byte.MaxValue;
            byte? indicatorStageKey =
                model.ResolveIndicatorStage(currentStageKey);
            if (!indicatorStageKey.HasValue)
            {
                return false;
            }

            CastStage? stage =
                model.GetStage(indicatorStageKey.Value);
            zone = stage?.Def as
                DirectionalMultiZoneDamageStageDef;
            return zone != null;
        }
    }
}
