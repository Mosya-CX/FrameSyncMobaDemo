# FrameSyncMobaDemo -- Module Status

> Last updated: 2026-07-24, post-0095-0100 audit remediation.

## Status meanings

| Status | Meaning |
|---|---|
| `Missing` | No meaningful current implementation |
| `Scaffold` | Interfaces or data shapes exist, but the observable behavior is not complete |
| `Partial` | A useful generic slice works, with explicitly recorded later capability remaining |
| `Implemented` | The current formal slice is implemented and compiles |
| `Verified` | The current formal slice is implemented, compiled, and behavior-verified |

## Current validation baseline

| Validation | Result |
|---|---|
| Unity | Unity 2022.3.62f1c1 |
| Compilation | **Passing** -- zero errors (Editor.log confirmed clean) |
| Current EditMode total | **529 tests passed** (post-0094 baseline) |
| Previous PlayMode total | ~32 tests passed (pre-0087 baseline) |
| Audit status | **Remediated** -- 4 P0 violations fixed (0095-0096), 2 Gameplay modules completed (0097/0100), Capture assertions added (0099) |

## Current module matrix

| Module | Status | Current evidence and remaining non-blocking work |
|---|---|---|
| Deterministic foundation | **Verified** | Tick context, deterministic random state, fixed-point geometry, canonical byte writing, stable UID. |
| Runtime configuration | **Implemented** | `GlobalPrefabTable`, `GlobalGameplayData`, `AbilityAsset` SO bake, `MinionWaveConfig`, `JungleCampConfig` (0088). |
| Physics / spatial grid | **Verified** | Deterministic shapes, range queries, grid, stable pair ordering. LateUpdate for Unity Transform sync added (0095). |
| Unit / prefab composition | **Verified** | `Unit` and Handlers are prefab-authored `MonoBehaviour`s. |
| Stats / XP | **Verified** | Stat and experience state consolidated in `StatHandler`. |
| Combat | **Verified** | Global request sequence, shield/life settlement, death ownership. |
| Attack | **Verified** | Fixed-point timing, explicit TickRate injection. |
| Ability | **Partial** | Generic sessions, CastModels, stages, SO authoring. Production stages remain future. |
| Buff | **Implemented** | Runtime store, reactions, ownership, restore, lifecycle rebuild hooks. |
| Crowd control | **Implemented** | Stable handles, immunity, unstoppable, forced movement, snapshot. |
| Projectile | **Implemented** | Stable UID, pending spawn lifecycle, deterministic hit order. |
| Equipment / shop / gold | **Verified** | Fixed-point Bake/runtime, sole-owner `GoldIncomeRuntime`. |
| Snapshot / rollback | **Verified** | Aggregate snapshot tree, explicit Restore/Resolve/Rebuild. |
| FrameSync / match flow | **Verified** | Canonical command header, stable collection, AuthorityFrame. `MatchFlowStateMachine` (0090), `MatchResultSnapshot` (0090). |
| Player input | **Implemented** | Callbacks enqueue local events; processing resolves aim. |
| Non-hero AI | **Implemented** | Stable UID ordering, typed runtime state, minion wave, lane AI. Jungle camp config bake (0088). |
| Movement / pathfinding | **Verified** | A*, FlowField, RVO, WallPenetration, Radius Clearance. Integration tests (0091). MovementHandler reads SimulationTickContext internally; UnitLocomotionAgent reads PhysicsEntity2D.Transform2D (0095). |
| Bootstrap / composition root | **Verified** | Client/server bootstrap, `GameBootstrap`, configuration assets. Scoreboard+Minimap (0087), Cooldown (0089), **Result Screen (0092)**. |
| Presentation bridge | **Partial** | PresentationEventDispatcher, AttackSfxHandler, HitReactionPresenter, DeathPresenter. Full animation, particle VFX, Unity Transform sync now implemented via PhysicsEntity2D.LateUpdate (0095). |
| Ability indicator | **Implemented** | SkillIndicatorDriver with direction/range/ground-target indicators. |
| UI / Lua | **Partial** | Shop UI, Scoreboard (0087), Minimap (0087), Cooldown (0089), **Result Screen (0092)**, **Hero Select (0093)**. Lua: result.lua, hero_select.lua. |
| Ability authoring | **Implemented** | `AbilityAsset` SO, CastModelAuthoring, StageDefAuthoring, Editor bake. |
| Integration tests | **Implemented** | RVO+FlowField integration (0091), Full gameplay loop + Shop pipeline (0094). |
| Network / authority layer | **Missing** | `GameApplicationFlowManager`, `LobbySessionFlowNetwork`, `CommandDispatcher`, `AuthorityFrameReplicator`, `AuthorityRecovery` -- deferred to Phase 11/14 per ROADMAP. |
| TeamBase system | **Missing** | No `TeamBase` type exists. MatchRuleRuntime uses simplified unit-world base tracking. Deferred to Phase 13+. |

## Permanent constraints

- Unit and current Handlers remain prefab-authored `MonoBehaviour`s.
- Inspector-facing numeric authoring may use `float`, but Gameplay calculations use `fp`.
- Production heroes, specific abilities, Buffs, equipment, map objects and balance values remain out of scope.
- Presentation is read-only consumption; Gameplay never reads presentation state.
- `UiSnapshotDto` and `LuaRuntime` are presentation-only and never enter `GameplaySnapshot` or `SharedGameplayChecksum`.
