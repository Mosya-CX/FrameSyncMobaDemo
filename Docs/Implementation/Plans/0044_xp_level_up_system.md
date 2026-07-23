# Plan 0044: XP & Level-Up System

> Status: Completed
> Created: 2026-07-22
> Design: `Docs/Design/unit_behavior_framework_design_v27_3.md` §7.4, `Docs/Design/moba_combat_system_design_v13_2.md` §DeathReward
> Predecessor: 0043 On-Hit Effect Pipeline
> Lines target: ~450

## Scope

Implement the full experience and level-up pipeline: XP tracking, level thresholds, kill XP rewards, stat growth on level-up, skill point granting, and level-based respawn scaling.

### New files — Unit/XP/

| # | File | Lines | Purpose |
|---|---|---|---|
| 1 | `Unit/XP/LevelExperienceConfig.cs` | ~95 | Config: CanLevelUp, InitialLevel, MaxLevel, InitialExperience, RequiredExperiencePerLevel, HealthOnLevelUp, CastResourceOnLevelUp, LevelUpCurrentValueRule enum, CreateDefault18() factory |
| 2 | `Unit/XP/ExperienceTracker.cs` | ~130 | Per-unit XP state: Level, TotalExperience, ExperienceToNextLevel, PendingSkillPoints. GrantExperience() → processes consecutive level-ups → updates StatHandler.Level → returns LevelUpResult. IRollback support. |
| 3 | `Unit/XP/ExperienceTrackerSnapshot.cs` | ~20 | Snapshot struct for cross-Tick state. |
| 4 | `Unit/XP/XpRewardTable.cs` | ~55 | Static kill/assist XP reward lookup: GetKillXpReward(victimLevel, killerLevel), GetAssistXpReward(). Level differential penalty. |

### Modified files

| # | File | Change |
|---|---|---|
| 5 | `Unit/Stats/StatId.cs` | +1: Add `CurrentExperience = 25` |
| 6 | `Unit/Stats/StatPreset.cs` | +1: Add `LevelExperienceConfig LevelExperience` property |
| 7 | `Unit/Core/Unit.cs` | +2: Add `ExperienceTracker Xp`, `int Level` derived property |
| 8 | `Unit/Core/UnitWorld.cs` | +35: Initialize ExperienceTracker in SpawnUnit from prototype config; add `GrantExperience(UnitUid, int)` method with skill point granting and level-up event publishing |
| 9 | `Unit/Combat/DeathEffectDispatcher.cs` | +25: Fill DistributeXpToHero (was empty stub) — compute victim level, get XP reward from XpRewardTable, call UnitWorld.GrantExperience. Now instance method (was static). |
| 10 | `Unit/Combat/CombatSystem.cs` | +5: Update `GetRespawnDelay(Unit)` from fixed 1800 ticks → `300 + (Level-1)*60` ticks |
| 11 | `Unit/Combat/CombatEvents.cs` | +5: Add `RaiseLevelUp(UnitUid, int previousLevel, int newLevel)` |
| 12 | `Unit/Core/UnitEventBus.cs` | +15: Add LevelUpHandler delegate, OnLevelUp event, PublishLevelUp method, clear in Clear() |
| 13 | `FrameSync/GameplaySnapshot.cs` | +1: Add `ExperienceTrackerSnapshot XpState` to UnitSnapshot |
| 14 | `FrameSync/SimulationTickPipeline.cs` | +2: Capture/Restore XpState in aggregate snapshot |

## Key design conformance

- Unit v27.3 §7.4: LevelExperienceConfig with CanLevelUp, InitialLevel, MaxLevel, RequiredExperiencePerLevel
- Unit v27.3 §7.4.1: LevelUpCurrentValueRule.KeepCurrent for health/cast resource on level-up
- Unit v27.3 §5.1: StatHandler.Level recalculation via existing L = max(Level-1,0) formula
- Combat v13.2 §DeathReward: XP distribution on kill/assist, level-differential scaling
- Ability v15.2: Skill point granting on level-up via AbilityHandler.GrantSkillPoint()
- Snapshot v7.2: ExperienceTracker state captured in GameplaySnapshot for rollback

## Level-up flow

```
DeathEffectDispatcher.DispatchDeathEffects(death)
  → DistributeXpToHero(killer, death)
    → XpRewardTable.GetKillXpReward(victimLevel, killerLevel)
    → UnitWorld.GrantExperience(killerUid, xpAmount)
      → ExperienceTracker.GrantExperience(amount)
        → totalExperience += amount
        → while totalExperience >= threshold:
            → level++, skillPoints++
            → StatHandler.Level = newLevel  (dirties all entries → recalc on next GetStat)
        → AbilityHandler.GrantSkillPoint() × N
        → CombatEvents.RaiseLevelUp(unitUid, prevLevel, newLevel)
```

## Remaining limitations

- Level-up does not yet heal/resource-fill (LevelUpCurrentValueRule.KeepCurrent only)
- No UI bridge for level-up notification
- No XP bar or HUD integration (presentation-layer deferred)
- RespawnTimer level scaling is fixed formula; no phase multiplier yet (deferred to MatchRuleRuntime plan)
