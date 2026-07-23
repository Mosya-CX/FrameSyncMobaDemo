# ExecPlan 0026  �?Projectile System Foundation

> **Design authority**: `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md`
> **Estimated code**: ~550�?50 lines
> **Dependencies**: Combat �?/ Ability �?/ Physics �?/ Unit �?
## Rationale

Projectile is the natural next Gameplay system: AttackHandler already supports ranged attacks (projectileDefId), and Ability stages need to spawn missiles. The Physics layer (Entity, World, Grid) provides the spatial substrate. Combat provides the hit-pipeline. This is a self-contained vertical slice that feeds directly into existing systems.

## Scope

### New files (~450 lines)

| File | Lines | Description |
|---|---|---|
| `Unit/Projectile/ProjectileUid.cs` | 30 | Stable UID: sourceTick, sourceUnitUid, sequence |
| `Unit/Projectile/ProjectileDef.cs` | 35 | Config: speed, max lifetime ticks, hit radius, collision mask, on-hit effects |
| `Unit/Projectile/ProjectileRuntime.cs` | 120 | Per-instance: position, velocity, remaining lifetime, hit targets set, active flag. TickUpdate: advance position, check expiry. Implement IRollback |
| `Unit/Projectile/ProjectileWorld.cs` | 160 | Global projectile registry: Spawn, Destroy, TickAll, CollectHits (query PhysicsWorld for overlaps). Stable ordered iteration |
| `Unit/Projectile/ProjectileSnapshot.cs` | 25 | Cross-Tick state per projectile |
| `Unit/Projectile/ProjectileHitResult.cs` | 20 | Struct: hitUnitUid, hitPosition, impactTick |
| `Unit/Projectile/ProjectileFlightParams.cs` | 15 | Config: start pos, target pos/unit, speed, acceleration |

### Modified files (~100 lines)

| File | Lines | Change |
|---|---|---|
| `Unit/Attack/AttackHandler.cs` | +25 | On impact tick, if ranged: call ProjectileWorld.Spawn |
| `Unit/Ability/AbilityHandler.cs` | +30 | Stage OnEnter/OnTick can spawn projectiles |
| `Unit/Combat/CombatSystem.cs` | +20 | Receive ProjectileHitResult and process as combat request |
| `FrameSync/FrameSyncGameRuntime.cs` | +15 | Create and tick ProjectileWorld in Tick loop |
| `Physics/PhysicsWorld.cs` | +10 | Expose query for projectile overlap checks |

### Tests (~150 lines)

| File | Lines |
|---|---|
| `Unit/Tests/ProjectileWorldTests.cs` | 80 | Spawn→tick→hit→destroy lifecycle, deterministic same-input |
| `Unit/Tests/ProjectileIntegrationTests.cs` | 70 | AttackHandler ranged→projectile spawn→hit→CombatSystem damage |

## Key conformance

- ProjectileUid is stable, field-wise comparable, independent of creation order
- ProjectileWorld owns all runtimes; TickAll in fixed order
- Hit detection uses PhysicsWorld spatial query (no Unity physics)
- Each projectile tracks hit targets to prevent double-hit
- Projectile destroyed on: lifetime expiry, first hit (if single-target), max hits reached
- All spatial math uses fp/fp2
- ClearForDespawn: destroy all projectiles owned by a Unit
