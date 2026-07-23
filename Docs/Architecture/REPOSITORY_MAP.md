# FrameSyncMobaDemo — Repository Map

> Last verified: 2026-07-22 during ExecPlan 0046 closure.  
> This map describes the current filesystem and Unity MCP evidence, not the repository state before the accepted D-024 deletions.

## Unity project baseline

| Item | Current location / result |
|---|---|
| Unity version | 2022.3.62f1c1 |
| Gameplay source root | `Assets/Scripts/FrameSyncMoba/` |
| Current design authority | `Docs/Architecture/DESIGN_INDEX.md` → `Docs/Design/` |
| Fixed point | `Unity.Mathematics.FixedPoint.fp` from `com.danielmansson.mathematics.fixedpoint` |
| Client/server composition scenes | `Assets/Scenes/ClientBootstrap.unity`, `Assets/Scenes/ServerBootstrap.unity` |
| Input Actions | `Assets/Input/Gameplay.inputactions` |
| Global configuration | `Assets/Config/Runtime/GlobalPrefabTable.asset`, `Assets/Config/Runtime/GlobalGameplayData.asset` |
| Composition root | `FrameSyncMoba.Bootstrap.GameBootstrap` |
| Unity validation | AssetDatabase refresh/compile succeeds; no C# compiler/product Error. MCP log-clear itself reports an `ai-editor-logs.txt` file lock |

## Project assembly map

| Assembly | Direct project dependencies | Ownership |
|---|---|---|
| `FrameSyncMoba.Deterministic` | none | Tick context, random, canonical primitives, deterministic helpers |
| `FrameSyncMoba.Physics` | none | Fixed-point shapes, grid, contacts, logical physics state |
| `FrameSyncMoba.RuntimeConfig` | none | Unity authoring/configuration assets and baked values |
| `FrameSyncMoba.Unit` | Deterministic, Physics, RuntimeConfig | Unit/Handler composition, gameplay modules, lifecycle, module snapshots |
| `FrameSyncMoba.FrameSync` | Deterministic, Unit, Physics, RuntimeConfig | Commands, authority frames, rollback, aggregate snapshot/checksum, match flow |
| `FrameSyncMoba.PlayerInput` | FrameSync, Unit, Deterministic, Physics | Input callbacks, local buffer, aim resolution, command requests |
| `FrameSyncMoba.Bootstrap` | RuntimeConfig, Deterministic, Physics, Unit, FrameSync, PlayerInput | Client/server application composition root |
| `FrameSyncMoba.Testing` | Deterministic, Physics, RuntimeConfig, Unit | Shared Editor-only deterministic fixtures |
| `FrameSyncMoba.Deterministic.Tests` | Deterministic | EditMode tests |
| `FrameSyncMoba.Physics.Tests` | Physics | EditMode tests |
| `FrameSyncMoba.Physics.PlayModeTests` | Physics | Unity boundary PlayMode tests |
| `FrameSyncMoba.Unit.Tests` | Unit, Physics, RuntimeConfig, Deterministic, Testing | EditMode module tests |
| `FrameSyncMoba.Unit.PlayModeTests` | Unit prerequisites | MonoBehaviour/GameObject lifecycle tests |
| `FrameSyncMoba.FrameSync.Tests` | FrameSync prerequisites, Testing | EditMode protocol/rollback tests |
| `FrameSyncMoba.PlayerInput.Tests` | PlayerInput prerequisites, Testing | EditMode input-state tests |
| `FrameSyncMoba.Bootstrap.PlayModeTests` | Bootstrap and composed runtime assemblies | Scene/composition PlayMode smoke test |

External/vendor asmdefs under Plugins and UOS are not part of the Gameplay dependency graph.

```text
Deterministic ─┐
Physics ───────┼──> Unit ───────┐
RuntimeConfig ─┘       │        ├──> FrameSync ──> PlayerInput
        └──────────────┴────────┘          └──────────┬───────┘
                                                      v
                                                  Bootstrap
```

No project assembly cycle was found. Deterministic Gameplay assemblies do not depend on Presentation, UI, device state, or transport implementations.

## Runtime ownership map

| Domain | Primary code | Current composition / ownership |
|---|---|---|
| Unit and lifecycle | `Unit/Unit.cs`, `Unit/UnitWorld.cs`, `Unit/UnitHandler.cs` | Prefab-authored MonoBehaviour Unit/Handlers; UnitWorld owns stable topology and lifecycle APIs |
| Stats / XP | `Unit/Stats/` | `StatHandler` owns stats, modifiers, level and experience |
| Movement / path route data | `Unit/Movement/`, `Unit/Pathfinding/` | `MovementHandler` owns movement; route evaluation remains partial |
| Physics integration | `Physics/`, FrameSync physics binder | PhysicsWorld owns deterministic entity/contact state; Unit UID/team are preserved |
| Attack | `Unit/Attack/` | `AttackHandler` owns timing, target and snapshot state |
| Combat | `Unit/Combat/` | `CombatSystem` owns stable settlement/deferred queues; source systems own their handles |
| Ability | `Unit/Ability/` | `AbilityHandler` owns slots, sessions, blackboards and passive runtime |
| Buff | `Unit/Buff/` | `BuffHandler` owns Buff runtimes and source-stable restore |
| Crowd control | `Unit/CrowdControl/` | `CrowdControlHandler` owns handles, constraints and forced movement |
| Projectile | `Unit/Projectile/` | ProjectileWorld owns UID allocation, pending/active topology and snapshot |
| Equipment / gold | `Unit/Equipment/`, FrameSync gold integration | Equipment runtime owns inventory/shop log; `GoldIncomeRuntime` solely owns gold batches/digests |
| NonHero AI | `Unit/NonHero/` | UID-stable minion/camp/controller state and typed restore |
| Command / authority / rollback | `FrameSync/` | One canonical GameplayCommand, continuous AuthorityFrame pipeline, aggregate rollback/checksum |
| Player input | `PlayerInput/` | InputAction callbacks enqueue local events only; later frame processing creates Commands |
| Match | `FrameSync/MatchRuleRuntime.cs`, `MatchStatisticsRuntime.cs` | Runs on all simulation endpoints and participates in aggregate state/checksum |
| Bootstrap | `Bootstrap/GameBootstrap.cs` | Loads global assets and composes runtime services |

## Public protocol ownership audit

| Protocol | Authoritative owner | Duplicate result |
|---|---|---|
| Unit UID | `FrameSyncMoba.Unit.UnitUid` | one public type |
| Runtime UID adapter | Deterministic runtime UID contract | one project adapter; no competing Unit UID |
| Gameplay Command | `FrameSyncMoba.FrameSync.GameplayCommand` / `CommandHeader` | one schema and canonical byte path |
| Gameplay Snapshot | `FrameSyncMoba.FrameSync.GameplaySnapshot` | one aggregate tree; module snapshots are owned children |
| Aim | `FrameSyncMoba.Unit.AimSnapshot` | one tagged union |
| Ability signal | `FrameSyncMoba.Unit.AbilitySignal` / `AbilitySignalVerb` | one signal language |
| Shared checksum | `FrameSyncMoba.FrameSync.SharedGameplayChecksum` | one aggregate checksum path |
| Fixed point | package `Unity.Mathematics.FixedPoint.fp` | no project duplicate |
| Runtime DTOs | module-owned request/snapshot/view types | public simple-name scan found no competing duplicate protocol type |

## Snapshot, ordering and lifecycle

- Aggregate capture includes Unit topology, module state, deterministic random, physics pairs/state, Projectile, match state, and checksum-owned data.
- Restore is explicitly separated into Restore, Resolve, and Rebuild. Missing stable references fail deterministically rather than being silently removed.
- Gameplay-impacting collections use explicit stable keys: UnitUid, command canonical order, global Combat sequence, Projectile UID, or documented tuple order.
- Unit death enters through `UnitWorld.RequestEnterDying`, recovery through `RequestRecoverFromDying`, and formal death through `ConfirmUnitDeath`.
- D-009 is enforced: ordinary death does not globally clear Stat modifiers or CombatModifiers; each source owns removal/rebuild.

## Unity assets and composition

- `GlobalPrefabTable.asset` owns stable prefab lookup.
- `GlobalGameplayData.asset` owns TickRate, respawn, minion/jungle, shop sell, grid and growth authoring values. Unity MCP verified its serialized fields and forced Unity-side serialization.
- `Gameplay.inputactions` provides Gameplay and UI action maps required by the player-input design.
- Client/server bootstrap scenes host the composition entry point. PlayMode smoke validation confirms the root can compose from runtime-created equivalent assets.

## Closed and remaining findings

All P0 and implementation-blocking P1 findings recorded by the pre-0046 audit are closed in the current repository. The old findings about pure-C# Unit composition, default physics identity, untagged Aim, incomplete canonical Command, positional snapshots, unstable Combat contribution, float sell/runtime configuration, empty composition scenes, absent Input Actions, and missing focused tests are historical and must not be reintroduced as current blockers.

Non-blocking remaining framework work:

- Formal Ability authoring/targeting/indicator Bake pipeline, including automatic `CastModelDef` → player-input profile generation.
- Deterministic active-route execution in `UnitLocomotionAgent`.
- Broader presentation, UI/Lua and final content authoring.
- Local allocation/formatting cleanup that does not affect deterministic output.

No remaining item authorizes a duplicate UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint, or convenience runtime DTO.
