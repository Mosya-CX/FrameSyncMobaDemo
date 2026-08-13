using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// On-hit strike effect for a ready-state buff: deals bonus magic damage
    /// equal to a ratio of the target's MaxHealth (capped for monsters) using
    /// the configured attack-effect source identity. The owning buff is not
    /// removed here — the buff owner (e.g. a fixed passive) consumes it on
    /// the next deterministic Tick, which keeps removal outside buff-store
    /// enumeration.
    /// </summary>
    [Serializable]
    public sealed class MaxHealthOnHitBuffEffect : BuffEffect
    {
        public int SourceAbilityId;
        public int RecipeId;
        public int[] MaxHealthRatioBasisPointsByUnitLevel =
            Array.Empty<int>();
        public int[] MonsterDamageCapByUnitLevel =
            Array.Empty<int>();

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnHitDealt(
            BuffRuntime runtime,
            Unit owner,
            in OnHitEventData data)
        {
            if (data.IsRepeated ||
                owner?.World?.CombatSystem == null ||
                owner.BuffHandler == null ||
                !owner.World.TryGetUnit(
                    data.TargetUid,
                    out Unit target) ||
                target.UnitKind == UnitKind.Structure ||
                target.StatHandler == null)
            {
                return;
            }

            int levelIndex =
                owner.Level <= 1 ? 0 : owner.Level - 1;
            if (levelIndex >=
                MaxHealthRatioBasisPointsByUnitLevel.Length)
            {
                return;
            }
            fp ratio =
                (fp)MaxHealthRatioBasisPointsByUnitLevel[
                    levelIndex] /
                (fp)10000;
            fp damage =
                target.StatHandler.GetStat(
                    StatId.MaxHealth) *
                ratio;
            if (target.UnitKind == UnitKind.Monster &&
                MonsterDamageCapByUnitLevel != null &&
                MonsterDamageCapByUnitLevel.Length > 0)
            {
                int capIndex = levelIndex;
                if (capIndex >=
                    MonsterDamageCapByUnitLevel.Length)
                {
                    capIndex =
                        MonsterDamageCapByUnitLevel.Length - 1;
                }
                fp cap = MonsterDamageCapByUnitLevel[capIndex];
                if (damage > cap) damage = cap;
            }
            if (damage <= fp.zero)
            {
                return;
            }

            var request = new DamageRequest
            {
                Header = CombatRequestHeader.Create(
                    owner.UnitUid,
                    target.UnitUid,
                    CombatSourceType.AttackEffect,
                    SourceAbilityId,
                    RecipeId),
                BaseDamage = damage,
                DamageType = DamageType.Magic,
            };
            owner.World.CombatSystem.SubmitDamage(request);
        }
    }
}
