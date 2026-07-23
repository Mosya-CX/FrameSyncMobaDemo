# Candidate Plans — Batch 0037–0039

> Created: 2026-07-22 (post 0036 verification)
> Based on: MODULE_STATUS.md, DESIGN_INDEX.md, Known Gaps

---

## Candidate A: Buff/Equipment Passive Rebuild on Respawn (~350 lines)

**Gap**: Death/respawn lifecycle is partial. BuffHandler and EquipmentHandler have `ClearForDeath`/`ClearForRespawn` stubs but don't actually rebuild passives, buff runtimes, or equipment stat modifiers after respawn.

### New files

| File | Lines | Description |
|---|---|---|
| — | — | No new files |

### Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Buff/BuffHandler.cs` | +80 | ClearForDeath: remove non-permanent buffs; ClearForRespawn: rebuild permanent buffs from store |
| `Unit/Equipment/EquipmentHandler.cs` | +60 | ClearForRespawn: re-apply equipment stat modifiers |
| `Unit/Core/Unit.cs` | +30 | Formal death/respawn handler hook; call ClearForDeath/ClearForRespawn in stable order |
| `Unit/Combat/CombatSystem.cs` | +40 | Death-triggered on-kill effects, death-triggered projectile dispatch |
| `Unit/Core/UnitWorld.cs` | +30 | Respawn timer tracking; respawn position lookup per camp/team |

### Design conformance
- Unit v27.3: ClearForDeath/ClearForRespawn stable ordering across systems
- Buff v14.2: permanent vs temporary buff classification; rebuild on respawn
- Equipment v12: equipment stat modifier re-application after respawn
- Combat v13.2: death/kill triggered effects (onKill, onDeath projectiles)

---

## Candidate B: Death/Respawn Full Lifecycle + Hit Reaction (~400 lines)

**Gap**: The death-to-respawn chain is incomplete. No respawn timer, no death-triggered projectile effects, no hit-reaction state machine for units being hit/stunned/knocked back.

### New files

| File | Lines | Description |
|---|---|---|
| `Unit/Core/RespawnTimer.cs` | ~70 | Per-unit respawn countdown; team-based respawn delay lookup |
| `Unit/Combat/HitReactionState.cs` | ~80 | Hit reaction state machine: flinch/stagger/knockback/interrupt handling |
| `Unit/Combat/DeathEffectDispatcher.cs` | ~50 | On-death effects: experience distribution, gold bounty, death-triggered projectile spawn |

### Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Core/UnitWorld.cs` | +40 | Respawn tick processing; BeginRespawn→CompleteRespawn flow |
| `Unit/Combat/CombatSystem.cs` | +50 | On-kill/on-death effect dispatch during Combat settlement |
| `Unit/Core/Unit.cs` | +30 | HitReactionState property; death animation trigger tick |

### Design conformance
- Combat v13.2: deferred death-effect requests, onKill/onDeath event ordering
- Unit v27.3: LifeState machine: Dying→Dead→Respawning→Alive
- Projectile v19: death-triggered projectile creation (e.g., on-death AoE)

---

## Candidate C: Pathfinding Foundation (~600 lines)

**Gap**: No deterministic pathfinding at all. Units can move in straight lines via MovementHandler but cannot navigate around obstacles. This is a foundational requirement before any real moba gameplay.

### Scope warning
Pathfinding is a large system. This candidate implements only the **foundation**: grid map, A* planner, and basic path following. Flow-field and RVO are deferred to later plans.

### New files

| File | Lines | Description |
|---|---|---|
| `Unit/Pathfinding/PathGridMap2D.cs` | ~120 | Binary obstruction grid; AABB→cell index mapping; walking-radius expand |
| `Unit/Pathfinding/PathNode.cs` | ~40 | A* node: g/h/f costs, parent index, closed flag |
| `Unit/Pathfinding/PathPlanner.cs` | ~150 | Deterministic A*; stable priority queue; path smoothing |
| `Unit/Pathfinding/PathFollower2D.cs` | ~100 | Waypoint-following agent; reach threshold; look-ahead with collision |
| `Unit/Pathfinding/PathRequestQueue.cs` | ~50 | Per-tick path request batching; stable request ordering |

### Modified files

| File | Lines | Change |
|---|---|---|
| `Unit/Movement/MovementHandler.cs` | +60 | Path-following mode: consume waypoints from PathFollower, blend with collision |
| `Unit/Movement/MovementSnapshot.cs` | +20 | CurrentWaypointIndex, PathNodeBuffer for cross-tick state |

### Design conformance
- Pathfinding v13.1: grid-based A*, deterministic neighbor enumeration, stable open-set ordering
- Physics v13.1: PhysicsSpatialGrid for static obstacle registration
- Snapshot v7.2: PathFollower state captured in MovementSnapshot

---

## Recommendation

**Priority order: A → B → C**

- **A (Passive Rebuild)** is the smallest and most immediate gap — it completes the death/respawn lifecycle for Buff and Equipment that are already implemented.
- **B (Full Lifecycle + Hit Reaction)** builds on A's respawn work and adds the missing combat feedback loop.
- **C (Pathfinding)** is the largest effort and benefits from having the movement system (0035) and collision (0035) already in place, but it's a multi-plan system.
