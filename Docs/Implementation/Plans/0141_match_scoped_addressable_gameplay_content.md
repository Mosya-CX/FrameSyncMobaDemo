# ExecPlan 0141 — Match-scoped Addressable gameplay content

Plan ID: 0141
Status: Completed
Created: 2026-08-26
Completed: 2026-08-27
Risk: High
Design conformance: Approval required (approved by the current user request; D-048 will be amended by D-051)
Estimated code delta: 1,800–3,200 lines plus migrated ScriptableObject and Addressables assets
Actual code delta: approximately 3,000 C# lines plus migrated ScriptableObject, scene, Addressables group and generated inventory assets
Affected assemblies: FrameSyncMoba.RuntimeConfig; FrameSyncMoba.Unit; FrameSyncMoba.Bootstrap; FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.ClientContent; focused EditMode/PlayMode test assemblies
Design sources: Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md §§3.5, 4.2–4.4, 17.1, 17.4–17.9; Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md §§1.3–1.5, 7; Docs/Architecture/DESIGN_INDEX.md
Decision dependencies: D-038; D-045; D-046; D-048; D-050; planned D-051
Validation basis: Unity 2022.3.62f1c1; Addressables 1.22.3; deterministic content inventory; focused EditMode/PlayMode; client/server Addressables configuration audit

## 1. Purpose

Replace the monolithic, direct-reference `GlobalPrefabTable` and eager full
hero/ability catalog composition with one small root index plus local
Addressable content partitions. A match loads only Core, its map and the
deduplicated set of heroes selected by all player slots. Both client and
Dedicated Server finish loading, canonicalize and freeze the deterministic
content scope before bootstrap Snapshot materialization or Tick 0.

The observable result is that selecting Varus does not load Aatrox's Unit
prototype, logical prefab, ability definitions or client hero view, while a
match containing both heroes loads both partitions and produces the same baked
registries regardless of selection/insertion or async completion order.

## 2. Progress

- [x] Resolve current authority, D-048 deviation approval and Unity/Console baseline.
- [x] Create and register this High-risk ExecPlan.
- [x] Generate the pre-migration direct/transitive resource inventory.
- [x] Change `GlobalPrefabTable` into an ID/range root index with Addressable partition references.
- [x] Add path-only child partition assets and a loaded/frozen runtime prefab table.
- [x] Split formal Unit and Ability authoring into Core, Varus and Aatrox partitions.
- [x] Configure local Logic/Core, Logic/Hero and client Hero Addressables groups.
- [x] Hand off the stable selected-hero set from Lobby into GameScene loading.
- [x] Load deterministic partitions asynchronously, canonicalize by stable IDs and gate bootstrap/Tick 0.
- [x] Amend Dedicated Server packaging to retain logic-only local Addressables while excluding client groups.
- [x] Add focused deterministic, configuration, server-scope and PlayMode lifecycle tests.
- [x] Compile and inspect the Unity Console after each source batch.
- [x] Run focused EditMode/PlayMode validation and dependency audits.
- [x] Run independent read-only High-risk review and resolve scope-local findings.
- [x] Freeze D-051 and update resource architecture, module status and current handoff.

## 3. Repository facts and discoveries

- `GameScene` currently serializes the full `FullMatchUnitRuntimeCatalog` and
  `FormalHeroAbilityRuntimeCatalog`; `GameBootstrap.Awake` bakes every Unit and
  Ability before an authoritative `GameStartConfig` is applied.
- `PlayerSlotConfig.HeroConfigId` is the selected `UnitPrototypeId`; it is not a
  runtime `UnitUid` and is not the same namespace as `PrefabId`.
- The formal content currently has hero prototypes 1001 (Varus) and 1002
  (Aatrox). `VarusAbilityRuntimeCatalog` already exists; the combined formal
  ability catalog contains both heroes and shared slot definitions.
- The current full Unit catalog has two heroes plus the map's minion, tower and
  base prototypes. The map/common topology must therefore be a Core partition,
  not inferred from the local controlled hero.
- The current `GlobalPrefabTable` is the sole `PrefabKind + PrefabId` authority
  but each Unit/Projectile/Misc entry directly references a logical prefab.
- Current client Addressables groups use `PackTogether`; selective hero loading
  therefore also requires hero-specific groups or packing changes, not only
  address strings.
- D-048 currently forbids Addressables on Dedicated Server. The user explicitly
  approved option 2, so D-051 will supersede only that loading/packaging clause:
  logic-only local catalogs are allowed on every endpoint; presentation groups
  remain client-only and remote catalogs/updates remain forbidden.
- Async completion order cannot become registry order. Loaded partitions and
  entries must be sorted and validated before construction of the immutable
  runtime lookup.
- The worktree has unrelated untracked `PROJECT_AUDIT.md`,
  `RESUME_TECH_ANALYSIS.md` and `tmp/`; this plan does not modify them.
- Unity's open `HeroTestScene` is clean. The intake Console contains only
  AI Game Developer/MCP Hub negotiation errors, recorded as a tool baseline.

## 4. Design sources and traceability

- FrameSync v10.2 §§4.2–4.4: `GameStartConfig.PlayerSlots[].HeroConfigId` is
  the authoritative match roster.
  -> `MatchContentSelectionTests` proves sorted unique selection and payload
  mismatch rejection.
- FrameSync v10.2 §§17.4–17.8: fixed `PrefabKind`, stable per-kind IDs, one
  global table, canonical Bake and no load-order ID assignment.
  -> `GlobalPrefabPartitionTests` proves duplicate/range/missing-address failure
  and async/insertion-order independent runtime lookup.
- FrameSync v10.2 §17.9: version mismatch prevents frame sync.
  -> root partition expected-version/content-hash validation plus existing
  `FrameSyncVersionHandshake` coverage.
- Presentation v13.2 §§1.3–1.5 and 7: presentation is reconstructible and uses
  the common prefab contract without becoming Gameplay authority.
  -> existing client binder tests plus selected-hero partition lifecycle tests.
- D-038: one formal resource chain, no competing PrefabId registry.
  -> root and child assets are one `GlobalPrefabTable` aggregate; build-time
  audit rejects direct logical prefab references and duplicate runtime tables.
- Approved D-051: Addressables transports local deterministic authoring into a
  pre-Tick frozen scope but never participates in Tick, Snapshot, checksum,
  spawn order or rollback.
  -> bootstrap loading-gate PlayMode tests and server/client group audits.

## 5. Scope

### In scope

- Root `GlobalPrefabTable` partition index and path-only child table assets.
- Path-only logical prefab and client-view entries.
- Core/Map/Hero partition selection from the complete match roster.
- Core versus Varus/Aatrox Unit and Ability catalog partitioning for current
  formal content.
- Local logic Addressables available to client and Dedicated Server.
- Client-only view partitions and exact handle ownership.
- Explicit loading gate before initial spawn materialization, Snapshot restore,
  `BootstrapApplied`, launch commit or Tick advancement.
- Stable content versions/hashes, visible missing/duplicate failures and
  deterministic canonical registry construction.
- Current resource inventories and Addressables configuration/docs.

### Out of scope

- Remote catalogs, CDN, downloads, hot update or runtime catalog mutation.
- Snapshot, checksum, Command, UID, Combat or Projectile semantic changes.
- Adding a third-party package.
- New heroes, abilities, maps, equipment or balance content.
- Initiating the final Windows/Linux Player build; build commands remain
  user-owned under the repository build procedure.

### Implications

- `GlobalPrefabTable` serialized schema and bootstrap initialization lifecycle
  change. Prefab IDs and Gameplay configuration IDs do not change.
- `GameStartConfig` and bootstrap wire payload remain unchanged; selection is
  derived from existing player slots. A local expected partition version/hash
  is validated against the root index, so no new wire field is required.
- Snapshot schema and checksum membership remain unchanged.
- Unity scenes lose direct Unit/Ability/Projectile/Buff/CC/Equipment logical
  content references once equivalent Core/Hero partitions are available.

## 6. Implementation plan

1. **Inventory and authority migration**
   - Extend the existing dependency inventory to report root, partition,
     Addressable address, asset path, GUID, ownership and direct/transitive
     dependencies in path-sorted CSV/Markdown.
   - Record the pre-migration direct-reference graph before asset mutation.
2. **Root and child contracts**
   - Convert serialized `PrefabEntry.UnityPrefab` to `LogicAssetAddress`; keep a
     nonserialized resolved prefab only in the match runtime table.
   - Add partition descriptors keyed by Core/Map/Hero and owner config ID.
   - Add a child asset containing path-only prefab groups and typed catalog
     addresses. It is part of the single global-table aggregate, not a second
     PrefabId authority.
3. **Runtime loading and freeze**
   - Add one bootstrap-owned Addressables service available in both client and
     server players. It initializes once, loads selected child assets and logic
     assets asynchronously, owns every handle and releases the match scope once.
   - Sort partitions and entries by explicit stable keys, validate root expected
     versions, then build the resolved synchronous `GlobalPrefabTable` used by
     UnitWorld/ProjectileWorld.
4. **Catalog composition**
   - Split Unit authoring into Core, Varus and Aatrox assets while retaining one
     shared stat-definition/dispose-policy authority.
   - Split Ability authoring into Varus and Aatrox assets and compose their
     definitions into one match registry with canonical per-slot ability lists.
   - Put universally required map/projectile/buff/CC/equipment configuration in
     Core for the current vertical slice.
5. **Application flow gate**
   - Freeze the all-player selected-hero set when Lobby locks the roster and
     hand it through `GameSessionContext` before GameScene activation.
   - Refactor GameBootstrap into pre-content registration, asynchronous content
     preparation and synchronous runtime composition. `Update` and network
     callbacks guard until preparation succeeds.
   - Verify the later authoritative `GameStartConfig` has the same hero set.
6. **Addressables/build configuration**
   - Create Logic-Core and per-hero Logic/Client groups with local paths and
     stable semantic addresses.
   - Dedicated Server includes only Logic groups and rejects Client groups or
     presentation dependencies; client includes both.
7. **Tests, review and documentation**
   - Add pure/EditMode ordering, validation and composition tests.
   - Add PlayMode loading-gate and selected-resource tests.
   - Compile/test via Unity MCP, run a separate read-only review, generate the
     post-migration inventory and update current documentation.

## 7. Public contracts and ownership

- `GlobalPrefabPartitionReference`: RuntimeConfig-owned root metadata mapping a
  stable partition kind/owner ID to one local Addressable child-table key and
  expected content version/hash.
- `GlobalPrefabSubTableAsset`: RuntimeConfig-owned path-only child data inside
  the single global-table aggregate.
- `PrefabEntry.LogicAssetAddress`: stable local key replacing serialized
  `UnityPrefab`; `ClientViewAddress` remains presentation metadata.
- `MatchContentSelection`: Bootstrap-owned, sorted unique match content request
  derived from existing `GameStartConfig`/Lobby fields; never snapshotted.
- `AddressableMatchContentScope`: Bootstrap-owned handle/lifetime object. It is
  the sole owner of deterministic content loads for one match.
- Resolved `GlobalPrefabTable`: nonserialized match runtime lookup consumed
  synchronously by Gameplay after sealing.

No duplicate UID, Command, Snapshot, checksum, Ability ID, PlayerSlot or
PrefabId authority is introduced.

## 8. Validation

- Unity compilation and Console inspection after each C# batch.
- EditMode:
  - root/child schema validation, duplicate and range rejection;
  - selected roster sorting/deduplication and mismatch rejection;
  - Core + Hero composition independent of request/load order;
  - combined Ability slots and definitions for Varus-only, Aatrox-only and both;
  - no serialized logical GameObject references in formal global/child tables;
  - local-only group paths, expected addresses and client/server inclusion rules;
  - deterministic inventory output and forbidden presentation dependency audit.
- PlayMode:
  - direct formal GameScene waits for content then materializes its fixture;
  - selected hero spawns with abilities and view; unselected hero partition is
    absent from the loaded scope;
  - network callback/payload arriving during loading is queued safely;
  - disposal releases every retained handle exactly once.
- Regression:
  - focused RuntimeConfig, Bootstrap, FrameSync and Unit tests affected by the
    contract;
  - existing ClientContent real Addressables load/release test.
- Player builds are not initiated by this plan. Editor build-scope tests prove
  configuration; final Windows client/Linux server packaging remains explicit
  external acceptance.

## 9. Independent review

The separate read-only High-risk review found five scope-local issues. All were
resolved before completion:

- Aatrox Q VFX metadata and roots 3101–3103 were moved from Varus/shared
  ownership to Hero 1002; Varus Blight VFX 4102–4104 moved to Hero 1001.
- authoritative roster mismatch validation now runs before Unit materialization
  or initial Snapshot work;
- load/destroy races now retain a local scope until ownership transfer and
  release it idempotently on cancellation or failure;
- Lobby custom message handlers now unregister symmetrically;
- Dedicated Server build validation now audits every Logic root's transitive
  dependency graph and rejects client-presentation dependencies before build
  configuration is mutated.

Focused tests cover the corrected partition ownership, early roster rejection,
double-dispose/destroy cleanup and positive/negative server dependency audit.

## 10. Failure and recovery

- All asset migrations are generated through Unity Editor APIs and validated
  before scene references are cleared.
- The existing full catalogs remain until the split assets have passed focused
  tests; they may remain as non-addressed migration evidence but cannot be a
  formal runtime dependency.
- A load failure disables Tick advancement, reports the exact partition/address
  and releases all successfully acquired handles. It never silently falls back
  to an incomplete registry.
- Root/child version or roster mismatch fails before Snapshot materialization.
- Resume from this plan's Progress and current Git diff; never touch the three
  unrelated untracked paths.

## 11. Results

- D-051 is frozen: local deterministic Addressables are allowed as a pre-Tick
  transport on client and Dedicated Server; remote catalogs and runtime updates
  remain forbidden.
- `GlobalPrefabTable` is now an empty root index over four path-only children:
  Core, Map 1, Varus and Aatrox. Their 20 stable prefab mappings are preserved
  with zero direct logical `GameObject` references.
- Four `Logic-*` groups contain 35 deterministic roots. Eight `Client-*` groups
  retain 63 local-only presentation roots; Varus owns eight hero roots and
  Aatrox owns six, including Q VFX 3101–3103.
- Lobby LoadScene v2 freezes the complete map/hero closure. `GameBootstrap`
  loads, validates, canonicalizes and seals only that closure before spawn,
  initial Snapshot or Tick 0, and rejects a later authoritative roster mismatch
  before materialization.
- Dedicated Server packaging enables only Logic groups and now rejects both
  direct and transitive client-presentation dependencies. Build-scope state is
  restored even when validation fails.
- Unity MCP synchronous compilation passed on 2026-08-27; the final Console
  Error and Exception queries are empty.
- Bootstrap EditMode passed 117/117. Aatrox formal content passed 10/10 and the
  Equipment/Core partition passed 6/6.
- PlayMode exact-method acceptance passed 4/4: Varus and Aatrox scoped loads,
  exclusion/Q-VFX ownership, formal GameBootstrap composition and
  destroy-during-load handle cleanup. One earlier class-filter MCP invocation
  failed to return after Unity had exited PlayMode; rerunning each exact method
  completed normally and is the recorded evidence.
- The broader Unit probe remains 545 passed / 10 retained unrelated failures in
  the previously recorded categories; this migration introduced no new Unit
  failure category.
- Corrected Windows Client and Linux Dedicated Server Player rebuild,
  BuildReport inspection and package-size comparison remain explicit external
  acceptance and were not initiated by this plan.
