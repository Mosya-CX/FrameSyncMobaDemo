# Match-scoped local Addressables and server resource architecture

## Goal and authority

The installed client and Dedicated Server both use one local Addressables
catalog. There is no remote catalog, CDN, runtime download, content update or
hot-update path. The client catalog contains deterministic logic and
presentation groups; the server build contains only deterministic `Logic-*`
groups.

`GlobalPrefabTable` remains the sole `PrefabKind + PrefabId` authority. D-051
changes its storage and loading boundary, not the ID namespaces: the formal
root is a small partition index, child tables contain path-only mappings, and a
nonserialized resolved table is frozen for the selected match before Gameplay
starts.

```text
locked Lobby roster
  MapConfigId + sorted unique HeroConfigIds
                     |
                     v
GlobalPrefabTable root index
  Core / Map / Hero child-table addresses + expected version/hash
                     |
          local asynchronous Addressables load
                     v
validate -> canonicalize -> compose -> freeze before initial Snapshot/Tick 0
             |                              |
             v                              v
resolved logic prefab table        client presentation addresses
UnitWorld / ProjectileWorld        async view lease/binding
```

Addressables is therefore a pre-Tick resource transport. It does not determine
spawn order, execute inside the deterministic Tick, or enter Command, Snapshot,
checksum, rollback or restore state. A missing address, version/hash mismatch,
duplicate ID or roster mismatch fails visibly before bootstrap materialization.

## Root and partition contract

The root asset is `Assets/Config/Formal/GlobalPrefabTable.asset`. Its serialized
prefab groups are empty. It references these child tables by semantic address:

| Partition | Owner | Child-table address | Selection rule |
|---|---:|---|---|
| Core | 0 | `content/table/core` | every match |
| Map | 1 | `content/table/map/1` | `MapConfigId == 1` |
| Hero | 1001 | `content/table/hero/1001` | Varus is present in any locked player slot |
| Hero | 1002 | `content/table/hero/1002` | Aatrox is present in any locked player slot |

Each root descriptor owns a non-zero content version and dependency-backed
64-bit hash. Each `GlobalPrefabSubTableAsset` repeats that identity and contains:

- path-only prefab rows keyed by `(PrefabKind, PrefabId)`;
- `LogicAssetAddress` for deterministic Unit/Projectile/map prefabs;
- optional semantic `ClientViewAddress` for reconstructible presentation;
- typed addresses for Unit, Ability, Projectile, Buff, CrowdControl,
  Equipment and Map configuration assets.

Logical asset addresses are their normalized local Unity asset paths. This
keeps the source mapping auditable and avoids another registry. IDs are still
assigned by authored configuration; load order never allocates an ID.

## Implemented match partitions

| Group | Deterministic ownership | Roots | Client | Server |
|---|---|---:|:---:|:---:|
| `Logic-Core` | Core child table; common units/projectiles; Core Unit, Projectile and Buff catalogs; shared Unit dispose-policy table; full CC and Equipment | 14 | Yes | Yes |
| `Logic-Map-1` | Map child table, logic map prefab and deterministic map config | 3 | Yes | Yes |
| `Logic-Hero-1001` | Varus child table, logic prefabs and Unit/Ability/Projectile/Buff catalogs | 10 | Yes | Yes |
| `Logic-Hero-1002` | Aatrox child table, logic prefabs and Unit/Ability/Projectile/Buff catalogs | 8 | Yes | Yes |

The root plus four children cover the original 20 prefab rows with the same
stable IDs and contain zero direct logical `GameObject` references. The former
combined catalogs remain non-addressed migration evidence; `GameScene` no
longer serializes them.

## Client presentation groups

Only independently requested roots are Addressable. Models, clips, materials,
textures and other ordinary dependencies remain transitive dependencies unless
the runtime requests them by their own stable address.

| Group | Purpose | Roots |
|---|---|---:|
| `Client-Hero-1001` | Varus Unit, Projectile and ability VFX roots | 8 |
| `Client-Hero-1002` | Aatrox Unit, Projectile and Q VFX roots | 6 |
| `Client-UnitViews` | six common Unit views | 6 |
| `Client-ProjectileViews` | two common attack Projectile views | 2 |
| `Client-VFX` | shared independently spawned effect | 1 |
| `Client-Audio` | formal audio root | 1 |
| `Client-Shared` | map and shared client roots | 4 |
| `Client-UI` | pages, indicators and independently resolved sprites | 35 |

Client presentation still has 63 independently addressed roots in total.
Addresses use semantic prefixes such as `view/unit/`, `view/projectile/`,
`view/map/`, `vfx/`, `audio/`, `ui/page/`, `ui/indicator/` and `ui/icon/`.

`VfxLibrary` keeps the stable VFX definition ID and Addressable address, plus
an optional `OwnerHeroConfigId` (`0` means shared). Before client presentation
dispatch starts, `GameBootstrap` passes the frozen match hero list to
`VfxManager.PreloadAsync`; only shared entries and entries owned by a selected
hero acquire a lease and create their first inactive pool instance. This keeps
the first-use path warm without loading another hero's VFX, while a standalone
scene with no match scope may explicitly use the full-library overload.

## Runtime selection and lifecycle

1. Lobby freezes the selected map and the complete locked player-slot roster.
   Hero IDs are sorted and deduplicated; local player identity is not used to
   decide the match closure.
2. `GameSessionContext` stores the selection before `GameScene` activation.
   Standalone fixture scenes derive an equivalent selection from their
   player-controlled initial spawns and the sole available map partition.
3. `AddressableMatchContentService` initializes Addressables, selects Core,
   the requested Map and requested Heroes, and loads every required child,
   catalog and logical prefab asynchronously.
4. Child identity, version/hash, addresses, ID uniqueness and required catalog
   kinds are validated. Partitions and registry rows are sorted by explicit
   stable keys before composition.
5. `GameBootstrap` replaces the authoring prefab table in
   `GlobalGameplayData` with the resolved match-local table, combines selected
   catalogs and only then creates Gameplay worlds and initial state.
6. A bootstrap payload arriving while content is loading is queued. Its
   authoritative map/hero closure must exactly match the frozen local request.
7. `Update` cannot advance Tick until `InitializationTask` succeeds. A failure
   reports the exact partition/address and releases already acquired handles.
8. One `AddressableMatchContentScope` owns all deterministic-content handles
   and releases them once at match teardown. Client view caches continue to use
   exact reference-counted leases and generation checks.

Async completion order is never registry order. A Varus-only scope contains
Unit prototype 1001 and Ability 10011 but excludes Aatrox prototype 1002,
Ability 10021 and its client hero roots; the inverse property holds for an
Aatrox-only scope.

## Assembly ownership

| Ownership | Location | Runtime role |
|---|---|---|
| Root/child contracts | `Assets/Scripts/RuntimeConfig/` | IDs, path-only partition metadata, validation and resolved runtime table |
| Match selection/loading | `Assets/Scripts/Bootstrap/` | Lobby handoff, local Addressables loading, canonical composition and loading gate |
| Deterministic content | `Assets/Config/Formal/MatchContent/`, `Prefabs/Logic/` | split catalogs and logic prefabs |
| Client views | `Assets/ClientContent/Views/` and other `Assets/ClientContent/` roots | reconstructible models, Animator, VFX, audio and UI |
| Client binders/loaders | `Assets/Scripts/ClientContent/` | presentation leases, UID/object reconciliation; excluded by `UNITY_SERVER` |
| Build/migration tools | `Assets/Scripts/Bootstrap/Editor/Addressables/` | group configuration, partition migration, inventories and client/server audits |

Gameplay assemblies consume the already resolved synchronous table and do not
reference Addressables APIs. Bootstrap owns Addressables initialization and
Unity scheduling on both endpoint types.

### Shared ScriptableObject ownership

Runtime identity can differ across bundles even when several serialized fields
point to the same source GUID: an implicit dependency may be embedded once per
bundle and deserialize as separate Unity object instances. Shared deterministic
configuration whose identity is validated during composition must therefore
have one partition owner, not references copied into several `Logic-*` roots.

For the current Unit split, `CoreUnitRuntimeCatalog` is the sole owner of
`FullMatchUnitDisposePolicyTable`. `VarusUnitRuntimeCatalog` and
`AatroxUnitRuntimeCatalog` contain only their hero prototypes and serialize no
dispose-policy reference. Core is selected in every match, so selected Unit
catalogs still bake with one complete policy authority. The migration and
EditMode configuration guard preserve this topology; do not weaken the runtime
conflict check or re-add the policy reference to Hero catalogs.

## Dedicated Server packaging

The server no longer deletes the whole Addressables output. Its build scope:

- temporarily enables `IncludeInBuild` only for `Logic-Core`, `Logic-Map-1`,
  `Logic-Hero-1001` and `Logic-Hero-1002`;
- builds a local server catalog and logic bundles with the Player;
- restores every group flag and default group after the build attempt;
- strips presentation scene components and client objects;
- audits the output for catalog/bundle presence and rejects any `client-*`
  bundle or forbidden presentation dependency.

Client builds include Logic plus Client groups. Platform guards still ensure a
Windows player cannot embed Linux bundles and vice versa. `UNITY_SERVER` still
excludes `FrameSyncMoba.ClientContent`; only the Bootstrap match-content loader
is shared.

## Authoring and migration workflow

For a new hero or map:

1. allocate stable Gameplay and Prefab IDs in the existing namespaces;
2. keep deterministic configuration and logic prefabs under
   `Assets/Config/Formal/` and client presentation under
   `Assets/ClientContent/`;
3. create one child table and the split Unit/Ability/Projectile/Buff catalogs;
4. add one `Logic-Hero-<id>` or `Logic-Map-<id>` local group and, when needed,
   one `Client-Hero-<id>` group;
5. add one root descriptor with a bumped content version and regenerated
   dependency hash; never add another PrefabId registry;
6. run `FrameSyncMoba/Addressables/Migrate Match-Scoped Gameplay Content` for
   the current formal slice or equivalent Unity Editor authoring APIs;
7. regenerate `Generate Current Dependency Inventory`, verify the manifest,
   compile, and run focused EditMode plus real-load PlayMode tests.

Do not hand-edit ScriptableObject or Addressables YAML. Do not use
`WaitForCompletion`, remote paths, runtime catalog updates or a silent full-table
fallback.

## Evidence and remaining acceptance

- Post-migration logic manifest: `MATCH_CONTENT_RESOURCE_MANIFEST.*`.
- Pre-migration comparison: `MATCH_CONTENT_PRE_MIGRATION_DEPENDENCIES.*`,
  `MATCH_CONTENT_PRE_MIGRATION_ROOTS.*` and
  `MATCH_CONTENT_PRE_MIGRATION_FORMAL_ROOTS.csv`.
- Current client roots: `ADDRESSABLE_ROOTS.*` (63 roots, 0 remote).
- Current dependency graph: `CURRENT_DEPENDENCIES.*` (149 roots, 345 unique
  dependencies, 987 edges at the 2026-08-27 capture). The formal policy table
  is a direct dependency of the Core Unit catalog only.
- Focused validation: Bootstrap EditMode 123/123; VFX preload/reuse/filter
  EditMode 2/2; real Varus/Aatrox scoped load,
  exclusion and release PlayMode 2/2; formal GameBootstrap composition and
  destroy-during-load cleanup PlayMode 2/2; Aatrox
  content 10/10; equipment/Core partition 6/6.

The corrected Windows Client and Linux Dedicated Server Player rebuild,
BuildReport inspection and post-partition package-size measurement remain
user-owned external acceptance. The previous approximately 612 MB client
measurement predates this partition build and must not be treated as the new
package size.
