# ExecPlan 0157 — UOS Launcher Full Install and Incremental Update

Plan ID: 0157
Status: Completed
Created: 2026-08-31
Completed: 2026-08-31
Risk: High
Design conformance: Strict
Estimated code delta: 1 signed CDN manifest protocol, 1 deterministic package
builder, 1 resumable downloader, 1 staged installer, launcher UI/state updates,
self-tests, publish configuration and 4 operational-document updates
Actual code delta: signed manifest/config contracts; package/audit/key/bootstrap
commands; resumable HTTP downloader; full/incremental staged installer; embedded
trust root; strict recovery/offline validation; WinForms update flow; loopback
integration self-tests; three build scripts and four operational/status updates
Affected assemblies: Standalone .NET 8 WinForms launcher only; no Unity asmdef
or Gameplay assembly changes
Design sources: `Docs/Implementation/GAME_LAUNCHER_GUIDE.md` §§作用范围、玩家包目录、
启动流程、构建和自测; `Docs/Implementation/BUILD_GUIDE.md` §§3.5–3.7;
`Docs/Architecture/DECISION_LOG.md` D-051
Decision dependencies: D-051 local Addressables remains unchanged; this plan
downloads and installs the complete built Windows client outside the Unity
runtime and does not enable remote catalogs or runtime Addressables updates.
Validation basis: Release build, launcher self-test, generated-package audit,
local HTTP full-install and incremental-update integration tests, interrupted
install recovery, signature/hash rejection, Unity MCP compilation/Console
isolation, diff review and one independent read-only security/recovery review.

## 1. Purpose

Allow the player-facing `FrameSyncMobaLauncher.exe` to start from a Demo package
whose `Game` directory is empty, retrieve a signed Windows-client release from
UOS CDN, install it into the fixed sibling `Game` directory, update an existing
installation by downloading only changed client files, and start the validated
`AAALOL.exe`. Developers receive a repeatable package builder plus exact UOS CLI
and Console upload instructions; no UOS credential or secret enters the repo or
player package.

## 2. Progress

- [x] Record current launcher, build-output and Unity Console baselines.
- [x] Freeze scope against D-051: file-level Player distribution only; no remote
      Addressables migration.
- [x] Implement signed manifest/package generation and local signing-key flow.
- [x] Implement CDN configuration, manifest verification and resumable download.
- [x] Implement first-install ZIP, file-level incremental staging and atomic
      rollback/recovery.
- [x] Integrate progress/update/start behavior into the existing four-art UI.
- [x] Expand self-tests and run local HTTP full-install/incremental/failure tests.
- [x] Build/publish, generate the current `Builds/Demo/Game` CDN upload set and
      audit every manifest/object/package hash.
- [x] Force Unity refresh, inspect isolated Console output and run the required
      independent read-only review.
- [x] Update current operational/status documents and close the plan.

## 3. Repository facts and discoveries

- The current launcher is a standalone .NET 8 WinForms single-file executable
  under `Tools/UosGameLauncher`; it is outside every Unity asmdef.
- The formal install root is the fixed sibling `Builds/Demo/Game`, with entry
  point `AAALOL.exe` and required `AAALOL_Data`.
- The unfiltered current `Game` tree contains 413 files and approximately 750 MB. Thirteen
  local Addressables bundles account for approximately 585 MB; the largest
  current bundle is `Client-Hero-1001` at approximately 419 MB, so file-level
  incrementality cannot reduce changes inside that one bundle.
- D-051 explicitly keeps catalogs and bundles local and disables CDN/runtime
  Addressables update behavior. The user requested launcher-managed complete
  client distribution, so this plan leaves those Unity settings untouched.
- UOS CLI sync avoids re-uploading unchanged publisher files, but a single ZIP
  still forces players to re-download the whole ZIP. The release layout will
  therefore carry both a full-install ZIP and content-addressed file objects.
- The pre-change Unity Editor is idle and not compiling. Its retained Console
  history contains older unrelated errors; final evidence must use a fresh time
  window instead of erasing that history.
- After excluding PDB and Burst `DoNotShip` artifacts, the generated 1.0.0 target
  has 277 logical files and 272 unique SHA-256 objects. Installed size is
  702.23 MiB, the complete ZIP is 548.74 MiB and the upload directory is
  1250.93 MiB before UOS-side storage deduplication.
- The self-contained Launcher executable is approximately 155 MiB; its audited
  Launcher-only bootstrap ZIP with an empty `Demo/Game` is approximately 75 MiB.

## 4. Design sources and traceability

| Requirement | Source | Protection |
| --- | --- | --- |
| Fixed `Launcher` + sibling `Game` layout | `GAME_LAUNCHER_GUIDE.md` 玩家包目录 | locator and install integration tests |
| Entry point remains `AAALOL.exe` | `GAME_LAUNCHER_GUIDE.md` 启动流程 | package/installed-layout validation |
| UOS Release/Badge distribution | `BUILD_GUIDE.md` §3.5 | generated upload layout and guide |
| Remote Addressables remains disabled | D-051; `BUILD_GUIDE.md` §3.6 | scoped diff and Unity configuration audit |
| No embedded UOS secret | project constitution and UOS CLI auth model | config/schema test and diff scan |
| Failed updates preserve a launchable old client | approved user request and launcher recovery design | interrupted swap/hash/signature tests |

## 5. Scope

### In scope

- Detached RSA-SHA256 signature over exact UTF-8 manifest bytes.
- Content-addressed objects keyed by SHA-256 plus a versioned full ZIP.
- A launcher-adjacent public CDN configuration containing only Bucket ID,
  Badge and manifest path.
- Empty-`Game` first installation, installed-version comparison, changed-file
  download, resumable temporary files, progress/cancellation and bounded retry.
- Same-volume staged assembly, exact target-manifest validation, atomic directory
  swap, backup restoration and interrupted-install recovery.
- Self-contained launcher publishing so a clean player machine does not require
  a separately installed .NET Desktop Runtime.
- Developer commands/scripts and Chinese UOS upload/runbook documentation.

### Out of scope

- Remote Addressables profiles/catalogs, content-update builds or runtime cache
  changes; D-051 remains authoritative.
- Binary-delta algorithms inside one changed file or AssetBundle.
- Launcher self-update, authentication, payment, announcements or installer UI.
- UOS Console access, credential storage or automated Badge promotion.
- Unity Gameplay, Snapshot, checksum, command, networking or scene changes.

No Unity serialization, Snapshot schema, checksum, deterministic lifecycle or
network public contract changes.

## 6. Implementation plan

1. Add immutable manifest/config DTOs, strict relative-path validation and
   detached signature verification shared by package and install flows.
2. Add a developer-only package command that hashes `Builds/Demo/Game`, writes
   content-addressed objects, builds the full ZIP, signs the manifest and audits
   the output before success.
3. Add a local RSA key generation command. Keep the private PEM only under the
   ignored `Builds` tree; copy the public PEM into launcher publish assets.
4. Add HTTP manifest/object/package download with redirect support, partial-file
   resume when the server returns 206, retry and SHA-256 verification.
5. Add full-install and incremental staged installation, local installed
   manifest ownership, safe path/deletion checks and rollback recovery.
6. Integrate asynchronous update states and progress into the existing UI,
   preserving login and process argument contracts.
7. Extend `--self-test` and add a local loopback HTTP integration mode covering
   empty install, one-file incremental update, corruption/signature rejection
   and interrupted swap recovery.
8. Publish the self-contained launcher, generate the current upload tree and
   update UOS CLI/Console instructions and current-state evidence.

## 7. Public contracts and ownership

- `client-manifest.json` is the authoritative immutable target file set for one
  Windows client release. It owns schema version, client version, minimum
  launcher version, entry point, full ZIP descriptor and every relative file
  path/size/SHA/object path.
- `client-manifest.sig` is a detached RSA-SHA256 signature over the exact
  manifest bytes. The executable embeds the trusted public key; the adjacent
  public PEM is release evidence only, and the ignored local package workflow
  owns the private key.
- `launcher.cdn.json` is public, non-secret distribution routing only: enabled
  flag, UOS Bucket ID, Badge and manifest path.
- `.launcher-installed-manifest.json` under `Game` records the signed manifest
  installed by the launcher. It is not Gameplay state and is excluded from the
  target-file member list.
- The existing `-onlineFlow` and `--TestAccountId` launch boundary is unchanged.

## 8. Validation

- `dotnet build -c Release` and self-contained `dotnet publish` complete with
  zero warnings and errors.
- `--self-test` covers strict paths, signature verification, hashes, settings,
  arguments, install-layout checks and process lifecycle.
- Local HTTP integration proves empty first install, exact-manifest/full-hash
  no-op, same-version/same-size replacement, local same-size corruption repair,
  changed-file-only update, deleted-file removal, corrupt-object full-package
  fallback, invalid signature rejection and recovery after an interrupted swap.
- Package audit re-hashes the full ZIP and all content-addressed objects and
  verifies the signed manifest before reporting success.
- The generated package installs to a temporary Demo root and validates
  `AAALOL.exe` plus `AAALOL_Data` without starting the real game.
- Unity MCP forced refresh completes with no new compile errors; a fresh Console
  time window contains no new launcher-related Unity errors.
- Scoped diff review confirms no Addressables, Gameplay, Snapshot, checksum,
  network or developer-launcher changes.
- One independent read-only review covers manifest trust, path traversal,
  partial download, staging/swap recovery and secret handling.

## 9. Independent review

The first independent read-only review found no P0 and three P1 findings:
same-version/same-length content could bypass update, recovery could delete a
backup after only length checks, and offline fallback accepted unsigned partial
layouts. All were fixed with exact-manifest/full-hash no-op, full validation of
both recovery candidates and signed full-hash offline admission. The same
review also drove explicit download exceptions/full-ZIP fallback, conservative
external-process handling, embedded trust root and bootstrap allowlisting.

A focused re-review found one further P1 at the post-commit backup cleanup
boundary. Swap now has an explicit commit point, rechecks the game process, and
never deletes the committed `Game` when deferred backup cleanup fails. A fault-
injection self-test proves the committed client remains trusted and later
recovery can clean the backup. Final independent re-review reports no P0/P1.

## 10. Failure and recovery

- Downloaded bytes remain outside `Game` until hash and signature checks pass.
- An incomplete first install leaves `Game` absent/unchanged and remains
  retryable.
- Incremental updates assemble a complete staging tree. The old `Game` is moved
  to a fixed backup only during the final same-volume swap; failure restores it.
- At launcher startup, `Game` missing plus a valid backup restores the backup.
  Stale staging data is never treated as an installed client.
- The launcher allows an already validated old client to run when update checks
  fail, but never launches a partially installed or manifest-invalid tree.
- Losing the private key prevents publishing updates accepted by old launchers;
  the user must back it up outside the repository. Compromise requires a new
  launcher/public-key release.
- Actual UOS upload, Badge assignment, propagation and external download-speed
  acceptance remain user-owned and are documented after source verification.

## 11. Results

Completed as a standalone launcher/package workflow without changing Unity
Gameplay, networking, scenes, asmdefs or D-051 local Addressables.

- Release `dotnet build` passed with 0 warnings/0 errors; `dotnet format
  --verify-no-changes`, source `--self-test` and published self-contained EXE
  `--self-test` all returned exit code 0.
- The audited 1.0.0 upload set contains 277 logical files, 272 unique objects,
  736,341,742 installed bytes and a 575,400,035-byte full ZIP. The full ZIP
  SHA-256 is `4b517ff2aa2ff3e26c021a2be98638065094d7935d2a24586443026e012ed913`.
  Packaging excludes 136 PDB/Burst debug files (13,479,771 bytes).
- The final 1.1.0 bootstrap ZIP contains seven allowlisted Launcher files plus
  an explicit empty `Demo/Game`; it has no PDB, is 79,271,490 bytes and has
  SHA-256 `e35bce93fff683dd07286aa317f8a62fe7251ddb50b965fb29c9835f4126cb21`.
- Unity MCP `ForceUpdate` refresh succeeded; the final one-minute Console Error
  query was empty. Unity EditMode/PlayMode were not rerun because this plan did
  not change Unity source/assets or runtime contracts.
- The private key remains under ignored `Builds/CdnSigning`; the package and
  bootstrap builders never include it.
- Actual UOS upload, Bucket/Badge configuration, CDN propagation, bandwidth and
  a clean player-machine first-install/incremental acceptance remain external
  user-operated steps described by `GAME_LAUNCHER_GUIDE.md`.
