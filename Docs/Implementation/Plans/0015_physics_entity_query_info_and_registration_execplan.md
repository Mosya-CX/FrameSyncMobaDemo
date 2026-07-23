# ExecPlan 0015 — PhysicsEntityQueryInfo and PhysicsEntity2D Registration

> Status: **Approved — executed.** Owner approved A->B->C as 0013/0014/0015.
> Source: candidate 0013C. RuntimeUidQueryValue placed in Physics per §2.3.

## Purpose
Implement frozen PhysicsEntityQueryInfo (Physics v13.1 §2.3) and wire it into PhysicsEntity2D as the query-identity surface for future RangeQueryService/ProjectileHitQuery.

## Results
- PhysicsEntityKind.cs: enum (Unit=0, Projectile=1)
- RuntimeUidQueryValue.cs: readonly struct (SpawnLogicTick + RuntimeEntityPrefabId + SpawnSequenceInTick) + IEquatable
- PhysicsEntityQueryInfo.cs: readonly struct (UidSnapshot + Kind + TeamSnapshot(byte) + Owner + IsSet)
- PhysicsEntity2D.cs: QueryInfo property (private set) + internal SetQueryInfo
- PhysicsEntityQueryInfoTests.cs: 11 cases (kind values, RuntimeUidQueryValue storage/equality/hashcode/default, QueryInfo 4-field storage/IsSet/null owner, PhysicsEntity2D SetQueryInfo readonly + default-before-set)
- Compile verified via dotnet csc.dll: 0 errors (31 sources, 254 refs including UnityEngine)
- Unity runtime test: PENDING
- Note: RuntimeUidQueryValue placed in Physics assembly for now; may move to a shared assembly if cross-module ownership is confirmed later