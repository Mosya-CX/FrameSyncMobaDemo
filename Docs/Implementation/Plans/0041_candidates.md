# Candidate Plans — Batch 0041–0043

> Created: 2026-07-22 (post 0040 Hit Reaction Integration)
> Based on: MODULE_STATUS.md Known Gaps + Design Docs v13.1 / v13.2 / v19
> Recommendation: A → B → C

---

## Candidate A: Pathfinding Foundation — Grid + A* + PathFollower (~720 lines)

**Gap**: Zero pathfinding. Units can only move via direct MoveCommand. A* search, grid map, path following, and UnitLocomotionAgent are all absent. This is the largest remaining framework gap.

**Design authority**: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §4–§9, §14–§15

### Assembly placement

All new types go into `FrameSyncMoba.Unit` (noEngineReferences: true — pure deterministic, no UnityEngine).

### New files — Unit/Pathfinding/ (~520 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `Unit/Pathfinding/PathNode.cs` | ~55 | A* node struct: `CellX`, `CellY`, `GCost` (fp), `HCost` (fp), `ParentIndex` (int), `Closed` (bool). `FCost => GCost + HCost`. Implements `IComparable<PathNode>` for stable heap ordering: FCost desc, tie-break by (CellX, CellY). |
| 2 | `Unit/Pathfinding/IndexedMinHeap.cs` | ~95 | Deterministic indexed binary min-heap for A* open-set. `Push(PathNode)`, `Pop()`, `DecreaseKey(int index, fp newFCost)`. Array-backed; internal indices stable within a search. `Clear()` reuses arrays via SearchId pattern. No boxing, no LINQ. |
| 3 | `Unit/Pathfinding/PathGridMap2D.cs` | ~130 | Binary obstruction grid. Fields: `fp2 WorldCenter`, `fp CellSize`, `int Width`, `int Height`, `bool[] Walkable`, `bool[] WalkableByRadius[RadiusClass]`. Methods: `WorldToCell(fp2 worldPos) → (int cx, int cy)`, `CellToWorld(int cx, int cy) → fp2`, `IsPassable(int cx, int cy, RadiusClass rc) → bool`, `GetNeighbors(int cx, int cy) → ReadOnlySpan<(int,int)>` (8-way clockwise stable order). `BuildFromPhysics(PhysicsWorld)` scans static obstructions; `RecalculateRadiusLayers()` erodes by radius class. |
| 4 | `Unit/Pathfinding/AStarPathService.cs` | ~180 | Deterministic A*. `SearchId` pattern: increment per search, skip reallocating ClosedSet. Methods: `FindPath(fp2 start, fp2 target, RadiusClass rc, int maxIterations = 1200) → PathResult`. Steps: (1) validate start/end cells, (2) neighbor-expand target if blocked (8-dir expanding search, max 3 cells radius), (3) indexed heap open-set, (4) octile heuristic (`h = max(dx,dy) * sqrt2Cost + min(dx,dy) * straightCost`), (5) LOS-based path smoothing on result indices. Internal state: `int _searchId`, `int[] _closedSetSearchIds`, `int[] _parentIndices`, `fp[] _gCosts`. All reused across searches. |
| 5 | `Unit/Pathfinding/PathFollower2D.cs` | ~110 | Consumes `int[] pathCellIndices` from A*. Fields: `int PathCursor`, `bool RouteFinished`, `fp ReachThreshold`, `fp PathCorridorTolerance`. Methods: `AdvanceCursor(fp2 currentPosition, PathGridMap2D grid)`: advances cursor when waypoint within ReachThreshold. `IsOutsideCorridor(fp2 position, PathGridMap2D grid) → bool`: checks lateral distance to current path-segment centerline. `BuildLocomotionResult(fp2 position, fp moveSpeed, PathGridMap2D grid) → LocomotionResult`: computes DesiredDirection from current waypoint, sets HasMovement/Status. Capture/Restore: saves `PathCursor`, `RouteFinished`, `pathCellIndices`. |
| 6 | `Unit/Pathfinding/UnitLocomotionAgent.cs` | ~100 | Per-unit locomotion decision-maker. Fields: `Unit Owner`, `PhysicsEntity2D Entity`, `MovementTask CurrentTask`, `RouteRuntime Route`, `PathFollower2D Follower`, `AStarPathService PathService`, `PathGridMap2D Grid`. Methods: `AcceptRouteRequest(RouteMoveRequest req) → MoveAcceptResult`: validates task, sets up RouteRuntime. `CancelRoute(MoveCancelReason reason)`: clears task. `Evaluate() → LocomotionResult`: (1) if no task → Idle, (2) if NeedRepath → call AStarPathService, (3) Follower.AdvanceCursor, (4) check outside-corridor → set NeedRepath, (5) check arrival → complete task, (6) Follower.BuildLocomotionResult. Gate: if `!Owner.CanRunActiveGameplayThisTick` → return Idle (spawn Tick still evaluates passives). |

### New files — Unit/Pathfinding/ data types (~50 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 7 | `Unit/Pathfinding/PathResult.cs` | ~25 | `bool Success`, `PathStatus Status` (enum: Success/InvalidStart/InvalidEnd/EndBlocked/NoPath/MaxIterationReached/SystemNotReady), `int[] PathCellIndices`. |
| 8 | `Unit/Pathfinding/MovementTask.cs` | ~25 | `MovePurpose Purpose` (enum: MoveToPosition/FollowTarget/Flee/MoveToLane), `MoveTarget Target` (fp2 Position or UnitUid Uid), `fp StopDistance`, `bool AllowRVO`, `bool AllowRepath`, `MovementTaskState State`. |

### Modified files (~200 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 9 | `Unit/Movement/MovementHandler.cs` | +65 | Add `ApplyRouteMovement(in LocomotionResult locomotion, in RvoResult rvo)`: computes final velocity from LocomotionResult + RVO blend, calls `PhysicsEntity2D.SetLogicPose(...)`. Add path-following mode to `ApplyMoveInput`: if `CurrentTask.Purpose == RouteMove`, delegate to Agent output. Add `LocomotionResult _lastLocomotion` field (per-tick transient, not snapshotted). |
| 10 | `Unit/Movement/MovementSnapshot.cs` | +25 | Add: `int CurrentWaypointIndex`, `int[] SnapshotPathCellIndices` (nullable, shallow copy on Capture). |
| 11 | `Unit/Core/Unit.cs` | +10 | Add `UnitLocomotionAgent Locomotion` property (nullable; null for structures/non-moving units). Add `bool CanRunActiveGameplayThisTick`: `LifeState == Alive && SpawnLogicTick < SimulationTickContext.Current.Tick`. |
| 12 | `Unit/Core/UnitWorld.cs` | +30 | Add `PathGridMap2D PathGrid` singleton property. `SpawnUnit()`: if prototype has LocomotionAgent, new `UnitLocomotionAgent(unit, entity, PathGrid, PathService)`. `TickAll()`: call `unit.Locomotion?.Evaluate()` before MovementHandler.Advance. |
| 13 | `FrameSync/SimulationTickPipeline.cs` | +40 | In `ExecuteTick`: (1) BuildRvoGrid from current positions, (2) for each unit: `locomotion = unit.Locomotion?.Evaluate()`, (3) DeterministicRVO stub or skip for first pass, (4) for each unit: `unit.Movement.Advance(locomotion, rvo)`, (5) WallPenetrationResolver pass, (6) BuildUnitFinalGrid. |
| 14 | `Unit/Pathfinding/RouteMoveRequest.cs` | ~15 | New struct: `fp2 TargetPosition` or `UnitUid TargetUid`, `MovePurpose`, `fp StopDistance`, `bool AllowRepath`. |
| 15 | `Unit/Pathfinding/LocomotionResult.cs` | ~25 | New struct: `UnitUid UnitUid`, `bool HasMovement`, `fp2 DesiredDirection`, `fp DesiredSpeed`, `RouteEvaluationStatus Status` (Idle/Moving/Reached/Blocked/NoRoute). Tick-local, not snapshotted. |

### Design conformance

- §4 PathGridMap2D: binary grid, radius layers, WorldToCell/CellToWorld
- §7 AStarPathService: SearchId reuse, indexed heap, octile heuristic, LOS smoothing
- §9 PathFollower2D: cursor advance, corridor check, arrival detection
- §6 UnitLocomotionAgent: single locomotion entry, Evaluate per tick, idle on spawn Tick
- §15 Snapshot: PathFollower state (cursor, path indices) captured; A* open/closed sets are rebuildable
- §14 LocomotionResult: tick-local, no cross-tick storage

### RVO — deferred

The DeterministicRVOSystem (§10) and RvoGrid (§10) are deferred to a follow-up plan. First pass uses direct LocomotionResult without RVO blending. Units will pathfind and move, but won't avoid each other dynamically. This is acceptable for initial pathfinding validation.

### Tests needed (~120 lines)

- `AStarPathServiceTests.cs`: straight line, obstacle detour, unreachable target, max iterations, stable order
- `PathGridMap2DTests.cs`: cell conversion round-trip, radius erosion, neighbor order
- `PathFollower2DTests.cs`: cursor advance, arrival detection, corridor deviation
- `MovementHandlerPathTests.cs`: LocomotionResult consumption, physics pose application

---

## Candidate B: Presentation Bridge Foundation (~550 lines)

**Gap**: Zero visual/audio feedback from Gameplay. Attack commits, ability casts, Buff applications, and hit reactions produce no VFX or SFX. The presentation layer (UnitPresentationHost, VisualEventOutput, VfxManager, AudioManager) is entirely scaffold.

**Design authority**: `Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md` §1–§11

### Assembly strategy

| Assembly | What | Why |
|---|---|---|
| `FrameSyncMoba.Unit` | `VfxEvent`, `SfxEvent`, `PresentationEventId`, `VisualEventOutput`, `PresentationEventId` structs | Pure data, no UnityEngine dependency |
| `FrameSyncMoba.FrameSync` | `UnitPresentationHost`, `UnitPresentationRegistry`, `UnitAnimationDriver`, `PresentationSyncManager` | MonoBehaviour, needs UnityEngine |
| New: `FrameSyncMoba.Presentation` | `VfxManager`, `AudioManager`, `GlobalPrefabTable` runtime | Clean separation; references Unit + UnityEngine |

**Decision**: For this slice, put MonoBehaviour components in `FrameSyncMoba.FrameSync` (already has UnityEngine access) to avoid creating a new asmdef. A dedicated `FrameSyncMoba.Presentation` assembly can be extracted later when VfxManager/AudioManager grow.

### New files — Unit/ (pure data, ~210 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `Unit/Presentation/PresentationEventId.cs` | ~45 | Stable event identity struct: `int SourceLogicTick`, `PresentationSourceKind SourceKind` (enum: Unit/Projectile), `UnitUid SourceRuntimeUid` (or generic id), `int EventSequence`, `int EventKey`. Implements `IEquatable<PresentationEventId>`. |
| 2 | `Unit/Presentation/VfxEvent.cs` | ~35 | Struct: `PresentationEventId Id`, `int VfxDefId`, `fp2 WorldPosition`, `fp2 WorldDirection`, `UnitUid? AttachToUnit`, `int SocketKey`, `fp DurationScale`, `int ChargeTicks` (for charge-ratio scaling). |
| 3 | `Unit/Presentation/SfxEvent.cs` | ~35 | Struct: `PresentationEventId Id`, `int SfxDefId`, `SfxAnchor Anchor` (enum: UnitRoot/Camera/World), `UnitUid? AttachToUnit`, `int SocketKey`, `fp PitchScale`, `fp VolumeScale`. `OneShotNoReplay` policy is in SfxDefinition, not here. |
| 4 | `Unit/Presentation/VisualEventOutput.cs` | ~60 | Static pure-data collection entry point. Fields: per-tick `List<VfxEvent> _vfxBuffer`, `List<SfxEvent> _sfxBuffer`. Methods: `SubmitVfx(in VfxEvent evt)`, `SubmitSfx(in SfxEvent evt)`. `ConsumeVfxEvents() → IReadOnlyList<VfxEvent>`: returns and clears buffer. `ConsumeSfxEvents() → IReadOnlyList<SfxEvent>`. Called by Gameplay systems (AttackHandler, AbilityHandler, CombatSystem, BuffHandler) during Tick execution; consumed by PresentationSyncManager at Tick end. Not a MonoBehaviour; no Unity object instantiation. |
| 5 | `Unit/Presentation/PresentationEventSequence.cs` | ~20 | Per-source sequence counter (for EventSequence assignment). `int _nextUnitSequence`, `int _nextProjectileSequence`. `int NextSequence(PresentationSourceKind kind)`. Reset on rollback restore. |
| 6 | `Unit/Presentation/VfxDefId.cs` | ~10 | Strongly-typed ID struct wrapping int, for VfxDefinition lookup. |
| 7 | `Unit/Presentation/SfxDefId.cs` | ~10 | Strongly-typed ID struct wrapping int, for SfxDefinition lookup. |

### New files — FrameSync/ (MonoBehaviour components, ~260 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 8 | `FrameSync/UnitPresentationHost.cs` | ~70 | MonoBehaviour on Unit root GO. Fields: `Unit OwnerUnit`, `UnitAnimationDriver AnimationDriver`, `PresentationSocketSet SocketSet`. `OnEnable()`: registers with `UnitPresentationRegistry`. `OnDisable()`: unregisters. Public read-only queries: `TryGetSocket(int key, out Transform)`. Does NOT manage VFX/SFX instances. |
| 9 | `FrameSync/UnitPresentationRegistry.cs` | ~45 | Static registry: `Dictionary<UnitUid, UnitPresentationHost>`. Methods: `Register(UnitUid, UnitPresentationHost)`, `Unregister(UnitUid)`, `TryGetHost(UnitUid, out host)`, `TryGetSocket(UnitUid, int socketKey, out Transform)`. Presentation-layer only; does not depend on UnitWorld. |
| 10 | `FrameSync/UnitAnimationDriver.cs` | ~100 | MonoBehaviour; reads Gameplay state and drives Animator. Fields: `Animator Animator`, `UnitAnimationProfile Profile`. `LateUpdate()`: (1) read `Owner.LifeState`, (2) read `ActionStateView.MainKind/BaseKind`, (3) if Attack: read `AttackHandler` state → resolve AttackState + normalized time, (4) if Cast: read `AbilityCastView` → resolve ability animation, (5) set Animator Bool/Int/Float/Trigger parameters. Handles: death pose hold, respawn transition, attack backswing restore, ability stage mapping. Does NOT read AbilityCastEvent. Does NOT write root Transform (PhysicsEntity2D owns that). |
| 11 | `FrameSync/PresentationSyncManager.cs` | ~65 | Per-tick presentation consumption. Methods: `ConsumeAllEvents()` called at end of `SimulationTickPipeline.ExecuteTick`. Steps: (1) `VisualEventOutput.ConsumeVfxEvents()` → iterate, call `VfxManager.PlayOrReconcile(evt)`, (2) `VisualEventOutput.ConsumeSfxEvents()` → iterate, call `AudioManager.PlayOrReconcile(evt)`, (3) for each active UnitPresentationHost: `host.AnimationDriver.LateUpdate()`. Handles rollback: during Replay, compares Expected vs current Playing set; removes stale, starts new. |

### Modified files (~80 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 12 | `Unit/Attack/AttackHandler.cs` | +15 | After successful Commit: construct `SfxEvent` with `SourceKind=Unit`, `EventSequence=committedAttackSequenceIndex`, `EventKey=commitSfxEventId`. Call `VisualEventOutput.SubmitSfx(in evt)`. |
| 13 | `Unit/Combat/CombatSystem.cs` | +10 | On damage applied: construct `VfxEvent` for hit impact VFX. On death confirmed: construct `VfxEvent` + `SfxEvent` for death effects. |
| 14 | `Unit/Buff/BuffHandler.cs` | +10 | On Buff created: construct `VfxEvent` for buff application. On Buff removed: construct `VfxEvent` for buff expiry (if configured). |
| 15 | `Unit/Ability/AbilityHandler.cs` | +10 | On Stage entry: if StageDef has Vfx/Sfx config, construct and submit events. |
| 16 | `FrameSync/SimulationTickPipeline.cs` | +25 | At end of `ExecuteTick`: call `PresentationSyncManager.ConsumeAllEvents()`. On rollback restore: call `PresentationSyncManager.ResetForRestore(SnapshotTick)`. |
| 17 | `Unit/Core/UnitWorld.cs` | +10 | `SpawnUnit()`: after creating Unit GO, ensure `UnitPresentationHost` is bound (lookup on prefab, register). `DestroyUnit()`: unregister presentation host. |

### New files — FrameSync/ managers (stubs, ~80 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 18 | `FrameSync/VfxManager.cs` | ~40 | Stub MonoBehaviour. `PlayOrReconcile(in VfxEvent evt)`: placeholder — logs event details. Full implementation (ParticleSystem pool, definition lookup, attach-to-socket) deferred to follow-up plan. |
| 19 | `FrameSync/AudioManager.cs` | ~40 | Stub MonoBehaviour. `PlayOrReconcile(in SfxEvent evt)`: placeholder — logs event details. Full implementation (AudioSource pool, OneShotNoReplay dedup, rollback reconciliation) deferred. |

### Design conformance

- §2 UnitPresentationHost: lightweight host, owns AnimationDriver + SocketSet, registers on enable
- §3 UnitAnimationDriver: reads LifeState/ActionStateView/AttackHandler/AbilityCastView; no AbilityCastEvent listening
- §4/5 VfxManager/AudioManager: consume independent event streams; stubs for now
- §8 PresentationEventId: SourceLogicTick + SourceKind + SourceRuntimeUid + EventSequence + EventKey
- §9 Typical flows: Attack Commit → SfxEvent; Stage entry → VfxEvent; no animation via VisualEvent
- §10.4 VFX/SFX interface: `VisualEventOutput.SubmitVfx/SubmitSfx`, consumed at Tick end
- §1.3 PhysicsEntity2D boundary: presentation never writes root Transform

### Tests needed (~80 lines)

- `PresentationEventIdTests.cs`: equality, hash code stability
- `VisualEventOutputTests.cs`: submit/consume cycle, buffer clearing
- `UnitPresentationRegistryTests.cs`: register/lookup/unregister, missing key

---

## Candidate C: On-Hit Effect Pipeline + Projectile AoE (~420 lines)

**Gap**: `ProjectileHitResolver` detects hits and calls `RegisterHit()` but no damage, Buff, or CC effects fire. Projectiles are currently visual-only gameplay-wise. The full projectile→target effect loop is incomplete.

**Design authority**: `Docs/Design/MOBA_FrameSync_Unity_Projectile_System_Design_v19.md` §on-hit pipeline + `Docs/Design/moba_combat_system_design_v13_2.md` §damage settlement

### New files — Unit/Projectile/ (~180 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `Unit/Projectile/ProjectileOnHitEffect.cs` | ~55 | Config data for on-hit effects. Nested types: `ProjectileOnHitDamage` (int Amount, DamageType Type, fp DamageRatio), `ProjectileOnHitBuff` (BuffConfigId BuffId, int DurationTicks), `ProjectileOnHitCC` (CrowdControlType CCType, int DurationTicks). Container struct: `ProjectileOnHitEffects` with `Damage[]`, `Buff[]`, `CC[]` arrays. |
| 2 | `Unit/Projectile/ProjectileEffectDispatcher.cs` | ~95 | Static dispatch logic. Methods: `DispatchOnHit(ProjectileRuntime proj, UnitUid targetUid, UnitWorld world)`: reads `proj.Def.OnHitEffects`, for each Damage → `world.Combat.RequestDamage(...)`, for each Buff → `target.BuffHandler.AddBuff(...)`, for each CC → `target.CC.ApplyCC(...)`. `DispatchAoE(ProjectileRuntime proj, fp2 center, fp radius, UnitWorld world)`: queries `PhysicsSpatialGrid` for targets in radius, calls `DispatchOnHit` for each. `ComputeDamageValue(ProjectileOnHitDamage dmg, StatHandler sourceStats) → int`: applies damage ratio to source stats if configured. |
| 3 | `Unit/Projectile/ProjectileAoEConfig.cs` | ~30 | Struct in ProjectileDef: `bool HasAoE`, `fp AoERadius`, `int MaxAoETargets`, `AoETrigger Trigger` (enum: OnDestroy/OnImpact/OnExpire). |

### Modified files (~240 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 4 | `Unit/Projectile/ProjectileDef.cs` | +30 | Add fields: `ProjectileOnHitEffects OnHitEffects`, `ProjectileAoEConfig AoE`. |
| 5 | `Unit/Projectile/ProjectileRuntime.cs` | +35 | Add `AoE` property from Def. In `TickUpdate()`: if AoE.Trigger == OnExpire and expiry check, call `EffectDispatcher.DispatchAoE(...)`. On `Destroy()`: if AoE.Trigger == OnDestroy, call `EffectDispatcher.DispatchAoE(...)`. |
| 6 | `FrameSync/ProjectileHitResolver.cs` | +35 | After `proj.RegisterHit(targetUid)`: call `ProjectileEffectDispatcher.DispatchOnHit(proj, targetUid, UnitWorld)`. If `proj.Def.AoE.Trigger == OnImpact`: call `ProjectileEffectDispatcher.DispatchAoE(proj, proj.Position, proj.Def.AoE.AoERadius, UnitWorld)`. |
| 7 | `Unit/Combat/CombatSystem.cs` | +25 | Add `RequestDamage(CombatRequestHeader header, int amount, DamageType type, UnitUid sourceUid)`: constructs DamageRequest, enqueues via existing settlement pipeline. Ensure projectile-sourced damage properly records contribution tracking. |
| 8 | `Unit/Combat/DamageRequest.cs` | +10 | Add optional `UnitUid? ProjectileSourceUid` field for kill-credit tracking. |
| 9 | `Unit/Projectile/ProjectileWorld.cs` | +20 | Add `CombatSystem Combat` reference (set during initialization). `Spawn()`: pass Combat reference to ProjectileRuntime. |
| 10 | `FrameSync/SimulationTickPipeline.cs` | +20 | Pass `CombatSystem` to `ProjectileWorld` during init. In `ExecuteTick`: after `ProjectileHitResolver.ProcessAllHits`, call `ProjectileWorld.TickAll()` (already exists) — ensure order: hit resolution before projectile movement tick. |

### Edge cases handled

| Case | Behavior |
|---|---|
| Projectile hits already-dead unit | `RegisterHit` checks `target.IsAlive`; skips if dead |
| AoE hits source unit | Skip self via UnitUid comparison |
| Multiple projectiles hit same target same tick | Each `RegisterHit` fires independently; Combat settlement deduplicates via request idempotency |
| Projectile with zero damage but Buff + CC | Damage loop skipped; Buff and CC still applied |
| AoE hits more than MaxAoETargets | Sort by distance from center, take first N |
| Kill credit for projectile kill | Uses `ProjectileSourceUid` on DamageRequest → tracked in DamageContributionTracker |

### Design conformance

- Projectile v19: on-hit effects, AoE radius, max targets
- Combat v13.2: damage settlement through CombatSystem.RequestDamage, deferred death/kill reactions
- Deterministic: all effect dispatch within Gameplay Tick; no Unity-side effect application
- Presentation: VfxEvent for hit impact is separate — this plan only handles Gameplay effects

### Tests needed (~100 lines)

- `ProjectileEffectDispatcherTests.cs`: on-hit damage, on-hit buff, on-hit CC, AoE multi-target
- `ProjectileHitResolverEffectTests.cs`: full pipeline from hit detection to damage application
- `ProjectileAoETests.cs`: OnDestroy AoE, OnImpact AoE, max targets enforcement, self-exclusion

---

## Comparison Matrix

| Dimension | A: Pathfinding | B: Presentation Bridge | C: On-Hit Effects |
|---|---|---|---|
| **Lines (new + modified)** | ~720 | ~550 | ~420 |
| **New files** | 8 | 19 (8 data + 11 MB) | 3 |
| **Modified files** | 7 | 6 | 7 |
| **Assemblies touched** | Unit, FrameSync | Unit, FrameSync | Unit, FrameSync |
| **Design doc** | Pathfinding v13.1 | Presentation v13.2 | Projectile v19 + Combat v13.2 |
| **Risk** | Medium (complex algorithm) | Low (mostly data plumbing + stubs) | Low (straightforward dispatch) |
| **Unlocks** | Real MOBA movement, AI pathing | Visual feedback for all systems | Complete projectile gameplay loop |
| **Blocked by** | PhysicsSpatialGrid (✅ done) | PhysicsEntity2D.LateUpdate (✅ done) | ProjectileHitResolver (✅ done) |
| **Dependencies** | None pending | None pending | CombatSystem settlement (✅ done) |

## Recommendation

**A → B → C**

1. **Pathfinding first** — it's the largest missing framework piece. Without it, units can't navigate the map, AI can't function, and the MOBA feel is absent. At ~720 lines it's substantial but well-scoped by the design doc's recommended Stage 3+4 landing.

2. **Presentation Bridge second** — once pathfinding gives units meaningful movement, visual feedback becomes the next priority. The stubbed VfxManager/AudioManager keep this plan focused on the data pipeline (VisualEventOutput → managers) without ballooning into full asset authoring.

3. **On-Hit Effects third** — closes the projectile loop. By this point, projectiles fly, hit, and now actually do something. Natural follow-up to having both pathfinding (targets reachable) and presentation (impacts visible).

All three plans are in the 400–720 line range, meeting the 300–1000 line target.
