# ExecPlan 0013 — Unit CapabilityState Storage and Death Lifecycle Gate

> Status: **Approved — executed.** Owner approved A->B->C as 0013/0014/0015.
> Source: candidate 0013A. Selected 2026-07-21.

## Purpose
Implement frozen CapabilityState storage (Unit v27.3 §1.9) and wire it to LifeState death/respawn transitions (0011).

## Results
- CapabilityState.cs: struct with 5 fields (CanMove/CanAttack/CanCast/CanTurn/IsTargetable) + DisableAllActions + ResetAliveDefault + CreateAliveDefault
- Unit.cs: CapabilityState property (ref readonly) + internal RefCapabilityState
- UnitWorld.cs: ConfirmUnitDeath calls DisableAllActions; CompleteRespawn calls ResetAliveDefault; Dying/BeginRespawn do NOT touch capability
- CapabilityStateTests.cs: 8 cases (default all-true, death disables, Dying doesn't, respawn stays disabled, complete resets, full cycle, isolation, recover keeps enabled)
- Compile verified via dotnet csc.dll: 0 errors
- Unity runtime test: PENDING (MCP approval-layer outage code 1211)