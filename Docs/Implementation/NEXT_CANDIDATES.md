# Candidate Plans -- Batch post-0108 (Gameplay Core Priority)

> Updated: 2026-07-25 after completing 0105-0108 candidates.
> Compilation: Clean (0 errors).
> 0101-0108: All design-audit gaps + Gameplay core systems completed.

---

## Completed in This Batch (0105-0108)

| Candidate | Description | Status |
|-----------|-------------|--------|
| 0105 | GroundTargetCastModelDef + VectorTargetCastModelDef | **DONE** |
| 0106 | Ability Resource Cost (mana/energy check + channel drain) | **DONE** |
| 0107 | Ability Cooldown HUD Presenter + Pipeline Verification | **DONE** |
| 0108 | Level-Up Skill Point Bridge (XP -> SkillPoints -> Allocate) | **DONE** |

---

## Current Module Status After 0108

- **Verified (11/18)**: Deterministic foundation, Physics, Unit/Prefab, Stats/XP, Combat, Attack, Equipment/Gold, Snapshot/Rollback, FrameSync/MatchFlow, Movement/Pathfinding, Bootstrap

- **Implemented (6/18)**: Buff (OnHit/Dependent/MaxBuffs/dispel), CrowdControl, Projectile, PlayerInput, NonHero AI, Integration Tests

- **Partial (2/18)**: Ability runtime (GroundTarget/VectorTarget added, resource costs wired, cooldown verified; ground-target indicator rendering remains), Presentation bridge (SFX/event dispatchers exist; some visual channels pending)

- **Missing (Deferred)**: Network/Authority layer, TeamBase system

---

## Candidate 0109: Skill Indicator Ground-Target + Vector Rendering (~250 lines)

### Problem

SkillIndicatorDriver already has `groundTargetPrefab` and `directionIndicatorPrefab` references, and `AimKind.Point` / `AimKind.Direction` are fully defined in the enum. However, the indicator rendering for these modes only does basic show/hide. There is no:

1. **Ground-target circle rendering**: When an ability with `GroundTargetCastModelDef` enters Aim stage, a circle indicator should appear at the cursor position, clamped within MaxRange of the caster, with Radius visual feedback for AoE abilities.

2. **Vector-target arrow rendering**: For `VectorTargetCastModelDef`, a directional arrow should extend from caster toward cursor, clamped to MaxRange, with MinRange indicator for minimum cast distance.

3. **Range feedback**: Ground/Vector indicators should visually communicate when the cursor is out of range (red tint) versus in range (blue/green).

### Implementation

**Part A: Ground-target circle rendering (~100 lines)**
- Extend `SkillIndicatorDriver.UpdateCursor()` for `AimKind.Point`:
  - Compute cursor world position via existing raycast
  - Clamp position to MaxRange from caster: `direction = cursor - caster; if length > MaxRange: position = caster + normalize(direction) * MaxRange`
  - Position the `_groundTargetInstance` at the clamped world position
  - Scale circle indicator by Radius param (read from ability def CastRange or Model.Radius)
  - Color feedback: out-of-range = red, in-range = blue

**Part B: Vector-target arrow rendering (~100 lines)**
- Extend `SkillIndicatorDriver.UpdateCursor()` for `AimKind.Direction`:
  - Compute direction from caster to cursor
  - Clamp to MaxRange (arrow body length), enforce MinRange (arrow minimum distance)
  - Position `_directionInstance` arrow from caster forward, scaled to length
  - Rotate arrow to match direction vector
  - Width/thickness proportional to ability width (if defined)

**Part C: Integration with AbilityHandler (~50 lines)**
- During Aim stage of GroundTarget/VectorTarget, expose indicator state via `ActiveAbilityCastInfo`
  - Add `CastRange` (fp) and `AimKind` fields to `ActiveAbilityCastInfo`
- Call `SkillIndicatorDriver.Show()` with appropriate kind, range, and position data
- On Commit: hide indicator, lock aim data into session

### Files Changed
- `Assets/Scripts/PlayerInput/SkillIndicatorDriver.cs` -- ground circle + vector arrow rendering
- `Assets/Scripts/Gameplay/Ability/AbilityHandler.cs` -- +CastRange, +AimKind in ActiveAbilityCastInfo
- `Assets/Scripts/FrameSync/UnitAnimationDriver.cs` -- minor: reference updated struct

### Lines: ~250. Priority: MEDIUM (Gameplay UX -- visual indicator for ground/vector abilities).

---

## Candidate 0110: Jungle Camp AI Behavior Completion (~300 lines)

### Problem

Non-Hero Unit Design v5 defines jungle camp behavior: creeps spawn at camp positions, patrol within a leash radius, aggro on nearby enemy units, reset when pulled too far. Currently:
- `JungleCampSystem` manages camp state (alive/respawning)
- `JungleCampConfig` is baked from SO (Plan 0088)
- But individual jungle creep AI behavior (patrol, aggro, leash, reset) is incomplete

Without this system, jungle creeps stand idle and never fight back or reset.

### Implementation

**Part A: Jungle creep patrol behavior (~80 lines)**
- Add `JungleCreepAI` component extending existing `UnitAIController`:
  - `fp2 CampOrigin`: spawn position (center of camp)
  - `fp LeashRadius`: maximum distance from camp before reset
  - `fp PatrolRadius`: small area around camp origin to wander
  - `void TickPatrol()`: if no target, pick random point within PatrolRadius, pathfind there
- Hook into existing `UnitAIController.Tick()` lifecycle

**Part B: Aggro and combat behavior (~120 lines)**
- `void TickAggro()`: scan for enemy units within aggro range
  - Use existing `PhysicsWorld` range query with `UnitTargetFilter` (enemy team, alive)
  - Prioritize closest enemy or enemy dealing damage to this camp
  - On aggro: switch to combat mode, pathfind toward target, attack when in range
- `void TickCombat()`: if target dead or out of LeashRadius, reset aggro

**Part C: Leash reset behavior (~60 lines)**
- When creep is pulled beyond `LeashRadius` from `CampOrigin`:
  - Clear current target
  - Begin returning to camp origin (pathfind back)
  - Rapidly regenerate health while returning (out-of-combat regen)
  - On reaching camp origin: reset to patrol mode, full health restore

**Part D: Camp respawn integration (~40 lines)**
- Wire `JungleCampSystem.RespawnCamp()` to spawn `JungleCreepAI` units via existing `UnitWorld.SpawnUnit()`
- Reset AI state on respawn
- Ensure deterministic spawn order (stable UID assignment)

### Files Changed
- `Assets/Scripts/Gameplay/NonHero/JungleCreepAI.cs` -- new file
- `Assets/Scripts/Gameplay/NonHero/UnitAIController.cs` -- integrate patrol/aggro phases
- `Assets/Scripts/Gameplay/NonHero/JungleCampSystem.cs` -- wire respawn -> AI
- `Assets/Scripts/Gameplay/NonHero/Tests/JungleCreepAITests.cs` -- new file

### Lines: ~300. Priority: HIGH (Gameplay core -- jungle is essential MOBA PvE content).

---

## Candidate 0111: Combat Kill Streak + Multikill Tracking (~200 lines)

### Problem

Combat Design v13.2 section 9 defines kill streak and multikill logic:
- **Kill Streak**: consecutive kills without dying. At 3/5/7/10+ kills, broadcast "Killing Spree" / "Dominating" / "Unstoppable" / "Legendary" events
- **Multikill**: multiple kills within a short time window (10 seconds). At 2/3/4/5 kills, broadcast "Double Kill" / "Triple Kill" / "Quadra Kill" / "Penta Kill" events
- **Shutdown Gold**: killing an enemy on a kill streak awards bonus gold proportional to streak length

Currently, MatchRuleRuntime.Statistics tracks kills/deaths/assists but does not compute kill streaks or multikills. Shutdown gold is not calculated.

### Implementation

**Part A: Kill streak tracking (~70 lines)**
- Add to `MatchRuleRuntime.Statistics`:
  - `Dictionary<int, int> PlayerKillStreaks` (playerSlot -> current streak)
  - `OnUnitKill`: increment killer's streak, reset victim's streak to 0
  - Emit `KillStreakEvent` when threshold crossed (3/5/7/10)
- Add `KillStreakTier` enum: None, KillingSpree, Dominating, Unstoppable, Legendary

**Part B: Multikill tracking (~70 lines)**
- Track `List<(int playerSlot, int killTick)> RecentKills` (last 300 ticks = 10 seconds)
- On each kill: prune old entries (>300 ticks ago), count same-player entries
- If count >= 2: emit `MultikillEvent` with tier (Double/Triple/Quadra/Penta)
- Add `MultikillTier` enum: Double, Triple, Quadra, Penta

**Part C: Shutdown gold (~40 lines)**
- When a player on kill streak is killed:
  - Compute bonus gold: `baseShutdownGold + streakLength * goldPerStreak`
  - Award to killer via existing `GoldIncome.RequestGoldIncome(killerSlot, shutdownGold, GoldIncomeReason.UnitKill)`
- Configurable via GameModeConfigAuthoring: baseShutdownGold, goldPerStreakKill

**Part D: Snapshot and events (~20 lines)**
- `KillStreakEvent` and `MultikillEvent` are Presentation-only (no Gameplay state)
- Publish via existing `VisualEventOutput` pipeline
- No GameplaySnapshot impact

### Files Changed
- `Assets/Scripts/FrameSync/MatchRuleRuntime.cs` -- +kill streak, +multikill, +shutdown gold
- `Assets/Scripts/FrameSync/MatchStatisticsRuntime.cs` -- +KillStreaks, +RecentKills tracking
- `Assets/Scripts/RuntimeConfig/GlobalGameplayData.cs` -- +shutdown gold config
- `Assets/Scripts/Gameplay/Presentation/KillEventDef.cs` -- new file (KillStreakEvent, MultikillEvent)

### Lines: ~200. Priority: MEDIUM (Gameplay polish -- kill feedback).

---

## Candidate 0112: Death Recap Data Pipeline (~200 lines)

### Problem

Combat Design v13.2 section 8 defines death recap: when a hero dies, the game records the last N sources of damage (3-5 entries) showing source unit, ability/attack name, and damage amount. This data is consumed by the death recap UI.

Currently, `CombatSystem` processes damage but does not accumulate death recap data. There is no pipeline to expose "what killed me" information to Presentation.

### Implementation

**Part A: DeathRecapData accumulation (~80 lines)**
- Add `DeathRecapTracker` to `CombatSystem`:
  - `Dictionary<UnitUid, List<DeathRecapEntry>> _recapData` (per target)
  - On each damage event: prepend entry to target's list (newest first)
  - Trim to `MaxRecapEntries` (default 5)
- `DeathRecapEntry` struct:
  - `UnitUid SourceUnitUid`, `int DamageAmount`, `DamageType Type`
  - `int AbilityId` (0 for attack), `int Tick`

**Part B: Death recap snapshot and clearance (~50 lines)**
- On unit death: capture recap data, clear from active tracker
- Pass to `MatchRuleRuntime.Statistics.DeathRecaps` for post-death consumption
- On unit respawn: data already cleared from previous death

**Part C: Presentation exposure (~40 lines)**
- Add `DeathRecapSnapshot` to `UiSnapshotDto`:
  - `List<DeathRecapUiEntry>` with source name, damage, percentage
  - Populate from `MatchRuleRuntime.Statistics.DeathRecaps[playerSlot]` on death event
- Wire via `LuaBridge` for Lua-side death recap UI

**Part D: Tests (~30 lines)**
- EditMode: unit takes damage from 3 sources, dies, recap data matches
- Verify recap data cleared on respawn
- Verify max entries enforced

### Files Changed
- `Assets/Scripts/Gameplay/Combat/CombatSystem.cs` -- +DeathRecapTracker
- `Assets/Scripts/FrameSync/MatchStatisticsRuntime.cs` -- +DeathRecaps storage
- `Assets/Scripts/FrameSync/UiSnapshotDto.cs` -- +DeathRecapSnapshot
- `Assets/Scripts/Bootstrap/LuaBridge.cs` -- wire death recap
- `Assets/Scripts/Gameplay/Tests/DeathRecapTests.cs` -- new file

### Lines: ~200. Priority: MEDIUM (Gameplay polish -- death feedback).

---

## Recommended Execution Order

| Order | Candidate | Lines | Priority | Summary |
|:-----:|-----------|------:|----------|---------|
| 1 | **0109** -- Skill Indicator Ground/Vector Rendering | ~250 | MEDIUM | Visual indicators for AoE circles and skillshot arrows |
| 2 | **0110** -- Jungle Camp AI Patrol/Aggro/Leash | ~300 | **HIGH** | Jungle creep behavior: patrol, aggro, leash reset, respawn |
| 3 | **0111** -- Kill Streak + Multikill + Shutdown Gold | ~200 | MEDIUM | "Killing Spree", "Double Kill", bounty gold on streak kills |
| 4 | **0112** -- Death Recap Data Pipeline | ~200 | MEDIUM | Track "what killed me" data for death recap UI |

**Total: ~950 lines across 4 candidates.**

**Rationale:**
- 0110 is the only HIGH priority candidate -- jungle is core MOBA PvE content and currently non-functional
- 0109 is MEDIUM Gameplay UX -- ground/vector indicators unlock the new cast models added in 0105
- 0111 and 0112 are MEDIUM Gameplay polish -- kill feedback and death recap are expected MOBA features
- All are generic framework, data-driven for future configuration
- None implement specific heroes, abilities, or production content

---

## Deferred Candidates (for future batches)

| # | What | Priority | Reason |
|---|------|----------|--------|
| D1 | Scoreboard + Minimap UI enhancement | MEDIUM | UI layer, lower priority per user directive |
| D2 | Lua scripting infrastructure enhancement | MEDIUM | Requires substantial Lua-side work |
| D3 | Network/Authority layer | DEFERRED | Phase 11/14 per ROADMAP |
| D4 | TeamBase system | DEFERRED | Requires map design |
| D5 | Specific hero/ability/buff/equipment content | DEFERRED | Out of scope (D-020) |
| D6 | Large-scale integration tests | LOW | Per user directive |

---
