# ExecPlan 0158 — UOS CDN Console-Safe Chunked Upload

Plan ID: 0158
Status: Completed
Created: 2026-09-01
Completed: 2026-09-01
Risk: High
Design conformance: Strict
Estimated code delta: manifest schema v2 chunk descriptors; package splitting;
chunk download/assembly; compatibility validation; self-tests and release docs
Actual code delta:
Affected assemblies: standalone .NET 8 WinForms launcher only; no Unity asmdef
or Gameplay assembly changes
Design sources: `Docs/Implementation/GAME_LAUNCHER_GUIDE.md` sections 生成 CDN
客户端包、上传到 UOS CDN、首装/增量和恢复行为;
`Docs/Architecture/DECISION_LOG.md` D-051
Decision dependencies: D-051 local Addressables remains unchanged. Physical CDN
transport chunks reconstruct exact built Player files before install.
Validation basis: Release build/format, package audit, loopback chunked full and
incremental install, corrupt/missing chunk rejection, published self-test, Unity
MCP isolation and independent read-only security/recovery review.

## 1. Purpose

Replace the two browser-blocking 419.19 MiB and 548.74 MiB UOS Entries with
signed, independently hashed files no larger than 95,000,000 bytes (about
90.60 MiB). The player still sees
one exact client release: the launcher downloads and reassembles chunks, verifies
the complete object/ZIP hash, stages the full target and starts `AAALOL.exe`.

## 2. Progress

- [x] Identify the oversized object and full ZIP and confirm their source paths.
- [x] Record the schema/packager/downloader migration plan.
- [x] Implement schema-v2 chunk descriptors and strict validation.
- [x] Split package/object outputs and teach the launcher to reassemble them.
- [x] Add chunked loopback tests and audit every generated physical Entry.
- [x] Regenerate the 1.0.0 Upload tree and prove its maximum file size.
- [x] Publish/retest the launcher, run Unity isolation and independent review.
- [x] Update operational/status/handoff documents and close the plan.

## 3. Repository facts and discoveries

- Object `adde1c9...b5c2` is the current
  `client-hero-1001_assets_all_...bundle`; it is 439,556,589 bytes
  (419.19 MiB).
- `AAALOL-1.0.0-full.zip` is 575,400,035 bytes (548.74 MiB).
- UOS public CDN documentation describes browser file/folder upload but does not
  publish a dependable web-console per-file limit. The user's console rejects
  these two Entries, so the package must not depend on that undocumented limit.
- Existing schema v1 owns one physical path per object/package. The bootstrap has
  not reached a usable CDN Release, so schema v2 may replace it with Launcher
  version 1.2.0 without migrating an installed production population.

## 4. Design sources and traceability

| Requirement | Source | Protection |
| --- | --- | --- |
| Browser-uploadable physical Entries | current user request | max-file-size package audit |
| Exact signed target remains authoritative | 0157 manifest contract | schema/chunk validation and full hash |
| Empty Game full installation | launcher guide | chunked ZIP loopback test |
| File-level incremental update | launcher guide | chunked object loopback test |
| Addressables remain local Player files | D-051 | scoped diff/Unity isolation |

## 5. Scope

### In scope

- Schema v2 chunk lists for the complete ZIP and every content object.
- Deterministic 95,000,000-byte physical chunks with individual length/SHA-256;
  multi-chunk paths are globally content-addressed as `chunks/<chunkSha256>` so
  unchanged chunks can be reused across files and Releases.
- Resumable per-chunk download, ordered assembly and whole-file hash verification.
- Output audit proving every referenced chunk exists and no upload file exceeds
  the configured limit.
- Rebuilt Upload tree, Launcher/bootstrap and Chinese browser-console instructions.

### Out of scope

- Changing Addressables groups/bundle composition or D-051.
- Binary delta inside an Addressables bundle.
- UOS credentials, live console access, actual Release/Badge promotion.
- Unity Gameplay, Snapshot, checksum, networking, scenes or assets.

## 6. Implementation plan

1. Add `CdnChunkEntry` and schema-v2 validation to package/file descriptors.
2. Split large object and ZIP byte streams into deterministic content-addressed
   chunk paths and
   delete browser-blocking aggregate files from `Upload`.
3. Download each signed chunk with existing Range resume, assemble to a local
   cache temporary file, verify the aggregate hash, then install as before.
4. Extend package audit and self-test with a tiny test chunk size to force
   multi-part full/incremental flows and corruption rejection.
5. Regenerate actual 1.0.0 output, prove maximum physical Entry size, republish
   Launcher/bootstrap and update release instructions/evidence.

## 7. Public contracts and ownership

- `client-manifest.json` schema v2 remains owned by the standalone launcher.
- `CdnChunkEntry` owns one physical path, byte length and SHA-256.
- `CdnPackageEntry.Chunks` and `CdnFileEntry.Chunks` are ordered complete byte
  coverage of their logical aggregate. The aggregate size/hash remains mandatory.
  A single small aggregate retains its legacy logical path; a multi-chunk
  aggregate references globally content-addressed `chunks/<sha256>` Entries.
- Installed files and `.launcher-installed-manifest.json` retain their existing
  logical meaning; no Unity/runtime public contract changes.

## 8. Validation

- Release build/publish: zero warnings/errors; format verification clean.
- Self-test forces multi-chunk ZIP and object download, Range handling, same-
  version replacement, corrupt/missing chunk failure/fallback and swap recovery.
- Package audit re-hashes each chunk and reconstructed aggregate.
- Actual Upload audit reports maximum physical file below decimal 100 MB.
- Published EXE self-test and bootstrap allowlist/empty-Game audit pass.
- Unity ForceUpdate succeeds and isolated Console Error query remains empty.

## 9. Independent review

Independent read-only review found no P0/P1 security issue. It verified schema,
chunk order/size/hash, content-addressed output, Range resume, aggregate hash,
safe staging/swap and the absence of oversized Entries. It identified a P1
reliability risk in disk-space budgeting; the implementation now reserves target
Game plus two download aggregates on the install drive and pre-reserves missing
chunks plus aggregate assembly on the cache drive. Remaining test gaps are live
UOS propagation and web-console boundary behavior, both outside local authority.

## 10. Failure and recovery

- Partial chunks remain resumable cache files and never enter `Game`.
- Aggregate assembly is temporary and promoted only after whole-file SHA-256.
- Missing/corrupt chunks fail safely; incremental object failure may use the
  independently chunked full ZIP. Existing staging/swap recovery remains intact.
- Actual UOS browser upload and external machine acceptance remain user-owned.

## 11. Results

Completed. The 1.0.0 upload set now contains 286 physical Entries, 284 referenced
content chunks and no file at or above 100,000,000 bytes; the maximum is exactly
95,000,000 bytes. The large `client-hero-1001` Bundle is represented by five
content-addressed chunks and the full ZIP by seven chunks. Release build,
source/published self-tests, format check, package audit, bootstrap allowlist
audit, Unity refresh and isolated Console check all passed. Launcher version is
1.2.0 with manifest schema v2. Actual UOS console upload, Release/Badge creation
and external machine acceptance remain user-owned.
