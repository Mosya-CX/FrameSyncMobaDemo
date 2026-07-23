# ExecPlan 0018 — StatHandler Foundation

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0017B. Unit v27.3 §5.1-5.5.

## Purpose
Implement the StatHandler numerical foundation: StatId, StatDefinition, StatPreset, StatModifier, StatRuntimeEntry, and StatHandler with AddModifier/SetModifierValue/RemoveModifier/GetStat/Recompute using the frozen fixed calculation order (§5.3.3) and deterministic StatSeq allocation.

## Results
- 12 new types: StatId, StatDefinition, StatDefinitionTable, StatPreset, StatPresetEntry, StatModifierOperation, StatModifier (internal), StatModifierHandle, StatModifierView, StatChange, StatRuntimeEntry (internal), StatHandler
- Unit.cs: added StatHandler property (nullable until SpawnUnit assigns)
- FrameSyncMoba.Unit.asmdef: added Unity.Mathematics + Unity.Mathematics.FixedPoint references
- FrameSyncMoba.Unit.Tests.asmdef: added Unity.Mathematics + Unity.Mathematics.FixedPoint references
- StatTestHelpers.cs: shared test fixture builder
- StatHandlerModifierTests.cs: 9 cases (Add/Set/Remove/TryGet/Clear/invalid/wrong-owner)
- StatHandlerCalculationTests.cs: 12 cases (Flat/BaseRatio/FinalRatio/full formula/clamp/level growth/dirty/determinism/level change)
- StatHandlerSeqTests.cs: 4 cases (starts at 1, never reused, shared counter, invalid handle)
- StatHandlerChangeTests.cs: 6 cases (no change/delta/net zero/finalize/after finalize/not in preset)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211)
- Unity runtime test: PENDING — owner must manually run Test Runner
- Fixed issue: missing `using FrameSyncMoba.Deterministic;` in StatHandler.cs (for DeterministicSimulationException on StatSeq overflow)