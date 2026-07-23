# Current Handoff — FrameSyncMobaDemo

> Last updated: 2026-07-23 after ExecPlans 0047, 0048, 0049 implementation.
> Read `AGENTS.md`, `.agent/PLANS.md`, `DESIGN_INDEX.md`, `DECISION_LOG.md`, this file, and the active ExecPlan before implementation.

## Current state

- All three approved candidates (A: A* Pathfinding, B: Ability Input Profile Bake, C: Minion Wave + Lane AI) have been implemented as ExecPlans 0047, 0048, 0049.
- The implementations follow the corresponding current design documents.
- Compilation verification pending: run in Unity Editor and check Console.
- Tests written but not yet executed: compile + run via Unity Test Runner.

## Completed ExecPlans

- `Docs/Implementation/Plans/0047_astar_pathfinding_execplan.md` — A* Pathfinding: IndexedMinHeap, AStarPathService, PathFollower2D, UnitLocomotionAgent.Evaluate() implementation.
- `Docs/Implementation/Plans/0048_ability_input_profile_bake_execplan.md` — Ability CastModelDef → Player Input Profile Bake: AbilityInputProfileBaker, AbilityInputProfileProvider, AimKind field on AbilityDef.
- `Docs/Implementation/Plans/0049_minion_wave_spawning_execplan.md` — Minion Wave Spawning + Lane Push AI: MinionSystem.SpawnWave, MinionAIController three-state FSM, NonHeroSnapshot enum updates.

## New files (this round)

| File | Plan |
|---|---|
| `Assets/Scripts/Gameplay/Pathfinding/IndexedMinHeap.cs` | 0047 |
| `Assets/Scripts/Gameplay/Pathfinding/AStarPathService.cs` | 0047 |
| `Assets/Scripts/Gameplay/Pathfinding/PathFollower2D.cs` | 0047 |
| `Assets/Scripts/Gameplay/Tests/AStarPathfindingTests.cs` | 0047 |
| `Assets/Scripts/PlayerInput/AbilityInputProfileBaker.cs` | 0048 |
| `Assets/Scripts/PlayerInput/AbilityInputProfileProvider.cs` | 0048 |
| `Assets/Scripts/PlayerInput/Tests/AbilityInputProfileTests.cs` | 0048 |

## Modified files (this round)

| File | Plan | Change |
|---|---|---|
| `Pathfinding/RouteRuntime.cs` | 0047 | Added PathFollowerState field |
| `Pathfinding/LocomotionAgentSnapshot.cs` | 0047 | Added FollowerState field |
| `Pathfinding/UnitLocomotionAgent.cs` | 0047 | Full Evaluate() implementation with A* integration |
| `Ability/AbilityDef.cs` | 0048 | Added AimKind field |
| `Ability/AbilityHandler.cs` | 0048 | Added GetAbilityDef() method |
| `NonHero/MinionSystem.cs` | 0049 | Added SpawnWave() method |
| `NonHero/UnitAIController.cs` | 0049 | Enhanced MinionAIController with three-state FSM |
| `NonHero/NonHeroSnapshot.cs` | 0049 | Updated MinionAIState enum values |

## Validation status

- Unity compilation: NOT YET VERIFIED (MCP unavailable). Compile in Unity Editor.
- EditMode tests: NOT YET RUN. Run FrameSyncMoba.Unit.Tests and FrameSyncMoba.PlayerInput.Tests.
- No existing tests were modified or removed.

## Architecture constraints (unchanged)

- Unit and current Handlers remain prefab-authored `MonoBehaviour`s.
- Dependency direction: RuntimeConfig/Deterministic/Physics → Unit → FrameSync → PlayerInput → Bootstrap.
- One authoritative UID, Command, Snapshot, AimSnapshot, AbilitySignal, SharedGameplayChecksum and `fp` contract exists.
- No production heroes, named abilities, Buffs, equipment, or balance values were added.
- The intentional tracked deletions accepted by D-024 remain the baseline.

## Next candidates

See `Docs/Implementation/NEXT_CANDIDATES.md` for the next batch of candidate plans.
