# ExecPlan Standard for FrameSyncMobaDemo

Use an ExecPlan for tasks that:

```text
Span more than one assembly
Change a public contract
Change serialization or snapshots
Change frame synchronization
Require several implementation sessions
Touch several system designs
Have meaningful migration or compatibility risk
```

Plans live under:

```text
Docs/Implementation/Plans/
```

## Required properties

An ExecPlan must be self-contained. A new contributor should understand what to do without relying on chat history.

Keep it updated while working. Do not treat it as a one-time proposal.

## Required sections

Every ExecPlan contains:

### 1. Purpose

Describe the observable result for the player, developer, test harness, or server.

### 2. Progress

Use checkboxes with concrete completed and remaining work.

### 3. Surprises and discoveries

Record facts found in the repository that changed the plan.

### 4. Decision log

Record implementation decisions, alternatives, and reasons. Do not duplicate frozen architecture decisions from `DECISION_LOG.md`; link them.

### 5. Current repository context

List relevant paths, assemblies, public types, scenes, and tests as they actually exist.

### 6. Design sources

List exact current design paths and relevant sections.

### 7. Scope

Separate:

```text
In scope
Out of scope
```

### 8. Implementation plan

Describe the work in dependency order and name the files or types expected to change.

### 9. Public contracts

List interfaces, structs, enums, serialized schemas, and assembly dependencies added or changed.

### 10. Validation

List exact compile, EditMode, PlayMode, determinism, snapshot, rollback, and integration checks.

### 11. Failure and recovery

Explain how partial changes can be safely resumed or reverted.

### 12. Results

At completion, summarize behavior delivered, files changed, tests, and remaining limitations.

## Plan discipline

- Update Progress after each meaningful step.
- Record unexpected repository facts immediately.
- Do not hide design conflicts.
- Do not expand scope silently.
- If a task is split, create child ExecPlans and link them.
- A plan is complete only after validation results are recorded.
