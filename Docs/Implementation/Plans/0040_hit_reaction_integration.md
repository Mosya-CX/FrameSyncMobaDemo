# ExecPlan 0040 – Hit Reaction Integration

> **Design authority**: `Docs/Design/moba_crowd_control_system_design_v6_2.md`, `Docs/Design/unit_behavior_framework_design_v27_3.md`
> **Estimated code**: ~200 lines
> **Dependencies**: HitReactionState (0038), MovementHandler, AbilityHandler, AttackHandler, CombatSystem

## Rationale

HitReactionState existed on Unit since 0038 but was never checked. This plan wires it into all three gameplay handlers and the Combat damage pipeline.

## Scope – Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Movement/MovementHandler.cs` | +25 | Add Unit owner field; gate ApplyMoveInput + TickUpdate on InterruptsMovement |
| `Unit/Ability/AbilityHandler.cs` | +20 | Gate HandleSignal on InterruptsAbility (Cancel bypasses); interrupt active sessions in TickUpdate |
| `Unit/Attack/AttackHandler.cs` | +20 | Gate ApplyAttackInput on InterruptsAttack; interrupt windup in TickUpdate |
| `Unit/Combat/CombatSystem.cs` | +30 | ApplyHitReaction: damage → Flinch (3 ticks); >10% max HP → Stagger (6 ticks) |
| `FrameSync/SimulationTickPipeline.cs` | +5 | Per-unit HitReaction.TickUpdate() in ExecuteTick loop |
| `Unit/Core/UnitWorld.cs` | +2 | SpawnUnit: pass unit to MovementHandler constructor |

## Key conformance
- CC v6.2: CC gates at top, HitReaction gate next, then Handler logic
- Unit v27.3: Handler lifecycle respects HitReaction interrupts
- Attack v6.2: interrupted windup resets cycle immediately
