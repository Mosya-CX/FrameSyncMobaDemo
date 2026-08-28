# ExecPlan Index

> Document class: Current Plan Locator
> Updated: 2026-08-27
> Default read: yes, when `.agents/PLANS.md` says an ExecPlan is required

## Active

- None.

## Verification Pending

- `Docs/Implementation/Plans/0138_local_addressables_and_dedicated_server_presentation_split.md`
  — implementation and focused verification are complete; matching rebuilt
  client/Linux Dedicated Server inspection remains pending.

ExecPlan 0136 completed its declared source/formal-asset and focused-test scope.
Matching rebuilt Local C/S and UOS live acceptance was explicitly outside that
plan. Create a new plan if the user requests that external acceptance.

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

Existing numbered Markdown files in this directory are engineering history and
are not read by default. Their historical status wording is not retroactively
normalized in this migration.

New plans must use the metadata and lifecycle in `.agents/PLANS.md`. When a new
plan becomes Active or Verification Pending, list its exact path above. Remove
it from those sections when it becomes Completed or Superseded.

Completed plans are frozen. Follow-up work creates a new Plan ID and references
the prior plan instead of appending new scope or a long-running history.
