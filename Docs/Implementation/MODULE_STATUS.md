# FrameSyncMobaDemo — Module Status

> Last updated: 2026-07-22, ExecPlan 0046 conformance recovery closure.  
> Evidence priority: current repository, current designs indexed by `DESIGN_INDEX.md`, Unity MCP compilation/Console, then focused Unity Test Runner results.

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
| Unity | Unity 2022.3.62f1c1; Unity MCP connected; AssetDatabase refresh/compile succeeds |
| Console | No C# compiler or product-runtime Error; the only final Error entries are MCP's own `ai-editor-logs.txt` file-lock diagnostics from attempting to clear its log cache |
| Deterministic EditMode | 51/51 passed (unchanged verified baseline) |
| Physics EditMode | 71/71 passed |
| Physics PlayMode | 30/30 passed (unchanged verified baseline) |
| FrameSync EditMode | 16/16 passed |
| PlayerInput EditMode | 3/3 passed |
| Unit EditMode | Latest full run: 218 passed/14 failed; every failed class was corrected and then passed in focused reruns: Attack 11/11, Combat 10/10, contribution 3/3, Stats calculation 11/11, Stats snapshot 6/6, Spawn 13/13, Equipment snapshot 2/2 |
| Unit PlayMode | 1/1 passed |
| Bootstrap PlayMode | 1/1 passed |

Per the approved low-overhead workflow, the corrected focused suites were not followed by another redundant full-suite run. No test was removed, disabled, or weakened.

## Current module matrix

| Module | Status | Current evidence and remaining non-blocking work |
|---|---|---|
| Deterministic foundation | **Verified** | Tick context, deterministic random state, fixed-point geometry helpers, canonical primitive writing, stable UID value semantics. Project owns no replacement `fp`. |
| Runtime configuration | **Implemented** | `GlobalPrefabTable` and `GlobalGameplayData` are project-owned ScriptableObjects. Inspector floats convert once to `fp`; runtime values are fixed-point. More content databases remain future authoring work. |
| Physics / spatial grid | **Verified** | Deterministic shapes, range queries, grid, stable pair/event ordering, `PreviousPairs` snapshot state, and collision event ownership are present. Unity physics is not Gameplay authority. |
| Unit / prefab composition | **Verified** | `Unit` and all current Handlers are prefab-authored `MonoBehaviour`s. `UnitWorld` owns stable UID topology and formal spawn/lifecycle. Physics binding preserves UID/team identity. |
| Stats / XP | **Verified** | Stat and experience state are consolidated in `StatHandler`; modifier ownership, level progression, current-value behavior, snapshot/restore, and deterministic tests are present. |
| Combat | **Verified** | Global request sequence, shield/life settlement, D-009 death ownership, killer source, canonical assistants, contribution expiry, deferred order, snapshot validation, and stable resolve are implemented. |
| Attack | **Verified** | Fixed-point timing, explicit TickRate injection, stable target reference validation, windup/commit/cooldown behavior, and snapshot tests are present. Formal Action-runtime unification is a later capability, not required by the recovered slice. |
| Ability | **Partial** | Generic sessions, CastModels, stages, tagged Aim, blackboard/passive state, snapshot/resolve, and lifecycle are implemented. Production authoring/Bake and advanced targeting/indicator definitions remain future generic framework work. |
| Buff | **Implemented** | Runtime store, reactions, ownership, restore, source validation, and lifecycle rebuild hooks are present. Additional effect families require future generic definitions, not production content branches. |
| Crowd control | **Implemented** | Stable handles, immunity, unstoppable, priority, forced movement, snapshot and lifecycle behavior are present. |
| Projectile | **Implemented** | Stable UID, pending spawn lifecycle, deterministic hit order, effect dispatch, snapshot/restore/resolve and owner/target validation are present. |
| Equipment / shop / gold | **Verified** | Fixed-point Bake/runtime values, explicit sell rate, stable shop log/snapshot validation, sole-owner `GoldIncomeRuntime`, batch digest and checksum integration are present. |
| Snapshot / rollback | **Verified** | One aggregate snapshot tree with stable identity, explicit Restore/Resolve/Rebuild, random/physics/module state, invalid-reference failure, and canonical checksum path is implemented. |
| FrameSync / match flow | **Verified** | Canonical command header/bytes, stable collection, continuous AuthorityFrame acceptance, rollback boundary, recovery, match rules/statistics, gold digest, and shared checksum are present. |
| Player input | **Implemented** | Callbacks enqueue local events; processing resolves aim and creates canonical Commands. Focus/Commit/de-duplication present. AbilityInputProfileBaker and AbilityInputProfileProvider implement automatic CastModelDef to BakedPlayerAbilityInputProfile derivation. |
| Non-hero AI | **Implemented** | Stable UID ordering, typed runtime state, snapshot/restore. MinionSystem.SpawnWave creates actual minion units. MinionAIController has three-state FSM (AdvanceLane/EngageTarget/ReturnToLane) with target priority selection. |
| Movement / pathfinding | **Implemented** | Deterministic A* pathfinding (AStarPathService), PathFollower2D waypoint tracking, IndexedMinHeap open-set, and full UnitLocomotionAgent.Evaluate() pipeline are implemented. RVO and FlowField remain deferred. |
| Bootstrap / composition root | **Verified** | Client/server bootstrap scenes, `GameBootstrap`, configuration assets, Input Actions and a PlayMode smoke test exist. |
| Presentation bridge | **Scaffold** | Read-only event/output bridge exists; final render/audio/VFX/UI assets remain intentionally deferred and cannot write Gameplay. |
| UI / Lua | **Scaffold** | No formal project bridge yet; this does not block deterministic core framework execution. |

## Closed audit priorities

All previously recorded P0 and implementation-blocking P1 findings covered by ExecPlan 0046 are closed in the current repository: MonoBehaviour composition, lifecycle/physics identity, command/aim/input boundary, aggregate snapshot phases, Combat ordering/ownership, Projectile identity, Ability/Buff/CC state, Equipment/Gold fixed point and digest, Stats/XP ownership, NonHero stable state, AuthorityFrame/checksum, configuration, scenes, Input Actions, compilation and focused tests.

Remaining items are non-blocking generic capability work: Ability authoring/Bake (including automatic input-profile derivation), route execution, broader presentation/UI, and final content authoring. They are not regressions and do not justify parallel shortcut protocols.

## Permanent constraints

- Unit and current Handlers remain prefab-authored `MonoBehaviour`s; deterministic authority comes from stable IDs and explicit state, never Unity object identity.
- Inspector-facing numeric authoring may use `float`, but Gameplay calculations and persisted authoritative values use `fp`.
- Production heroes, specific abilities, Buffs, equipment, map objects and balance values remain out of scope unless explicitly requested.
- The intentional tracked deletions accepted by D-024 remain the current implementation baseline and are not restored.
