# ExecPlan 0006 — PhysicsEntity2D Point/Circle Spatial Core

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This plan creates only the deterministic logical-space core of `PhysicsEntity2D`. It does not claim that PhysicsWorld, range queries, Unity Transform presentation sync, Unit spawning, or the complete Physics v13.1 module is implemented.

## 1. Purpose

Establish the first production-owned 2D logical-space component used later by Units and Projectiles. The component stores one fixed-point pose and one Point or Circle shape, derives its fixed-point AABB after every approved spatial write, and never treats the Unity Transform as Gameplay authority.

Observable production behavior:

```text
Given the same initial logical pose, shape and sequence of spatial API calls,
PhysicsEntity2D produces identical Transform2D, Shape and Bounds values.

Ordinary movement advances PrevPosition from the prior Position.
Teleport makes PrevPosition equal Position and therefore prevents a long sweep.
Point and Circle bounds update immediately after pose, facing or shape changes.
Gameplay spatial methods neither read nor write the GameObject Transform.
```

This is a reusable framework capability. It contains no production hero, ability, Buff, equipment, projectile definition, map object or balance data.

## 2. Progress

- [x] Confirm formal ExecPlans 0001 through 0005 are complete in the current repository records.
- [x] Re-read the Physics v13.1 ownership, spatial-write, Point/Circle, bounds, restore and recommended-order sections.
- [x] Re-check Assets and Packages for an existing `PhysicsEntity2D`, `PhysicsTransform2D`, `PhysicsShape2D`, `PhysicsBounds2D` or `PhysicsShapeKind` definition; none exists.
- [x] Confirm the approved fixed-point package supplies `fp`, `fp2` and deterministic fixed-point vector math.
- [x] Identify unresolved dependencies that must not be invented in this slice: `RuntimeUidQueryValue`, concrete `TeamId`, `ProjectileUid`, `GridMap`, `CellSpan` and the aggregate snapshot owner.
- [x] Create this self-contained ExecPlan before adding a Physics assembly or public spatial type.
- [x] Reconfirm the current Unity compile/Console baseline immediately before implementation.
- [x] Create the Physics runtime and focused test assemblies.
- [x] Implement the Point/Circle logical value contracts and AABB math.
- [x] Implement the formal `PhysicsEntity2D` logical write APIs and internal restore seam.
- [x] Add focused EditMode and PlayMode behavior tests.
- [x] Compile and inspect Console through Unity MCP.
- [x] Run targeted Physics EditMode and PlayMode suites, then the full relevant baselines.
- [x] Review duplicate contracts, dependency direction, deterministic math and scope.
- [x] Update this plan, `MODULE_STATUS.md` and `REPOSITORY_MAP.md` with actual results.

## 3. Surprises and discoveries

- Physics v13.1 is the sole owner of `PhysicsBounds2D` and specifies all Point/Circle AABB operations, but it does not publish a complete field layout for the value. A later section mentions `Bounds.CellSpan`, while the GridMap/CellSpan contract does not yet exist.
  Impact: this slice freezes only the minimal AABB value required by the formal Point/Circle equations: inclusive `Min` and `Max` fixed-point corners. Grid cell coverage remains derived PhysicsWorld state and is not added to this value yet.
- Physics v13.1 recommends `PhysicsEntityQueryInfo` and `RuntimeUidQueryValue` in the first broad landing stage, but the current repository has only `UnitUid`; it has no `ProjectileUid`, common runtime UID query value, or concrete `TeamId` contract.
  Impact: QueryInfo and business binding are excluded instead of creating a second identity DTO or prematurely modifying Unit contracts.
- Physics v13.1 requires `LateUpdate` to convert logical coordinates through `GridMap.ToWorld3D`, but no current GridMap contract or composition root exists.
  Impact: this slice intentionally does not add `LateUpdate`, authoring preview, Inspector conversion, gizmos or presentation flags. A later presentation-sync slice must add them atomically with the mapping contract.
- The formal sweep pseudocode uses `PrevPosition` as the sweep start and the current offset-adjusted Point/Circle center as the end.
  Impact: 0006 follows that exact pseudocode; it does not invent a previous-facing or previous-local-offset field.
- `PhysicsEntity2D` is formally a `MonoBehaviour`, while all authoritative calculations remain fixed-point and independent of Unity object identity or time.
  Impact: pure geometry is tested in EditMode, and component/GameObject boundary behavior is additionally tested in PlayMode.
- The fixed-point package exposes scalar absolute value through `fpmath.abs`, not `fp.Abs`.
  Impact: the first Unity import produced one local compiler error; the implementation was corrected to the package API and the following targeted/full runs compiled and passed.
- `fpmath.normalize` can produce an axis component one raw unit below mathematical one because fixed-point square-root normalization is quantized.
  Impact: the PlayMode test now verifies the exact package-normalized raw value and AABB derived from it. Production behavior remains deterministic and no float tolerance was introduced.

## 4. Decision log

### D-0006-01 — Dedicated lower-level Physics assembly

Create `FrameSyncMoba.Physics` as the sole current owner of the spatial contracts. It references only `Unity.Mathematics`, `Unity.Mathematics.FixedPoint` and UnityEngine required by `MonoBehaviour`.

It does not reference `FrameSyncMoba.Unit`, Deterministic services, Presentation, Input, networking or UOS. Future Unit and Projectile integrations depend toward Physics, never the reverse.

### D-0006-02 — Minimal AABB contract

`PhysicsBounds2D` contains only immutable `fp2 Min` and `fp2 Max` corners with the invariant:

```text
Min.x <= Max.x
Min.y <= Max.y
```

Creation, expansion and union helpers remain internal. `CellSpan` is excluded because it is a grid-derived value and its type/ownership is not yet frozen. Adding a future read-only derived CellSpan must not change the Min/Max meaning.

### D-0006-03 — Complete shape data, limited supported factories

Reserve explicit stable enum values for all four formally named shape kinds, but expose construction factories only for the implemented Point and Circle forms:

```text
Point   = 0
Circle  = 1
Segment = 2
Rect    = 3
```

`PhysicsShape2D` carries the formal field set so future shape support does not require a competing DTO. Public Point/Circle factories zero every unused field. `SetLogicShape` rejects unsupported or invalid shapes before mutating entity state.

### D-0006-04 — Entity owns all logical writes

External code receives immutable value copies through `Transform2D`, `Shape` and `Bounds`. The only public mutation path is the exact Physics v13.1 method set:

```text
SetLogicPosition
SetLogicPose
ApplyLogicPositionDelta
TeleportLogicPosition
SetLogicForward
SetLogicShape
```

No public per-field setter or Unity Transform fallback is added.

### D-0006-05 — Facing normalization is fixed-point and centralized

Non-negligible forward input is normalized with the package fixed-point math, and Right is always the clockwise perpendicular `(Forward.y, -Forward.x)` required by `PerpRight`. Zero or below-threshold input preserves the prior facing exactly. The threshold is one private fixed-point constant shared by all facing APIs and covered at its boundary; no float/double conversion occurs at runtime.

### D-0006-06 — Bounds follow the formal Point/Circle equations

The current world point/center is:

```text
Position + Right * LocalOffset.x + Forward * LocalOffset.y
```

Non-swept Point uses a zero-area AABB. Circle expands its center by Radius. Swept Point uses the component-wise min/max of `PrevPosition` and the current world point. Swept Circle unions its current circle AABB with the `PrevPosition`-to-current-center segment AABB expanded by Radius.

### D-0006-07 — Restore seam without a snapshot DTO

Add one internal `RestoreLogicSpatialState(in PhysicsTransform2D transform, in PhysicsShape2D shape)` seam. It assigns the exact stored transform and sanitized shape, then recomputes Bounds. It does not call ordinary movement APIs and therefore preserves `PrevPosition` exactly.

This is not an aggregate snapshot protocol. The future owning Unit/Projectile restore process will call this seam after its snapshot schema is implemented.

### D-0006-08 — Presentation work remains absent, not stubbed

0006 does not add an empty `LateUpdate`, placeholder `PresentationDirty`, unused `LogicStateInitialized`, authoring float fields or a fake GridMap conversion. PlayMode tests verify that the logical APIs leave Unity Transform unchanged. Physics v13.1 section 16.2 remains a later coherent slice.

## 5. Current repository context

- Unity version is `2022.3.62f1c1`.
- Formal 0005 records a clean imported compile state, empty post-refresh Console, 46/46 full EditMode tests and no PlayMode fixtures.
- Current production assemblies are:

```text
FrameSyncMoba.Deterministic
    noEngineReferences=true
    references Unity.Mathematics and Unity.Mathematics.FixedPoint

FrameSyncMoba.Unit
    noEngineReferences=true
    no explicit references
```

- `FrameSyncMoba.Unit` currently owns `UnitUid`, minimal `Unit`, internal `UnitRegistry` and `UnitWorld`; it has no Physics reference or spawn path.
- No project-authored Physics assembly or logical-space production type exists.
- Scene colliders and geometry are content/presentation assets and are not authoritative Gameplay physics.
- `GameScene` was clean at the formal 0005 baseline. This plan requires no scene, prefab, ScriptableObject or Input Actions edit.
- The resolved fixed-point package is `com.danielmansson.mathematics.fixedpoint@d44836cab6`, assembly `Unity.Mathematics.FixedPoint`.

Expected implementation paths:

```text
Assets/Scripts/FrameSyncMoba/Physics/AssemblyInfo.cs
Assets/Scripts/FrameSyncMoba/Physics/FrameSyncMoba.Physics.asmdef
Assets/Scripts/FrameSyncMoba/Physics/PhysicsShapeKind.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsTransform2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsShape2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsBounds2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsGeometry2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsEntity2D.cs
Assets/Tests/EditMode/Physics/FrameSyncMoba.Physics.Tests.asmdef
Assets/Tests/EditMode/Physics/PhysicsShape2DTests.cs
Assets/Tests/EditMode/Physics/PhysicsGeometry2DTests.cs
Assets/Tests/PlayMode/Physics/FrameSyncMoba.Physics.PlayModeTests.asmdef
Assets/Tests/PlayMode/Physics/PhysicsEntity2DPlayModeTests.cs
Docs/Implementation/Plans/0006_physics_entity_2d_point_circle_spatial_core_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md
```

Exact file grouping may be reduced without merging ownership boundaries or expanding behavior. No existing production asmdef is modified by this slice.

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md`
  - selects `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md` as the Physics owner;
  - selects Unit v27.3 and Snapshot appendix v7.2 for downstream ownership boundaries.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
  - 1.1–1.2: sole Physics spatial-contract ownership and fixed-point authority;
  - 2.1–2.2: `PhysicsEntity2D` MonoBehaviour and authoritative Transform/Shape/Bounds ownership;
  - 2.5: exact `PhysicsTransform2D` members;
  - 2.6: formal spatial write APIs, previous-position, teleport, facing, shape and restore behavior;
  - 2.7 and 12: Gameplay methods must not read/write Unity Transform; LateUpdate is a later presentation boundary;
  - 4.1–4.5: shape kinds, formal shape data, Point/Circle world-space and AABB equations;
  - 14.5: Bounds is rebuilt after restore and spatial grids are not restored as serialized buckets;
  - 16.1–16.2: Point/Circle first, then presentation/scene and complete shape/grid work.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - 7.3 and 9.2: future synchronous spawning initializes Physics through `SetLogicPose` before registration;
  - 7.15: UnitWorld later owns aggregate restore/rebuild;
  - core conclusion 20: Physics alone defines `PhysicsEntity2D`, and Unit/movement use its public logical APIs.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - authoritative logical state is captured by its owning aggregate; derived spatial indices and Unity Transform are rebuilt/not authoritative.
- `Docs/Architecture/DECISION_LOG.md`
  - D-020 excludes production content;
  - D-022 selects package `fp` for Gameplay and permits float only at authoring conversion boundaries;
  - D-023 requires proportional feature tests;
  - D-024 accepts the current implementation baseline.
- `Docs/Implementation/ROADMAP.md`
  - Phase 2 requires Unit registration before complete spawn/lifecycle;
  - Phase 3 requires logical physics and range foundations before movement/query integrations.
- `Docs/Implementation/Plans/0005_unit_world_stable_registry_kernel_execplan.md`
  - records PhysicsEntity2D Point/Circle as the approved next framework direction.

## 7. In scope

- new project-owned `FrameSyncMoba.Physics` runtime assembly;
- immutable fixed-point `PhysicsTransform2D`, `PhysicsShape2D` and `PhysicsBounds2D` values;
- stable `PhysicsShapeKind` enum values;
- validated public Point and Circle shape factories;
- formal `PhysicsEntity2D : MonoBehaviour` logical state properties and spatial-write APIs;
- deterministic fixed-point facing normalization and right-vector derivation;
- immediate Point/Circle AABB rebuild after pose, facing, shape and restore changes;
- Point/Circle `SweepFromPrev` bounds exactly as described by Physics v13.1;
- internal restore seam preserving exact previous/current pose without a snapshot DTO;
- pure EditMode geometry/state tests and minimal PlayMode GameObject/Transform boundary tests;
- Unity MCP compile, Console and targeted/full test validation;
- status/repository documentation updates after implementation.

## 8. Out of scope

- `PhysicsEntityQueryInfo`, `RuntimeUidQueryValue`, `PhysicsEntityKind`, TeamId or Owner binding;
- Unit/Projectile reference fields, Unit asmdef changes, synchronous SpawnUnit or Physics registration;
- `PhysicsWorld`, spatial grids, GridMap, CellSpan, RangeQuery or ProjectileHitQuery;
- Segment/Rect geometry, narrow-phase intersections or collision events;
- pathfinding, RVO, wall penetration or movement handlers;
- public snapshot structs, aggregate capture, serialization bytes, checksum, rollback coordinator or grid rebuild;
- `PresentationDirty`, `LogicStateInitialized`, `LateUpdate`, Unity Transform sync, authoring preview, Inspector fields or gizmos;
- scenes, prefabs, ScriptableObjects, Input Actions, Packages or ProjectSettings;
- Unity Rigidbody/Collider authority or `UnityEngine.Physics`/`Physics2D` queries;
- production heroes, abilities, Buffs, equipment, units, projectiles, map objects or balance values.

## 9. Affected assemblies

```text
FrameSyncMoba.Physics
    new production assembly
    references Unity.Mathematics and Unity.Mathematics.FixedPoint
    noEngineReferences=false because PhysicsEntity2D is a MonoBehaviour
    autoReferenced=false
    does not reference FrameSyncMoba.Unit or FrameSyncMoba.Deterministic

FrameSyncMoba.Physics.Tests
    new Editor-only EditMode test assembly
    references FrameSyncMoba.Physics and the two mathematics assemblies

FrameSyncMoba.Physics.PlayModeTests
    new test assembly for GameObject/MonoBehaviour boundary checks
    references FrameSyncMoba.Physics and the two mathematics assemblies
```

Dependency direction:

```text
Unity.Mathematics.FixedPoint -> FrameSyncMoba.Physics
FrameSyncMoba.Physics -> future Unit / Projectile / Pathfinding consumers
FrameSyncMoba.Physics -> Physics tests

FrameSyncMoba.Physics -/-> Unit
FrameSyncMoba.Physics -/-> Presentation / Input / Transport / UOS
```

The first arrow block denotes upstream-to-downstream ownership, not asmdef reference syntax: consumers reference Physics; Physics never references its business consumers.

## 10. Exact production types

```text
FrameSyncMoba.Physics.PhysicsShapeKind
FrameSyncMoba.Physics.PhysicsTransform2D
FrameSyncMoba.Physics.PhysicsShape2D
FrameSyncMoba.Physics.PhysicsBounds2D
FrameSyncMoba.Physics.PhysicsEntity2D
FrameSyncMoba.Physics.PhysicsGeometry2D (internal)
```

No UID, Command, Snapshot, Aim, AbilitySignal, Checksum, FixedPoint wrapper or Runtime DTO type is added.

## 11. Public contracts

The implementation must preserve these public semantics and signatures:

```csharp
public enum PhysicsShapeKind : byte
{
    Point = 0,
    Circle = 1,
    Segment = 2,
    Rect = 3,
}

public readonly struct PhysicsTransform2D
{
    public fp2 Position { get; }
    public fp2 PrevPosition { get; }
    public fp2 Forward { get; }
    public fp2 Right { get; }
}

public readonly struct PhysicsShape2D
{
    public PhysicsShapeKind Kind { get; }
    public fp2 LocalOffset { get; }
    public fp Radius { get; }
    public fp Length { get; }
    public fp Width { get; }
    public fp2 HalfExtents { get; }
    public bool SweepFromPrev { get; }

    public static PhysicsShape2D CreatePoint(
        fp2 localOffset,
        bool sweepFromPrev = false);

    public static PhysicsShape2D CreateCircle(
        fp2 localOffset,
        fp radius,
        bool sweepFromPrev = false);
}

public readonly struct PhysicsBounds2D
{
    public fp2 Min { get; }
    public fp2 Max { get; }
}

public sealed class PhysicsEntity2D : MonoBehaviour
{
    public PhysicsTransform2D Transform2D { get; private set; }
    public PhysicsShape2D Shape { get; private set; }
    public PhysicsBounds2D Bounds { get; private set; }

    public void SetLogicPosition(fp2 position);
    public void SetLogicPose(fp2 position, fp2 forward);
    public void ApplyLogicPositionDelta(fp2 delta);
    public void TeleportLogicPosition(fp2 position);
    public void SetLogicForward(fp2 forward);
    public void SetLogicShape(in PhysicsShape2D shape);
}
```

`PhysicsTransform2D` construction, general shape construction, bounds construction and `RestoreLogicSpatialState` remain internal. Both named Physics test assemblies receive friend access only where required to validate internal fixed-point geometry and restore behavior; no production consumer gains an alternate mutation path.

## 12. Ownership and dependency direction

- `PhysicsEntity2D` is the sole mutable owner of Transform2D, Shape and Bounds.
- Shape and transform values are copied into/out of the component; callers cannot retain a mutable alias.
- Bounds is derived only from the entity's current logical state.
- Business identities, teams and owner objects remain owned by Unit/Projectile modules.
- Future Unit/Projectile aggregate restore owns lifecycle and calls Physics internal restore through an explicitly reviewed assembly seam; Physics does not locate owners or self-register.
- Communication in this slice is synchronous direct method calls. There are no events, global service locators, Unity callbacks, reflection or string dispatch.
- No scene/bootstrap change is required. Future prefabs host the component; future application roots compose PhysicsWorld separately.

## 13. Deterministic ordering and numeric rules

- This slice owns no collection and therefore introduces no iteration order.
- Every calculation uses `fp`/`fp2`; no authoritative float or double is stored or computed.
- No Unity time, `UnityEngine.Random`, Unity object identity, Transform value, hierarchy order, component registration order, Dictionary/HashSet enumeration or Presentation state participates.
- Facing normalization and all AABB min/max/expand/union operations use fixed-point package math only.
- Each public mutation is O(1), allocation-free, LINQ-free and failure-atomic.
- Enum numeric values are explicit and stable for future snapshot/serialization use; 0006 itself writes no canonical bytes.

## 14. Snapshot and serialization impact

No aggregate snapshot type, serializer, checksum member or byte layout is added.

Future ownership is prepared as follows:

```text
Authoritative cross-Tick candidate state:
    PhysicsTransform2D
    PhysicsShape2D

Derived/rebuilt state:
    PhysicsBounds2D
    future spatial-grid buckets / CellSpan

Non-authoritative and absent in 0006:
    Unity Transform
    presentation flags
```

The internal restore seam preserves `Position`, `PrevPosition`, `Forward`, `Right` and Shape exactly, validates invariants, and recomputes Bounds. It does not repair invalid facing pairs or unsupported shapes silently; invalid restored state fails deterministically before mutation.

Exact aggregate membership and canonical field order remain owned by the selected Snapshot appendix and future Unit/Projectile snapshot plans.

## 15. Implementation steps

1. Through Unity MCP, confirm idle compilation and inspect the pre-change Console. Stop only for an external compile blocker that prevents this slice from compiling.
2. Create `FrameSyncMoba.Physics.asmdef` with the exact lower-level dependencies and no Unit/Presentation edge.
3. Add stable `PhysicsShapeKind` and immutable transform, shape and AABB values. Expose only validated Point/Circle factories.
4. Add allocation-free internal fixed-point geometry helpers for perpendicular facing, Point/Circle world parameters, AABB creation, expansion and union.
5. Add `PhysicsEntity2D` with the formal public properties and write APIs. Centralize shape validation, facing normalization and one Bounds recomputation path.
6. Add the internal restore seam. Validate the entire input before assigning any component state.
7. Add Editor-only pure tests for shape factories, validation, facing math, Point/Circle bounds, sweep bounds and deterministic repetition without constructing GameObjects.
8. Add focused PlayMode tests using temporary GameObjects for the public pose transitions, failure atomicity, restore preservation and Unity Transform boundary. Destroy all test objects in teardown.
9. Refresh/compile through Unity MCP, wait for idle, read all relevant Console entries, and fix only 0006-scoped problems.
10. Run targeted Physics EditMode and PlayMode suites, then full EditMode and PlayMode baselines.
11. Search production code for duplicate spatial/protocol types, float/double authority, Unity Transform reads/writes, Unity time/random/object identity, unordered collection enumeration, empty implementations, swallowed exceptions, disabled tests and TODO substitutes.
12. Record exact results in this plan and synchronize `MODULE_STATUS.md`; update `REPOSITORY_MAP.md` because a production assembly and protocol owner are added.

## 16. EditMode tests

At minimum:

- Point and Circle factories preserve formal fields, zero unused fields and assign explicit kinds;
- negative Circle radius and unsupported shape construction fail deterministically;
- fixed-point facing normalization produces the exact clockwise Right for non-negligible input;
- zero/below-threshold facing input is classified consistently without division by zero;
- Point bounds cover the offset-adjusted current point;
- swept Point bounds cover formal PrevPosition-to-current-world-point component minima/maxima;
- Circle bounds expand the offset-adjusted center by Radius;
- swept Circle bounds union the current circle with the expanded PrevPosition-to-current-center segment;
- repeated identical geometry inputs produce equal raw fixed-point results;
- no test merely verifies that a struct or component can be created.

## 17. PlayMode tests

PlayMode is required because the formal production owner is a MonoBehaviour hosted by a GameObject.

At minimum:

- adding `PhysicsEntity2D` to a temporary GameObject and applying the public pose/shape APIs produces the expected fixed-point state and Bounds;
- `SetLogicPosition` copies old Position to PrevPosition and recomputes Bounds;
- `ApplyLogicPositionDelta` has exactly the same transition as `SetLogicPosition(current + delta)`;
- `TeleportLogicPosition` sets Position and PrevPosition to the same value;
- `SetLogicPose` normalizes a non-negligible forward and derives the exact clockwise Right;
- zero/below-threshold input preserves the prior Forward/Right;
- `SetLogicForward` changes facing without changing Position/PrevPosition and refreshes offset-dependent Bounds;
- `SetLogicShape` refreshes Bounds immediately without changing pose;
- invalid shape assignment fails before any component state changes;
- internal restore preserves distinct PrevPosition/Position and supplied facing, then derives identical Bounds; invalid restore input is failure-atomic;
- repeated identical public operation sequences produce equal raw fixed-point Transform/Shape/Bounds values on separate components;
- a GameObject with a deliberately unrelated Unity position/rotation does not seed or alter logical state;
- public logical writes and one rendered frame do not change the Unity Transform because 0006 has not implemented the later `LateUpdate` presentation slice;
- no scene asset is loaded, created or saved; all temporary objects are destroyed by the fixture.

## 18. Unity MCP validation

Use Unity MCP for all Unity-side validation:

1. Refresh the AssetDatabase / trigger script import and wait until `IsCompiling=false` and `IsUpdating=false`.
2. Read the complete relevant Console; record compiler errors and warnings instead of clearing evidence before capture.
3. Run `FrameSyncMoba.Physics.Tests` in EditMode.
4. Run the full EditMode suite and require all pre-existing tests to remain green.
5. Run `FrameSyncMoba.Physics.PlayModeTests` in PlayMode.
6. Run the full PlayMode suite and record discovery/pass/fail/skip counts.
7. Confirm the open project scene remains clean and unchanged.

Tests must not be deleted, disabled, weakened or changed to accept non-deterministic output.

## 19. Failure conditions and recovery

Stop implementation and do not mark 0006 complete if:

- an existing authoritative spatial type or public contract is discovered that conflicts with this plan;
- Point/Circle implementation requires defining `RuntimeUidQueryValue`, TeamId, ProjectileUid, GridMap/CellSpan or modifying UnitUid;
- the selected design requires a different public Bounds meaning that cannot coexist with immutable Min/Max AABB corners;
- a Package must be added or an out-of-scope core public protocol must change;
- an external repository compile blocker prevents validation and cannot be fixed inside 0006.

Ordinary fixed-point helper or Unity test-assembly details are local implementation work and do not require stopping.

Partial work is isolated to the new Physics runtime/test folders and documentation. Resume by reading Progress and current Console output. Do not restore historical deleted files, modify unrelated modules, or erase test evidence.

## 20. Completion criteria

- `FrameSyncMoba.Physics` is the sole project-owned Physics spatial-contract assembly and has no Unit/Presentation/Input/network dependency;
- all exact public types and spatial-write methods in section 11 exist without alternate per-field write paths;
- Point/Circle shape construction is validated and unsupported forms cannot partially mutate an entity;
- ordinary movement, delta, teleport, pose, facing and shape changes implement their formal PrevPosition/Bounds semantics;
- Point/Circle local offsets and sweep bounds follow Physics v13.1 exactly;
- restore preserves authoritative pose/shape and rebuilds Bounds without using ordinary movement APIs;
- no authoritative float/double, Unity time/random/object identity, Unity Transform, unordered enumeration or Presentation writeback participates;
- pure deterministic behavior tests pass in EditMode and MonoBehaviour/Transform-boundary tests pass in PlayMode;
- Unity compilation and Console are clean of new product diagnostics, and all prior relevant tests remain green;
- no UID, Command, Snapshot DTO, Aim, AbilitySignal, Checksum, fixed-point wrapper or runtime identity DTO is duplicated;
- no Package, existing asmdef, scene, prefab, ScriptableObject, Input Actions, ProjectSettings or production content changes;
- this plan, `MODULE_STATUS.md` and `REPOSITORY_MAP.md` record the actual implementation and remaining limitations.

## 21. Production-content exclusion

All test inputs use neutral synthetic positions, facings, offsets and radii. No type, asset or fixture is named after a production hero, ability, Buff, equipment item, unit archetype, projectile, monster, lane or map feature. Design examples remain acceptance semantics only.

## 22. Why this slice is next

This slice is preferred over the other deferred candidates because it closes the immediate dependency between the completed Unit identity/registry kernel and later synchronous Unit spawning without inventing Prototype, Team or Projectile identity contracts.

Comparison:

| Candidate | Observable result | Why now / why later |
|---|---|---|
| `PhysicsEntity2D` Point/Circle spatial core — selected | Deterministic logical pose, shape and AABB respond correctly to formal writes on a real component | Immediate prerequisite for Unit/Projectile spatial binding; exact Point/Circle semantics are sufficiently frozen and form a small EditMode+PlayMode loop |
| Unit synchronous spawn integration | SpawnUnit returns a registered, spatially initialized UnitUid | Still requires Physics host plus unresolved Prototype/prefab/Team/query identity contracts, so implementing it now would create placeholders or broaden scope |
| Deterministic random geometry | Fixed-point random point/direction helpers produce replayable samples | Contract is implementable but has no current production consumer and does not unblock Unit spawning or spatial ownership |

The selected work is one cohesive spatial-state system, not several independent systems. PhysicsWorld, range query and presentation synchronization remain separate later plans.

## 23. Results

```text
Production files added/changed:
    Assets/Scripts/FrameSyncMoba/Physics/AssemblyInfo.cs
    Assets/Scripts/FrameSyncMoba/Physics/FrameSyncMoba.Physics.asmdef
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsShapeKind.cs
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsTransform2D.cs
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsShape2D.cs
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsBounds2D.cs
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsGeometry2D.cs
    Assets/Scripts/FrameSyncMoba/Physics/PhysicsEntity2D.cs

Public contracts added/changed:
    PhysicsShapeKind with explicit Point/Circle/Segment/Rect values
    immutable PhysicsTransform2D, PhysicsShape2D and PhysicsBounds2D
    PhysicsShape2D.CreatePoint / CreateCircle
    PhysicsEntity2D logical state properties and six formal spatial-write APIs

Internal contracts added:
    PhysicsGeometry2D fixed-point facing and Point/Circle AABB operations
    PhysicsEntity2D.RestoreLogicSpatialState
    friend access for the two named Physics test assemblies only

Tests added/changed:
    Assets/Tests/EditMode/Physics/PhysicsShape2DTests.cs (4 cases)
    Assets/Tests/EditMode/Physics/PhysicsGeometry2DTests.cs (7 cases)
    Assets/Tests/PlayMode/Physics/PhysicsEntity2DPlayModeTests.cs (8 cases)
    two focused test asmdefs

Unity compilation and Console:
    The first import exposed fp.Abs as an incorrect package API assumption.
    It was changed to fpmath.abs; subsequent Unity test imports compiled.
    Final editor state was idle and the relevant runs produced no new product diagnostic.

Targeted EditMode:
    FrameSyncMoba.Physics.Tests: 11 returned cases passed, 0 failed, 0 skipped.
    The MCP summary separately reported 57 project-discovered EditMode cases.

Full EditMode:
    57 passed, 0 failed, 0 skipped.

Targeted PlayMode:
    FrameSyncMoba.Physics.PlayModeTests: 8 passed, 0 failed, 0 skipped.

Full PlayMode:
    8 passed, 0 failed, 0 skipped.

Deterministic invariants verified:
    All authoritative calculations use fp/fp2 and fixed-point package math.
    Ordinary movement, delta, teleport, facing and shape updates preserve formal
    PrevPosition and immediate Bounds rules.
    Point/Circle LocalOffset and SweepFromPrev AABBs use the formal equations.
    Restore preserves exact pose/shape and rebuilds derived Bounds.
    Logical writes do not read or write Unity Transform, even across a frame.
    Invalid shapes fail before component state mutation.

Duplicate/dependency review:
    These are the sole current project definitions of the five spatial contracts.
    FrameSyncMoba.Physics references only the two mathematics assemblies plus the
    UnityEngine surface required by MonoBehaviour; it has no Unit, Presentation,
    Input, networking or UOS dependency.

Remaining limitations:
    QueryInfo/runtime UID mirrors, PhysicsWorld, grids/CellSpan, range queries,
    Segment/Rect, Unit/Projectile binding, authoring, LateUpdate presentation
    synchronization and aggregate snapshot ownership remain unimplemented.

Scope-external changes:
    None. No Package, existing asmdef, Unit code, scene, prefab, ScriptableObject,
    Input Actions, ProjectSettings or production content changed.
```
