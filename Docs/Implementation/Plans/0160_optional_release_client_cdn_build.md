# ExecPlan 0160 — Optional Release Client + CDN Build

Plan ID: 0160
Status: Completed
Created: 2026-09-01
Completed: 2026-09-01
Risk: Medium
Design conformance: Strict
Estimated code delta: one Unity Editor window, focused build-menu helpers/tests,
and packaging-guide updates
Actual code delta: one new EditorWindow, focused additions to the existing build
menu, and five focused EditMode tests
Affected assemblies: Unity Assembly-CSharp-Editor and its EditMode tests;
standalone Launcher packager invoked as an external existing tool
Design sources: `Docs/Implementation/BUILD_GUIDE.md` sections 1-3.7 and
`Docs/Implementation/GAME_LAUNCHER_GUIDE.md` packaging contract
Decision dependencies: D-051 local Addressables build/audit remains unchanged
Validation basis: Unity compilation/Console, focused EditMode tests, standalone
packager self-test and read-only path/argument inspection; no Player build

## 1. Purpose

Add an explicit Unity menu window for a formal Windows release client. It builds
`Builds/Demo/Game/AAALOL.exe` and its Unity-generated `AAALOL_*` companions,
optionally invoking the existing signed schema-v3 CDN packager afterward. The
existing `Builds/UosClient/FrameSyncMobaClient.exe` test build remains independent.

## 2. Progress

- [x] Confirm current UOS menus build only the test client and server archive.
- [x] Confirm the existing CDN packager consumes `Builds/Demo/Game` but has no
  Unity menu integration.
- [x] Add the release build window, optional checkbox and validated version field.
- [x] Add distinct build root/name/key and safe external packager invocation.
- [x] Add focused EditMode tests for menu, paths, validation and isolation.
- [x] Compile through Unity, run focused tests and update operational evidence.

## 3. Repository facts and discoveries

- `BuildClientUosCore` currently writes `Builds/UosClient/FrameSyncMobaClient.exe`.
- `BuildCdnPackage.cmd` defaults to `Builds/Demo/Game` and requires `AAALOL.exe`.
- The release package base name controls Unity companions such as `AAALOL_Data`.
- Actual Player build execution remains user-controlled by the build discipline.

## 4. Design sources and traceability

| Requirement | Source | Protection |
| --- | --- | --- |
| Optional, not mandatory, CDN packaging | current user request | window checkbox/default and EditMode tests |
| Release executable uses `AAALOL` basename | current user request | output-path tests |
| UosClient test output never conflicts | current user request | distinct constants/build keys and tests |
| Existing signed 95 MB schema-v3 packager | GAME_LAUNCHER_GUIDE | external command composition/self-test |
| Existing client Addressables audit | D-051/BUILD_GUIDE | reuse `Build(..., uosOnline: true)` |

## 5. Scope

### In scope

- One Unity EditorWindow opened from a new menu item.
- Numeric client version input and an optional post-build CDN-package toggle.
- Formal output `Builds/Demo/Game/AAALOL.exe` with the UOS Online define.
- Safe synchronous invocation of the existing .NET packager only after a
  successful Player build when the option is selected.
- Focused reflection/helper tests and guide updates.

### Out of scope

- Running the real Player build during implementation.
- Changing `BuildClientUos`, `BuildUosClientAndServerOnce`, UOS server packaging,
  schema v3, Launcher runtime behavior or Release folder contents.
- Automatically copying unaccepted artifacts into Git-tracked `Release`.

## 6. Implementation plan

1. Add stable release-root/name constants and a release build method reusing the
   existing Windows UOS client pipeline.
2. Add an EditorWindow with version field, optional CDN toggle and one build
   button; dispatch through a unique replay-guard key.
3. Validate version and project/tool/source paths, execute `dotnet run` without
   shell interpolation, capture output and fail visibly on nonzero exit.
4. Cover menu discovery, default option, output isolation and packager argument
   composition in EditMode; update build instructions.

## 7. Public contracts and ownership

- `LocalNgoBuildMenu.ReleaseClientBuildRoot` owns the formal staging root.
- `LocalNgoBuildMenu.ReleaseClientExecutableName` owns the `AAALOL` basename.
- Existing `UosClientBuildRoot` remains the test-client owner and is unchanged.
- CDN manifest/schema/signing contracts remain owned by `Tools/UosGameLauncher`.

## 8. Validation

- Unity ForceUpdate compilation and Console Error inspection.
- Focused `UosBuildMenuTests` EditMode suite.
- Standalone Launcher/packager Release build and self-test.
- Static proof that the window has one build action, optional toggle and no path
  overlap with `Builds/UosClient`.
- No PlayMode test: no runtime scene, asset or lifecycle behavior changes.

## 9. Independent review

Not required for this Medium-risk Editor-only orchestration change.

## 10. Failure and recovery

- A failed Player build prevents packaging; a failed packager is surfaced as a
  failed menu operation and leaves `Builds/UosClient` untouched.
- The optional toggle can be disabled to build only the formal Player.
- Generated output remains under ignored `Builds`; the user alone promotes an
  accepted archive into `Release`.

## 11. Results

- Added a modal release-client build window whose packaging toggle is false by
  default and whose only action builds the formal Player.
- The release path is anchored to the project root and fixed at
  `Builds/Demo/Game/AAALOL.exe`; the test path remains
  `Builds/UosClient/FrameSyncMobaClient.exe`. The release build clears only its
  exact fixed `Game` root first so stale files cannot enter a new manifest.
- Optional post-build packaging invokes the existing .NET schema-v3 packager
  without shell interpolation and surfaces stdout/stderr on failure.
- Unity ForceUpdate compilation completed with no Console errors. Focused
  `UosBuildMenuTests` passed 6/6. The standalone packager Release build completed
  with 0 warnings/0 errors and `--self-test` exited successfully.
- No real Player build or CDN upload was executed; output-name and end-to-end
  package acceptance remain part of the user's next release build.
