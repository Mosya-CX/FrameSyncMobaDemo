# ExecPlan 0007 — Deterministic Random Geometry Operations

> Status: **Complete — implemented and Unity-verified on 2026-07-20.**  
> This plan extends only the existing single deterministic Gameplay random stream with the three 2D geometric operations named by FrameSync v10.2.

## 1. Purpose

Complete the geometric portion of the formal deterministic random API:

```text
RandomDirection2D()
RandomPointInsideCircle(radius)
RandomPointOnCircle(radius)
```

Observable result:

```text
The same seed and call sequence produce identical raw fp2 results.
Direction and circle-boundary calls consume one base draw.
Area-uniform circle-interior calls consume two base draws.
Capture -> geometry calls -> Restore -> replay reproduces every result.
```

## 2. Progress

- [x] Complete and Unity-verify ExecPlan 0006 independently.
- [x] Re-read the current FrameSync random and Snapshot ownership sections.
- [x] Confirm the existing package supplies fixed-point PI, sin, cos and sqrt.
- [x] Confirm no second geometric random helper or Gameplay random stream exists.
- [x] Create this ExecPlan before changing the public service.
- [x] Implement the three exact public methods on `DeterministicRandomService`.
- [x] Add focused EditMode mapping, draw-count, validation and replay tests.
- [x] Compile and inspect Console through Unity MCP.
- [x] Run targeted Deterministic and full EditMode/PlayMode baselines.
- [x] Review duplicates, deterministic math, allocations and scope.
- [x] Update this plan, `MODULE_STATUS.md` and necessary repository-map entries.

## 3. Surprises and discoveries

- FrameSync v10.2 names the three functions and requires a stable number of base random values, but does not freeze the polar mapping or draw order.
  Impact: this plan records one explicit area-uniform fixed-draw mapping rather than using rejection sampling.
- The approved fixed-point package exposes `fpmath.PI_TIMES_2`, `sin`, `cos` and `sqrt` directly.
  Impact: no lookup table, float conversion, new numeric wrapper or Physics dependency is required.
- `NextFp01` already maps one complete `NextUInt` draw to a nonnegative Q32.32 fractional value below one.
  Impact: all geometry functions compose the accepted primitive mapping and remain snapshot-replayable without new state.
- The package trigonometric and square-root functions passed the raw-value mapping tests and the 128-sample fixed-point circle-bound test with the planned `fp.FromRaw(1L << 20)` quantization allowance.
  Impact: no alternate approximation, lookup table, float conversion or post-implementation tolerance expansion was needed.
- The Unity MCP assembly filter reports all 65 project EditMode tests as `TotalTests`, while the returned filtered result set contains the 43 deterministic cases.
  Impact: validation records both values explicitly instead of treating the project discovery count as the filtered case count.

## 4. Decision log

### D-0007-01 — Exact public additions

Add only:

```csharp
public fp2 RandomDirection2D();
public fp2 RandomPointInsideCircle(fp radius);
public fp2 RandomPointOnCircle(fp radius);
```

No new service, stream, seed, DTO, snapshot member or 3D helper is added.

### D-0007-02 — Canonical angle mapping

One angular draw `u = NextFp01()` maps to:

```text
angle = u * fpmath.PI_TIMES_2
direction = fp2(cos(angle), sin(angle))
```

This mapping is used by direction and both circle methods. The component order is fixed as x=cos, y=sin.

### D-0007-03 — Fixed draw counts and draw order

- `RandomDirection2D`: one angle draw.
- `RandomPointOnCircle`: validate radius, then one angle draw; result is direction times radius.
- `RandomPointInsideCircle`: validate radius, then angle draw followed by radial draw; distance is `sqrt(radialDraw) * radius` for area-uniform sampling.
- A valid zero radius still consumes the method's normal one/two draws and returns zero.
- A negative radius throws before consuming any draw.

No rejection loop is permitted.

### D-0007-04 — Fixed-point-only production path

All operations use `fp`, `fp2` and `fpmath`. No float/double, UnityEngine.Random, Unity time, trigonometric platform API, allocation, LINQ or Physics assembly dependency is introduced.

## 5. Current repository context

- `FrameSyncMoba.Deterministic` is a no-engine, non-auto-referenced assembly depending only on both mathematics assemblies.
- `DeterministicRandomService` is the sole Gameplay random stream and already owns primitive, chance, indexed-pick and shuffle operations plus State capture/restore.
- `FrameSyncMoba.Deterministic.Tests` has 35 focused cases before 0007.
- Formal 0006 full baselines are EditMode 57/57 and PlayMode 8/8.
- No asmdef, scene, prefab, ScriptableObject, Input Actions, Package or Unity asset change is required.

Expected modified paths:

```text
Assets/Scripts/FrameSyncMoba/Deterministic/DeterministicRandomService.cs
Assets/Tests/EditMode/Deterministic/DeterministicRandomServiceTests.cs
Docs/Implementation/Plans/0007_deterministic_random_geometry_operations_execplan.md
Docs/Implementation/MODULE_STATUS.md
Docs/Architecture/REPOSITORY_MAP.md (status text only if necessary)
```

## 6. Exact design sources

- `Docs/Architecture/DESIGN_INDEX.md` selects FrameSync v10.2 and Snapshot Appendix v7.2.
- `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md`
  - 16.1 requires one Gameplay random source;
  - 16.2 requires snapshot replay of its state;
  - 16.3 names the three exact 2D geometry methods and excludes a core Direction3D;
  - 16.4 requires higher operations to consume stable counts of base random values.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - section 11 keeps State as the required random snapshot member and requires identical output after restore.
- `Docs/Implementation/ROADMAP.md`
  - Phase 1 requires deterministic random services and fixed-point authoritative output.
- `Docs/Architecture/DECISION_LOG.md`
  - D-022 selects package `fp` for Gameplay;
  - D-023 requires proportional feature tests.
- `Docs/Implementation/Plans/0001_deterministic_tick_context_and_random_state_execplan.md`
  - established the sole service and deferred advanced operations.
- `Docs/Implementation/Plans/0003_deterministic_random_collection_operations_execplan.md`
  - preserved fixed draw counts and explicitly deferred geometry to this separate slice.

## 7. Scope

### In scope

- the three exact public signatures in D-0007-01;
- canonical fixed-point polar mapping and explicit draw order;
- area-uniform interior radius through fixed-point square root;
- negative-radius validation before state mutation;
- valid zero-radius draw-count preservation;
- snapshot replay and same-seed raw equality tests;
- Unity MCP compilation, Console and test validation;
- plan/status synchronization.

### Out of scope

- alternate random streams, derived seeds or CallCount snapshot state;
- 3D directions, spheres, cones, arcs, rectangles, polygons, weighted sampling or noise;
- PhysicsEntity2D/PhysicsWorld integration or spatial query consumers;
- new fixed-point/trigonometry wrappers or lookup-table assets;
- gameplay/content spawning, loot, AI, abilities or map placement;
- asmdef, Package, scene, prefab, ScriptableObject, Input Actions or ProjectSettings changes.

## 8. Affected assemblies and implementation plan

```text
FrameSyncMoba.Deterministic
    add three methods to the existing service
    dependency set remains unchanged

FrameSyncMoba.Deterministic.Tests
    add focused Editor-only cases
```

Implementation order:

1. Add one private fixed-point angle-to-direction helper that consumes no random state.
2. Implement `RandomDirection2D` with one `NextFp01` angle draw.
3. Implement radius validation and the circle-boundary operation with one draw.
4. Implement the circle-interior operation with angle then radial draw and fixed-point sqrt.
5. Add exact mapping/draw-count, range, zero-radius, invalid-state, same-seed and capture/restore tests.
6. Refresh/compile, inspect Console and run tests through Unity MCP.
7. Review the two production/test files for deterministic and scope invariants.
8. Complete Results and synchronize status documents.

## 9. Public contracts, ownership and ordering

The only modified public owner is:

```text
FrameSyncMoba.Deterministic.DeterministicRandomService
```

The existing service State remains the sole authority. Method call order is the random-stream order; no collection or enumeration is added. The exact draw order for interior points is angle first, radius second.

No UID, Command, Snapshot DTO, Aim, AbilitySignal, Checksum, FixedPoint wrapper or Runtime DTO type is added.

## 10. Snapshot and serialization impact

No schema, canonical bytes or checksum field changes. `DeterministicRandomSnapshot.State` already captures the full package stream. Geometry replay is verified solely through fixed draw counts and the existing Capture/Restore API.

## 11. Validation

### EditMode tests

- exact direction output equals the documented one-draw polar mapping;
- exact on-circle output equals direction multiplied by radius and consumes one draw;
- exact inside-circle output equals angle-first, sqrt(radial)-scaled mapping and consumes two draws;
- many samples remain within the fixed-point circle bound, allowing only the test's explicit `fp.FromRaw(1L << 20)` trigonometric quantization margin;
- same seed/call sequence produces raw-identical fp2 outputs;
- Capture/Restore replays a mixed geometry sequence;
- valid zero radius returns zero while preserving one/two draws;
- negative radius throws without changing State;
- existing primitive/collection/canonical-writer and downstream module tests remain green.

### PlayMode tests

No new PlayMode fixture. This is a no-engine pure deterministic extension. Run the existing full PlayMode baseline to protect 0006.

### Unity MCP

Refresh/import scripts, wait for idle compilation, inspect current Console diagnostics, run targeted `FrameSyncMoba.Deterministic.Tests`, full EditMode and full PlayMode, and confirm GameScene remains clean.

## 12. Failure and recovery

Stop if the formal design or current code requires a different public method owner, a second stream/snapshot state, a Package/asmdef change, or an out-of-scope public protocol.

Ordinary fixed-point mapping/test corrections remain local to the two named C# files. Never use rejection sampling, weaken draw-count tests, repair failure through float conversion, or disable existing tests.

## 13. Completion criteria

- the sole random service exposes all three exact methods;
- direction/on-circle/inside-circle follow the documented fixed-point mapping and draw order;
- valid calls consume exactly 1/1/2 draws, including radius zero;
- invalid radius consumes no draw and leaves State unchanged;
- same seed and Capture/Restore produce raw-identical results;
- no float/double, Unity random/time/identity, variable draw loop, allocation or new dependency is added;
- Unity compiles without a new product diagnostic;
- targeted and full EditMode plus existing full PlayMode pass;
- no duplicate protocol, asmdef, Package, Unity asset or production-content change occurs;
- this plan and status documents record actual results.

## 14. Production-content exclusion

All tests use neutral synthetic seeds and radii. No production hero, ability, Buff, equipment, unit, projectile, map object, loot table or balance configuration is implemented.

## 15. Results

Completed on 2026-07-20.

Production change:

```text
Assets/Scripts/FrameSyncMoba/Deterministic/DeterministicRandomService.cs
```

The existing sole random service now exposes exactly `RandomDirection2D`, `RandomPointInsideCircle(fp)` and `RandomPointOnCircle(fp)`. Direction uses one `NextFp01` draw mapped by `angle = draw * fpmath.PI_TIMES_2` and `(cos(angle), sin(angle))`. On-circle uses the same direction and one draw. Inside-circle consumes angle then radial draws and applies `sqrt(radialDraw) * radius`. Valid zero radii preserve the normal one/two-draw contract; negative radii fail before State changes.

Test change:

```text
Assets/Tests/EditMode/Deterministic/DeterministicRandomServiceTests.cs
```

Eight focused cases verify exact raw mapping, draw counts, area-radius mapping, same-seed equality, Capture/Restore replay, zero-radius behavior, negative-radius state preservation and the fixed-point circle bound. These tests validate observable values and State transitions rather than object construction.

Unity MCP validation:

```text
Targeted FrameSyncMoba.Deterministic.Tests: 43 returned cases passed, 0 failed, 0 skipped
Full EditMode:                                65/65 passed
Full PlayMode:                                 8/8 passed
Editor after validation:                       not playing, not compiling, not updating
Recent Console Error/Exception window:          empty
GameScene:                                      loaded and clean; four roots
```

Review confirmed no second random stream, UID, Command, Snapshot DTO, Aim, AbilitySignal, Checksum, FixedPoint wrapper or Runtime DTO; no asmdef/dependency change; no float/double authority, Unity random/time/object identity, variable-draw rejection loop, LINQ or new allocation path. No scene, prefab, ScriptableObject, Input Actions, Package, ProjectSettings or production-content change occurred.

Remaining limitations are intentional: this slice does not add alternate streams, derived seeds, weighted/3D/shape-specific sampling, aggregate snapshots/checksums or Physics integration.
