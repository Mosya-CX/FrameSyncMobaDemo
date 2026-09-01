# ExecPlan 0156 — Formal Launcher Art and Scope Finalization

Plan ID: 0156
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: Low
Design conformance: Strict
Estimated code delta: 1 launcher service cleanup, 1 launcher self-test assertion,
4 artwork inputs, 4 operational-document updates
Actual code delta: fixed-path cleanup, self-test artwork/persistence assertions,
four supplied artwork files plus generated multi-size ICO, and operational-doc
scope corrections
Affected assemblies: Standalone .NET 8 WinForms launcher only
Design sources: `Docs/Implementation/GAME_LAUNCHER_GUIDE.md`,
`Docs/Implementation/BUILD_GUIDE.md`, `Docs/Implementation/CURRENT_HANDOFF.md`,
`Docs/Implementation/MODULE_STATUS.md`
Decision dependencies: None
Validation basis: Release build, published `--self-test`, package asset manifest,
fixed Game-path checks and scoped diff check

## 1. Purpose

Finalize the player-facing Demo launcher as a minimal whiteboard entry: supplied
`Background.png`, `Banner.png`, `Logo.png` and a generated multi-size
`AppIcon.ico`, one login-name field, and start/stop controls. The launcher must
use the fixed `Builds/Demo/Game/AAALOL.exe` sibling layout and must not expose
settings, directory selection, announcements, CDN download or update controls.

## 2. Progress

- [x] Remove directory-picker wording and unused folder-selection helper.
- [x] Keep the fixed game path out of persisted player settings.
- [x] Copy the four supplied images into the launcher source asset directory.
- [x] Convert `AppIcon.png` to a six-size `AppIcon.ico` and exclude the source
      PNG from the published package.
- [x] Reduce the player-facing guide and build guide to the agreed scope.
- [x] Rebuild, publish, run self-test and inspect the final package.
- [x] Record results and close the plan.

## 3. Repository facts and discoveries

- `Builds/Demo/Game/AAALOL.exe` and `AAALOL_Data` are present in the copied
  client directory.
- The existing developer launcher under `Tools/UosClientLauncher` remains
  outside this slice and must not be changed.
- `LauncherArtwork` already uses safe cloned image handles and can fall back to
  painted UI when a file is absent or invalid.
- The supplied AppIcon is a PNG; Windows launchers need an ICO container, so a
  multi-frame ICO is generated once as a source artifact.

## 4. Scope and ownership

In scope: fixed sibling-path resolution, login-name persistence, launch/stop
lifecycle, four local artwork files and the corresponding operational docs.

Out of scope: Unity assemblies, Addressables, UOS protocol, CDN/update flow,
settings UI, directory picker, announcements, authentication, or the developer
launcher.

The launcher owns only its private `LoginName` convenience value. The Unity
client remains authoritative for login, matching, logging and gameplay.

## 5. Validation

- Release `dotnet build` completes with zero warnings and errors.
- Published `FrameSyncMobaLauncher.exe --self-test` exits zero and asserts that
  `GameExecutablePath` is not serialized.
- The published asset directory contains exactly `Background.png`, `Banner.png`,
  `Logo.png` and `AppIcon.ico` among launcher artwork files; `AppIcon.png` is
  source-only.
- The real `Builds/Demo/Game/AAALOL.exe` and `AAALOL_Data` are checked without
  launching the game.
- `Tools/UosClientLauncher` remains unchanged.
- `git diff --check` passes for the scoped files.

## 6. Results

The formal launcher now has the agreed minimal surface: login name, start/stop,
fixed sibling-path validation and the four supplied visual resources. The unused
folder-selection helper and all player-facing directory-selection wording were
removed. `LauncherSettings` serializes only `LoginName`; the fixed
`GameExecutablePath` is derived at runtime and is explicitly covered by the
self-test.

Release `dotnet build` completed with 0 warnings and 0 errors. The published
`Builds/Demo/Launcher/FrameSyncMobaLauncher.exe --self-test` exited 0 and loaded
all four artwork handles, checked complete/missing `AAALOL` layouts, asserted the
fixed path is not persisted, and exercised the short child-process lifecycle.
The final package contains `FrameSyncMobaLauncher.exe`, its PDB and exactly four
launcher artwork files: `Background.png`, `Banner.png`, `Logo.png` and
`AppIcon.ico`; the AppIcon ICO header contains 256/128/64/48/32/16 frames and
the source `AppIcon.png` is excluded from publish. The real
`Builds/Demo/Game/AAALOL.exe` (666624 bytes) and `AAALOL_Data` directory were
verified without launching the game. `Tools/UosClientLauncher` remains
unchanged, and the scoped `git diff --check` is clean.
