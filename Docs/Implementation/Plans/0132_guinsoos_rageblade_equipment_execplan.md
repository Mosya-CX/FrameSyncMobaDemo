# ExecPlan 0132 — Guinsoo's Rageblade equipment vertical slice

> Status: Implemented and focused-tested; cs26 packaged-Shop bootstrap
> regression corrected, C/S pair rebuilt successfully and awaiting runtime acceptance
> (2026-08-11).

## Goal

Add the first formal equipment catalog under `Assets/Config/Formal/` and
implement the five-item Guinsoo crafting tree from the user-provided design:
Dagger, Amplifying Tome, Pickaxe, Recurve Bow and Guinsoo's Rageblade.

The observable closure is:

```text
GameScene loads a non-empty formal EquipmentCatalogAsset.
The shop can resolve and craft every item in the tree.
Fixed stats use the existing baked fp path.
Recurve Bow deals 15 physical On-Hit damage.
Guinsoo deals 30 magic On-Hit damage.
Seething Strike is a 3-second, four-stack Buff granting 8% AttackSpeed per stack.
At full stacks, every third real basic-attack hit repeats eligible On-Hit effects once.
The repeat is not another Attack and cannot recursively request another repeat.
Equipment module counters survive Snapshot/Restore and participate in the checksum.
```

## Authoritative inputs

- `Docs/Architecture/DECISION_LOG.md`
- `Docs/Architecture/DESIGN_INDEX.md`
- `Docs/Design/moba_equipment_shop_gold_system_design_v12.md`
- `Docs/Design/BuffSystem_Design_v14_2_PermanentBuffRespawnPatch.md`
- `Docs/Design/moba_combat_system_design_v13_2.md`
- `E:/EgdeDownLoad/鬼索的狂暴之刃装备设计案.md`

The current user task outranks older implementation status text. Existing
Local C/S and live UOS acceptance remain the baseline and are not reopened by
this content slice.

## Contract decisions

- Equipment IDs: Dagger `31001`, Amplifying Tome `31002`, Pickaxe `31003`,
  Recurve Bow `31004`, Guinsoo's Rageblade `31005`.
- Seething Strike BuffConfigId: `31901`.
- The attack that reaches four stacks is the first full-stack attack counted
  by the every-third-hit counter.
- A repeated On-Hit carries `IsRepeated = true`. Ability, Buff and ordinary
  equipment On-Hit handlers may consume it; the stack-granting module and the
  repeat-generator explicitly ignore it, preventing fake attacks and
  recursion.
- Equipment module execution receives its exact runtime-state slot by `ref`,
  matching Equipment v12 section 3.3. This also fixes the existing internal
  cooldown modules, whose previous struct copies did not persist mutations.
- The formerly unused `TimerTicks` snapshot member is renamed to the formal
  `TriggerCount`. Snapshot/checksum schema advances from 21 to 22.
- No active-equipment targeting contract is introduced.

## Implementation steps

- [x] Correct CURRENT_HANDOFF and MODULE_STATUS against current Git/source/log evidence.
- [x] Add ref-based equipment effect execution context and repeat-safe On-Hit metadata.
- [x] Add generic OnHitRepeatModule and configure BuffEquipmentModule repeat policy.
- [x] Add deterministic unit tests for damage, stacking, every-third repeat, recursion guard and snapshot round trip.
- [x] Create `Assets/Config/Formal/Equipment/` assets and update the formal Buff catalog.
- [x] Assign the equipment catalog to GameScene through Unity APIs.
- [x] Compile, inspect Console, run focused EditMode tests and relevant regression assemblies.
- [x] Update DECISION_LOG, MODULE_STATUS and this plan with final evidence.
- [x] Follow-up: replace HeroTest's direct infinite-gold mutations with the
  formal local-Tick shop/gold composition and a 10000-gold baseline.
- [x] Follow-up: bind catalog/equipment-bar icons, dynamic recipe prices,
  owned-slot selling and post-Tick Shop refresh; unblock owned-cell raycasts.
- [x] Follow-up: recompile and run the focused shop EditMode/PlayMode coverage.
- [x] Follow-up: diagnose cs26's zero-price/unbuyable formal Shop from the
  packaged logs and runtime binding chain.
- [x] Follow-up: keep `ShopTraderRuntime` lazy while allowing pre-transaction
  price/RequestCheck resolution through the formal PlayerSlot mapping.
- [x] Follow-up: add action-scoped `[ShopRequest]` diagnostics and verify that
  minion experience range converts authored distance through the real
  stat-to-logic distance scale.
- [x] Follow-up: diagnose cs27's packaged purchase exception and move the
  formal Shop submitter wiring to the completed local-player bind point.
- [x] Follow-up: audit cs28 logs and prove confirmed minion-kill income against
  the observed shop balance.
- [x] Follow-up: move kill-reward currency from `fp` to `int`, author the
  requested 1500/21/14/300 values and use exact 3/5 hero-killer allocation.
- [x] Follow-up: add confirmation diagnostics and focused reward/config tests;
  remove PlayMode test contamination of the formal PrefabTable.

## Verification evidence

- Unity MCP script compilation passed after the final source and asset changes.
- Focused EditMode tests passed for the formal catalog, six-hit damage/
  stacking sequence, repeated-hit recursion guard, repeated Capture on the
  same snapshot, death/respawn module-state preservation,
  Restore/Resolve/replay equivalence and checksum coverage.
- The FrameSync EditMode assembly passed 76/76. The broader Unit run executed
  459 passing tests and reported 12 unrelated existing failures; none is an
  equipment/Guinsoo test (see MODULE_STATUS for the categories).
- Observed six-hit damage sequence: `31, 31, 31, 31, 31, 61`; Seething Strike
  remained at four stacks and the repeat counter transitioned `2 -> 0`.
- `GameBootstrapPlayModeTests.ClientComposition_InitializesFromProjectAssets`
  passed. The unrelated existing
  `UnitPrefabCompositionPlayModeTests.FormalSpawn_...` failed because its
  test PrefabId `9` is outside the current configured `[1000, 1999]` range.
- Follow-up correction: `HeroTestDriver.BuildWorld()` still constructed the
  intentionally empty pre-D-039 database. It now bakes
  `FormalEquipmentCatalog`; the dedicated HeroTest shop PlayMode regression
  passes and resolves all five entries including equipment 31005.
- Follow-up shop verification passed: `EquipmentShopTransactionTests` 3/3,
  `CellPrefabRefTests` 2/2, `HeroTestSceneEquipmentPlayModeTests` 2/2,
  `GameBootstrapPlayModeTests` 1/1 and
  `GuinsoosRagebladeEquipmentTests` 5/5. A live HeroTest run verified the
  HUD/shop shared balance (`10000 -> 9750` after Dagger), an owned catalog
  cell remained clickable, its detail opened and Sell became interactable.
- The broader `UiLuaPagesSmokeTests` remains blocked before reaching Shop by
  its existing `ClientFrameworkSmoke` fixture mismatch: initial spawn 1 is
  Team 2 while SpawnPoint 1 is configured for another team.
- A new local C/S package pair was built together through
  `LocalNgoBuildMenu.BuildBoth()` on 2026-08-10. Both Unity build reports
  finished with `Result: Success`; Server data completed at 21:37 and Client
  data at 21:42. The new pair has not yet received a fresh multi-process
  runtime acceptance. No new UOS Linux package was requested.
- The cs26 process run exposed a packaged-only initialization regression:
  formal catalog enumeration succeeded, but every dynamic price was `0` and
  purchase RequestCheck failed because no Trader existed yet. This formed a
  deadlock: v12 requires Trader state to stay absent until a successful
  transaction, while the implementation required Trader state to resolve the
  controlled hero. The runtime now resolves the hero from the stable
  `ControlledByPlayerSlot` mapping before the first transaction and only
  creates Trader state after a Purchase/Sell plan succeeds.
- cs26 had no shop-action diagnostics. Purchase/Sell/Undo now emit one
  `[ShopRequest]` line per user action with slot, gold, dynamic price,
  allowed result and failure reason. No per-Tick shop logging was added.
- `MatchStatisticsRuntime.MinionRewardShareRadius` is authored in stat-distance
  units. Its squared comparison now uses
  `radius * UnitWorld.StatDistanceToLogicDistanceScale`; the 7.99/8.01 logic
  boundary test passes at the current 0.01 scale.
- Post-fix evidence: `EquipmentShopRequestTests` 9/9,
  `EquipmentShopTransactionTests` 3/3,
  `MatchRewardDistanceTests` 1/1, FrameSync EditMode 77/77,
  `GameBootstrapPlayModeTests` 1/1 and
  `HeroTestSceneEquipmentPlayModeTests` 2/2.
- The post-cs26 local C/S pair was rebuilt together once through
  `LocalNgoBuildMenu.BuildBoth()` on 2026-08-11. Both Unity player reports
  finished with `Result: Success`; the Server managed assemblies were written
  at 01:10 and the Client managed assemblies at 01:12. The cs27 run confirmed
  non-zero prices, then exposed a client-side purchase exception:
  `EquipmentShopRuntime requires a command submitter before RequestPurchase`.
  `GameBootstrap.Awake()` had attempted to read `CommandRequester` before
  `BindLocalPlayer()` created it. The stale early check is removed and the
  existing `PlayerCommandRequester` is now injected immediately after its
  initialization. The formal composition PlayMode regression confirms an
  allowed Purchase produces one canonical EquipmentShop/Purchase command;
  the focused PlayerInput shop-command test also passes. A new matching C/S
  build and process-level purchase acceptance remain pending.
- cs28 then accepted non-zero prices and Purchase/Sell/Undo across the packaged
  Server and both Clients with no deterministic mismatch or Shop rejection.
  Its slot-0 balance mathematically includes all logged minion last hits. New
  source adds `[GoldIncomeConfirmed]` for direct runtime evidence and changes
  the public allocation Amount to `int`. Formal values are initial 1500,
  melee/ranged 21/14 and hero 300, with exact 3/5 killer share when assistants
  exist. FrameSync EditMode, formal config and the three focused PlayMode tests
  pass; a matching package rebuild is still required for live verification of
  these latest source values and logs.

## Out of scope

- UTP queue sizing and UOS launch-timeline diagnostics.
- Result/return/remote settlement.
- Jungle content.
- `EquipmentTargetPolicy` values or active-item targeting.
- Production balance changes beyond the supplied item design.
