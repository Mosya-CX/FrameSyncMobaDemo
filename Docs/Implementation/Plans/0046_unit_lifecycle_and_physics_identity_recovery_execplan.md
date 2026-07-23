# Superseded historical draft — do not execute

> **Status: Superseded / Do not execute.** The owner rejected this draft's pure-C# Unit/Handler assumption on 2026-07-22. Its replacement was subsequently incorporated into the completed `0046_full_design_conformance_recovery_execplan.md`; do not execute either historical component plan independently.

# ExecPlan 0046 — Unit lifecycle and Physics identity recovery (historical)

> Historical status: never executed.  
> Created: 2026-07-22 after the design-conformance re-audit.  
> This is the only executable plan numbered 0046. `0046_candidates.md` is a superseded historical draft and must not be executed.

## 1. Purpose

Restore a trustworthy deterministic-core gate before changing Command, Snapshot, Combat, Ability, or other broad protocols. After this slice:

- formal death no longer destroys `CombatModifier` handles owned by surviving systems;
- every bound Unit physics entity exposes the lossless Unit UID query value, actual team, Unit kind, and owner before registration;
- invalid or uninitialized Unit/Projectile query metadata fails visibly at registration instead of entering the spatial world;
- the existing Unit and FrameSync test fixtures use the current Tick, stat-definition, fixed-point, and lifecycle contracts;
- all previously discovered Unit and FrameSync failures are either green or reduced to a precisely documented out-of-scope production defect that blocks completion and receives a child ExecPlan.

The observable developer result is a green, meaningful foundation on which later Command/Snapshot migrations can be performed without confusing stale tests with production regressions. No player-facing production content is added.

## 2. Progress

- [x] Re-audit the current filesystem rather than trusting plans 0009–0045.
- [x] Re-run the five current Unity test assemblies and record the baseline.
- [x] Confirm the lifecycle and Unit–Physics identity defects against current formal designs.
- [x] Separate accepted pure-C# architecture variance from required behavioral contracts.
- [ ] Review this plan.
- [ ] Add focused regression tests for death ownership and Unit physics query metadata.
- [ ] Correct production lifecycle and query binding/registration behavior.
- [ ] Repair stale Unit and FrameSync fixtures without weakening asserted behavior.
- [ ] Run targeted tests, then all five project suites through Unity MCP.
- [ ] Inspect the Console and final diff; update status/map/results.

## 3. Surprises and discoveries

- `Unit.ClearForDeath()` globally clears `CombatModifiers`, while D-009 explicitly forbids that. One current test expects the forbidden behavior, so production and test must be corrected together.
- `FrameSyncGameRuntime.BindUnitPhysics()` binds `UidSnapshot = default` and `teamSnapshot = 0` for every Unit. This can collapse query identity and invalidates team filtering.
- `PhysicsWorld.RegisterUnit()` and `RegisterProjectile()` document metadata requirements but currently validate only null and duplicate object registration.
- Eleven spawn-related failures invoke formal context-owned behavior without opening an active `SimulationTickContext`; changing production code to tolerate that would weaken determinism.
- Several Combat/FrameSync fixtures omit stat definitions now required by the runtime. Three stat failures compare the package fixed-point result to an ideal decimal rather than its canonical raw representation.
- The current semantic Unit/Handler runtime is pure C# in a no-engine assembly, although the design document illustrates MonoBehaviour handlers. The recovery preserves the pure-C# authority and treats Unity objects as adapters.
- Unity MCP can run explicit assembly suites, but a full EditMode call without an assembly filter reported no tests. The MCP Console clear operation also fails while its own log file is locked; neither issue justifies changing project code.

## 4. Decision log

- **Preserve the pure-C# semantic runtime.** Converting Unit and every Handler to MonoBehaviour would increase Unity identity/lifecycle coupling and is unnecessary for the formal behavior in this slice. A later spawn/composition plan will define the injected Unity factory/adapter boundary.
- **Migrate existing contracts in place.** This slice must not introduce another UID, query DTO, lifecycle API, Command, Aim, Snapshot, checksum, or fixed-point type.
- **Do not make spawn work outside a Tick.** Tests must establish `SimulationTickContext`; production continues to reject missing authoritative context.
- **Treat fixed-point raw values as authority.** Tests use exact raw values or a package-appropriate expectation derived without authoritative float/double arithmetic.
- **Registration fails early.** A physics entity with wrong kind, missing owner, or an invalid/default UID query value must throw a deterministic, actionable exception before list insertion.
- **Limit scope before protocol migration.** Command headers, aggregate snapshots, Combat settlement, and AuthorityFrame handling are recorded P0/P1 work but are deliberately excluded from 0046.

These choices do not replace the frozen decisions in `Docs/Architecture/DECISION_LOG.md`; D-008, D-009, D-022, D-023, and D-024 remain controlling.

## 5. Current repository context

### Assemblies

- `FrameSyncMoba.Deterministic`: owns `SimulationTickContext` and `fp`-based deterministic helpers.
- `FrameSyncMoba.Physics`: owns `RuntimeUidQueryValue`, `PhysicsEntityQueryInfo`, `PhysicsEntity2D`, and `PhysicsWorld`.
- `FrameSyncMoba.Unit`: owns `UnitUid`, `TeamId`, `Unit`, `UnitWorld`, and `CombatModifierSet`.
- `FrameSyncMoba.FrameSync`: owns `FrameSyncGameRuntime.BindUnitPhysics`.
- Unit, Physics EditMode, Physics PlayMode, and FrameSync test assemblies depend downstream in the existing acyclic direction.

No asmdef change is expected.

### Production paths

- `Assets/Scripts/FrameSyncMoba/Unit/Core/Unit.cs`
- `Assets/Scripts/FrameSyncMoba/Unit/Core/UnitWorld.cs`
- `Assets/Scripts/FrameSyncMoba/Unit/Team/TeamId.cs`
- `Assets/Scripts/FrameSyncMoba/Physics/Core/RuntimeUidQueryValue.cs`
- `Assets/Scripts/FrameSyncMoba/Physics/Core/PhysicsEntityQueryInfo.cs`
- `Assets/Scripts/FrameSyncMoba/Physics/Core/PhysicsWorld.cs`
- `Assets/Scripts/FrameSyncMoba/FrameSync/FrameSyncGameRuntime.cs`

### Tests and baseline

- Deterministic EditMode: 51 passed.
- Physics EditMode: 70 passed.
- Physics PlayMode: 30 passed.
- Unit EditMode: 186 passed, 42 failed.
- FrameSync EditMode: 0 passed, 6 failed.

Relevant fixtures include `SpawnUnitTests`, `UnitWorldIntegrationTests`, `RangeQueryServiceTests`, `CombatSystemTests`, `StatHandlerCalculationTests`, `StatHandlerSnapshotTests`, `AttackHandlerTests`, `PhysicsWorldRegistrationTests`, `PhysicsEntityQueryInfoTests`, and `FrameSyncPipelineTests`.

### Unity assets

`GameScene` is empty and no project InputActionAsset or Gameplay configuration asset exists. This slice needs no scene, prefab, ScriptableObject, Input Action, or package modification.

## 6. Design sources

- `Docs/Architecture/DECISION_LOG.md`
  - D-008: spawn-Tick active Gameplay gate.
  - D-009: normal death does not globally clear StatHandler or CombatModifiers.
  - D-022: package `fp` is authoritative; authoring float converts once.
  - D-023: each feature carries proportional focused tests.
- `Docs/Design/unit_behavior_framework_design_v27_3.md`
  - Unit UID and spawn identity.
  - `UnitWorld.SpawnUnit` context and synchronous registration behavior.
  - handler lifecycle ownership and `ClearForDeath`/`ClearForRespawn`.
- `Docs/Design/MOBA_UnitPhysics_RangeQuery_Design_v13.1.md`
  - `PhysicsEntityQueryInfo` mirrors business UID/team/kind/owner.
  - Unit UID conversion into `RuntimeUidQueryValue`.
  - registration order and final-grid identity.
- `Docs/Design/FrameSync_Snapshot_Contents_Appendix_v7_2.md`
  - used only to ensure no new snapshot membership or rollback repair is introduced in this slice.

## 7. Scope

### In scope

- Remove the global `CombatModifiers.Clear()` from normal death cleanup while preserving the full pool-reset cleanup.
- Add or centralize a lossless UnitUid-to-`RuntimeUidQueryValue` conversion without creating a second UID.
- Bind actual `Unit.TeamId.Value`, Unit kind, owner, and UID query value before `PhysicsWorld.RegisterUnit`.
- Validate Unit and Projectile registration metadata, including correct `PhysicsEntityKind`, non-null owner, and non-default/valid query identity.
- Add focused production-behavior tests for lifecycle ownership and query metadata.
- Repair existing stale tests for active Tick context, required stat definitions, canonical fixed-point expectations, and D-009 behavior.
- Diagnose every remaining Unit/FrameSync failure after those repairs. Fix it only if the correction is within the exact lifecycle/identity/fixture boundary; otherwise record it as a blocking follow-up and leave this plan incomplete.
- Update this plan, `MODULE_STATUS.md`, and the relevant validation row of `REPOSITORY_MAP.md` after execution.

### Out of scope

- Formal `UnitSpawnRequest`, prefab factory/pool composition, or changing Unit/Handlers to MonoBehaviour.
- GameplayCommand header/canonical schema, AimSnapshot, input receipts, or InputActionAsset creation.
- Aggregate GameplaySnapshot migration, checksum expansion, AuthorityFrame flow, or rollback algorithm redesign.
- Combat request ordering, shield ownership migration, Projectile UID migration, Ability/Buff/CC/Equipment completion.
- Pathfinding, MatchRule, presentation assets, UI/Lua, networking transport, authoring/Bake assets, or production content.
- asmdef, package, scene, prefab, ScriptableObject, Input Action, or design-document changes.

## 8. Implementation plan

1. Add focused failing tests before each production change:
   - normal death preserves unrelated `CombatModifier` records/handles;
   - pool reset still clears all dynamic modifiers;
   - Unit UID conversion preserves `SpawnLogicTick`, `RuntimeEntityPrefabId`, and `SpawnSequenceInTick`;
   - `BindUnitPhysics` produces actual UID/team/kind/owner metadata;
   - registration rejects missing owner, wrong kind, and default invalid identity;
   - stable range-query dedup and team filtering work with two distinct bound Units.
2. Change `Unit.ClearForDeath` only; do not change `ResetForPool` ownership semantics.
3. Implement the conversion at the lowest owner that can read UnitUid without making Physics depend on Unit. Prefer a Unit/FrameSync-side explicit converter or constructor call; do not add a Unit reference to Physics.
4. Correct `FrameSyncGameRuntime.BindUnitPhysics` and guard a missing `PhysicsWorld` consistently with the existing composition contract.
5. Strengthen `PhysicsWorld.RegisterUnit` and `RegisterProjectile` validation before mutation. Error messages must name the violated field/kind.
6. Repair stale fixtures:
   - wrap spawn/active Gameplay calls in `SimulationTickContextController.BeginTick`/scope teardown;
   - build complete minimal `StatDefinitionTable` fixtures used by the tested code path;
   - assert package fixed-point raw/canonical expectations;
   - replace the forbidden death-clears-all assertion with source-ownership preservation;
   - keep assertions behavior-oriented, not object-creation-only.
7. Run targeted classes until green, then all five project suites. Investigate remaining failures by reading the current code/design rather than broadening production changes.
8. Review the diff for accidental command/snapshot/public-protocol expansion and update status/results.

## 9. Public contracts

Expected public-contract treatment:

- Reuse `UnitUid`, `TeamId`, `RuntimeUidQueryValue`, `PhysicsEntityQueryInfo`, and `PhysicsEntityKind` unchanged where possible.
- If a conversion member is added, it must be a deterministic, allocation-free, lossless adapter whose output fields exactly mirror UnitUid. It must not become a new identity owner.
- `PhysicsWorld.RegisterUnit` and `RegisterProjectile` retain their signatures; their preconditions become enforced behavior.
- `Unit.ClearForDeath` is internal lifecycle behavior, not a new public API.
- No serialized schema or snapshot layout is changed.
- No assembly dependency is added or reversed.

Any need to alter a core Command, Snapshot, Projectile UID, Aim, AbilitySignal, checksum, or fixed-point contract is a failure condition for this plan and must move to the owning follow-up plan.

## 10. Validation

### EditMode

- Run the new lifecycle and UID-conversion tests.
- Run `PhysicsEntityQueryInfoTests`, `PhysicsWorldRegistrationTests`, and relevant range-query tests.
- Run `SpawnUnitTests`, `UnitWorldIntegrationTests`, `CombatSystemTests`, stat tests, attack tests, and `FrameSyncPipelineTests`.
- Run full filtered assemblies:
  - `FrameSyncMoba.Deterministic.Tests`
  - `FrameSyncMoba.Physics.Tests`
  - `FrameSyncMoba.Unit.Tests`
  - `FrameSyncMoba.FrameSync.Tests`

### PlayMode

- Run `FrameSyncMoba.Physics.PlayModeTests` because `PhysicsEntity2D` and registration touch Unity component lifecycle.
- No scene-level test is required unless execution introduces a GameObject composition behavior; if it does, scope has expanded and must be reviewed.

### Determinism and ownership

- Two Units with distinct UnitUids produce distinct query values regardless of registration order.
- Team filtering reads the Unit's immutable TeamId mirror.
- Invalid query metadata fails before collection mutation and produces the same exception type/message for the same input.
- Formal death preserves unrelated CombatModifiers; pool reset clears dynamic state.
- No authoritative float/double, Unity time, object instance ID, unordered dictionary enumeration, or presentation write-back is introduced.

### Unity MCP

1. Refresh AssetDatabase and wait for `IsCompiling=false` and `IsUpdating=false`.
2. Read Console Errors, Exceptions, Asserts, and Warnings; separate MCP operational diagnostics from project diagnostics.
3. Execute targeted and full per-assembly tests with explicit assembly filters.
4. Record exact passed/failed/skipped counts in this plan Results.

## 11. Failure and recovery

- Changes are limited to small lifecycle/binding/validation methods and focused tests; each can be resumed independently from the Progress checklist.
- Do not delete or disable a failing test. If a test is stale, replace its invalid setup or assertion and record why the former expectation contradicted the current formal contract.
- If metadata validation reveals existing callers registering incomplete entities, fix only Unit callers in this slice. Projectile callers become an explicit follow-up unless they already have enough authoritative data for a local correction without schema change.
- If a remaining Unit/FrameSync failure requires Command, Snapshot, Combat ordering, Projectile UID, or another excluded contract change, stop 0046 as incomplete, record the exact blocker, and create a child ExecPlan after review.
- No destructive asset or Git operation is needed. Scenes/assets remain untouched.

## 12. Results

Not executed yet.

Expected completion record:

- production files changed;
- tests added or corrected, including why any stale assertion changed;
- public-contract additions, if any;
- Unity compile and Console result;
- exact EditMode and PlayMode results;
- verified lifecycle/identity invariants;
- remaining blockers and follow-up plan link;
- confirmation that no scene, prefab, ScriptableObject, Input Action, package, asmdef, formal design, or production content changed.
