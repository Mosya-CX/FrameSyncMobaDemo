# ExecPlan 0035 — Movement System Completion

> **Design authority**: `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
> **Estimated code**: ~250 lines
> **Dependencies**: PhysicsWorld (0019), MovementHandler scaffold

## Rationale

MovementHandler already resolves position from MoveIntent with speed and facing. The gap is collision constraint: units move through each other and outside the map boundary. Adding a deterministic collision resolver that queries PhysicsSpatialGrid completes the movement loop.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| `Unit/Movement/MovementCollisionResolver.cs` | ~100 | Query PhysicsSpatialGrid, clamp position to nearest non-colliding point, push-out resolution |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Movement/MovementHandler.cs` | +40 | Optional IMovementCollisionResolver; apply after position step; MovementSnapshot.IsMoving |
| `Unit/Movement/MovementSnapshot.cs` | +15 | Add IsMoving flag, TargetDirection |
| `FrameSync/SimulationTickPipeline.cs` | +20 | Create collision resolver, pass to MovementHandler; apply after TickUpdate |

## Key conformance

- Physics v13.1: collision boundary query via PhysicsSpatialGrid
- Snapshot v7.2: IsMoving enters MovementSnapshot (cross-tick state)
- Deterministic: fp math only, stable ordering
