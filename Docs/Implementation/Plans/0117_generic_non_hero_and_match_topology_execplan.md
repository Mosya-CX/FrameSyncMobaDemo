# ExecPlan 0117: Generic non-hero and match topology

> Status: Complete (2026-07-28).
> Parent: `0109_design_conformance_remediation_program_execplan.md`, Gate 8.
> Estimated production/test change: 1,600-2,500 lines.

## Purpose

Provide explicit Inspector-authored lane/camp/base topology that deterministically
spawns generic minions and ordinary monsters through `UnitWorld`, registers their
existing AI controllers, survives snapshot restore, and registers exactly two
TeamBase Units for authority-confirmed match completion.

## Exact design sources

- `Docs/Design/moba_non_hero_unit_modules_design_v5.md`, sections 1-7 and 10.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`, sections 7.2-7.5.1,
  9.1-9.2.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`, section 14.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`.

## In scope

- Design-shaped Minion schedule plus explicit `LaneAuthoring` bake.
- `JungleCamp : MonoBehaviour` with stable slots, no `Update`, deterministic
  Tick lifecycle and new-generation UnitUids.
- UnitWorld-owned stable camp registration/AI dispatch/death cleanup.
- Explicit initial-spawn TeamBase roles and stable registration.
- Snapshot/checksum changes and focused EditMode/PlayMode tests where required.

## Out of scope

- Production minions/monsters/map, epic monsters, tower combat specialization,
  final balance/art, new packages or transport authority implementation.

## Ownership and dependency direction

`Bootstrap authoring -> FrameSync orchestration -> UnitWorld -> MinionSystem /
JungleCamp -> existing Unit/Planner/Handler contracts`.

`UnitWorld` alone owns Unit lifecycle and UnitUid-to-controller registration.
Each `JungleCamp` owns its scene topology/runtime state. `MatchRuleRuntime` owns
base result state. No `JungleCampSystem`, hierarchy-order inference or parallel
spawn/AI protocol remains.

## Determinism and snapshot

- Lanes: LaneId; team spawns: TeamId; camps: CampId; slots: SlotIndex.
- Minion tickets: SpawnTick, TeamId, LaneId, StableEntryIndex.
- All Inspector floats convert to `fp` once during initialization.
- Camp/minion future state and AI controller state enter aggregate snapshot and
  shared checksum; Transform references and controller references do not.
- Restore/Resolve/Rebuild remain separate and invalid references fail.

## Implementation steps

1. Replace the prohibited JungleCampSystem with scene-authored JungleCamp.
2. Add explicit LaneAuthoring runtime bake and formal wave tickets.
3. Make UnitWorld own MinionSystem, camps, stable AI ticking and death routing.
4. Wire Bootstrap serialized lanes/camps; remove the duplicate jungle config.
5. Add explicit initial TeamBase role and register bases after Tick-0 spawning.
6. Update snapshot/checksum schema and focused tests.

## Validation and completion

- EditMode: wave/ticket ordering, camp slot order, death/respawn, snapshot
  round trip, AI spawn gate, explicit base registration and authority-only end.
- PlayMode only for MonoBehaviour authoring/bake composition if EditMode cannot
  cover it.
- Unity MCP compilation, Console and focused tests must pass.
- No production content or duplicate public protocol may be introduced.

## Results

- Added explicit lane, wave, camp-slot and two-base topology; all spawning and
  death cleanup goes through `UnitWorld`.
- `JungleCamp` is the scene-authored MonoBehaviour owner; the prohibited
  `JungleCampSystem` and duplicate runtime-config camp schema were removed.
- Minion/monster AI now emits the existing semantic Orders only. Locomotion,
  attack targets and camp targets are resolved by the existing
  Order/Intent/Planner chain; AI Snapshot no longer duplicates a minion's
  current attack target.
- Aggregate Gameplay Snapshot schema is 13 and includes future AI decision
  timing, stable camp slots and wave tickets.
- Unity MCP compilation passed. Focused EditMode results:
  `NonHeroTopologyTests` 5/5, `MatchTopologyTests` 2/2,
  `SnapshotChecksumCompletenessTests` 4/4 and
  `LocalCommandGoldMatchFlowTests` 4/4.
- No PlayMode test was required: the MonoBehaviour authoring is baked and
  lifecycle-validated by EditMode fixtures without scene or render behavior.
