# Local Addressables and Dedicated Server resource architecture

## Goal and boundary

The client ships one local Addressables catalog with the installed player. The
project has no remote catalog, CDN, download, cache-version or hot-update path.
Dedicated Server neither initializes Addressables nor receives the client
catalog, bundles, render models, animation, materials, VFX, audio or UI assets.

Deterministic Gameplay remains synchronous and Tick-based. Addressables never
owns Unit/Projectile creation, Snapshot, restore, checksum or Command state.

```text
GlobalPrefabTable
  PrefabId -> logicPrefab + optional clientViewAddress
                    |                    |
                    v                    v
      UnitWorld / ProjectileWorld   ClientContent loader
          synchronous logic          asynchronous view
                    \                 /
                     stable UID binding
```

## Folder and assembly ownership

| Ownership | Location | Runtime role |
|---|---|---|
| Deterministic logic prefabs | `Assets/Config/Formal/Prefabs/Logic/` | Synchronous Unit, Projectile and map logic |
| Client view roots | `Assets/ClientContent/Views/` | Models, Animator, renderer, outline and view adapters |
| Client shared presentation | `Assets/ClientContent/Animation`, `Materials`, `VFX`, `Audio`, `Indicators`, `UI` | Dependencies or independently addressed roots |
| Client loader/binders | `Assets/Scripts/ClientContent/` | Addressables handles, async leases and UID reconciliation; excluded by `UNITY_SERVER` |
| Server build policy | `Assets/Scripts/Bootstrap/Editor/DedicatedServerPresentationBuildPipeline.cs` | Content suppression, scene stripping and build-report audit |
| Migration evidence | `Assets/Archive/LegacyMonolithicUnitPrefabs/` | Non-runtime comparison fixtures; never a formal root |

`FrameSyncMoba.RuntimeConfig` owns the single PrefabId table and stores only the
address string. Gameplay assemblies do not reference Addressables APIs.
`FrameSyncMoba.ClientContent` implements the loading contract and is absent from
Dedicated Server Player assemblies.

## Address policy

Only independently loaded roots are marked Addressable. Models, clips,
materials, textures and other ordinary dependencies are intentionally not all
given their own addresses; Addressables includes them transitively in the root's
bundle. Mark a dependency separately only when the runtime must request it by a
stable address.

The current six groups are:

| Group | Purpose | Roots |
|---|---|---:|
| `Client-UnitViews` | eight unit view prefabs | 8 |
| `Client-ProjectileViews` | eight projectile view prefabs | 8 |
| `Client-VFX` | independently spawned effects | 7 |
| `Client-Audio` | formal audio clip roots | 1 |
| `Client-Shared` | map and shared client roots | 4 |
| `Client-UI` | pages, indicators and independently resolved sprites | 35 |

Addresses use semantic prefixes such as `view/unit/`, `view/projectile/`,
`view/map/`, `vfx/`, `audio/`, `ui/page/`, `ui/indicator/` and `ui/icon/`.
The complete authoritative inventory is `ADDRESSABLE_ROOTS.csv`.

## Runtime lifecycle

1. Client bootstrap registers one `AddressablesClientContentService` and loads
   the local catalog.
2. Logic worlds synchronously create deterministic objects from direct formal
   prefabs.
3. Unit/projectile binders observe the stable UID and PrefabId, acquire the view
   asynchronously, instantiate it and attach presentation readers.
4. Reconciliation removes a view when its logic object disappears. If rollback
   creates a new object with the same UID, object-identity mismatch forces a
   clean rebind.
5. Every acquired asset is represented by a disposable lease. Cache reference
   counts release Addressables handles exactly when the last lease is disposed.
6. Cancellation and generation checks prevent a completed old sprite request
   from writing back after registry clear or scene teardown.

Gameplay remains valid when a view is loading or fails; failures are logged with
the exact address and any acquired handle is released.

## Dedicated Server exclusion

The server build scope performs four independent safeguards:

- sets `DoNotBuildWithPlayer` for Addressables content;
- rejects all Addressables streaming paths, including stale content previously
  built for a client;
- strips known presentation components and client objects from server scenes;
- audits the completed build and fails on forbidden catalogs, bundles or client
  presentation dependencies.

Player build entry points also establish an explicit platform boundary before
Addressables runs. Windows client content is built only while the active target
is `StandaloneWindows64/Player`; Linux server compilation uses
`StandaloneLinux64/Server`. Before a client build, only the previously generated
`<Client>_Data/StreamingAssets/aa` directory is removed. Afterward,
`settings.json`, the platform directory and at least one bundle must all declare
the expected target, and a stale opposite-platform directory fails the build.
This guard was added after the first acceptance package embedded Linux bundles
inside a Windows Player and rendered all bundle shaders magenta.

The internal streaming-path hook is deliberately pinned to Addressables 1.22.3.
Its focused test fails visibly if a future package version changes that API.

## Adding a new client asset

1. Keep deterministic logic/config under its formal non-client folder.
2. Put the presentation prefab or independently loaded asset under
   `Assets/ClientContent/`.
3. Add only the independently requested root to the matching `Client-*` group
   and assign a stable semantic address.
4. For Unit/Projectile views, add the address to the existing
   `GlobalPrefabTable` entry; never create a second PrefabId registry.
5. Run local configuration tests, representative real-load PlayMode tests and
   regenerate the root/dependency reports.
6. For server-sensitive changes, run the server scope test, clean Dedicated
   Server compilation and final BuildReport audit.

## Current measured evidence and limitations

- 63 roots, zero remote entries, seven bundles plus one catalog.
- Current dependency graph: 135 roots, 363 unique dependencies, 1,413 edges.
- Linux content output: 612,459,164 bytes. Projectile source GLBs dominate the
  size; optimizing them is an import/content task, not an address duplication fix.
- `UIManager` currently loads seven page prefabs during initialization even when
  a page is configured not to pre-instantiate. This is client memory work left
  for a focused lazy-page change.
- Presentation assets and the new loader assembly are excluded from server;
  older presentation classes still reside in shared managed assemblies. Moving
  all of that code behind client-only asmdefs is a separate large refactor.

See `BASELINE_DEPENDENCIES.*`, `CURRENT_DEPENDENCIES.*` and
`ADDRESSABLE_ROOTS.*` in this directory for reproducible reference evidence.
