# FrameSyncMobaDemo -- Module Status

> Last updated: 2026-08-01 after ExecPlan 0125 real-map minion/tower logic validation.
> A clean compile alone is not treated as behavior verification.

## Validation baseline

| Validation | Current result |
|---|---|
| Unity | 2022.3.62f1c1, connected through Unity MCP |
| Compilation | Passing through Unity MCP; final Console query returned 0 errors |
| EditMode | Non-hero topology/priority 8/8 plus the prior real-map pathfinding, A*, flow build, RVO and integrated pathfinding suites passed. |
| PlayMode | Real ClientBootstrap minion/tower logic 1/1 passed: authored spawns, 2+1 waves, flow movement, tower selection/settlement, minion death and base-win closure. |
| Multi-process | Local Server + two Clients connected, applied StartTick 3 bootstrap and advanced Gameplay to Tick 10/11 |
| Duplicate protocols | No duplicate UID, Command, Snapshot, Aim, AbilitySignal, Checksum or FixedPoint type found |

## Module matrix

| Module | Status | Current evidence / remaining limit |
|---|---|---|
| Deterministic foundation | **Verified** | `SimulationTickContext.Current` is the sole cross-system simulation-time source; active Tick ownership is tracked separately from its retained latest value. Project `fp`, stable UID/random/order and canonical serialization are integrated. |
| Runtime configuration | **Integrated for current full-match fixture** | Inspector float authoring bakes to fixed-point runtime tables; current Hero, Minion and Tower prototype/prefab mappings pass the real ClientBootstrap Catalog bake. Equipment and jungle fixture content remain later work. |
| Physics / movement / pathfinding | **Verified on the real Map fixture** | `PhysicsEntity2D` owns authoritative pose; Collider-aligned oriented-wall rasterization, grid-snapped lane skeletons, distance-graded flow convergence, exact blue/red waypoint chains, Direct/A*, radius-aware LOS, team flow fields and deterministic RVO passed focused EditMode/PlayMode tests. `Resources/Prefab/Map` owns map config, lanes, six baked fields and an Editor-only switchable visualizer that draws OwnerLane boundaries at full resolution; Bootstrap scene duplicates were removed. |
| Unit / lifecycle / composition | **Verified for seven formal prefabs** | Every formal Hero/Minion/Tower prefab under `Resources/Prefab/Unit` has exactly one root Unit, required Handlers, PhysicsEntity2D, Presentation host and eight semantic sockets. |
| Stats / XP | **Implemented** | Fixed-point runtime and authoring Bake are composed. |
| Combat / modifiers | **Verified** | Formal source headers, stable modifier formula/policy slots, shields, Attack and Projectile settlement passed 21 focused tests. |
| Attack | **Implemented** | Begin/Commit/cancel/reset and successful-output sequence ownership use formal Combat source semantics. |
| Ability / PlayerInput | **Implemented** | Ability authoring, cost/stage validation, Focus/Commit, aim and canonical future-Tick Commands are integrated. |
| Buff / crowd control | **Partial** | Core lifecycle and stable control arbitration work; the complete catalog of generic effect modules remains later framework work. |
| Projectile | **Implemented** | Deterministic motion/filter/order, Combat/Buff/CC dispatch, pooling and rollback state are integrated. |
| Equipment shop / gold | **Framework verified; scene catalog empty** | Runtime purchase/sell/undo tests exist, but GameBootstrap creates an empty `EquipmentDatabase` and no test equipment asset is present, so the current scene cannot exercise Shop. |
| Equipment active use | **Partial** | Command/codec/basic cooldown/charge execution exist. The current design names `EquipmentTargetPolicy` but defines no values or exact match contract, so target/range/NeedApproach arbitration is deliberately not invented. |
| Snapshot / rollback / checksum | **Implemented** | Schema 13 covers integrated future-affecting state with strict restore and stable ordering. |
| FrameSync authority / recovery | **Verified** | Canonical relay, authority history release, initial baseline, authority-before-prediction buffering, gap recovery and prediction limits passed focused tests and local process validation. |
| UOS / NGO application flow | **Local composition verified; live UOS pending** | Client/Server scenes own UTP/NGO, lobby identity/Ready, fragmented bootstrap transport and FrameSync messages. Local Server + two Client flow passed; provider allocation/upload is an external gate. |
| Non-hero / TeamBase | **Verified for the current real-map minion/tower fixture** | Map-owned `MinionSpawns` feed three 2-melee + 1-ranged waves per team. Stable sub-kind priority, no-chase tower orders, projectile Combat settlement, formal minion death, base death and winning-team transition passed. Locked-target tower-projectile gating/red-line presentation and jungle content remain later work. |
| Presentation | **Formal Unit and tower binding verified** | Varus uses its available clips. Blue/red tower prefabs now use their authored Idle/Death clips; no attack animation is synthesized, so attacks correctly retain Idle. Presentation reads simulation time only through `SimulationTickContext.Current`. |
| UI / Lua | **Prefab lifecycle integrated; Lua/content partial** | `UIManager` owns Lobby/HeroSelect/HUD/Shop/Result page prefabs and their open/close lifecycle; serialized component binding passed PlayMode. Existing Lua runtime depth and final visual/content authoring remain later work. |

## Playable-logic closure audit

The bootstrap/network smoke chain is closed locally: authoritative payload
construction/application, deterministic map topology, client binding, NGO lobby
barrier and continuous authority execution form one runnable path. That result
does not yet prove a complete MOBA match. Full-match work is tracked in
`TEST_PREPARATION.md` and `NEXT_CANDIDATES.md`.

## Remediation disposition

- No known code-side P0 remains in the already-tested bootstrap/network smoke chain.
- ExecPlan 0125 P0/P1 findings (duplicate tower composition, non-persistent
  flow fields, and premature clearing of the shared Tick context) are fixed
  and focused-tested.
- Remaining full-match work includes locked-target tower-projectile/red-line
  conformance, jungle/equipment fixture content, final Lua behavior and
  operator visual acceptance.
- All code-side P1 items tracked by ExecPlan 0109 were repaired and focused-tested where the Unity Test Runner returned a result.
- P2 cleanup was limited to touched correctness paths; duplicate/dead shop planning code was removed.
- The incomplete `EquipmentTargetPolicy` design contract is an accepted non-blocking design underspecification, not a guessed production API.

## Permanent constraints

- Unit and Handlers remain prefab-authored MonoBehaviours.
- Inspector values may be `float`; authoritative Gameplay converts once and uses `fp`.
- Bootstrap owns Unity frame scheduling, NGO/UOS, scenes and UI; deterministic assemblies do not depend on those implementations.
- No production hero, concrete ability, Buff, equipment or map content is introduced by the remediation.
