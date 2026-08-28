# ExecPlan 0146 — Retain the indicator Shader in the client Player core

Plan ID: 0146
Status: Completed
Created: 2026-08-27
Completed: 2026-08-28
Risk: Medium
Design conformance: Strict
Estimated code delta: 70–140 C# lines plus focused tests/document updates
Actual code delta: focused build-scope Shader retention/restoration, material-keyword cleanup and EditMode assertions in existing files
Affected assemblies: FrameSyncMoba.Bootstrap.Editor; FrameSyncMoba.Bootstrap.EditModeTests
Design sources: Docs/Design/moba_presentation_layer_integrated_design_v13_2_fifth_round_audio_entry.md
Decision dependencies: D-048; D-051
Validation basis: 20:42 rebuilt-client logs; 20:30 Windows Player/Addressables build artifacts; Unity compilation/Console; focused EditMode/PlayMode tests

## 1. Purpose

Prevent the Windows Player from rendering generic skill indicators with the
magenta error shader. The indicator Material assets remain common client
presentation content in `Client-Shared`, while their one-variant project Shader
is explicitly retained by the client Player build. Dedicated Server builds must
exclude that Shader and restore project settings after every build attempt.

## 2. Progress

- [x] Confirm the 20:42 Players load and show/hide all generic indicator Prefabs without the previous lookup/disable error.
- [x] Confirm the 20:30 Windows Bundle contains the expected materials, textures and supported one-pass Shader compiled for D3D11.
- [x] Confirm `GraphicsSettings.m_AlwaysIncludedShaders` does not contain the indicator Shader.
- [x] Add build-scoped client Shader retention and server exclusion with exact restoration.
- [x] Add focused tests for client/server scope behavior and project-state restoration.
- [x] Compile, inspect Console, run focused EditMode/PlayMode tests and update current evidence without building.

## 3. Repository facts and discoveries

- `Client-Shared` is already the common client presentation group. The three
  Material assets are transitive dependencies of its indicator Prefabs; moving
  them to `Logic-Core` would not change Shader availability and would violate
  the logic-only Dedicated Server boundary.
- The latest Player logs contain normal indicator `Show`/`Hide` output and no
  material validation failure. The error is therefore at Player draw time.
- The built `client-shared` Bundle resolves every indicator Material to
  `FrameSyncMoba/SkillIndicatorUnlit`, with the expected texture, blue tint,
  `isSupported == true` and one pass. `Editor.log` records one retained D3D11
  vertex and fragment program during the Addressables build.
- Unity 2022.3 documents `Always Included Shaders` or a Shader Variant
  Collection as the explicit Player-side retention mechanism for shaders used
  through AssetBundles/Addressables. This project Shader has no keyword variant
  expansion, so client-only Always Included retention is bounded.

## 4. Design sources and traceability

- Presentation v13.2 and D-048: indicators and their graphics dependencies are
  client-only presentation and never affect Gameplay.
- D-051: `Logic-*` remains the Dedicated Server content closure; client bundles
  and presentation shaders are excluded from server output.
- `DedicatedServerAddressablesBuildScopeTests`: proves client retention, server
  exclusion and exact GraphicsSettings restoration.
- `GameBootstrapPlayModeTests.GenericSkillIndicators_BindDedicatedRuntimeMaterials`:
  retains material binding, source-Shader identity and blue/non-magenta
  framebuffer behavior.

## 5. Scope

In scope: client/server build scope, GraphicsSettings Shader retention,
restoration, diagnostics and focused tests. Out of scope: changing indicator
geometry/tint, moving presentation assets into `Logic-Core`, Gameplay, network,
Snapshot/checksum, packages and issuing Player builds.

No public runtime contract, serialization, Snapshot or checksum changes.

## 6. Implementation plan

1. Extend `AddressablesPlayerBuildScope` to capture the GraphicsSettings
   Always Included Shader list, include the required Shader for client builds,
   exclude it for server builds and restore the exact original array on dispose
   or constructor failure.
2. Add observable editor helpers/tests covering both scope directions and
   restoration without running a Player build.
3. Compile through Unity MCP, run focused Bootstrap EditMode and indicator
   PlayMode tests, then update current evidence.

## 7. Public contracts and ownership

No public runtime contract changes. `AddressablesPlayerBuildScope` remains the
editor-only owner of temporary client/server build configuration. The source
Shader remains under `Assets/ClientContent` and Material/Prefab leases remain
owned by `ClientContentRuntimeHost`.

## 8. Validation

- Unity refresh/compilation and isolated Console Error/Exception inspection.
- Focused build-scope EditMode tests for client include, server exclude and
  exact restoration.
- Bootstrap EditMode retained suite.
- Real Addressables generic-indicator PlayMode framebuffer test.
- No Player build command; rebuilt-client visual acceptance remains user-owned.

## 9. Independent review

Not required: Medium-risk editor build configuration with no runtime public
contract or deterministic Gameplay change.

## 10. Failure and recovery

The build scope restores GraphicsSettings in `Dispose` and constructor failure
paths. The code change can be reverted without touching Material/Prefab assets.
No build command will be issued.

## 11. Results

Completed without issuing a build command. The project GraphicsSettings now
retains `FrameSyncMoba/SkillIndicatorUnlit` for ordinary/client builds.
`AddressablesPlayerBuildScope` captures the exact Always Included Shader array,
enforces the required Shader for clients, removes it for Dedicated Server
builds, and restores the original array on disposal or constructor failure.
The presentation migration also clears every indicator Material keyword; the
source assets now have the expected Shader, texture and an empty keyword set.

Unity refresh/compilation succeeded and final Console Error/Exception is empty.
Bootstrap EditMode passed 120/120, covering client retention, server exclusion,
exact GraphicsSettings restoration and clean material keywords. The real
Addressables generic-indicator PlayMode test passed 1/1 with four Renderers,
exact source-Shader inheritance, visible blue pixels, zero magenta pixels and
runtime cleanup. Final rebuilt Windows Client visual acceptance remains
user-owned.
