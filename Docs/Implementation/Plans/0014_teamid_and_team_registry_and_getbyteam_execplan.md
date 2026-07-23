# ExecPlan 0014 — TeamId Value Type, Global Team Registry and GetByTeam Query

> Status: **Approved — executed.** Owner approved A->B->C as 0013/0014/0015.
> Source: candidate 0013B. TeamId confirmed as byte by owner.

## Purpose
Define TeamId(byte), global TeamRegistry mapping table, Unit.TeamId, and GetByTeam registry query (Unit v27.3 §1.2/§7.5).

## Results
- TeamId.cs: readonly struct (byte Value) + IEquatable + Neutral(0) + operators
- TeamRegistry.cs: global team mapping (RegisterTeam/TryGetTeam/IsRegistered/Count) + TeamInfo struct (TeamId + Name)
- Unit.cs: construction extended with TeamId parameter (4th arg)
- UnitRegistry.cs: GetByTeam(TeamId) stable UID-ordered query (buffer reuse, no LINQ)
- UnitWorld.cs: GetUnitsByTeam(TeamId) public passthrough
- All existing tests updated to 4-arg construction (UnitUid, UnitKind, subKindId, TeamId)
- TeamIdAndRegistryTests.cs: 15 cases (TeamId equality, TeamRegistry registration/validation, GetByTeam query + stable order + empty + no mutation)
- Compile verified via dotnet csc.dll: 0 errors
- Unity runtime test: PENDING