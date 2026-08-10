using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class DeathEffectDispatcher
    {
        private readonly UnitWorld _unitWorld;
        private readonly CombatSystem _combatSystem;

        public DeathEffectDispatcher(UnitWorld unitWorld, CombatSystem combatSystem)
        {
            _unitWorld = unitWorld;
            _combatSystem = combatSystem;
        }

        public void DispatchDeathEffects(DeathResult death)
        {
            if (!death.VictimUid.IsValid()) return;
            if (!_unitWorld.TryGetUnit(death.VictimUid, out Unit victim)) return;

            DistributeExperience(death);
            DistributeGold(death, victim);
            FireOnKillEvents(death, victim);
        }

        private void DistributeExperience(DeathResult death)
        {
            if (death.KillerHeroUid.IsValid()
                && _unitWorld.TryGetUnit(death.KillerHeroUid, out Unit killer))
            {
                DistributeXpToHero(killer, death);
            }

            if (death.AssistantHeroUids != null)
            {
                fp assistXp = ComputeAssistXp(death);
                for (int i = 0; i < death.AssistantHeroUids.Length; i++)
                {
                    if (_unitWorld.TryGetUnit(death.AssistantHeroUids[i], out Unit assistant))
                    {
                        DistributeXpToHero(assistant, death, isAssist: true);
                    }
                }
            }
        }

        private void DistributeGold(DeathResult death, Unit victim)
        {
            // Gold allocation is produced by CombatSystem.GoldAllocations and
            // wired to GoldIncomeRuntime in SimulationTickPipeline.
            // This method serves as a hook for any additional gold-related
            // death effects (e.g., bounty streak bonuses, shutdown gold).
            // Base kill gold is handled via GoldAllocation in CombatSystem.ResolveDying.
        }

        private void FireOnKillEvents(DeathResult death, Unit victim)
        {
            CombatEvents.RaiseUnitDeath(victim.UnitUid, death.KillerHeroUid);

            if (death.KillerHeroUid.IsValid()
                && _unitWorld.TryGetUnit(death.KillerHeroUid, out Unit killer))
            {
                CombatEvents.RaiseUnitKill(killer.UnitUid, death.VictimUid);
            }
        }

        private void DistributeXpToHero(Unit hero, DeathResult death, bool isAssist = false)
    {
            if (hero.StatHandler == null || !hero.StatHandler.CanLevelUp) return;
            if (hero.LifeState != LifeState.Alive && hero.LifeState != LifeState.Dying) return;

            int xpReward;
            // Look up victim level for XP scaling
            int victimLevel = 1;
            if (death.VictimUid.IsValid() && _unitWorld.TryGetUnit(death.VictimUid, out Unit victimUnit))
            {
                victimLevel = victimUnit.Level;
            }

            if (isAssist)
            {
                int assistantCount = death.AssistantHeroUids?.Length ?? 1;
                xpReward = XpRewardTable.GetAssistXpReward(victimLevel, hero.Level, assistantCount);
            }
            else
            {
                xpReward = XpRewardTable.GetKillXpReward(victimLevel, hero.Level);
            }

            if (xpReward <= 0) return;

            _unitWorld.GrantExperience(hero.UnitUid, xpReward);
        }

        private static fp ComputeAssistXp(DeathResult death)
        {
            return fp.zero;
        }
    }
}
