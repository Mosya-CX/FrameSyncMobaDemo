# ExecPlan 0039 – Projectile Hit Resolution + Def Registry

> **Design authority**: `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md`
> **Estimated code**: ~350 lines
> **Dependencies**: ProjectileWorld, PhysicsWorld, PhysicsSpatialGrid2D, AttackHandler

## Rationale

ProjectileWorld.Restore() had GetProjectileDef() returning null. ProjectileRuntime moved but never detected hits. No code called RegisterHit(). This plan closes the projectile loop: Def lookup, per-tick sweep tests, hit registration, and ranged attack projectile spawn.

## Scope – New files

| File | Lines | Description |
|---|---|---|
| `Unit/Projectile/ProjectileDefRegistry.cs` | ~50 | Def lookup by DefId; Register/RegisterAll/FindById/Clear |
| `FrameSync/ProjectileHitResolver.cs` | ~100 | Per-tick sweep test against PhysicsSpatialGrid; calls proj.RegisterHit; skip owner/dead/already-hit |

## Scope – Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Projectile/ProjectileWorld.cs` | +10 | Add DefRegistry property; replace GetProjectileDef stub with DefRegistry.FindById |
| `Unit/Projectile/ProjectileRuntime.cs` | +5 | Add PrevPosition field; capture before position step for sweep test |
| `Unit/Attack/AttackHandler.cs` | +25 | Add ProjectileWorld property; replace ranged TODO with actual Spawn() call |
| `FrameSync/SimulationTickPipeline.cs` | +10 | Add ProjectileHitResolver property; call ProcessAllHits after TickAll |

## Key conformance
- Projectile v19: per-tick sweep test, HitTargets prevents double-hit, DestroyOnFirstHit/MaxHitCount respected
- Physics v13.1: PhysicsGeometry2D.SweptPointOverlapsCircle + PointOverlapsCircle + PhysicsSpatialGrid2D.CollectCandidates
