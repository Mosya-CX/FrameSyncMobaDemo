# ExecPlan 0036 – Snapshot/Rollback NonHero Integration

> **Design authority**: `Docs/Design/MOBA_Snapshot_Rollback_Design_v7.2.md`, `Docs/Design/MOBA_NonHero_Design_v5.md`
> **Estimated code**: ~350 lines
> **Dependencies**: PredictionRollbackCoordinator (0024), NonHeroSnapshot, MinionSystem, JungleCampSystem (0032)

## Rationale

NonHeroWorldSnapshot was already captured in GameplaySnapshot, but the restore/rollback pipeline ignored it. During rollback, minion waves, jungle camps, and AI controllers would be out of sync with the restored unit state. This plan integrates NonHero systems into all three rollback phases: Restore, Resolve, Rebuild.

## Scope – New files

| File | Lines | Description |
|---|---|---|
| `FrameSync/NonHeroRestoreHelper.cs` | ~100 | Central coordinator: captures, restores, resolves, rebuilds NonHero systems from snapshot |

## Scope – Modified files

| File | Lines | Change |
|---|---|---|
| `FrameSync/PredictionRollbackCoordinator.cs` | +10 | Add NonHeroHelper property; call ResolveNonHero/RebuildNonHero in ExecuteRollback |
| `FrameSync/SimulationTickPipeline.cs` | +10 | Add NonHeroHelper property; call RestoreNonHero in RestoreFromSnapshot |
| `Unit/Core/UnitWorld.cs` | +20 | Add ReconstructAIController(snapshot) for AI controller restoration |
| `Unit/NonHero/UnitAIController.cs` | +5 | Add virtual Resolve/Rebuild to base class |
| `Unit/NonHero/MinionSystem.cs` | +5 | Add Resolve/Rebuild (no-op stubs for now) |
| `Unit/NonHero/JungleCampSystem.cs` | +5 | Add Resolve/Rebuild + fix CS8156 in Restore |

## Key conformance

- Snapshot v7.2: Restore → Resolve → Rebuild phases, NonHeroState member of GameplaySnapshot
- Non-Hero v5: MinionSystem/JungleCampSystem capture/restore round-trip
- Unit Framework v27.3: AI controller lifecycle, CleanupNonHeroDeath during rollback trim
