# ExecPlan 0025  �?Crowd Control Foundation

> **Design authority**: `Docs/Design/moba_crowd_control_system_design_v6_2.md`
> **Estimated code**: ~450�?50 lines
> **Dependencies**: Buff �?(0023) / Movement ⚠️ Scaffold / Unit lifecycle �?
## Rationale

CC is the first post-Buff module unlocked. The design explicitly requires BuffHandler as the submitter of CC requests (stun on hit, slow from buff, etc.). With Buff in place, CC can now be built: constraint types, immunity/unstoppable handles, priority arbitration (CrowdControlHandler as sole decider), and forced-move integration with MovementHandler.

## Scope

### New files (~350 lines)

| File | Lines | Description |
|---|---|---|
| `Unit/CC/CrowdControlType.cs` | 12 | Enum: Stun, Root, Slow, Silence, Disarm, Knockback, Suppression |
| `Unit/CC/CrowdControlConstraint.cs` | 20 | Struct: type, duration ticks, source, priority |
| `Unit/CC/CrowdControlHandler.cs` | 180 | Per-Unit: submit constraint, resolve priority (highest active), enforce immunity, unstoppable flag, forced-move arbitration. Implement IRollback |
| `Unit/CC/CrowdControlSnapshot.cs` | 18 | Cross-Tick state per Unit |
| `Unit/CC/ImmunityHandle.cs` | 15 | Handle returned when immunity granted; Detach on removal |
| `Unit/CC/UnstoppableToken.cs` | 12 | Token for temporary CC immunity |
| `Unit/CC/ForcedMoveData.cs` | 25 | Forced position/velocity trajectory struct |

### Modified files (~150 lines)

| File | Lines | Change |
|---|---|---|
| `Unit/Core/Unit.cs` | +5 | Add CrowdControlHandler property |
| `Unit/Core/UnitWorld.cs` | +10 | Create CC handler in SpawnUnit, lifecycle hooks |
| `Unit/Movement/MovementHandler.cs` | +80 | Consume forced-move trajectory from CC; apply displacement priority: ForcedMove > Dash > RouteMove |
| `Unit/Buff/BuffHandler.cs` | +30 | Buff effects submit CC requests via owner.CrowdControl |
| `Unit/Combat/CombatSystem.cs` | +25 | Combat settlement triggers CC requests (on-hit stun etc.) |

### Tests (~120 lines)

| File | Lines |
|---|---|
| `Unit/Tests/CrowdControlTests.cs` | 80 | Priority resolution, immunity block, duration expiry, forced-move execution order |
| `Unit/Tests/MovementIntegrationTests.cs` | +40 | CC forced-move overrides route movement |

## Key conformance

- CrowdControlHandler is sole decider of active forced-move priority
- Immunity handles prevent new constraints; do not retroactively cancel active constraints
- Unstoppable token: temporary break-free, not permanent immunity
- Forced-move trajectory written by CC, executed by MovementHandler (not MovementHandler deciding priority)
- ClearForDeath/ClearForRespawn: remove all constraints, release handles
- All Tick reads via SimulationTickContext.Current
