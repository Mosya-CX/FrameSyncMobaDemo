using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class ProjectileRuntime
    {
        public ProjectileUid Uid { get; }
        public ProjectileDef Def { get; }
        public UnitUid OwnerUnitUid { get; }
        public TeamId TeamSnapshot { get; }
        public fp2 Position { get; private set; }
        public fp2 PrevPosition { get; private set; }
        public fp2 Velocity { get; private set; }
        public int RemainingLifetimeTicks { get; private set; }
        public bool IsActive { get; private set; }
        public int HitCount { get; private set; }
        public List<UnitUid> HitTargets { get; } = new List<UnitUid>();

        public ProjectileRuntime(
            ProjectileUid uid, ProjectileDef def, UnitUid ownerUnitUid, TeamId teamSnapshot,
            fp2 startPosition, fp2 direction)
        {
            Uid = uid; Def = def; OwnerUnitUid = ownerUnitUid;
            TeamSnapshot = teamSnapshot;
            Position = startPosition; Velocity = direction * def.Speed;
            RemainingLifetimeTicks = def.MaxLifetimeTicks; IsActive = true;
        }

        public void TickUpdate()
        {
            if (!IsActive) return;
            int delta = 1;
            RemainingLifetimeTicks -= delta;
            if (RemainingLifetimeTicks <= 0)
            {
                IsActive = false;
                return;
            }
            PrevPosition = Position;
            Position += Velocity;
            if (Def.Acceleration != fp.zero)
            {
                var speed = fpmath.length(Velocity) + Def.Acceleration;
                if (speed > fp.zero)
                    Velocity = fpmath.normalize(Velocity) * speed;
            }
        }

        public bool CanHitTarget(UnitUid targetUid)
        {
            for (int i = 0; i < HitTargets.Count; i++)
                if (HitTargets[i] == targetUid) return false;
            return true;
        }

        public void RegisterHit(UnitUid targetUid)
        {
            HitTargets.Add(targetUid); HitCount++;
            if (Def.DestroyOnFirstHit || HitCount >= Def.MaxHitCount)
                IsActive = false;
        }

        public void Destroy()
        {
            IsActive = false;
        }

        internal void RestoreFromSnapshot(in ProjectileRuntimeSnapshot snapshot)
        {
            Position = snapshot.Position; PrevPosition = snapshot.PreviousPosition; Velocity = snapshot.Velocity;
            RemainingLifetimeTicks = snapshot.RemainingLifetimeTicks;
            IsActive = snapshot.IsActive;
            HitCount = snapshot.HitCount;
            HitTargets.Clear();
            if (snapshot.HitTargets != null) HitTargets.AddRange(snapshot.HitTargets);
        }
    }
}
