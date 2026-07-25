using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class NonHeroRestoreHelper
    {
        private readonly UnitWorld _unitWorld;
        private readonly MinionSystem _minionSystem;
        private readonly JungleCampSystem _jungleCampSystem;
        private readonly List<UnitAIControllerSnapshot> _aiSnapshotBuffer = new List<UnitAIControllerSnapshot>();
        private readonly List<JungleCampSnapshot> _campSnapshotBuffer = new List<JungleCampSnapshot>();

        public NonHeroRestoreHelper(
            UnitWorld unitWorld,
            MinionSystem minionSystem,
            JungleCampSystem jungleCampSystem)
        {
            _unitWorld = unitWorld;
            _minionSystem = minionSystem;
            _jungleCampSystem = jungleCampSystem;
        }

        public void CaptureNonHero(ref NonHeroWorldSnapshot state)
        {
            state = NonHeroWorldSnapshot.CreateEmpty();
            _minionSystem?.Capture(ref state.MinionSystemState);

            _campSnapshotBuffer.Clear();
            _jungleCampSystem?.Capture(_campSnapshotBuffer);
            state.JungleCampStates = _campSnapshotBuffer.ToArray();

            _aiSnapshotBuffer.Clear();
            var aiControllers = _unitWorld.AIControllers;
            foreach (var ai in aiControllers)
            {
                var snap = new UnitAIControllerSnapshot();
                ai.Capture(ref snap);
                _aiSnapshotBuffer.Add(snap);
            }
            state.AIControllerStates = _aiSnapshotBuffer.ToArray();
        }

        public void RestoreNonHero(in NonHeroWorldSnapshot state)
        {
            _minionSystem?.Restore(state.MinionSystemState);
            _jungleCampSystem?.Restore(new List<JungleCampSnapshot>(state.JungleCampStates));

            RestoreAIControllers(new List<UnitAIControllerSnapshot>(state.AIControllerStates));
        }

        public void ResolveNonHero(in RollbackContext context)
        {
            _minionSystem?.Resolve(context);
            _jungleCampSystem?.Resolve(context);

            var aiControllers = _unitWorld.AIControllers;
            foreach (var ai in aiControllers)
            {
                ai.Resolve(context);
            }
        }

        public void RebuildNonHero(in RollbackContext context)
        {
            _minionSystem?.Rebuild(context);
            _jungleCampSystem?.Rebuild(context);

            var aiControllers = _unitWorld.AIControllers;
            foreach (var ai in aiControllers)
            {
                ai.Rebuild(context);
            }
        }

        private void RestoreAIControllers(List<UnitAIControllerSnapshot> states)
        {
            states ??= new List<UnitAIControllerSnapshot>();
            UnitUid previousUid = default;
            for (int i = 0; i < states.Count; i++)
            {
                UnitAIControllerSnapshot state = states[i];
                if (!state.OwnerUnitUid.IsValid() ||
                    (i > 0 && previousUid.CompareTo(state.OwnerUnitUid) >= 0))
                {
                    throw new DeterministicSimulationException(
                        "AI snapshots must contain unique, strictly increasing owner UnitUid values.");
                }
                previousUid = state.OwnerUnitUid;
            }

            _unitWorld.ClearAIControllersForRestore();
            for (int i = 0; i < states.Count; i++)
            {
                UnitAIControllerSnapshot state = states[i];
                UnitAIController controller = _unitWorld.ReconstructAIController(state);
                controller.Restore(state);
                _unitWorld.RegisterAIController(controller);
            }
        }
    }
}
