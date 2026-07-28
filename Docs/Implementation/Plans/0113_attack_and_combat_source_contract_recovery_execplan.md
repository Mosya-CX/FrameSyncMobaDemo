# ExecPlan 0113: Attack and Combat source-contract recovery

> Status: Complete.
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 4.
> Design conformance: Strict -- no deviation.
> Estimated production/test change: 550-950 lines.

## Purpose and observable production behavior

Normal attacks use the formal Attack v6.2 lifecycle and Combat v13.2 source
contract. Beginning an attack consumes no animation sequence. Only a successful
direct Combat request or Projectile spawn advances the byte sequence. Invalid,
canceled and failed attacks consume no sequence. Snapshot/restore preserves the
complete windup/recovery state, and Combat derives attack/on-hit semantics from
`SourceDescriptor.SourceType`, never from an animation counter.

## Progress

- [x] Read the current Attack v6.2 and Combat v13.2 designs.
- [x] Inspect AttackHandler, snapshots, pipeline, Combat requests and tests.
- [x] Freeze the global attack-sequence reset interval.
- [x] Add the formal Combat source/header contracts without duplicates.
- [x] Implement the formal Attack lifecycle and protected extension boundary.
- [x] Update Planner/pipeline callers and Combat source-derived on-hit routing.
- [x] Align Attack snapshot/checksum and focused deterministic tests.
- [ ] Compile and validate with Unity MCP.

## Surprises and discoveries

- Current `AttackSequenceIndex` increments at Begin, so a canceled windup
  consumes presentation identity and contradicts the formal lifecycle.
- `DamageRequest.AttackSequenceIndex` is being used as an on-hit source tag,
  even though Attack v6.2 explicitly forbids writing it into Combat requests.
- Current `CombatRequestHeader` contains only ordering data and omits the
  formal source, target, recipe and source descriptor.
- `AttackHandler.TickUpdate` can mark a failed Commit as committed and emit SFX
  before validating that Gameplay output succeeded.
- Recovery progress is unreachable because `IsAttacking` becomes false as soon
  as `ImpactCommitted` becomes true.

## Current repository context

- Unit and all Handlers remain prefab-authored MonoBehaviours.
- `UnitWorld.CombatSystem`, `UnitWorld.ProjectileWorld` and the existing
  `RangeQueryService` are already the composition seams.
- `ProjectileWorld.RequestSpawn` returns an invalid UID on rejected static or
  runtime input, which is sufficient to define successful ranged output.
- GameplaySnapshot schema 6 currently writes the shorter AttackSnapshot.
- The user's open `ClientBootstrap` scene remains dirty and must not be saved or
  discarded for test execution.

## Exact design sources

- `Docs/Design/moba_attack_module_design_v6_2.md`, sections 2-6 and 7.2.
- `Docs/Design/moba_combat_system_design_v13_2.md`, sections 2.5, 3, 4 and 7.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`, behavior/action and
  Handler lifecycle boundaries.
- `Docs/Architecture/DECISION_LOG.md`, deterministic timing and presentation
  ownership decisions.

## In scope

- Frozen global `AttackSequenceResetIntervalTicks`.
- Formal Attack plan/begin/commit/cancel/reset lifecycle.
- Complete AttackSnapshot and checksum coverage.
- Formal `CombatRequestHeader`, `SourceDescriptor`, source-type and built-in
  stable IDs required by neutral basic attacks.
- Removal of animation sequence from DamageRequest/Combat events.
- Direct/ranged success handling and focused EditMode tests.

## Out of scope

Projectile hit-module settlement and filters (Gate 5), full Recipe registry and
formula expansion, attack-specific production content, Ability authoring,
movement-radius correction, Presentation dispatcher rollback and network flow.

## Affected assemblies and exact production types

- RuntimeConfig: `GlobalGameplayData`, `BakedGlobalGameplayData`.
- Unit/Attack: `AttackHandler`, `AttackSnapshot`, `BehaviorPlanner`.
- Unit/Combat: `CombatRequestHeader`, `DamageRequest`, `CombatSystem`,
  `CombatEvents`.
- FrameSync: `SimulationTickPipeline`, `SharedGameplayChecksum`,
  `FrameSyncGameRuntime`.
- Tests: `AttackHandlerTests` and focused source-contract tests.

## Public contracts

Reuse the sole `UnitUid`, `ProjectileUid`, `DamageRequest` and
`PresentationEventId`. Add the design-owned `CombatSourceType`,
`SourceDescriptor`, built-in source/recipe IDs, `AttackPlanStatus` and
`AttackTimerResetReason`. Do not add `AttackSequenceId`,
`AttackSourceContext`, `AttackKind`, a second DamageRequest or a second source
DTO.

## Ownership and dependency direction

```text
RuntimeConfig -> frozen reset interval
Planner -> AttackPlanStatus
AttackHandler -> CombatSystem or ProjectileWorld
CombatSystem -> source-derived settlement/reactions
Presentation <- read-only AttackSnapshot + PresentationEventId
```

Combat never writes Attack state. Presentation never writes Gameplay.

## Deterministic ordering

Attack timing reads only `SimulationTickContext.Current` and fixed-point stats.
Each Unit may Begin at most once per Tick. Combat assigns the existing unified
`SequenceInTick`. Animation sequence is a wrapping byte advanced only after
successful Gameplay output; lazy reset occurs only immediately before Begin.

## Snapshot and serialization impact

AttackSnapshot gains `IsEmpoweredAttack`, `LastSuccessfulAttackLogicTick`,
`ResolvedAttackDurationTicks` and `ResolvedWindupTicks`. GameplaySnapshot
schema increments from 6 to 7 in the same change, and shared checksum writes all
new future/presentation-relevant Gameplay state. Combat active requests remain
Tick-local; deferred DamageRequest snapshots use the formal header.

## Implementation steps

1. Bake and validate the global sequence reset interval.
2. Complete the formal Combat header/source contract and migrate DamageRequest.
3. Implement Attack plan validation, Begin, Commit, Cancel and Reset.
4. Emit direct basic-attack requests with fixed built-in source/recipe IDs.
5. Treat only a valid Projectile UID as successful ranged output.
6. Advance sequence and emit SFX only after output succeeds.
7. Update Planner/pipeline callers and source-derived on-hit routing.
8. Update snapshot/checksum/schema and focused tests.

## EditMode tests

- Begin does not advance; successful Commit advances exactly once.
- Cancel/invalid target/out-of-range/failed output consumes no sequence or SFX.
- 255 wraps to 0; lazy idle reset happens before Begin only.
- Direct request has Attack source, built-in source/recipe and no animation
  sequence payload.
- Combat on-hit eligibility uses `SourceType=Attack`, including sequence zero.
- Windup and recovery capture/restore preserve timing and animation progress.

No PlayMode test is required because this slice changes deterministic logic and
read-only presentation data, not Animator or GameObject lifecycle.

## Unity MCP validation

Refresh/compile and inspect Console. Run focused tests only if the dirty-scene
precondition allows it; otherwise execute the same deterministic scenarios
through MCP dynamic C# without saving or discarding the scene.

## Failure conditions

Stop this gate only if current formal designs disagree on the source/header
fields, if source migration requires a second protocol, or if the basic attack
cannot reach the existing Combat/Projectile owners without reversing assembly
dependencies.

## Completion criteria

- Formal sequence, cancel, failure, wrap and lazy-reset behavior is present.
- Direct attacks use the formal source/header contract.
- Attack animation identity is absent from Combat payloads.
- Complete Attack state round-trips and affects checksum.
- Unity compiles with no new errors; focused validation evidence is recorded.
- No production content or scope-external refactor is added.

## Production-content exclusion

Only neutral test units and built-in basic-attack semantics are used. No hero,
specific ability, Buff, equipment, projectile content, balance or final asset is
implemented.

## Decision log

- Pending.

## Results

Complete. Unity compilation passed. Seven focused MCP behavior checks passed:
begin/cancel, one-shot impact, failed output, byte wrap, lazy reset,
snapshot timing and invalid-reference rejection. Test Runner was not opened
because the user-owned scene remains dirty.
