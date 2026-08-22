# ExecPlan Standard for FrameSyncMobaDemo

> Document class: Current Workflow Standard
> Update policy: replace this standard; use Git for history

## When an ExecPlan is required

Use an ExecPlan for tasks that:

- span more than one assembly;
- change a public contract;
- change serialization, Snapshot or checksum;
- change frame synchronization or network application flow;
- require several implementation sessions;
- touch several Current system designs;
- have meaningful migration or compatibility risk;
- are classified High risk by `AI_WORKFLOW.md`.

Small localized tasks may be executed with an in-chat task plan.

Plans live under:

```text
Docs/Implementation/Plans/
```

`Docs/Implementation/Plans/INDEX.md` is the only default locator for an active
plan. Do not scan every historical plan at task intake.

## Required metadata

Every new ExecPlan begins with:

```text
Plan ID:
Status: Active | Verification Pending | Completed | Superseded
Created:
Completed:
Risk: Low | Medium | High
Design conformance: Strict | Approval required
Estimated code delta:
Actual code delta:
Affected assemblies:
Design sources:
Decision dependencies:
Validation basis:
```

Only `Active` and `Verification Pending` plans appear in the active section of
the index. At most one plan may be `Active` unless the user explicitly requests
parallel independent work.

## Required properties

An ExecPlan must be self-contained for its requested slice. A contributor must
understand the observable result, authority, scope, public contracts and proof
without relying on chat history.

Keep an Active plan updated while working. Do not copy the project constitution,
full workflow, unrelated design summaries or historical logs into it.

## Required sections

### 1. Purpose

Describe the observable result for the player, developer, test harness or
server.

### 2. Progress

Use checkboxes with concrete completed and remaining work.

### 3. Repository facts and discoveries

Record relevant paths, assemblies, public types, assets and unexpected facts
that materially changed the plan.

### 4. Design sources and traceability

List exact Current design paths, sections and Decision IDs. Map critical design
requirements to the exact tests that protect them.

### 5. Scope

Separate In scope and Out of scope. State public-contract, Snapshot,
serialization, checksum, lifecycle and Unity-asset implications explicitly.

### 6. Implementation plan

Describe work in dependency order and name expected files/types.

### 7. Public contracts and ownership

List interfaces, structs, enums, schemas and assembly dependencies added or
changed. State the authoritative owner of each protocol type.

### 8. Validation

List exact compilation, Console, EditMode, PlayMode, determinism, Snapshot,
rollback, integration and live checks required by the slice.

### 9. Independent review

For High risk plans, record the read-only review input and P0/P1/P2 findings or
an explicit no-finding result.

### 10. Failure and recovery

Explain safe resume/recovery and external acceptance still required.

### 11. Results

At completion, summarize delivered behavior, actual delta, files, contracts,
tests, validation basis and remaining limitations.

## Lifecycle discipline

- Register a newly Active plan in `Plans/INDEX.md` before implementation.
- Update Progress after meaningful milestones.
- Record unexpected repository facts immediately.
- Do not hide design conflicts or silently expand scope.
- Split genuinely independent or multi-stage work into linked plans.
- `Verification Pending` means implementation is complete but an in-scope proof
  is still outstanding.
- A plan becomes `Completed` only when every proof inside its declared scope is
  recorded.
- If external/live acceptance was explicitly out of scope, complete the source
  plan and create a new plan when that acceptance is requested.
- Completed plans are frozen. Later work creates a new plan and references the
  completed one instead of appending to it.
- Use Git history and Completed plans for engineering history; do not copy it
  into `CURRENT_HANDOFF.md`.
