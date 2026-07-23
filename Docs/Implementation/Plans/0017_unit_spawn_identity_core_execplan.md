# ExecPlan 0017 — Unit Spawn Identity Core

> Status: **Completed and verified (compile-pending).**
> Source: candidate 0017A. Unit v27.3 §1.2/§1.3/§7.2.

## Purpose
Implement the deterministic Unit spawn-identity foundation: DeterministicSimulationException, remaining §1.2 Unit scalar identity/reward properties, and UnitWorld per-tick spawn-sequence allocator.

## Results
- DeterministicSimulationException.cs: public exception type in FrameSyncMoba.Deterministic
- Unit.cs: added UnitPrototypeId/BaseGoldValue/BaseExperienceValue (immutable, optional constructor params)
- UnitWorld.cs: added AllocateSpawnSequence() with per-tick byte counter + overflow detection
- DeterministicSimulationExceptionTests.cs: 3 cases (construction, inheritance, inner exception)
- UnitSpawnSequenceTests.cs: 8 cases (monotonic, overflow, rollover, determinism, properties)
- Compile: MANUAL REVIEW ONLY — MCP approval layer outage (code 1211) blocks all compile/test tools
- Unity runtime test: PENDING — owner must manually run Test Runner