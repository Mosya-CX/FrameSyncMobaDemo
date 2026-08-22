# ExecPlan Index

> Document class: Current Plan Locator
> Updated: 2026-08-22
> Default read: yes, when `.agents/PLANS.md` says an ExecPlan is required

## Active

None.

## Verification Pending

None.

ExecPlan 0136 completed its declared source/formal-asset and focused-test scope.
Matching rebuilt Local C/S and UOS live acceptance was explicitly outside that
plan. Create a new plan if the user requests that external acceptance.

ExecPlan 0137 completed the D-047 structured Unit arbitration, fixed Main/Base
Runtime, schema-23 Snapshot/checksum and bootstrap-wire-4 migration. A matching
Local C/S or UOS live rebuild remains outside that plan.

## Completed / historical

Existing numbered Markdown files in this directory are engineering history and
are not read by default. Their historical status wording is not retroactively
normalized in this migration.

New plans must use the metadata and lifecycle in `.agents/PLANS.md`. When a new
plan becomes Active or Verification Pending, list its exact path above. Remove
it from those sections when it becomes Completed or Superseded.

Completed plans are frozen. Follow-up work creates a new Plan ID and references
the prior plan instead of appending new scope or a long-running history.
