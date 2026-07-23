# ExecPlan 0016 — PhysicsWorld Entity Registration Core

> Status: **Approved — executed.** Owner selected 0016C from candidate round.
> Source: candidate 0016C. Physics v13.1 §3.

## Purpose
Implement minimal PhysicsWorld registration/unregistration surface (Physics v13.1 §3.1-3.6).

## Results
- PhysicsWorld.cs: RegisterUnit/RegisterProjectile/Unregister/UnregisterUnit/UnregisterProjectile + UnitEntities/ProjectileEntities read-only lists
- PhysicsEntity2D.cs: added internal ClearRuntime (§3.6, resets Transform2D/Shape/Bounds/QueryInfo)
- PhysicsWorldRegistrationTests.cs (PlayMode): 14 cases
- Compile verified via dotnet csc.dll: 0 errors (33 sources)
- Unity runtime test: PENDING (MCP approval-layer outage code 1211)