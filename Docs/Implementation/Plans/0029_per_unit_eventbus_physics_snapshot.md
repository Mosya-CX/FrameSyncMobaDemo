# ExecPlan 0029 — Per-Unit EventBus + PhysicsRuntimeSnapshot

> **Design authority**: `Docs/Design/unit_behavior_framework_design_v27_3.md` §6, `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md` §10
> **Estimated code**: ~350–450 lines
> **Dependencies**: Buff ✓ / Combat ✓ / Physics ✓ / FrameSync ✓

## Rationale

The Unit design v27.3 explicitly requires a per-Unit `UnitEventBus` for strong-typed internal event routing. Currently, `BuffHandler` subscribes to the global static `CombatEvents` class as an interim solution. This plan replaces that with proper per-Unit event routing, and simultaneously adds the `PhysicsRuntimeSnapshot` (collision event buffer) required by the Snapshot Appendix §10.

## Scope — New files

| File | Lines | Description |
|---|---|---|
| `Unit/Core/UnitEventBus.cs` | ~120 | Per-Unit strong-typed event bus: DamageTaken/Dealt, HealTaken/Dealt, ShieldApplied, UnitDying/Death/Kill. Subscription/unsubscription with owner-filtering. Publish methods called by CombatSystem during settlement. |
| `Physics/PhysicsRuntimeSnapshot.cs` | ~80 | `UnitCollisionEventBufferSnapshot` with `PreviousPairs[]`; `PhysicsRuntimeSnapshot` struct per Snapshot Appendix §10 |

## Scope — Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Core/Unit.cs` | +10 | Add `UnitEventBus EventBus` property; create in constructor |
| `Unit/Buff/BuffHandler.cs` | ~50 | Replace `CombatEvents.OnDamageTaken += ...` with `_owner.EventBus.OnDamageTaken += ...`; unsubscribe on disposal |
| `Unit/Combat/CombatSystem.cs` | +40 | After settlement: publish events through `target.EventBus.Publish(DamageTaken, data)` instead of global `CombatEvents.RaiseDamageTaken` |
| `Unit/Combat/CombatEvents.cs` | ~20 | Keep global bus as fallback for systems without per-Unit access; mark as deprecated interim |
| `FrameSync/GameplaySnapshot.cs` | +15 | Add `PhysicsRuntimeSnapshot PhysicsState` field |
| `FrameSync/SimulationTickPipeline.cs` | +20 | Capture/Restore PhysicsRuntimeSnapshot; wire PhysicsWorld collision buffer |
| `Physics/PhysicsWorld.cs` | +15 | Expose collision event buffer for snapshot capture |
| `Unit/Combat/CombatSnapshot.cs` | +30 | Activate `ExpireLogicTick` in `DamageContributionTracker` — prune expired records during BeginTick |

## Key conformance

- Per-Unit `UnitEventBus` matches Unit v27.3 §6: "Unit 内部事件中心 UnitEventBus"
- `CombatSystem` publishes through per-Unit event bus, not global static
- `PhysicsRuntimeSnapshot` contains `UnitCollisionEventBufferSnapshot.PreviousPairs[]` per Snapshot Appendix §10
- Collision event buffer restored during rollback Restore phase per explicit restore order §12
- `DamageContributionRecord.ExpireLogicTick` activated — expired records pruned deterministically
