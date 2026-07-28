# FrameSyncMobaDemo -- Module Status

> Last updated: 2026-07-28, ExecPlan 0109 final remediation.
> A clean compile alone is not treated as behavior verification.

## Validation baseline

| Validation | Current result |
|---|---|
| Unity | 2022.3.62f1c1, connected through Unity MCP |
| Compilation | Passing, 0 Console errors after the latest PlayerInput/UI command change |
| EditMode | Focused suites passed: Equipment shop 4, authority/codec 8, Combat 21, non-hero topology 6, PlayerCommandRequester 5; Bootstrap assembly 36/36 |
| PlayMode | `FrameworkSmokeScenePlayModeTests` 1/1 passed |
| Duplicate protocols | No duplicate UID, Command, Snapshot, Aim, AbilitySignal, Checksum or FixedPoint type found |

## Module matrix

| Module | Status | Current evidence / remaining limit |
|---|---|---|
| Deterministic foundation | **Verified** | Fixed Tick context, project `fp`, stable UID/random/order and canonical serialization are integrated. |
| Runtime configuration | **Verified** | Inspector float authoring bakes to fixed-point runtime tables; Ability catalog/loadout ScriptableObjects have matching source files and persist across reload. |
| Physics / movement / pathfinding | **Verified** | `PhysicsEntity2D` owns authoritative pose; ForcedMove > Dash > Route, stable RVO and movement snapshots passed focused tests. |
| Unit / lifecycle / composition | **Implemented** | Unit and Handlers remain prefab-authored MonoBehaviours; neutral fixture spawns through `UnitWorld` and the bounded Tick driver. |
| Stats / XP | **Implemented** | Fixed-point runtime and authoring Bake are composed. |
| Combat / modifiers | **Verified** | Formal source headers, stable modifier formula/policy slots, shields, Attack and Projectile settlement passed 21 focused tests. |
| Attack | **Implemented** | Begin/Commit/cancel/reset and successful-output sequence ownership use formal Combat source semantics. |
| Ability / PlayerInput | **Implemented** | Ability authoring, cost/stage validation, Focus/Commit, aim and canonical future-Tick Commands are integrated. |
| Buff / crowd control | **Partial** | Core lifecycle and stable control arbitration work; the complete catalog of generic effect modules remains later framework work. |
| Projectile | **Implemented** | Deterministic motion/filter/order, Combat/Buff/CC dispatch, pooling and rollback state are integrated. |
| Equipment shop / gold | **Verified** | Component-aware purchases, full-inventory slot reuse, sell/undo, deep snapshot state, checksum and canonical UI Commands passed focused tests. |
| Equipment active use | **Partial** | Command/codec/basic cooldown/charge execution exist. The current design names `EquipmentTargetPolicy` but defines no values or exact match contract, so target/range/NeedApproach arbitration is deliberately not invented. |
| Snapshot / rollback / checksum | **Implemented** | Schema 13 covers integrated future-affecting state with strict restore and stable ordering. |
| FrameSync authority / recovery | **Verified** | Canonical bundle/relay, authority archive, gap recovery, prediction limits and codec behavior passed 8 focused tests. |
| UOS / NGO application flow | **Implemented** | Bootstrap-only adapters, lobby barriers, frozen start/result contracts and NGO FrameSync bridge compile; live provider/dashboard validation remains external. |
| Non-hero / TeamBase | **Verified** | Stable wave/camp/AI topology and authority-only base victory behavior are integrated; Tower target ownership is only `Unit.Intent`. |
| Presentation / UI / Lua | **Implemented** | Presentation histories are rollback-aware and read-only; shop UI now submits canonical Commands instead of mutating Gameplay. |

## Remediation disposition

- No known P0 remains in the implemented generic framework slice.
- All code-side P1 items tracked by ExecPlan 0109 were repaired and focused-tested where the Unity Test Runner returned a result.
- P2 cleanup was limited to touched correctness paths; duplicate/dead shop planning code was removed.
- The incomplete `EquipmentTargetPolicy` design contract is an accepted non-blocking design underspecification, not a guessed production API.

## Permanent constraints

- Unit and Handlers remain prefab-authored MonoBehaviours.
- Inspector values may be `float`; authoritative Gameplay converts once and uses `fp`.
- Bootstrap owns Unity frame scheduling, NGO/UOS, scenes and UI; deterministic assemblies do not depend on those implementations.
- No production hero, concrete ability, Buff, equipment or map content is introduced by the remediation.
