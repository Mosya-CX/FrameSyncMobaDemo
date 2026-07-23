# Candidate Plans — Batch 0033–0035

> Created: 2026-07-22 (post 0032 verification)
> Based on: MODULE_STATUS.md, DESIGN_INDEX.md, Known Gaps

---

## Candidate A: Ability Command Dispatch (~400 lines)

**Gap**: CastAbility/CancelAbility GameplayCommands exist but SimulationTickPipeline.DispatchCommand ignores them. AbilityHandler has no entry point from the Tick pipeline.

### New files

| File | Lines | Description |
|---|---|---|
| `Unit/Ability/AbilityCommandDispatch.cs` | ~100 | Dispatch logic: route CastAbility/CancelAbility to AbilityHandler.ApplyAbilitySignal |

### Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/SimulationTickPipeline.cs` | +30 | DispatchCommand: add CastAbility/CancelAbility case routing |
| `Unit/Ability/AbilityHandler.cs` | +80 | ApplyAbilitySignal(AbilitySignal) entry point; validate slot/life-state/CC gate |
| `Unit/Ability/AbilityRuntime.cs` | +60 | ReceiveAbilitySignal: Focus to begin charge; Commit to execute; Cancel to interrupt |
| `Unit/Ability/CastModelDef.cs` | +40 | HoldRelease timing: FocusTick, MinFocusTicks, MaxFocusTicks validation |

### Design conformance
- Ability v15.2: Focus/Commit signal language via CastAbility commands
- Player Input v1.1: Hold-release FSM produces Focus on press, Commit on release
- CC v6.2: CrowdControlHandler.IsActionRestricted gates ability usage

---

## Candidate B: Movement System Completion (~500 lines)

**Gap**: MovementHandler is scaffold-only. MoveIntent from commands only sets direction; no position resolution, speed application, or collision constraints.

### New files

| File | Lines | Description |
|---|---|---|
| `Unit/Movement/MovementResolver.cs` | ~120 | Resolve per-tick movement: speed x delta, collision boundary clamping, facing |
| `Unit/Movement/MovementConstraint.cs` | ~80 | Nav-mesh boundary check, physics spatial grid collision, push-out resolution |

### Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Movement/MovementHandler.cs` | +100 | ApplyMoveInput stores intent; TickUpdate resolves position; MovementSnapshot update |
| `FrameSync/SimulationTickPipeline.cs` | +40 | SyncMovementToPhysics uses resolved position; collision resolution in Tick |
| `Unit/Movement/MovementSnapshot.cs` | +30 | Add IsMoving flag, TargetPosition, CurrentWaypoint |

### Design conformance
- Pathfinding v13.1: deterministic position step, collision boundary, speed
- Physics v13.1: PhysicsWorld query for collision boundary
- Snapshot v7.2: MovementSnapshot captures cross-tick state

---

## Candidate C: Snapshot/Rollback NonHero Integration (~350 lines)

**Gap**: NonHeroWorldSnapshot is captured in GameplaySnapshot but PredictionRollbackCoordinator.Restore/Rebuild doesn't handle it.

### New files

| File | Lines | Description |
|---|---|---|
| `FrameSync/NonHeroRestoreHelper.cs` | ~90 | Restore MinionSystem, JungleCampSystem, UnitAIControllers from snapshot |

### Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/PredictionRollbackCoordinator.cs` | +60 | Capture: include NonHeroState; Restore: call NonHeroRestoreHelper; Rebuild: re-wire AIControllers |
| `Unit/Core/UnitWorld.cs` | +40 | AI controller registry snapshot round-trip; restore AIControllers after rollback |
| `Unit/NonHero/MinionSystem.cs` | +40 | Resolve/Rebuild phases for minion wave state |
| `Unit/NonHero/JungleCampSystem.cs` | +40 | Resolve/Rebuild phases for camp state |

### Design conformance
- Snapshot v7.2: separate Restore to Resolve to Rebuild phases
- Unit Framework v27.3: AI controller lifecycle during rollback
- Non-Hero v5: minion/jungle snapshot membership

---

## Recommendation

**Priority order: A to B to C**

- **A (Ability Dispatch)** closes the loop between PlayerInput and Ability execution - without it, QWER keys produce commands that are silently dropped.
- **B (Movement)** makes units actually move deterministically - critical before any pathfinding work.
- **C (Snapshot Integration)** hardens the rollback/replay determinism for non-hero systems.

Pathfinding (Candidate D) is intentionally deferred: it requires Movement completion first, and flow-field + RVO are each 500+ line systems.
