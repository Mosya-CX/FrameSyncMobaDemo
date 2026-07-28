# ExecPlan 0111: Snapshot and shared-checksum completeness

> Status: Implemented and MCP-validated; focused Test Runner pending dirty-scene resolution.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 2.
> Design conformance: Strict -- no deviation.
> Estimated production/test change: 800-1400 lines.

## Purpose and observable behavior

Make continuous execution and Snapshot/Restore/Replay preserve every currently
integrated future-affecting Unit behavior, action reservation, movement special
state, Combat modifier and locomotion state. Any difference in shared state must
change `SharedGameplayChecksum`.

## Progress

- [x] Reconfirm Gate 2 scope and current formal design sources.
- [x] Inventory exact current state and existing capture/restore paths.
- [x] Extend existing module snapshots without parallel DTOs.
- [x] Increment aggregate schema and reject incompatible snapshots.
- [x] Extend canonical checksum in explicit stable order.
- [x] Add focused round-trip, canonical-order and checksum-sensitivity tests.
- [x] Compile and validate through Unity MCP.

## Surprises and discoveries

- `ActionRuntimeSet` has no production `IActionRuntime` implementation and no
  production caller of `Add`; the actual Attack, Ability and movement state is
  owned by existing handlers. Capturing an interface reference would not be
  restorable, so aggregate capture now fails deterministically if this dormant
  container ever becomes non-empty.
- Forced-movement cross-Tick ownership already resides in
  `CrowdControlHandlerSnapshot` (`CrowdControlConstraint`,
  `ActiveForcedMoveHandle`, and per-Tick forced delta). Adding another movement
  copy would create conflicting ownership.
- `CombatModifierSet.Detach` used `RemoveAt` while updating only one index as if
  it had swap-removed. Detaching a later shifted handle could address an invalid
  index.
- Locomotion stored follower state both in `RouteRuntime` and
  `LocomotionAgentSnapshot`; capture did not keep the two copies equal.

## Decision log

- Snapshot the existing authoritative `UnitIntent`; do not add a second behavior
  DTO.
- Keep dormant `ActionRuntimeSet` out of the protocol until a concrete,
  restorable action runtime exists; enforce the boundary with a capture
  assertion.
- Treat CrowdControl as the sole current owner of persistent forced movement.
- Canonicalize Combat modifiers by strictly increasing `ModifierId`, and require
  the duplicated locomotion follower fields to agree during restore.
- Increment `GameplaySnapshot.SchemaVersion` from 4 to 5.

## Exact design sources

- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`, sections 5-6.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`, behavior/action,
  locomotion, snapshot and restore sections.
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md`, Dash and
  ForcedMove runtime state.
- `Docs/Architecture/DECISION_LOG.md`, D-003/D-004 and D-010 through D-013.

## Scope

In scope: existing `UnitSnapshot`, `MovementSnapshot`, behavior/action runtime
state, locomotion state, CombatModifier state, aggregate schema/version,
canonical serialization and focused tests.

Out of scope: Command Ability verb and TargetTick, Attack semantics, Projectile
Combat routing, new gameplay capabilities, production content and packages.

## Affected assemblies and ownership

- Unit owns module state and module snapshot records.
- FrameSync owns aggregate capture, three-phase restore, schema and shared checksum.
- Dependency remains Unit -> FrameSync; Unit does not reference aggregate types.

## Deterministic ordering

UnitUid order, action slot/kind order, modifier stable sequence order and
locomotion-owned stable path order are written explicitly. Dictionary/HashSet or
component registration order may not affect canonical output.

## Snapshot and serialization impact

`GameplaySnapshot.SchemaVersion` increments. Old schemas fail deterministically.
Restore, Resolve and Rebuild stay separate. Static definitions and presentation
state remain excluded.

## Implementation steps

1. Compare each live field with current module snapshots.
2. Add only future-affecting missing members to existing owned snapshots.
3. Capture/restore Planner/Intent/Action and movement special state.
4. Include CombatModifier and locomotion state in canonical checksum.
5. Update aggregate schema checks and invalid-reference failures.
6. Add round-trip, replay equivalence, insertion-order and checksum tests.

## Validation and completion

Unity MCP refresh/compile and Console inspection are mandatory. Run the smallest
relevant EditMode classes when the dirty-scene precondition permits; otherwise
record the refusal and execute direct MCP deterministic validation. No PlayMode
test is required because this slice changes pure deterministic state only.

Complete when continuous/replay equality and checksum sensitivity cover all
added state, compilation is clean, and status/parent plans are current.

## Failure conditions

Stop this slice for a conflict between current formal snapshot membership and a
current module design, or if correction requires changing a Gate 3+ protocol.

## Results

Implemented on 2026-07-26.

- `UnitSnapshot` now captures/restores/resolves `UnitIntent`.
- `MovementSnapshot` now owns active Dash direction, speed, remaining distance
  and end Tick.
- Locomotion capture deep-copies route/follower arrays, canonicalizes duplicate
  follower fields, validates topology and resolves target Unit references.
- Combat modifiers capture in stable ID order, restore validates arrays and
  IDs, and shifted indices after detach are repaired.
- `SharedGameplayChecksum` now writes Intent, CombatModifier, Dash and complete
  Locomotion state in explicit order.
- Added `SnapshotChecksumCompletenessTests` for aggregate round trip, checksum
  sensitivity, insertion-order independence, shifted detach indices and the
  live-action boundary.
- Unity MCP refresh/compile completed with no Console errors. Direct MCP
  validation used the real neutral catalog/prefab assets, spawned two Units,
  restored schema 5 state and observed checksum `599551440` change to
  `413954508` when Intent was removed.
- The focused Test Runner was not re-invoked because the pre-existing
  `ClientBootstrap.unity` scene remains dirty and the runner is known to refuse
  that state. No PlayMode validation is required for this deterministic slice.
