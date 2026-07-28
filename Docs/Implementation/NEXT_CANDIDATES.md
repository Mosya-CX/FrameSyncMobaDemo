# Candidate Plans -- Post Gap Remediation 2026-07-25

> Updated: 2026-07-25 after GAP-A through GAP-G remediation complete.
> All 7 gaps resolved. 528/529 EditMode tests pass.

---

## Completed (this round)

| Gap | Description | Status |
|---|---|---|
| GAP-A | Combat Domain Enum Types (CombatEnums.cs) | ? Already existed |
| GAP-B | Ability Indication Pipeline (IndicatorStageResolveRule + controller) | ? Already existed |
| GAP-C | Equipment Passive Applier Lifecycle | ? Already existed |
| GAP-D | Jungle Camp System Completion | ? Spawn timer + respawn already existed |
| GAP-E | Minion Wave System Completion | ? Phase cycling + siege already existed |
| GAP-F | Death/Respawn Animation Bridge | ? Already wired in GameBootstrap |
| GAP-G | Lua HUD Elements | ? hud.lua completed with full UI rendering |

## Bugfixes applied

| Issue | Fix |
|---|---|
| EquipmentHandler.Capture List index out of range | Changed state.Slots[i] = to state.Slots.Add( |
| MovementHandlerTests no tick context | Added SimulationTickContextController in SetUp/TearDown |
| Missing using directives | Added to DeathPresenter, RespawnPresenter |
| UnitPresentationHost missing Profile | Added Profile property |
| MinionSystem init order + config type | Fixed order, added BakedMinionWaveConfig |

## Next candidates (post-remediation)

### HIGH ！ Gameplay Core Completion

**0101 ！ Pathfinding Pipeline Integration Test Fix** (~100 lines)
- Fix Pipeline_SingleUnit_MovesWithCommand test: raw-constructor FrameSyncGameRuntime needs movement pipeline wired for pathfinding tests.
- Reference: FrameSync v10.2, Pathfinding v13.1

**0102 ！ Network/Authority Layer Foundation** (~600 lines, Deferred Phase 11)
- GameApplicationFlowManager, LobbySessionFlowNetwork, CommandDispatcher, AuthorityFrameReplicator, AuthorityRecovery
- Reference: FrameSync v10.2 sections 2-3, 11-12

**0103 ！ TeamBase System** (~400 lines, Deferred Phase 13+)
- TeamBase type, base destruction, victory condition integration
- Reference: FrameSync v10.2

### MEDIUM ！ Polish

**0104 ！ Ability Animation Plan Completion** (~200 lines)
- Per-ability animation plans for all 9 stage types
- Reference: Ability v15.2

**0105 ！ Cast Resource System** (~300 lines)
- Mana/energy/rage resource types with regen pipelines
- Reference: Unit v27.3

**0106 ！ Minimap Fog of War** (~250 lines)
- Team-based visibility, exploration state
- Reference: UI/Lua v9.1

### LOW ！ UI/Config

**0107 ！ Equipment Slot View Icons** (~100 lines)
- Replace placeholder icons with proper equipment type icons
- Reference: Equipment/Gold v12

**0108 ！ GlobalParamTable SO Defaults** (~80 lines)
- Populate default growth constants for stat scaling
- Reference: Unit v27.3

---

## Deferred (Phase 11/13/14)

| # | Type | Design | Phase |
|---|------|--------|-------|
| F1 | GameApplicationFlowManager | FrameSync v10.2 section 2 | Phase 14 |
| F2 | LobbySessionFlowNetwork | FrameSync v10.2 section 3 | Phase 14 |
| F3 | CommandDispatcher | FrameSync v10.2 section 11 | Phase 11 |
| F4 | AuthorityFrameReplicator | FrameSync v10.2 section 12 | Phase 11 |
| F5 | AuthorityRecovery | FrameSync v10.2 section 12 | Phase 11 |
| F6 | TeamBase | FrameSync v10.2 | Phase 13+ |
