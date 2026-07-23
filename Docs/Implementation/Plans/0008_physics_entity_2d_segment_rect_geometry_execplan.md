# ExecPlan 0008 — PhysicsEntity2D Segment/Rect Geometry

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This plan extends the existing formal 0006 spatial owner only from Point/Circle to Segment/Rect shape construction and AABB derivation. It does not implement PhysicsWorld, range queries, collision tests, Unity Transform synchronization or business identity binding.

## 1. Purpose

Complete the four-shape logical geometry family already owned by `FrameSyncMoba.Physics`.

Observable production behavior:

```text
Given the same fixed-point logical pose and Segment or Rect shape,
PhysicsEntity2D derives the same world-space shape parameters and inclusive AABB.

Changing Position, Forward or Shape immediately rebuilds those bounds.
Invalid dimensions fail before component state changes.
Gameplay geometry remains independent of Unity Transform, Unity Physics and render time.
```

This is a reusable framework slice. It adds no hero, ability, Buff, equipment, projectile, unit, map object or balance content.

## 2. Progress

- [x] Complete and Unity-verify formal ExecPlans 0006 and 0007.
- [x] Re-read Physics v13.1 sections 2.5–2.7, 4.1–4.5, 14.5 and 16.1.
- [x] Confirm `PhysicsShapeKind` and the complete `PhysicsShape2D` field family already have one authoritative owner.
- [x] Confirm Segment/Rect world parameters and AABB equations are explicitly specified by the current design.
- [x] Compare this slice with stable-container and checksum candidates.
- [x] Create this ExecPlan without starting its production implementation.
- [x] Reconfirm Unity compile, Console and clean-scene baseline before implementation.
- [x] Add validated Segment and Rect factories to the existing shape value.
- [x] Extend the existing internal fixed-point geometry and AABB calculation.
- [x] Add focused EditMode and PlayMode behavior tests.
- [x] Compile and inspect Console through Unity MCP.
- [x] Run targeted Physics and full EditMode/PlayMode baselines.
- [x] Review duplicate contracts, dependency direction, deterministic math and scope.
- [x] Update this plan, `MODULE_STATUS.md` and necessary `REPOSITORY_MAP.md` entries.
- [x] Record the owner's revised planning rule: a later planning task will prepare multiple candidate ExecPlans; this implementation-only task does not create 0009.

## 3. Surprises and discoveries

- Formal 0006 deliberately stored the complete shape field set and reserved explicit enum values for Segment and Rect while exposing only Point/Circle factories.
  Impact: 0008 extends one existing contract and owner; it must not introduce another shape DTO, enum or Physics assembly.
- Physics v13.1 defines `SweepFromPrev` as an available Segment field and optional Rect field, but its formal 4.5 AABB equations apply sweep union only to Point and Circle.
  Impact: 0008 preserves the flag in both shapes but does not invent swept Segment/Rect bounds or previous-facing state. Those semantics require a later explicitly designed query/collision slice.
- Segment/Rect orientation depends on the stored fixed-point `Forward`/`Right` basis. The future Unit spawn order sets pose before Physics registration, but that composition path is not yet implemented.
  Impact: tests initialize a valid logical facing before asserting oriented geometry. 0008 does not add a second initialization flag or business spawn API.
- The existing internal restore seam already accepts one `PhysicsShape2D` and rebuilds derived bounds.
  Impact: supporting the two shapes requires no new snapshot DTO or restore phase; focused restore behavior can reuse the existing seam.
- Unity compiled the implementation without a corrective production iteration. Exact fixed-point field, world-parameter and AABB assertions passed on the first imported test run.
  Impact: no tolerance, float conversion or alternate geometry mapping was introduced.
- The Physics EditMode filter returned 20 passing cases while separately reporting 74 project-discovered EditMode tests. Physics PlayMode returned 11/11.
  Impact: the Results record distinguishes filtered returned cases from project discovery totals.
- After 0008 implementation began, the owner clarified that future planning should prepare multiple candidate ExecPlans for comparison and forward sequencing, but asked this task to finish only 0008.
  Impact: no 0009 file is created in this task; the next planning task must produce multiple explicit candidates rather than one preselected plan.

## 4. Decision log

### D-0008-01 — Extend the single existing shape value

Add only these public factories to `PhysicsShape2D`:

```csharp
public static PhysicsShape2D CreateSegment(
    fp2 localOffset,
    fp length,
    fp width,
    bool sweepFromPrev = false);

public static PhysicsShape2D CreateRect(
    fp2 localOffset,
    fp2 halfExtents,
    bool sweepFromPrev = false);
```

No new public shape interface, DTO, enum, collider component or geometry service is added.

### D-0008-02 — Dimension validation

Segment `length` and `width` must be nonnegative. Both Rect half-extent components must be nonnegative. Public factories and internal shape validation enforce the same invariant. Invalid input throws before `PhysicsEntity2D` mutates Transform, Shape or Bounds.

Zero length, zero width and zero half extents are valid degenerate deterministic geometry; they are not silently expanded with epsilon or float tolerances.

### D-0008-03 — Formal Segment mapping

The world center remains the existing offset mapping:

```text
center = Position + Right * LocalOffset.x + Forward * LocalOffset.y
half = Length / 2
a = center - Forward * half
b = center + Forward * half
```

Its AABB is the component-wise segment min/max expanded equally on both axes by `Width / 2`.

### D-0008-04 — Formal oriented Rect AABB

Rect world state uses the stored center, Right, Forward and HalfExtents. Its AABB half-size is calculated without float or corner allocation:

```text
extent.x = abs(Right.x) * HalfExtents.x + abs(Forward.x) * HalfExtents.y
extent.y = abs(Right.y) * HalfExtents.x + abs(Forward.y) * HalfExtents.y
Bounds.Min = center - extent
Bounds.Max = center + extent
```

This is algebraically the component-wise min/max of the four oriented corners and preserves the design's `HalfExtents.x`=Right and `HalfExtents.y`=Forward convention.

### D-0008-05 — No new sweep semantics

Segment/Rect `SweepFromPrev` is retained exactly in `PhysicsShape2D`, but current bounds follow Physics v13.1 section 4.5 and do not union a motion sweep for these kinds. No previous-facing, previous-shape or continuous oriented-volume protocol is invented.

### D-0008-06 — No dependency or lifecycle expansion

All new authority remains internal to the current Physics assembly and uses only `fp`, `fp2` and `fpmath`. No asmdef reference, Package, PhysicsWorld registration, Unity callback, Unity Transform access, Unit/Projectile edge or aggregate lifecycle owner is added.

## 5. Current repository context

- Unity version: `2022.3.62f1c1`.
- Formal 0007 baseline: targeted Deterministic 43 returned cases passed; full EditMode 65/65; full PlayMode 8/8; final recent Error/Exception query empty; `GameScene` loaded and clean.
- `FrameSyncMoba.Physics` is the sole spatial-contract assembly. It references only `Unity.Mathematics`, `Unity.Mathematics.FixedPoint` and UnityEngine required by `MonoBehaviour`; it has no Unit, Projectile, Presentation, Input, networking or UOS edge.
- Formal 0006 production types already present:

```text
PhysicsShapeKind       Point=0, Circle=1, Segment=2, Rect=3
PhysicsTransform2D     Position, PrevPosition, Forward, Right
PhysicsShape2D         Kind, LocalOffset, Radius, Length, Width, HalfExtents, SweepFromPrev
PhysicsBounds2D        inclusive Min and Max fp2 corners
PhysicsEntity2D        sole logical-state owner and public spatial write boundary
PhysicsGeometry2D      internal fixed-point geometry owner
```

- `PhysicsShape2D.ValidateSupported` currently accepts Point/Circle and deliberately rejects Segment/Rect.
- `PhysicsGeometry2D.CalculateBounds` currently implements only Point/Circle.
- `PhysicsEntity2D.SetLogicShape`, pose/facing writes and `RestoreLogicSpatialState` already validate first and commit derived bounds atomically.
- Existing focused tests: 11 Physics EditMode and 8 Physics PlayMode cases.
- No scene, prefab, ScriptableObject, Input Actions, Package or ProjectSettings change is required.

Expected modified paths:

```text
Assets/Scripts/FrameSyncMoba/Physics/PhysicsShape2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsGeometry2D.cs
Assets/Tests/EditMode/Physics/PhysicsShape2DTests.cs
Assets/Tests/EditMode/Physics/PhysicsGeometry2DTests.cs
Assets/Tests/PlayMode/Physics/PhysicsEntity2DPlayModeTests.cs
Docs/Implementation/Plans/0008_physics_entity_2d_segment_rect_geometry_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md (status text only if necessary)
```

`PhysicsEntity2D.cs`, asmdefs and AssemblyInfo should not need modification unless a verified in-scope test exposes a defect in their existing generic shape commit/restore path.

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md`
  - selects `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md` as the sole current Physics owner.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
  - 1.1–1.2: sole spatial-contract ownership and fixed-point authority;
  - 2.1–2.2: `PhysicsEntity2D` owns Transform/Shape/Bounds;
  - 2.5–2.7: fixed-point basis, formal spatial writes and Unity Transform boundary;
  - 4.1–4.2: required four shape kinds and exact shared field family;
  - 4.4: Segment endpoints and Rect oriented world parameters;
  - 4.5: exact Segment and Rect AABB rules;
  - 14.5: Bounds is derived and rebuilt after restore;
  - 16.1: Point/Circle first, followed by Segment/Rect.
- `Docs/Architecture/DECISION_LOG.md`
  - D-020 excludes production content;
  - D-021 selects the actual current design file;
  - D-022 requires authoritative `fp` and permits float only at authoring conversion;
  - D-023 requires proportional feature tests;
  - D-024 accepts the current filesystem baseline.
- `Docs/Implementation/ROADMAP.md`
  - Phase 3 requires logical 2D physics and query structures before movement/input integration.
- `Docs/Implementation/Plans/0006_physics_entity_2d_point_circle_spatial_core_execplan.md`
  - owns the existing public shape family, spatial writes, AABB value and restore seam;
  - explicitly defers Segment/Rect to a later slice.
- `Docs/Implementation/Plans/0007_deterministic_random_geometry_operations_execplan.md`
  - confirms the current 65/8 test baseline and leaves Physics integration unchanged.

## 7. In scope

- two public factories on the existing `PhysicsShape2D` owner;
- nonnegative Segment length/width and Rect half-extent validation;
- fixed-point Segment world endpoints and width-expanded AABB;
- fixed-point oriented Rect AABB;
- immediate derived Bounds updates through existing pose, facing, shape and restore paths;
- explicit preservation of `SweepFromPrev` without new swept-volume behavior;
- focused EditMode geometry/value tests;
- focused PlayMode component state, atomic rejection and Unity Transform-boundary tests;
- Unity MCP compile, Console, targeted/full test and clean-scene validation;
- plan/status synchronization after implementation.

## 8. Out of scope

- new shape enum/type/DTO, public general geometry service or collider hierarchy;
- Point/Circle behavior changes;
- Segment/Rect narrow-phase intersection, overlap, raycast, sweep or hit queries;
- previous-facing/previous-shape storage or swept oriented-volume bounds;
- `PhysicsEntityQueryInfo`, `RuntimeUidQueryValue`, TeamId, Owner or business binding;
- PhysicsWorld, GridMap, CellSpan, spatial buckets, RangeQuery or result sorting;
- Unit/Projectile registration, spawning, movement, pathfinding or wall resolution;
- aggregate snapshot structs, canonical serialization, checksum or rollback orchestration;
- `LateUpdate`, PresentationDirty, authoring floats, GridMap world conversion or gizmos;
- Unity Rigidbody/Collider authority or UnityEngine Physics queries;
- asmdef, Package, scene, prefab, ScriptableObject, Input Actions or ProjectSettings changes;
- production hero, ability, Buff, equipment, unit, projectile, map or balance content.

## 9. Affected assemblies and exact production types

```text
FrameSyncMoba.Physics
    PhysicsShape2D
        add CreateSegment and CreateRect
        extend the existing invariant validator

    PhysicsGeometry2D
        extend internal world-parameter/AABB calculation for Segment and Rect

FrameSyncMoba.Physics.Tests
    extend existing shape and pure geometry fixtures

FrameSyncMoba.Physics.PlayModeTests
    extend existing PhysicsEntity2D GameObject-boundary fixture
```

No assembly definition or reference changes. Dependency direction remains:

```text
Unity.Mathematics + Unity.Mathematics.FixedPoint
    -> FrameSyncMoba.Physics
    -> Physics test assemblies

FrameSyncMoba.Physics -/-> Unit, Projectile, Presentation, Input, networking, UOS
```

## 10. Public contracts

Modified public contract:

```text
PhysicsShape2D
    + CreateSegment(fp2 localOffset, fp length, fp width, bool sweepFromPrev = false)
    + CreateRect(fp2 localOffset, fp2 halfExtents, bool sweepFromPrev = false)
```

Confirmed unchanged contracts:

```text
PhysicsShapeKind values
PhysicsTransform2D fields/meaning
PhysicsShape2D existing fields and Point/Circle factories
PhysicsBounds2D Min/Max meaning
PhysicsEntity2D public spatial methods
RestoreLogicSpatialState ownership
```

No UID, Command, Snapshot DTO, Aim, AbilitySignal, Checksum, FixedPoint wrapper, Runtime DTO or serialization schema is added.

## 11. Ownership, dependency direction and deterministic ordering

`PhysicsEntity2D` remains the sole state owner. `PhysicsGeometry2D` remains an internal pure calculation helper; callers cannot bypass the component with a second public mutable geometry model.

The slice adds no collection or enumeration. Shape dispatch is the existing explicit enum switch, so no Dictionary/HashSet, registration order, scene hierarchy or Unity object identity can affect results.

All authoritative calculations use `fp`, `fp2`, `fpmath.abs`, component-wise min/max and the stored deterministic basis. No float/double, `Time`, `UnityEngine.Random`, `GetInstanceID`, Unity Physics result or Presentation state participates.

## 12. Snapshot and serialization impact

No snapshot member, canonical byte layout, checksum field or serialization format changes. The existing internal restore seam receives the already-defined `PhysicsShape2D` value and recomputes Bounds for Segment/Rect exactly as it does for Point/Circle.

Focused tests must prove restore preserves Position, PrevPosition, Forward, Right and Shape while rebuilding the expected Segment/Rect Bounds. Aggregate capture/Restore/Resolve/Rebuild and byte round trips remain out of scope until their owning snapshot contracts exist.

## 13. Implementation steps

1. Reconfirm Unity is idle, the current Console has no new product compile error and all open scenes are clean.
2. Add `CreateSegment` and `CreateRect` with validation-before-construction and zeroing of every unused field.
3. Refactor `ValidateSupported` only as needed so all four kinds enforce their formal used/unused field invariants.
4. Add internal Segment world endpoint calculation from the existing offset center and fixed-point basis.
5. Extend `CalculateBounds` with Segment min/max expanded by half width.
6. Extend `CalculateBounds` with oriented Rect component extents using fixed-point absolute basis components.
7. Add EditMode exact-field, invalid-dimension, axis-aligned, rotated, local-offset, degenerate and restore-derived geometry cases.
8. Add PlayMode cases proving accepted shapes update through `SetLogicShape`/pose/facing, invalid shapes leave state unchanged and logical writes still do not affect Unity Transform.
9. Refresh/import and compile through Unity MCP; inspect Error and Exception logs.
10. Run targeted Physics EditMode and PlayMode suites, then full EditMode and PlayMode baselines.
11. Search for duplicate shape/spatial contracts and forbidden deterministic dependencies; review the changed production/test files against this scope.
12. Record actual results in this plan and synchronize module/repository status.
13. Record completion and stop after 0008. A later planning task will create multiple candidate ExecPlans under the owner's revised planning rule.

## 14. EditMode tests

- Segment factory records exact Kind/offset/length/width/sweep and zeros Radius/HalfExtents.
- Rect factory records exact Kind/offset/half extents/sweep and zeros Radius/Length/Width.
- negative Segment length or width is rejected;
- negative Rect x or y half extent is rejected;
- invalid internal values are rejected before entity state mutation through the existing friend-test seam;
- axis-aligned Segment endpoints and width-expanded AABB match exact raw `fp2` values;
- rotated Segment plus local offset follows stored Forward/Right and produces exact bounds;
- axis-aligned and rotated Rect AABBs match the four-corner equivalent result;
- degenerate zero dimensions remain deterministic without epsilon repair;
- shape `SweepFromPrev` is preserved but does not change the formal current Segment/Rect AABB;
- internal restore recomputes identical Segment/Rect Bounds from the restored logical state;
- Point/Circle regression cases remain green.

## 15. PlayMode tests

PlayMode is required because the authoritative owner is a `MonoBehaviour` and accepted/rejected commits must be checked on a real component.

- `SetLogicShape` accepts Segment and Rect and updates Shape/Bounds atomically;
- `SetLogicPose`, `SetLogicForward`, movement and teleport rebuild oriented bounds from logical state;
- invalid Segment/Rect values leave Transform2D, Shape and Bounds unchanged;
- restore preserves the exact logical basis and derives the expected bounds;
- across a rendered frame, these logical operations still neither read nor write the GameObject Transform.

No persistent test scene or asset is created.

## 16. Unity MCP validation

Use the connected Unity MCP to:

1. verify all opened scenes are clean;
2. refresh/import the changed scripts and trigger compilation;
3. wait until `IsCompiling=false` and `IsUpdating=false`;
4. query recent Console Error and Exception entries;
5. run targeted `FrameSyncMoba.Physics.Tests` in EditMode;
6. run targeted `FrameSyncMoba.Physics.PlayModeTests` in PlayMode;
7. run full EditMode and full PlayMode baselines;
8. reconfirm `GameScene` remains loaded and clean.

Do not clear, disable, delete or weaken a failing test to obtain a pass.

## 17. Failure and recovery

Stop this plan if implementing the documented shapes requires changing `PhysicsEntity2D` ownership, the meaning of existing public fields, a new identity/query/snapshot protocol, an asmdef/package edge or a design-external swept-volume rule.

Ordinary implementation defects in the two named Physics files and their focused tests are in scope. Preserve validation-before-mutation. A partial implementation is not complete while either reserved shape still throws or any existing Point/Circle regression fails.

## 18. Completion criteria

- the existing sole shape contract creates validated Segment and Rect values;
- exact world parameters and AABBs match Physics v13.1 using only fixed-point logic;
- every accepted pose/facing/shape/restore path refreshes Bounds;
- invalid dimensions fail before component state changes;
- `SweepFromPrev` is preserved without invented Segment/Rect sweep semantics;
- Point/Circle behavior remains unchanged;
- no duplicate protocol/type, new assembly edge, Package or Unity asset is added;
- targeted Physics EditMode/PlayMode and full project baselines pass;
- Unity is idle with no new product compiler diagnostic and the scene remains clean;
- this plan and status documents contain actual validation results;
- no later production slice is executed, and no single 0009 is preselected contrary to the owner's revised multi-candidate planning rule.

## 19. Production-content exclusion

Tests use neutral synthetic poses and dimensions only. No production hero, ability, Buff, equipment, unit, projectile, map object, collider asset, effect, visual/audio resource or balance value is implemented.

## 20. Candidate comparison and priority

### Candidate A — PhysicsEntity2D Segment/Rect geometry (selected)

- Observable result: all four already-declared shape kinds produce deterministic logical AABBs.
- Prerequisites: completed formal 0006 shape/pose owner only.
- Assemblies: existing Physics runtime and its two test assemblies.
- Public contract: two factories on the existing `PhysicsShape2D`; no new protocol type.
- Risk: fixed-point oriented bounds and validation, contained within one owner.
- Validation: exact raw geometry plus MonoBehaviour atomic-commit/Transform-boundary tests.
- Why now: the current design freezes exact equations and explicitly orders Segment/Rect after Point/Circle. It closes an existing reserved shape family without depending on unresolved identities, grids or aggregate schemas.

### Candidate B — General stable deterministic container foundation

- Observable result: a reusable insertion-order-independent collection surface.
- Prerequisites: exact consumer operations, mutation rules, capacity policy and public API ownership.
- Assemblies: likely Deterministic plus future consumers.
- Public contract: would introduce a new generic public surface.
- Risk: premature abstraction or a second ordering owner alongside UnitRegistry.
- Validation: permutation equivalence, mutation and allocation tests.
- Why not now: the Roadmap names stable containers, but current formal consumers do not freeze one universal API. The existing Unit registry already implements its module-specific stable order without exposing a general container.

### Candidate C — Checksum writer / SharedGameplayChecksum foundation

- Observable result: canonical state bytes can feed a deterministic checksum value.
- Prerequisites: frozen hash algorithm, aggregate field ordering and owning endpoint/schema.
- Assemblies: Deterministic plus later FrameSync/module contributors.
- Public contract: checksum value/writer and contribution order.
- Risk: compatibility-breaking protocol choice if invented early.
- Validation: golden hashes, field-order sensitivity and endpoint equality.
- Why not now: formal 0004 supplies primitive canonical bytes, but the current repository evidence still records the aggregate field order, hash algorithm and checksum value as missing. This plan must not invent those public protocol decisions.

Candidate A is preferred because it has the smallest complete validation loop, the most mature current owner and exact current-design math, while the other candidates would freeze broader public contracts without a sufficiently defined consumer/schema.

## 21. Results

Completed on 2026-07-20.

Production changes:

```text
Assets/Scripts/FrameSyncMoba/Physics/PhysicsShape2D.cs
Assets/Scripts/FrameSyncMoba/Physics/PhysicsGeometry2D.cs
```

`PhysicsShape2D` now exposes the planned `CreateSegment` and `CreateRect` factories. Segment length/width and both Rect half extents reject negative values before construction; zero dimensions remain valid. The same invariants are enforced by internal validation so `PhysicsEntity2D.SetLogicShape` and restore remain failure-atomic.

`PhysicsGeometry2D` now derives formal Segment endpoints/width and Rect center/basis/half-extents. Segment bounds expand endpoint min/max by half width. Rect bounds use absolute Right/Forward component contributions. Both paths use only `fp`, `fp2` and `fpmath`. `SweepFromPrev` is retained but does not add undocumented Segment/Rect swept-volume bounds.

Test changes:

```text
Assets/Tests/EditMode/Physics/PhysicsShape2DTests.cs
Assets/Tests/EditMode/Physics/PhysicsGeometry2DTests.cs
Assets/Tests/PlayMode/Physics/PhysicsEntity2DPlayModeTests.cs
```

Nine new EditMode cases cover factory fields, negative/unused-field validation, exact Segment/Rect world parameters, width/orientation AABBs and degenerate geometry. Three new PlayMode cases cover Segment component commits, Rect facing/movement updates and Rect restore; the invalid-assignment and Transform-boundary cases were updated to exercise supported shapes.

Unity MCP validation:

```text
Targeted FrameSyncMoba.Physics.Tests:          20 returned cases passed
Targeted FrameSyncMoba.Physics.PlayModeTests: 11/11 passed
Full EditMode:                                 74/74 passed
Full PlayMode:                                 11/11 passed
Editor after validation:                       not playing, not compiling, not updating
Recent Console Error/Warning/Exception window:  empty
GameScene:                                      loaded and clean; four roots
```

Review found one authoritative Physics shape/spatial type family, no duplicate UID/Command/Snapshot/Aim/AbilitySignal/Checksum/FixedPoint/Runtime DTO, no new asmdef edge and no forbidden float/time/random/object-identity/Unity-Physics input. No scene, prefab, ScriptableObject, Input Actions, Package, ProjectSettings or production content changed.

Remaining limitations are intentional: Segment/Rect narrow-phase and sweep tests, PhysicsWorld/grid/range queries, QueryInfo/business identity, Unit/Projectile integration, aggregate snapshots and LateUpdate presentation sync remain later slices. No 0009 was created because the owner requested this task stop after 0008 and changed future planning to multiple candidate ExecPlans.
