using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Per-tick orchestrator: gathers RVOInput from all active locomotion agents,
    /// runs DeterministicRVOSystem, and dispatches results back to MovementHandlers.
    /// (Pathfinding Design v13.1 section 10.6)
    /// </summary>
    public static class RvoOrchestrator
    {
        /// <summary>
        /// Execute one RVO step for all active agents with AllowRVO == true.
        /// Must be called after all LocomotionAgent.Evaluate() and
        /// before MovementHandler.TickUpdate() in the per-Tick loop.
        /// </summary>
        public static void Step(
            DeterministicRVOSystem rvoSystem,
            IReadOnlyList<UnitLocomotionAgent> agents,
            IReadOnlyList<MovementHandler> handlers)
        {
            if (rvoSystem == null || agents == null || handlers == null)
                return;

            if (agents.Count != handlers.Count)
                return;

            int count = agents.Count;

            // Phase 1: gather RVO inputs from agents with AllowRVO
            var inputs = new List<RVOInput>(count);
            var indexMap = new int[count]; // handlerIndex -> inputIndex (-1 if skipped)
            for (int i = 0; i < count; i++)
                indexMap[i] = -1;

            for (int i = 0; i < count; i++)
            {
                var agent = agents[i];
                var handler = handlers[i];

                // Only include units with active AllowRVO locomotion
                var task = agent.CurrentTask;
                if (task.State != MovementTaskState.Active || !task.AllowRVO)
                    continue;

                var snap = handler.Snapshot;
                if (snap.MoveSpeed <= fp.zero)
                    continue;

                // Compute desired velocity from current handler state + agent position
                var pos = agent.Position;

                inputs.Add(new RVOInput
                {
                    SelfUid = agent.Owner.UnitUid,
                    Position = pos,
                    DesiredVelocity = snap.TargetDirection * snap.MoveSpeed,
                    Radius = (fp)0.5m,
                    MaxSpeed = snap.MoveSpeed,
                });
                indexMap[i] = inputs.Count - 1;
            }

            if (inputs.Count == 0)
                return;

            // Phase 2: run RVO solver
            RvoResult[] results = rvoSystem.Step(inputs.ToArray());

            // Phase 3: dispatch results back to handlers
            for (int i = 0; i < count; i++)
            {
                int inputIdx = indexMap[i];
                if (inputIdx < 0 || inputIdx >= results.Length)
                    continue;

                handlers[i].ApplyRvoResult(results[inputIdx]);
            }
        }
    }
}
