# ExecPlan Index

> Document class: Current Plan Locator
> Updated: 2026-09-01
> Default read: yes, when `.agents/PLANS.md` says an ExecPlan is required

## Active

None.

## Verification Pending

- `Docs/Implementation/Plans/0138_local_addressables_and_dedicated_server_presentation_split.md`
  — implementation and focused verification are complete; matching rebuilt
  client/Linux Dedicated Server inspection remains pending.

ExecPlan 0136 completed its declared source/formal-asset and focused-test scope.
Matching rebuilt Local C/S and UOS live acceptance was explicitly outside that
plan. Create a new plan if the user requests that external acceptance.

## Completed

ExecPlan 0160 completed the isolated Unity release-client build window for
`Builds/Demo/Game/AAALOL.exe`. Its schema-v3 CDN packaging checkbox is optional
and disabled by default; the independent `Builds/UosClient` test-client flow is
unchanged. Unity compilation, focused EditMode 6/6 and standalone packager
build/self-test pass; a real Player build remains user-owned.

ExecPlan 0137 completed the D-047 structured Unit arbitration, fixed Main/Base
Runtime, schema-23 Snapshot/checksum and bootstrap-wire-4 migration. A matching
Local C/S or UOS live rebuild remains outside that plan.

ExecPlan 0139 completed traversal-neutral same-Tick Combat settlement, causal
reaction waves, scheme A killer attribution and scheme C exact-tie fallback.
UID remains the canonical identity/order and an allowed explicit complete-tie
key, but no longer creates implicit first-writer priority inside a sealed batch.

ExecPlan 0140 completed action-keyed deterministic Crit, immutable gameplay
participant identity, parent-path effect ordinals, neutral equal-distance
Projectile arbitration and schema-24/GameplayDataVersion-4 migration.

ExecPlan 0141 completed the D-051 migration from monolithic direct content
references to one root-indexed, match-scoped local Addressables aggregate,
including logic-only Dedicated Server groups, lifecycle ownership, dependency
audits and focused Unity acceptance. Corrected Player rebuild inspection remains
external acceptance shared with ExecPlan 0138.

ExecPlan 0142 corrected the built-player Unit catalog composition failure by
making Core the sole serialized owner of the shared dispose-policy table. Hero
catalog dependency closures, regenerated hashes, focused EditMode/PlayMode and
final Unity Console validation passed; matching Player rebuild/UOS acceptance
remains user-owned.

ExecPlan 0143 corrected the externally managed Select-to-Loading GameScene
handoff and normalized all generic skill-indicator materials to the same
built-client-compatible shader path already used by Aatrox Q. Source/assets,
Bootstrap EditMode and focused/cached-order PlayMode pass; rebuilt-client UOS
visual acceptance remains user-owned.

ExecPlan 0144 superseded 0143's insufficient asset-only generic-indicator
shader repair after the rebuilt Player remained magenta. Generic Direction,
RangeCircle and GroundTarget now use a project-owned URP shader plus
driver-owned runtime materials; actual Addressables acquisition and framebuffer
blue/not-magenta PlayMode pass. Rebuilt-client UOS visual acceptance remains
user-owned.

ExecPlan 0145 removed the remaining generic-indicator dependence on global
`Shader.Find` after the 20:12 rebuilt Player proved that an AssetBundle-contained
Shader was not globally discoverable. Runtime materials now clone the already-
loaded Addressables source materials and inherit their exact Bundle-resolved
Shader objects. Focused EditMode/PlayMode and framebuffer verification pass;
rebuilt-client visual acceptance remains user-owned.

ExecPlan 0146 addressed the 20:42 Player's solid magenta Quad rendering by
retaining the dedicated one-variant indicator Shader in the client Player core
and clearing migrated Material keywords. Dedicated Server build scope excludes
the Shader and restores GraphicsSettings exactly. Bootstrap EditMode 120/120,
indicator PlayMode 1/1 and final Unity Console validation pass; rebuilt-client
visual acceptance remains user-owned.

## Completed / historical

ExecPlan 0159 replaced the confusing schema-v2 `packages`/`objects`/`chunks`
upload tree with one signed schema-v3 `content/<sha256>` payload directory.
Launcher 1.3.0 retains chunked first install, file-level incremental update,
Range resume, complete hashes and the 95,000,000-byte cap. The audited 1.0.0
tree and empty-Game bootstrap 1.3.0 are ready for Bucket-root upload and live
acceptance.

ExecPlan 0158 completed the UOS console-safe migration of the Launcher CDN
manifest to schema v2. Large objects and the full ZIP are split into globally
content-addressed 95,000,000-byte chunks; the Launcher resumes and verifies each
chunk, reconstructs aggregate hashes, and retains the existing full-install,
incremental, rollback and D-051 behavior. The 1.0.0 upload set has no physical
Entry at or above 100,000,000 bytes. Local release/self-test/audit, Unity
isolation and independent review passed; live UOS console acceptance remains
user-owned.

ExecPlan 0157 completed the signed UOS CDN full-client bootstrap and file-level
incremental updater for the standalone Demo launcher. It generates an audited
full ZIP plus content-addressed objects, embeds the signing trust root, requires
full-file hash validation for no-op/offline/recovery paths, supports Range
resume and safe staged swap, and produces a Launcher-only bootstrap with empty
`Demo/Game`. Release build/format, source and published self-tests, package/
bootstrap audits, Unity isolation and two-round independent review passed with
no remaining P0/P1. Actual UOS upload/Badge/player-machine acceptance remains
user-owned.

ExecPlan 0156 finalizes the formal launcher whiteboard with the supplied
Background/Banner/Logo images and generated multi-size AppIcon ICO. It removes
directory-selection surface, keeps the fixed Game path out of player settings,
and leaves settings/news/CDN/update features out of scope. Release build,
published self-test and final package verification are recorded in the plan.

ExecPlan 0155 completed the initial formal single-client Demo launcher
whiteboard; its path-picker/art-hook wording is superseded by 0156. The
standalone .NET 8 WinForms source lives under `Tools/UosGameLauncher`, publishes
to `Builds/Demo/Launcher`, locates `Builds/Demo/Game/AAALOL.exe`, validates
`AAALOL_Data`, persists only the login name/path, and exposes optional art hooks
with a painted fallback. Build and published self-test passed with 0 warnings/
0 errors; the first independent review found no P0/P1. Final art, visual
acceptance, self-contained packaging and any CDN updater remain future scope.

ExecPlan 0154 completed the live Aatrox W accepted-after-execution latch fix,
client-only VFX preload/pool warmup with selected-hero filtering and first-play
timing diagnostics, projectile bind timing logs, the 1.5x minion
experience-sharing radius update, and UOS CDN guidance for complete-client ZIP
versus future remote Addressables deployment. Unity forced refresh and focused
PlayerInput `42/42`, FrameSync `123/123`, Bootstrap `123/123`, VFX/filter and
reward tests passed; rebuilt client/UOS acceptance remains user-owned.

ExecPlan 0153 completed atomic Addressables indicator generation replacement
and Varus Q accepted-after-rollback local-state recovery. Direction, Range and
Ground instances/material clones are rebuilt while both lease generations are
resident, then the old leases are released; Aatrox Q's independent line path
remains valid. A retired Q latch is restored only for the exact tracked command
observed missing at its original Tick and accepted at a strictly later Tick.
PlayerInput `41/41` and the focused indicator/input PlayMode cases pass; rebuilt
UOS visual/input acceptance remains user-owned.

ExecPlan 0152 completed match-scoped `ClientId + CommandSeq` acceptance
idempotency and client Bundle send gating. Repeated application-level copies
are ignored before retargeting/authorization, distinct consecutive sequences
remain valid, and unchanged canonical collector content no longer creates one
new Bundle per Unity Update. Full FrameSync `121/121` and Bootstrap EditMode
`123/123` pass; rebuilt UOS acceptance remains user-owned.

ExecPlan 0151 completed the source-side Varus input, generic indicator and
HeroTest regression closure. Addressables-loaded indicator materials are cloned
without a second global shader lookup, Q Focus/route Move/primary Commit is
covered at one TargetTick, pure Toggle W no longer blocks ordinary attacks,
diagnostic request/execution/session logs are present, and HeroTestScene is
confirmed as the Varus fixture. Focused Unity verification passed; rebuilt
Windows/UOS acceptance and a live W state-jump reproduction remain user-owned.
The required independent reviewer could not run because the host usage limit
rejected both dispatches; the local read-only review found no P0/P1.

ExecPlan 0150 completed the source-side W/Q request projection, generic
indicator material, locked-camera/root interpolation, Aatrox Q VFX timing and
Varus animation closure. Focused Unity verification and the resolved first
independent review are recorded in the plan. Tick 5152's live root cause was
explicitly deferred to the next rebuilt `-checksumDetail` UOS evidence; no
Snapshot/checksum/wire contract changed.

ExecPlan 0149 corrected moving-cast animation route evaluation, pending
Focus/Commit indicator continuity, Varus W-to-Q input coverage and locked-camera
jitter caused by coupled position/rotation projection clocks. Focused and broad
Unity evidence plus the resolved independent-review findings are recorded in the
plan; rebuilt-client visual acceptance remains user-owned.

ExecPlan 0148 completed D-052 presentation-owned animation sampling at a
configurable rate with render interpolation, match-isolated continuous time,
bounded attack progress and externally driven Idle/Walk/Move loop phase.
Focused EditMode/PlayMode and full FrameSync verification are recorded in the
plan; rebuilt-client visual comparison remains user-owned.

Existing numbered Markdown files in this directory are engineering history and
are not read by default. Their historical status wording is not retroactively
normalized in this migration.

New plans must use the metadata and lifecycle in `.agents/PLANS.md`. When a new
plan becomes Active or Verification Pending, list its exact path above. Remove
it from those sections when it becomes Completed or Superseded.

Completed plans are frozen. Follow-up work creates a new Plan ID and references
the prior plan instead of appending new scope or a long-running history.
