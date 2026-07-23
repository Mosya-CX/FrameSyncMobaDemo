# Plan 0049: Minion Wave Spawning + Lane Push AI

> Status: Completed
> Created: 2026-07-23
> Based on: `Docs/Design/moba_non_hero_unit_modules_design_v5.md` §4, §5; `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §5
> Predecessor: 0048 (Ability Input Profile Bake)
> Parent candidate: NEXT_CANDIDATES.md Candidate C

## Purpose

Implement minion wave spawning and lane-push AI so minions appear on schedule, advance down lanes, acquire enemy targets with stable priority ordering, and return to lane after chasing too far. This is the first end-to-end validation of Spawn → AI → Pathfinding → Movement → Combat.

## Observable behavior

- Minions spawn on schedule per wave interval
- Minions advance down their assigned lane
- Minions acquire targets: hero-assist → current target → enemy minion → enemy hero → structure
- Minions chase within boundary, return to lane on boundary exceeded

## In scope

1. `MinionSystem.SpawnWave()` — spawn full wave for a lane
2. `MinionAIController` — three-state FSM (AdvanceLane → EngageTarget → ReturnToLane)
3. `UnitLocomotionAgent` — `MovePurpose.MoveToLane` routing via A*
4. Pipeline wiring
5. Tests: wave spawn count, AI state transitions, target priority

## Out of scope

- TeamFlowFieldService — use A* for lane movement
- JungleCampSystem spawn/respawn
- Tower AI targeting
- Multi-lane wave coordination beyond single lane

## Modified files

| File | Lines | Change |
|---|---|---|
| `NonHero/MinionSystem.cs` | +120 | SpawnWave creates UnitSpawnRequest per minion |
| `NonHero/UnitAIController.cs` | +80 | Target selection with priority bands |
| `Pathfinding/UnitLocomotionAgent.cs` | +20 | MoveToLane routing via A* |
| `FrameSync/SimulationTickPipeline.cs` | +10 | Wire SpawnWave before tick loop |

## Estimated code change

- Modified: ~230 lines
- Tests: ~70 lines
- **Total: ~300 lines**
