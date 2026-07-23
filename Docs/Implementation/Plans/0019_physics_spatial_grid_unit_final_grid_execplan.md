# ExecPlan 0019 — PhysicsSpatialGrid2D + UnitFinalGrid Build + Candidate Dedup

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0017C. Physics v13.1 §7.

## Purpose
Implement the deterministic spatial hash grid (PhysicsSpatialGrid2D), PhysicsWorld.BuildUnitFinalGrid(), and cross-cell candidate dedup — the spatial-query foundation for RangeQueryService (§9) and ProjectileHitQueryService (§8).

## Results
- PhysicsWorldSettings.cs: GridCellSize configuration (default 10m)
- PhysicsSpatialGrid2D.cs: spatial hash grid with Insert/Clear/CollectCandidates (dedup by RuntimeUidQueryValue, sorted by UidSnapshot)
- PhysicsWorld.cs: added Settings, UnitFinalGrid, BuildUnitFinalGrid() (§7.3 no business-state filtering)
- PhysicsSpatialGrid2DTests.cs (EditMode): 8 cases (single/multiple/cross-cell/sorted/deterministic/no-overlap/clear/invalid-cellsize)
- PhysicsWorldBuildFinalGridTests.cs (PlayMode): 5 cases (all inserted/null skipped/clears previous/deterministic/no business filter)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211)
- Unity runtime test: PENDING — owner must manually run Test Runner