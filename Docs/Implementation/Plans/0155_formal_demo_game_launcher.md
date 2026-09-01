# ExecPlan 0155 — Formal Demo Game Launcher Whiteboard

Plan ID: 0155
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: Medium
Design conformance: Strict
Estimated code delta: 5–7 standalone .NET files and 3 operational documents
Actual code delta: 4 launcher C# files, 1 project file, 1 manifest, 1 publish
script, 4 supplied artwork files plus generated multi-size ICO, 1 new guide,
a README link and current plan/status-guide updates
Affected assemblies: Standalone .NET 8 WinForms launcher; no Unity asmdef or
Gameplay assembly changes
Design sources: `Docs/Implementation/BUILD_GUIDE.md`,
`Docs/Implementation/UOS_CLIENT_LAUNCHER_GUIDE.md`,
`Docs/Implementation/C_S_TEST_GUIDE.md`
Decision dependencies: None. This slice does not change Snapshot, checksum,
network, Addressables, or Unity runtime contracts.
Validation basis: `dotnet build`, published executable `--self-test`, package
path/data validation, process lifecycle smoke test, diff review.

## 1. Purpose

Deliver a player-facing whiteboard launcher separate from the existing
developer multi-client tool. The published launcher lives in
`Builds/Demo/Launcher`; it locates `AAALOL.exe` under `Builds/Demo/Game`, checks
that the Unity Player installation is complete, starts the online flow with a
single login name, and exposes a polished placeholder UI ready for supplied art.

## 2. Progress

- [x] Register the active plan and record repository facts.
- [x] Create `Tools/UosGameLauncher` without modifying
      `Tools/UosClientLauncher`.
- [x] Implement single-client launch, install validation, settings and
      process shutdown.
- [x] Implement whiteboard UI and optional art asset hooks.
- [x] Narrow the player-facing surface to login name only; do not create a
      launcher-owned log system or expose developer parameters.
- [x] Update build/operational documentation and art handoff list.
- [x] Build, publish and run the standalone self-test.
- [x] Complete the first independent read-only review; no P0/P1 was found, so
      a second review was not required.
- [x] Record results and close the plan.

## 3. Repository facts and discoveries

- The existing `Tools/UosClientLauncher` is a developer-oriented two-profile
  WinForms tool. Its output and documentation must remain unchanged as the
  developer path.
- The copied demo Player is `Builds/Demo/Game/AAALOL.exe` with
  `AAALOL_Data`; this is the formal launch contract for the new whiteboard.
- `ClientBootstrap` already accepts `-onlineFlow` and `--TestAccountId`, so the
  launcher does not need a Unity-side protocol change or a launcher log path.
- `Builds/` is ignored by Git; the launcher source and build command are the
  reviewable artifacts, while the published binary is a local build output.
- No launcher artwork is currently supplied. The UI therefore needs a
  deterministic painted placeholder and optional file-based replacement hooks.

## 4. Design sources and traceability

| Requirement | Source / implementation proof |
| --- | --- |
| Developer tool remains separate | `UOS_CLIENT_LAUNCHER_GUIDE.md`; no edits to `Tools/UosClientLauncher` |
| Online client flow and runtime flags | `BUILD_GUIDE.md` §3.5–3.6 and `ClientBootstrap`; `GameLaunchArgumentBuilder` |
| Complete Unity Player directory | `BUILD_GUIDE.md` §3.5; `GameInstallLocator` checks EXE and `<Exe>_Data` |
| One client per player-facing launcher | New `GameProcessManager`; UI has one Start/Stop lifecycle |
| Portable art replacement | `GAME_LAUNCHER_GUIDE.md` art paths and `LauncherArtwork` fallback |

## 5. Scope

### In scope

- A standalone .NET 8 Windows launcher under `Tools/UosGameLauncher`.
- Published output command targeting `Builds/Demo/Launcher`.
- Default sibling lookup of `Builds/Demo/Game/AAALOL.exe`, with a
  file picker for local installations.
- Validation of the Player executable and its `_Data` directory before launch.
- `-onlineFlow` and the login name as `--TestAccountId` only; no launcher-owned
  log file, advanced parameter panel or developer diagnostics.
- Single-instance guard, single-client process lifecycle, an open-folder action
  and clear missing-installation errors.
- Dark whiteboard UI with custom placeholder banner, news card and optional
  Logo/Banner/Background/Icon loading.
- Self-test and operational/documentation updates.

### Out of scope

- CDN downloader, patcher, version manifest, authentication UI or installer.
- Changes to Unity scenes, Addressables, Gameplay, Snapshot, checksum or
  network protocols.
- Replacing or deleting the developer launcher.
- Supplying final art assets; the user will provide those later.

## 6. Implementation plan

1. Add the standalone project, manifest and publish script. **Completed.**
2. Add model/path/settings services and a safe process manager. **Completed.**
3. Add the whiteboard form, optional artwork loader and login-name persistence.
   **Completed.**
4. Add `--self-test`/`--test-client`, then update build and handoff docs.
   **Completed.**
5. Build/publish, run self-test and inspect the generated package layout.
   **Completed.**
6. Request the first independent read-only review, fix any P1/P0 findings,
   and close only after the declared validation is recorded.

## 7. Public contracts and ownership

- `Tools/UosGameLauncher` owns only launcher-local settings and process types;
  no type is shared with deterministic Unity assemblies.
- The launcher-to-client boundary remains the existing Unity command-line
  contract. `-onlineFlow` and `--TestAccountId=<login name>` are the only
  player-facing arguments; client logging remains a client responsibility.
- Art files are optional presentation inputs and never participate in
  Gameplay or network state.

## 8. Validation

- Release `dotnet build` succeeds with nullable warnings treated as errors by
  inspection (no new warnings accepted).
- Published `Builds/Demo/Launcher/FrameSyncMobaLauncher.exe --self-test`
  validates argument construction, settings round trip, path checks and a
  short child-process launch.
- A missing `Builds/Demo/Game` installation produces a clear UI status rather
  than an unhandled exception.
- `git diff --check` passes for the scoped files.
- One independent read-only review is performed for the first launcher change.

## 9. Independent review

The first independent read-only review completed on 2026-08-31. It found no
P0 or P1 issue and confirmed the AAALOL path/data checks, single-instance guard,
working directory, process lifecycle, login-only arguments, absence of a
launcher-owned log system and publish-path consistency. No second review was
required under the current user policy. Non-blocking observations are recorded
as follow-up options: self-contained publishing, excluding the PDB from the
player package, and detecting an already-running client after reopening the
launcher.

## 10. Failure and recovery

- If the client is absent, the launcher remains usable for choosing a folder;
  it does not fabricate a successful launch.
- If settings JSON is invalid, it is backed up and replaced with defaults.
- If the package directory is read-only, the launcher still runs as long as
  the client itself is readable; it does not create a launcher-owned log tree.
- If publishing fails, source remains independently buildable and no Unity
  build command is involved; retry the standalone `dotnet publish` command.

## 11. Results

Delivered `Tools/UosGameLauncher` as a standalone .NET 8 WinForms project with
`BuildLauncher.cmd`. The whiteboard UI provides a single login-name field,
`AAALOL.exe` installation status, folder selection, start/stop lifecycle and
optional Logo/Banner/Background/AppIcon hooks. It defaults to the requested
`Builds/Demo/Launcher` + `Builds/Demo/Game` sibling layout, validates
`AAALOL_Data`, persists only path/login name under the user's local app data,
and passes only `-onlineFlow` and `--TestAccountId=<login name>` to the Player.
The existing developer launcher was not modified.

Release build completed with 0 warnings and 0 errors. The published
`Builds/Demo/Launcher/FrameSyncMobaLauncher.exe --self-test` exited 0, covering
argument construction, settings round trip, missing/complete installation
checks and a short child-process launch. `git diff --check` passed for the
scoped files. The real `Builds/Demo/Game/AAALOL.exe` and `AAALOL_Data` were
verified without launching the game. Final visual acceptance after the user's
art is supplied and any future CDN/update flow remain outside this plan.
