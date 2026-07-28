# ExecPlan 0116: Movement, forced move, Dash and RVO conformance

> Status: Complete.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 7.
> Executed: 2026-07-28.

## Purpose and observable behavior

Make `PhysicsEntity2D` the only owner of Unit pose, use the configured physics
radius throughout movement, and execute exactly one of ForcedMove, Dash,
RouteMove or Idle per Tick. Idle Units remain RVO obstacles.

## Design sources

- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`
  sections 10, 11, 14 and 15.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`.
- `Docs/Design/moba_crowd_control_system_design_v6_2.md`.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`.

## Scope and ownership

- `PhysicsEntity2D`: position, forward, shape and radius authority.
- `MovementHandler`: Dash/ForcedMove trajectory state and final pose commits.
- `CrowdControlHandler`: unique forced-move control and priority arbitration.
- `UnitLocomotionAgent`: route/path state.
- `PhysicsWorld.RvoGrid`: pre-move stable neighbor candidates.

No production content, package, asmdef, UID, Command, Aim, AbilitySignal,
FixedPoint or parallel runtime protocol was added.

## Implementation and decisions

- Added resolved Dash/ForcedMove trajectory state with positive-duration and
  source-handle validation.
- Enforced ForcedMove > spawn gate > Dash > RouteMove > Idle, one branch per
  Tick.
- Equal-priority forced controls replace atomically; movement completion removes
  only the owning control handle.
- Movement, wall correction, pathfinding and RVO derive radius/class from the
  Physics shape.
- RVO reads the pre-move grid, includes idle agents and selects neighbors in
  stable UID order with reused buffers.
- `MovementSnapshot` now contains only Dash and ForcedMove. Position/forward are
  restored by Physics; path state is restored by locomotion. Snapshot schema is
  11.

## Validation results

- Unity MCP script compilation: passed, 0 errors.
- `MovementHandlerTests`: 18 passed.
- `MovementConformanceTests`: 7 passed.
- `SnapshotChecksumCompletenessTests`: 4 passed.
- PlayMode: not required; this slice changes deterministic Gameplay behavior,
  not scene, input or presentation lifecycle.

## Results

Gate 7 is complete. The review found one in-scope P1: MovementSnapshot duplicated
Physics pose and obsolete path fields. It was removed and the aggregate
snapshot/checksum schema was updated. No remaining Gate 7 P0/P1 is known.
