# ExecPlan 0038 – Death/Respawn Full Lifecycle + Hit Reaction

> **Design authority**: `Docs/Design/moba_combat_system_design_v13_2.md`, `Docs/Design/unit_behavior_framework_design_v27_3.md`
> **Estimated code**: ~400 lines
> **Dependencies**: CombatSystem, UnitWorld, Unit.ClearForDeath/ClearForRespawn (0037)

## Rationale

The death-to-respawn chain was incomplete: no respawn timer, no on-death/kill event dispatch, no hit-reaction state machine. This plan adds the full lifecycle loop and combat feedback states.

## Scope – New files

| File | Lines | Description |
|---|---|---|
| `Unit/Core/RespawnTimer.cs` | ~70 | Per-unit respawn countdown; register on death, tick per frame, trigger respawn when ready |
| `Unit/Combat/HitReactionState.cs` | ~60 | Hit reaction state machine: None/Flinch/Stagger/Knockback/Interrupt with tick duration |
| `Unit/Combat/DeathEffectDispatcher.cs` | ~60 | On-death effect dispatch: kill/death/assist events, experience distribution, death event publishing |

## Scope – Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Combat/CombatSystem.cs` | +20 | Add DeathEffectDispatcher/RespawnTimer properties; call DispatchDeathEffects + RegisterDeath in ResolveDying; add GetRespawnDelay |
| `Unit/Combat/CombatEvents.cs` | +30 | Add OnUnitDeath/OnUnitKill/OnUnitAssist static events + Raise methods + Clear cleanup |
| `Unit/Core/UnitWorld.cs` | +5 | Add RespawnTimer/DeathEffectDispatcher properties |
| `Unit/Core/Unit.cs` | +5 | Add HitReactionState field |
| `FrameSync/SimulationTickPipeline.cs` | +5 | Tick RespawnTimer in TickNonHeroSystems |

## Key conformance

- Combat v13.2: death/kill reaction dispatch, deferred death-effect requests
- Unit v27.3: LifeState machine: Dying→Dead→Respawning→Alive
- Combat v13.2 §10: onKill/onDeath event ordering, UnitEventBus integration
