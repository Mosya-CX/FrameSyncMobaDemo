# ExecPlan 0142 — Core-owned Unit dispose policy

Plan ID: 0142
Status: Completed
Created: 2026-08-27
Completed: 2026-08-27
Risk: Medium
Design conformance: Strict
Estimated code delta: 40–100 C# lines plus three regenerated ScriptableObject assets and generated Addressables inventory/docs
Affected assemblies: FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.Bootstrap.EditModeTests
Design sources: Docs/Architecture/DECISION_LOG.md D-051; Docs/Implementation/Addressables/RESOURCE_ARCHITECTURE.md
Decision dependencies: D-038; D-048; D-051
Validation basis: Unity 2022.3.62f1c1; UOS client/server logs; focused EditMode/PlayMode tests; Unity Console; dependency inventory

## 1. Purpose

Fix the built-player match bootstrap failure caused by the same
`UnitDisposePolicyTable` being serialized as an implicit dependency of the
Core, Varus and Aatrox Unit Addressables bundles. In a Player build those
bundle-local dependency copies are distinct Unity object instances, so the
combined Unit catalog correctly rejects them as conflicting policy authorities.

The Core Unit partition is always selected and already owns shared Unit
configuration under D-051. The fix therefore makes Core the sole serialized
owner of the policy table, leaves Hero Unit catalogs responsible only for their
hero prototypes, and preserves the existing strict runtime conflict check.

## 2. Progress

- [x] Correlate corrected server log and both client diagnostics.
- [x] Confirm successful match-scoped Addressables loading before Unit bake.
- [x] Confirm all three Unit catalogs serialize the same policy-table GUID from separate Logic groups.
- [x] Record a clean Unity Console baseline and current worktree state.
- [x] Change the idempotent migration to assign the shared policy only to Core.
- [x] Regenerate and validate the three formal Unit partition assets through Unity APIs.
- [x] Add regression coverage for Core-only ownership and selected-roster baking.
- [x] Refresh hashes/dependency inventory and affected documentation.
- [x] Compile through Unity, inspect Console and run focused EditMode/PlayMode tests.
- [x] Review the diff against D-051 and record final evidence.

## 3. Repository facts and discoveries

- Both clients loaded Map 1 and Heroes 1001/1002 successfully before returning
  to the main scene; the Dedicated Server reached the same Unit catalog bake
  failure. Selection, UOS transport and Addressable address resolution are not
  the failing stage.
- `CoreUnitRuntimeCatalog`, `VarusUnitRuntimeCatalog` and
  `AatroxUnitRuntimeCatalog` all reference
  `FullMatchUnitDisposePolicyTable.asset`.
- The three catalogs are roots in three different `Logic-*` groups while the
  policy table is not an explicit Addressable root. The build therefore embeds
  the implicit dependency separately.
- `UnitRuntimeCatalogAsset.BakeCombinedOrThrow` intentionally requires one
  shared policy authority by Unity object identity. Weakening that validation
  would hide conflicting ownership and is not part of this fix.
- The migration has both initial and already-migrated paths; both currently
  restore the invalid Hero references and must be corrected.

## 4. Design traceability

- D-051: Core owns shared configuration and every match selects Core.
  -> Only `CoreUnitRuntimeCatalog` retains the policy reference.
- D-051: loaded partitions are composed and frozen before Tick 0.
  -> Combined bake remains strict and receives exactly one policy table.
- D-038/D-051: one formal resource chain, no competing runtime authority.
  -> Hero catalogs no longer serialize duplicate shared configuration.
- Addressables dependency isolation: logic partitions must not rely on a shared
  ScriptableObject being duplicated implicitly across bundles when runtime
  identity is semantically significant.
  -> Editor configuration test asserts the ownership topology directly.

## 5. Scope

### In scope

- Unit partition migration ownership for the shared dispose-policy table.
- Formal split Unit catalog regeneration, partition hash refresh and dependency
  inventory regeneration.
- Focused configuration, selected-roster bake and loading lifecycle tests.
- Current implementation/status/resource documentation.

### Out of scope

- UID, Command, Snapshot, checksum, Combat or Unit lifecycle semantics.
- New public protocols or Addressables packages.
- Remote catalogs, content updates or CDN delivery.
- Windows client or Linux Dedicated Server Player builds; the user owns
  packaging and final UOS live acceptance.

## 6. Implementation plan

1. Make both migration paths configure Core with the source policy table and
   configure Varus/Aatrox with `null` policy ownership.
2. Add an EditMode invariant that checks Core has the formal policy and every
   Hero Unit partition has none; keep the existing per-roster combined bake
   tests as behavioral coverage.
3. Run the idempotent migration through Unity so assets and content hashes are
   serialized by Editor APIs, then regenerate the dependency inventory.
4. Compile, inspect Console, run focused EditMode and Addressables/bootstrap
   PlayMode tests, and review the final diff.

## 7. Public contracts and ownership

No public type, ID, protocol, Snapshot or checksum contract changes. Ownership
is clarified within the existing D-051 partition contract:

- Core Unit catalog: shared stat definitions, shared dispose-policy table and
  non-hero Unit prototypes.
- Hero Unit catalog: only the selected hero prototype; no shared policy table.
- Combined bake: unchanged strict validation and deterministic registry output.

## 8. Validation

- Unity script compilation and final Error/Exception Console inspection.
- EditMode:
  - Core-only policy ownership topology;
  - Varus-only and Aatrox-only selected catalog bake;
  - both-hero composition through the existing configuration suite;
  - current Bootstrap Editor configuration/audit assembly.
- PlayMode:
  - match-scoped Addressables load/release lifecycle;
  - GameBootstrap loading gate and initialization lifecycle.
- Generated dependency inventory shows the policy table as a dependency of the
  Core Unit catalog only.
- Player builds and UOS live acceptance remain explicitly pending for the user.

## 9. Failure and recovery

- Do not remove or weaken the runtime conflict exception.
- Asset mutations run through the idempotent Unity migration and can be rerun.
- If compilation or tests fail, stop asset regeneration, retain the exact
  Console/test evidence and resume from this plan's Progress list.
- Do not issue any Player build command.

## 10. Results

- Both initial and already-migrated paths now configure the Core Unit catalog
  with the formal dispose-policy table and configure Varus/Aatrox with `null`.
  The migration is idempotent and cannot restore the invalid Hero references.
- Unity Editor regenerated the formal split catalogs. The resulting serialized
  topology is Core = GUID `a94aa9b65aa66d9438c9752a791476bd`, Varus = null,
  Aatrox = null. The current dependency graph contains the policy dependency
  from the Core Unit catalog only.
- Hero partition hashes changed to `15379179350999139146` (1001) and
  `8362605082240316254` (1002); root and child assets plus the resource manifest
  agree exactly.
- Bootstrap EditMode passed `118/118`. The ownership test additionally checks
  each Hero catalog's complete AssetDatabase dependency closure excludes the
  policy asset. Existing Varus-only/Aatrox-only combined bake cases passed.
- Addressable selection/release PlayMode passed `2/2`; GameBootstrap initialize
  and destroy-during-load lifecycle passed `2/2`; Aatrox content passed `10/10`;
  Equipment/Core content passed `6/6`.
- The broad Unit probe reproduced the retained baseline categories at
  `545 passed / 10 failures`; none is in this change's content ownership path.
- The MCP Console-clear operation failed twice because MCP held its own
  `Temp/mcp-server/ai-editor-logs.txt` file open. Fallback used Unity's
  `Debug.ClearDeveloperConsole`, then a fresh compile and focused test. The
  final one-minute MCP Error and Exception queries were both empty. This tool
  failure did not mutate project content and requires no Player-side recovery.
- No Player build command was issued. Matching rebuilt Windows Client and Linux
  Dedicated Server packages plus UOS live entry remain user-owned acceptance.
