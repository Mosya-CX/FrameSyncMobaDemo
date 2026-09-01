# ExecPlan 0159 — UOS CDN Flat Content Layout

Plan ID: 0159
Status: Completed
Created: 2026-09-01
Completed: 2026-09-01
Risk: Medium
Design conformance: Strict
Estimated code delta: schema-v3 manifest cleanup, one content-addressed upload
directory, focused launcher tests, regenerated CDN/bootstrap artifacts and docs
Actual code delta: four launcher production/project files, one self-test file,
three current operational/status documents, regenerated signed CDN and bootstrap
artifacts
Affected assemblies: standalone .NET 8 WinForms launcher only; no Unity asmdef
or Gameplay assembly changes
Design sources: `Docs/Implementation/GAME_LAUNCHER_GUIDE.md` sections 生成 CDN
客户端包、上传到 UOS CDN、首装/增量和恢复行为
Decision dependencies: D-051 local Addressables remains unchanged
Validation basis: Release build/format, source and published self-tests, package
audit, bootstrap audit, Unity Console isolation and live URL acceptance by user

## 1. Purpose

Make the browser-uploaded CDN tree unambiguous. The only remote payload folder
is `content/<sha256>`; `packages`, `objects` and `chunks` no longer appear as
separate paths, and the manifest does not describe a nonexistent physical ZIP.
The player retains signed chunked first install and file-level incremental update.

## 2. Progress

- [x] Prove the existing schema-v2 local output is complete and explain the
  logical `packages/...` path.
- [x] Record the replacement schema and directory contract.
- [x] Implement schema v3 and one content-addressed payload directory.
- [x] Update self-tests and audit rejection for missing/unreferenced content.
- [x] Regenerate the 1.0.0 Upload tree and Launcher/bootstrap 1.3.0 artifacts.
- [x] Run release build, formatting, self-tests, package/bootstrap audit and
  Unity Console verification.
- [x] Update operational/status/handoff documents and complete the plan.

## 3. Repository facts and discoveries

- The existing schema-v2 package is locally valid: 286 expected physical files,
  zero missing/unreferenced files, seven chunks reconstruct the 575,400,035-byte
  ZIP, and the largest Entry is 95,000,000 bytes.
- `fullPackage.path = packages/AAALOL-1.0.0-full.zip` is a logical aggregate
  name, not a required physical Entry when `chunks` contains multiple items.
  That distinction is correct but confusing in the UOS browser workflow.
- The user cleared the Bucket before a successful production install, so no
  installed schema-v2 population requires compatibility.

## 4. Design sources and traceability

| Requirement | Source | Protection |
| --- | --- | --- |
| One obvious upload payload directory | current user request | package layout self-test and audit |
| No phantom physical package path | current user request | schema-v3 serialization assertions |
| Files remain below 100 MB | ExecPlan 0158 contract | maximum-size audit |
| Signed exact client and safe install | launcher guide | loopback full/incremental/corruption tests |

## 5. Scope

### In scope

- Schema v3 owned by the standalone Launcher.
- `fullPackage.fileName` as a local reconstructed/cache display name rather than
  a CDN Entry path.
- Every physical payload Entry at `content/<chunkSha256>`, including single-
  chunk objects and multi-chunk aggregates.
- Removal of generated `packages`, `objects` and `chunks` upload directories.
- Regenerated signed Upload tree and Launcher/bootstrap artifacts.

### Out of scope

- Unity Player rebuild, Addressables layout, Gameplay, networking, Snapshot or
  checksum changes.
- UOS credentials, console uploads, Release creation or Badge promotion.
- Binary delta algorithms or compression-format changes.

## 6. Implementation plan

1. Replace the schema-v2 aggregate path/object-path contract with schema-v3
   package filename plus ordered `content/<sha256>` chunk descriptors.
2. Make the builder deduplicate all physical payload bytes in `content`, and
   make audit reject missing, oversized or unreferenced files.
3. Update loopback tests to prove the flat layout, first install, increment,
   resume, corruption failure and recovery.
4. Republish Launcher 1.3.0, regenerate the 1.0.0 upload set and bootstrap, then
   update the exact browser-console instructions.

## 7. Public contracts and ownership

- `client-manifest.json` advances from schema v2 to v3 and remains exclusively
  owned by `Tools/UosGameLauncher`.
- `CdnPackageEntry.Path` becomes `FileName`; `CdnFileEntry.ObjectPath` is
  removed. Ordered `CdnChunkEntry` lists are the sole remote-byte authority.
- Every `CdnChunkEntry.Path` must equal `content/<sha256>`.
- Launcher minimum/current version advances to 1.3.0. No Unity runtime public
  contract changes.

## 8. Validation

- `dotnet build -c Release` and `dotnet format --verify-no-changes`.
- Source and published `--self-test` pass.
- Actual 1.0.0 package audit reconstructs and verifies the complete ZIP and
  every logical file; only two root metadata files plus `content` exist.
- Bootstrap allowlist/empty-Game audit passes and embedded configuration remains
  `Upload/client-manifest.json`.
- Unity Console Error baseline remains empty before and after the standalone
  change.

## 9. Independent review

Not required for this Medium-risk standalone manifest migration. If scope grows
into Unity application flow or another public protocol, reclassify before work.

## 10. Failure and recovery

- The old local schema-v2 output can be regenerated from the current Git state
  until this plan completes; the Bucket was already cleared by the user.
- Package generation remains marker-guarded and uses temporary aggregate files.
- Missing/corrupt content fails before staging promotion; existing trusted local
  installs remain protected by signature and complete hashes.

## 11. Results

Completed. Launcher 1.3.0 accepts only schema v3. The signed 1.0.0 Upload tree
contains `client-manifest.json`, `client-manifest.sig` and 284 unique
`content/<sha256>` files (286 physical files total); it has no legacy directory
or manifest reference and its maximum Entry is 95,000,000 bytes. The complete
575,400,035-byte ZIP is represented by `fileName` plus seven ordered content
descriptors and is reconstructed locally. Release build has zero warnings/errors;
format verification, source/published loopback self-tests, actual package audit,
bootstrap allowlist/empty-Game inspection, Unity ForceUpdate and final Console
Error inspection all pass. The new bootstrap is
`Builds/Bootstrap/FrameSyncMobaDemo-Bootstrap-1.3.0.zip`, SHA-256
`64e20fdd7fbe178ff7b0cf548f580d35cd715f4bdaacfdc356b8bd040b41385f`.
Actual Bucket-root upload, Release creation, Badge promotion and player-machine
acceptance remain user-owned.
