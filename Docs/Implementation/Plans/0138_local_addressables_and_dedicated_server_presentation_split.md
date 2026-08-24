# ExecPlan 0138 — Local Addressables and Dedicated Server presentation split

Plan ID: 0138
Status: Verification Pending
Created: 2026-08-23
Completed:
Risk: High
Design conformance: Approval required (approved by the current user request; D-048 amendment will freeze the resource boundary)
Estimated code delta: 2,500–4,500 lines plus migrated Unity assets and generated dependency reports
Actual code delta: 337 changed paths including GUID-preserving asset moves/splits; source-line totals are not meaningful for imported model assets
Affected assemblies: FrameSyncMoba.RuntimeConfig; FrameSyncMoba.Unit; FrameSyncMoba.FrameSync; FrameSyncMoba.Bootstrap; FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.ClientContent; focused EditMode/PlayMode test assemblies
Design sources: Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md §§17.4–17.9; Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md §§1.3–1.5, 2, 7–10; Docs/Architecture/DESIGN_INDEX.md
Decision dependencies: D-019; D-023; D-038; D-043; D-045; planned D-048
Validation basis: Unity 2022.3.62f1c1; Addressables 1.22.3; focused EditMode/PlayMode; Addressables content build; Windows client and Linux Dedicated Server build reports; dependency-manifest diff

## 1. Purpose

Ship all client presentation resources through a local-only Addressables catalog
without remote update/download behavior, while Dedicated Server startup never
initializes Addressables and its player/build dependencies exclude unit models,
AnimatorControllers, animations, render materials, VFX, audio and UI content.

The observable result is unchanged Gameplay and networking, asynchronous client
view attachment after logical entities exist, reconstructible views after
rollback/respawn/pooling, and measurably smaller Dedicated Server build/runtime
content ownership.

## 2. Progress

- [x] Resolve current workflow, design index, D-019/D-038 and presentation boundaries.
- [x] Confirm the clean worktree, current Unity baseline and absence of Addressables.
- [x] Freeze D-048 resource/data-ownership amendment.
- [x] Generate baseline direct/transitive dependency manifest for formal assets.
- [x] Install and pin Addressables 1.22.3 (installed by the user); configure local-only profiles/groups.
- [x] Add single-table `PrefabId -> logic prefab + optional client view address` contract.
- [x] Add client-only async content service and exact handle/reference-count ownership.
- [x] Add logical-unit-to-client-view binding/reconciliation lifecycle.
- [x] Split Varus into logic prefab plus Addressable view as the vertical slice.
- [x] Validate Varus spawn, animation, outline, death/respawn, rollback and release.
- [x] Split Aatrox, four minions and two towers using the proven pipeline.
- [x] Split projectile/VFX/audio/UI presentation dependencies required by the formal game path.
- [x] Add Dedicated Server build exclusion and BuildReport dependency audit.
- [x] Generate post-migration dependency manifest and compare with baseline.
- [x] Compile, run focused EditMode/PlayMode and Addressables content tests.
- [ ] Build/inspect client and Linux Dedicated Server exactly once per approved build procedure.
- [x] Run independent read-only High-risk review and resolve scope-local P0/P1 findings.
- [x] Update module status, current handoff, build guide and resource architecture documentation.

## 3. Repository facts and discoveries

- The project is clean at plan creation.
- The user installed and pinned `com.unity.addressables` 1.22.3. The package
  manifest, lock file and Odin Addressables integration are user-owned changes
  and are preserved by this migration.
- The eight former monolithic unit prefabs are preserved under
  `Assets/Archive/LegacyMonolithicUnitPrefabs/` as migration evidence. Formal
  logic prefabs now live under `Assets/Config/Formal/Prefabs/Logic/Unit/`, and
  Addressable views live under `Assets/ClientContent/Views/Unit/`.
- `GlobalPrefabTable` is the one formal prefab-ID table. D-038 forbids a second
  competing runtime prefab table, so the migration extends each existing entry
  with an optional client-view address while retaining the direct logic prefab.
- `UnitWorld` synchronously instantiates/rents the `PrefabKind.Unit` GameObject.
  That path remains deterministic and synchronous; Addressables never enters
  Gameplay spawn, restore, snapshot or checksum code.
- Client presentation state is reconstructible by the current presentation
  design and therefore may bind asynchronously after the logic Unit exists.
- Existing MCP hub-negotiation Console messages are environment diagnostics,
  not Unity compilation failures.

## 4. Design sources and traceability

- FrameSync v10.2 §§17.4–17.9: fixed `PrefabKind`, stable PrefabId ranges,
  single `GlobalPrefabTable`, version handshake.
  -> `GlobalPrefabTableClientViewTests`, `ClientContentVersionTests`.
- Presentation v13.2 §§1.3–1.5: presentation reads logic state, never writes
  authoritative pose/state, and is reconstructible rather than snapshotted.
  -> `ClientUnitViewBindingPlayModeTests`, rollback/rebind test.
- Presentation v13.2 §§2, 7–10: Unit presentation host, sockets and prefab
  lookup are client presentation ownership.
  -> view-prefab composition and socket/Animator binding tests.
- D-038: one formal resource chain and no duplicate prefab registry.
  -> dependency audit asserts one stable PrefabId mapping and no second ID map.
- Current user request: local-only Addressables and server presentation exclusion.
  -> local-profile tests, no-remote-schema tests and Dedicated Server BuildReport audit.

## 5. Scope

### In scope

- Official Addressables 1.22.3 package and local-only settings/groups.
- Formal client models, animation, materials/shaders, VFX, audio, UI sprites and
  presentation prefabs reachable from the playable client path.
- Logic/View separation for all eight formal Unit prefabs and required projectiles.
- Client async initialization, load, bind, cancellation, pooling and release.
- Dedicated Server compile/build exclusion plus dependency and memory-oriented reports.
- Repeatable baseline/post-migration dependency manifests.

### Out of scope

- Remote catalogs, CDN, online updates, downloads, cache management or hot update.
- Gameplay rules, balance, networking protocol, Snapshot schema and checksum semantics.
- Replacing Addressables with a custom AssetBundle system.
- Uploading UOS artifacts.

### Contract implications

- `GlobalPrefabTable` serialization changes by adding client presentation metadata;
  stable PrefabKind/PrefabId semantics remain unchanged.
- No Snapshot, Command, checksum or wire schema change is planned.
- Addressables handles and view instances are client-local and never snapshotted.

## 6. Implementation plan

1. **Inventory and guardrails**
   - Add an Editor dependency reporter using `AssetDatabase.GetDependencies`.
   - Emit deterministic CSV/Markdown inventories sorted by asset path and GUID.
   - Classify each dependency as Logic, ClientPresentation, SharedConfig or EditorOnly.
2. **Addressables foundation**
   - Install 1.22.3, create local build/load paths only, disable remote catalog.
   - Create deterministic group/address naming and validation tooling.
3. **Public resource boundary**
   - Extend `PrefabEntry` with optional immutable client-view address metadata.
   - Keep logic prefab resolution in `RuntimeConfig`/`Unit`; put all Addressables
     API use in a client presentation assembly above Gameplay.
4. **Client content lifecycle**
   - Implement initialize/load/cache/release contracts with one retained handle
     per loaded view prefab and explicit instance ownership.
   - Never call `WaitForCompletion`; propagate failure and cancellation visibly.
5. **Unit view binding**
   - Bind by stable `UnitUid.RuntimeEntityPrefabId` and logic instance generation.
   - Reconcile spawn/despawn/pool/death/respawn/rollback without feeding state back.
6. **Asset migration**
   - Prove Varus first; then migrate the other seven units and their dependencies.
   - Move presentation-only roots/components into Addressable view prefabs and
     leave required deterministic MonoBehaviours on logic prefabs.
   - Migrate formal VFX/audio/UI and projectile views required by the game path.
7. **Server exclusion and audit**
   - Dedicated Server skips client content bootstrap by compile/build role.
   - Build preprocess/build report fails on forbidden client presentation assets
     or Addressables catalog/bundle content in the server player.
8. **Verification and docs**
   - Run focused suites, Addressables content build and dependency diff.
   - Run one independent High-risk review before declaring completion.

## 7. Public contracts and ownership

- `PrefabEntry.ClientViewAddress` (name provisional): stable client presentation
  metadata owned by `FrameSyncMoba.RuntimeConfig`; does not load assets.
- `IClientContentService`: asynchronous client-only load/release boundary owned
  above Gameplay, implemented by Addressables.
- `IClientUnitViewBinder` / `ClientUnitViewBinding`: client-local lifecycle owned
  by presentation/bootstrap; keyed by stable UnitUid and PrefabId.
- `ClientViewPrefab`: presentation composition root; reads a bound Unit but owns
  no Gameplay state.
- Dependency-manifest/build-audit DTOs are Editor-only operational contracts.

No duplicate PrefabId, UnitUid, snapshot, command or view-ID registry is allowed.

## 8. Validation

- Unity compilation and Console inspection after every C# batch.
- EditMode:
  - global prefab address validation and canonical ordering;
  - local Addressables settings/groups with no remote catalog/path;
  - handle cache acquire/release/failure behavior;
  - dependency-report deterministic output;
  - server forbidden-dependency audit.
- PlayMode:
  - logic Unit exists before view and remains valid during async load;
  - spawn/bind/despawn, pool reuse, death/respawn and rollback rebind;
  - Animator, outline and semantic sockets bind on Varus/Aatrox/minion/tower views;
  - Addressables release removes instances without affecting Gameplay.
- Integration:
  - build local Addressables content;
  - load the real GameScene client path from installed local catalog;
  - inspect Windows client and Linux server BuildReports;
  - compare server build size and forbidden asset list to baseline.

## 9. Independent review

The independent read-only review found no P0. Both P1 findings were resolved:

- Addressables 1.22.3 may copy an existing build output into StreamingAssets
  even when player content build is disabled. Dedicated Server build scope now
  pins the package's exact streaming-path filter to reject every Addressables
  path, restores it after the build, and has a focused regression test.
- Rollback may recreate a Unit or Projectile object under the same stable UID.
  Both binders now compare object identity as well as UID and replace stale
  bindings during reconciliation.

Two P2 limitations remain explicit rather than hidden: `UIManager` currently
loads the seven page prefabs during initialization even when a page is not
pre-instantiated, and older presentation implementations still compile in
shared managed assemblies although the new Addressables loader assembly and all
presentation assets are excluded from Dedicated Server. Neither puts models,
animation, VFX, audio or UI bundles into the server build; a full managed-code
assembly split is a separate architecture change.

## 10. Failure and recovery

- The clean starting worktree is the recovery anchor.
- Migration is staged per asset family; Varus must pass before the other seven
  unit prefabs change.
- Generated inventories are append-free and reproducible; they expose accidental
  client references left in logic assets.
- Do not delete original monolithic prefabs until all references have moved and
  Unity serialized-reference tests pass. Prefer moving/renaming only after the
  split is accepted.
- A failed Addressables load leaves Gameplay alive, reports the exact key and
  releases any valid handle; it never substitutes deterministic state.
- Live UOS upload is external and remains pending after local build acceptance.

## 11. Results

- Local-only catalog: 63 root addresses in six `Client-*` groups, zero remote
  entries. Dependencies remain implicit bundle dependencies unless they need an
  independent runtime address.
- Content build: seven bundles plus one catalog, 612,459,164 bytes, completed
  successfully for Linux. The largest bundle is projectile presentation; its
  size is dominated by three 72–82 MiB source GLB files rather than duplicate
  Addressable roots.
- Dependency evidence: baseline and current CSV/Markdown reports plus a complete
  Addressable-root report. Current graph contains 135 roots, 363 unique
  dependencies and 1,413 edges.
- Unity validation: Bootstrap EditMode 106/106; FrameSync EditMode 91/91;
  local Addressables configuration 5/5; real Addressables client service
  PlayMode 1/1; UI lifecycle/race PlayMode 3/3; previously recorded focused
  Aatrox 10/10, Aatrox prefab 8/8, map prefab 1/1 and HeroTest shop 2/2.
- Dedicated Server clean compilation completed with no C# error. Its 66 Player
  assemblies contain neither `FrameSyncMoba.ClientContent` nor the Odin
  Addressables Editor module. The server build scope rejects existing client
  catalog/bundle paths and its scene/build audit rejects forbidden assets.
- The retained full Unit-suite failures are unchanged project baselines:
  BuffEffectLibrary, ChargeAbility, CombatEnhancement, one authored lane test,
  Movement (three), active-Tick guards (two) and Unit assembly-boundary (one).
- The first combined Player acceptance build was rejected after runtime
  inspection: the Windows Player contained
  `StreamingAssets/aa/StandaloneLinux64` and its `settings.json` declared
  `m_buildTarget=StandaloneLinux64`. Linux shader variants caused all client
  models, TMP fonts and sprites to render magenta. The build entry now switches
  the active target/subtarget before Addressables runs, deletes only the prior
  generated `StreamingAssets/aa` directory, and fails the client build unless
  settings, platform directory and bundles are exactly StandaloneWindows64.
- The same build exposed server scene-strip ordering errors: URP additional
  Camera/Light data still depended on Camera/Light when those base components
  were removed. The stripper now removes the two URP dependency components
  first. Five new target/output/strip-order/server-output audit tests are
  included in Bootstrap EditMode 106/106.
- A user-owned corrected Windows client/Linux Dedicated Server rebuild and
  runtime visual acceptance remain pending. Codex must not initiate that build
  unless the user asks again.
