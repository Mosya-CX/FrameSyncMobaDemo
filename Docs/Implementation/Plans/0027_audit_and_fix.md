# ExecPlan 0027 — Design Audit & Fix

> **Design authority**: All current designs listed in `Docs/Architecture/DESIGN_INDEX.md`
> **Estimated code**: ~150–200 lines (fixes only, no new systems)
> **Dependencies**: All 0024–0026 modules (FrameSync Pipeline, Crowd Control, Projectile)

## Rationale

A full design-doc vs. code audit was performed against the Snapshot Appendix v7.2 and Combat v13.2 designs. Several structural mismatches were found in snapshot definitions and one field missing. This plan fixes those discrepancies without introducing new systems.

## Scope

### Modified files

| File | Change | Design reference |
|---|---|---|
| `Unit/Projectile/ProjectileSnapshot.cs` | Split `Projectiles` list into `PendingSpawns` + `ActiveProjectiles` per design §6; added `PendingSpawnRecordSnapshot` struct | Snapshot Appendix v7.2 §6 |
| `Unit/Projectile/ProjectileWorld.cs` | Added `PendingSpawnEntry` class, `_pendingSpawns` list, `GetProjectileDef()` stub; updated `Capture()` to populate both lists; updated `Restore()` to handle dual-list restore; `Clear()` now clears pending spawns too | Snapshot Appendix v7.2 §6/§12 |
| `Unit/Projectile/ProjectileRuntime.cs` | Added `RestoreFromSnapshot()` method for rollback restore phase | Snapshot Appendix v7.2 §12 |
| `Unit/Combat/CombatSnapshot.cs` | Added `ExpireLogicTick` field to `DamageContributionRecordSnapshot` | Combat v13.2 §7.1, Snapshot Appendix v7.2 §7.1 |
| `FrameSync/GameplaySnapshot.cs` | Added placeholder comments for deferred snapshot members (MatchRuleRuntimeSnapshot, EquipmentShopRuntimeSnapshot, PhysicsRuntimeSnapshot) | Snapshot Appendix v7.2 §4/§8/§10 |
| `FrameSync/SimulationTickPipeline.cs` | Updated projectile restore guard to use new snapshot structure | Snapshot Appendix v7.2 |
| `Docs/Implementation/MODULE_STATUS.md` | Updated Known Gaps section to reflect actual current state (11 items, no stale claims) | — |

### New types

| Type | Location | Purpose |
|---|---|---|
| `PendingSpawnRecordSnapshot` | `ProjectileSnapshot.cs` | Snapshot struct for pending projectile spawns per design §6 |
| `PendingSpawnEntry` | `ProjectileWorld.cs` (internal) | Runtime tracking for deferred projectile spawns |

## Key conformance verified

| Rule | Status |
|---|---|
| GoldIncomeRuntime NOT in GameplaySnapshot | ✓ Confirmed |
| ProjectileWorldSnapshot split into PendingSpawns + ActiveProjectiles | ✓ Fixed |
| CombatSystemSnapshot contains DamageContributionTrackers + DeferredRequests | ✓ Confirmed |
| DamageContributionRecordSnapshot has ExpireLogicTick | ✓ Fixed |
| GameplaySnapshot deferred member slots documented | ✓ Added |
| MODULE_STATUS.md Known Gaps accurate | ✓ Updated |

## Deferred (not fixed — requires other systems first)

| Issue | Reason |
|---|---|
| MatchRuleRuntimeSnapshot | MatchRuleRuntime not implemented |
| EquipmentShopRuntimeSnapshot | Equipment system not implemented |
| PhysicsRuntimeSnapshot | Collision event buffer snapshot not implemented |
| UnitLocomotionAgent (Unit.Locomotion) | Requires PathFollower2D + A* infrastructure |
| Per-Unit UnitEventBus | Global CombatEvents used as interim |
| ProjectileDef registry | Deferred to full Projectile system |
| NaturalRegenPipeline wiring | ProcessNaturalRegen method on CombatSystem not implemented |

## Compilation result

- MCP `assets-refresh`: **0 errors, 0 warnings**
- Unity 2022.3.62f1c1, IsCompiling=false

## Files changed

- `Assets/Scripts/FrameSyncMoba/Unit/Projectile/ProjectileSnapshot.cs`
- `Assets/Scripts/FrameSyncMoba/Unit/Projectile/ProjectileWorld.cs`
- `Assets/Scripts/FrameSyncMoba/Unit/Projectile/ProjectileRuntime.cs`
- `Assets/Scripts/FrameSyncMoba/Unit/Combat/CombatSnapshot.cs`
- `Assets/Scripts/FrameSyncMoba/FrameSync/GameplaySnapshot.cs`
- `Assets/Scripts/FrameSyncMoba/FrameSync/SimulationTickPipeline.cs`
- `Docs/Implementation/MODULE_STATUS.md`
