# ExecPlan 0128 - Buff design-conformance program

## 1. Purpose

Align the Buff implementation with `BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`
and the newly formalized section 13A (MaxBuffs/priority dispel, DECISION_LOG
D-025), while keeping deterministic behavior and the existing typed-event
lifecycle intact.

## 2. Progress

- [x] Audited current Buff implementation against design v14.2.
- [x] Formalized MaxBuffs/priority dispel as design section 13A + D-025.
- [x] Slice 1: `BuffDefinition` ScriptableObject-ization with design structure
  (`BuffDisplayInfo`, `LifeRuleConfig`/RefreshMode, `StackRuleConfig`/AddMode/
  ReduceMode, `TagSet`, `BuffEffectConfig`, `BuffSource`); runtime/handler/call
  sites/tests updated; MaxBuffs formalization kept. Snapshot schema 13 -> 14
  with BuffSource fields in BuffRuntimeSnapshot + checksum writer. Buff tests
  6/6 and equipment regression green; compile 0 errors.
- [x] Slice 2: `BuffBlackboard` static layout
  (`BuffBlackboardLayout`/`BuffStateSlotDefinition`/`BuffValue`), remove
  Dictionary-based storage. Snapshot/checksum serialize slots by stable
  BuffStateSlotId; schema 15.
- [x] Slice 3: config-driven reactions (`BuffLifecycleReactions`:
  Added/Reapplied/StackChanged/Periodic/Removed; `BuffEventReactions` incl.
  AbilityCast/LevelUp); apply-flow gaps (StackChanged 0 -> initial,
  Reapplied); ClearForDespawn handle-release without Removed reaction;
  OnAbilityCast wired at AbilityHandler cast-begin, OnLevelUp wired at
  StatHandler level-up.
- [x] Slice 4: read-only `BuffInfo` query layer
  (`GetBuffInfo`/`GetBuffInfosByTag`/`GetAllBuffInfos`) as HUD Buff bar source.
  Direct `GetAllBuffs()` runtime leak removed.

## 3. Surprises and discoveries

- Core mechanics (single-runtime overwrite, ordered store, handle ownership,
  ClearForDeath/Respawn/Despawn, typed events, snapshot/checksum) already match.
- Gaps are structural: definition not SO; no BuffSource; Blackboard is
  Dictionary-based; reactions embedded in effects instead of config; no
  Reapplied/initial-StackChanged; no AbilityCast/LevelUp events; no BuffInfo.
- `MaxBuffs`/priority dispel is deterministic and soft-capped; formalized as
  section 13A per owner confirmation.
- HUD design references `BaseAttackDamage`/`BonusAttackDamage` split views but
  the current StatHandler exposes a single `AttackDamage` stat with modifiers;
  buff/equipment modifiers target `AttackDamage` (owner-confirmed rule), the
  base/bonus split is a presentation-view follow-up.

## Validation (Slices 2-4)

- BuffReactionAndInfoTests 8/8 (blackboard layout round trip, first-apply
  StackChanged 0 -> initial, Reapplied, periodic interval slot, AbilityCast/
  LevelUp events, Removed-vs-Despawn split, BuffInfo fields/tag query,
  ValuePerStack modifier updates).
- BuffEffectLibraryTests 6/6; equipment regression green; compile 0 errors.

## 4. Decision log

- Design v14.2 is the contract; section 13A adds the owner-confirmed cap rule.
- `byte` tag convention retained via `TagSet` until a stronger tag contract
  exists.
- Slices keep the project compiling and focused tests green at each boundary.

## 5. Current repository context

`Assets/Scripts/Gameplay/Buff/` (12 files + 5 effects), call sites
`ApplyBuffStageDef`, `BuffEquipmentModule`, `ProjectileEffectDispatcher`,
tests `BuffEffectLibraryTests`.

## 6. Design sources

- `BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md` sections 1, 3-9, 11,
  13, 13A.

## 7. Scope

In scope: definition SO, BuffSource, blackboard layout, reaction config,
apply-flow semantics, BuffInfo queries, MaxBuffs formalization.

Out of scope: CC integration changes, snapshot schema changes beyond current
members, production buff content.

## 8. Implementation plan

Slice 1 first (definition + source), then blackboard, then reactions, then
BuffInfo. Each slice: compile via Unity MCP, console, focused EditMode tests,
update this plan.

## 9. Public contracts

Pending per slice: `BuffDefinition`, `BuffSource`, `BuffDisplayInfo`,
`BuffLifeRuleConfig`, `BuffStackRuleConfig`, `BuffTagSet`, `BuffEffectConfig`,
`BuffBlackboardLayout`, `BuffValue`, reaction config types, `BuffInfo`.

## 10. Validation

BuffEffectLibraryTests + EquipmentShop regression + compile 0 errors per slice.

## 11. Failure and recovery

Each slice is separable; definition SO changes are coordinated in one compile
cycle with all call sites.

## 12. Results

Program started 2026-08-02.
