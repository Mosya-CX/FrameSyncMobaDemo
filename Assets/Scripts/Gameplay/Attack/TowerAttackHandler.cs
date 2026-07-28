using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;
using UnitUid = FrameSyncMoba.Unit.UnitUid;
using UnitKind = FrameSyncMoba.Unit.UnitKind;
using LifeState = FrameSyncMoba.Unit.LifeState;
using TeamId = FrameSyncMoba.Unit.TeamId;
using UnitDisposePolicyConfig = FrameSyncMoba.Unit.UnitDisposePolicyConfig;
using UnitDisposePolicyKind = FrameSyncMoba.Unit.UnitDisposePolicyKind;
using UnitPrototype = FrameSyncMoba.Unit.UnitPrototype;
using UnitWorld = FrameSyncMoba.Unit.UnitWorld;
using UnitSpawnRequest = FrameSyncMoba.Unit.UnitSpawnRequest;
using AttackHandler = FrameSyncMoba.Unit.AttackHandler;

namespace FrameSyncMoba.Gameplay.Attack
{
    public class TowerAttackHandler
    {
        private readonly UnitType _owner;
        private readonly AttackHandler _baseAttackHandler;

        private UnitUid _currentTarget;
        private int _lockExpireTick;
        private int _cooldownUntilTick;

        private static readonly fp TowerAttackRange = (fp)8m;
        private static readonly int TowerAttackCooldownTicks = 45;
        private static readonly int TargetLockDurationTicks = 90;

        public TowerAttackHandler(UnitType owner, AttackHandler baseAttackHandler)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _baseAttackHandler = baseAttackHandler ?? throw new ArgumentNullException(nameof(baseAttackHandler));
        }

        public UnitUid CurrentTarget => _currentTarget;
        public bool HasTarget => _currentTarget.IsValid();
        public bool IsOnCooldown => SimulationTickContext.Current.Tick < _cooldownUntilTick;

        public void EvaluateTarget()
        {
            int currentTick = SimulationTickContext.Current.Tick;
            if (_currentTarget.IsValid() && currentTick < _lockExpireTick && IsValidTowerTarget(_currentTarget))
                return;
            _currentTarget = default;
            UnitUid bestTarget = FindBestTowerTarget();
            if (bestTarget.IsValid())
            {
                _currentTarget = bestTarget;
                _lockExpireTick = currentTick + TargetLockDurationTicks;
            }
        }

        public void SubmitAttack()
        {
            if (!_currentTarget.IsValid()) return;
            int currentTick = SimulationTickContext.Current.Tick;
            if (currentTick < _cooldownUntilTick) return;
            if (_baseAttackHandler != null && IsValidTowerTarget(_currentTarget))
            {
                _baseAttackHandler.ApplyAttackInput(_currentTarget);
                _cooldownUntilTick = currentTick + TowerAttackCooldownTicks;
            }
        }

        private UnitUid FindBestTowerTarget()
        {
            if (_owner.World == null) return default;
            var allUnits = _owner.World.GetAllUnits();
            fp2 towerPos = _owner.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
            TeamId myTeam = _owner.TeamId;
            UnitUid bestTarget = default;
            int bestPriority = int.MaxValue;
            fp bestDistance = new fp(int.MaxValue);
            for (int i = 0; i < allUnits.Count; i++)
            {
                UnitType candidate = allUnits[i];
                if (candidate == _owner) continue;
                if (candidate.TeamId == myTeam) continue;
                if (candidate.LifeState != LifeState.Alive) continue;
                fp2 candidatePos = candidate.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
                fp dist = fpmath.distance(towerPos, candidatePos);
                if (dist > TowerAttackRange) continue;
                int priority = GetTowerTargetPriority(candidate);
                if (priority < bestPriority || (priority == bestPriority && dist < bestDistance))
                {
                    bestPriority = priority;
                    bestDistance = dist;
                    bestTarget = candidate.UnitUid;
                }
                else if (priority == bestPriority && dist == bestDistance
                    && bestTarget.CompareTo(candidate.UnitUid) > 0)
                {
                    bestTarget = candidate.UnitUid;
                }
            }
            return bestTarget;
        }

        private int GetTowerTargetPriority(UnitType candidate)
        {
            TeamId myTeam = _owner.TeamId;
            if (candidate.AttackHandler != null)
            {
                UnitUid theirTarget = candidate.AttackHandler.CurrentTargetUid;
                if (theirTarget.IsValid()
                    && _owner.World.TryGetUnit(theirTarget, out UnitType victim)
                    && victim.TeamId == myTeam)
                {
                    if (victim.UnitKind == UnitKind.Hero) return 0;
                    if (victim.UnitKind == UnitKind.Minion) return 1;
                }
            }
            switch (candidate.UnitKind)
            {
                case UnitKind.Minion: return 2;
                case UnitKind.Hero: return 3;
                case UnitKind.Monster: return 4;
                default: return 5;
            }
        }

        private bool IsValidTowerTarget(UnitUid targetUid)
        {
            if (!targetUid.IsValid()) return false;
            if (_owner.World == null) return false;
            if (!_owner.World.TryGetUnit(targetUid, out UnitType target)) return false;
            if (target.LifeState != LifeState.Alive) return false;
            if (target.TeamId == _owner.TeamId) return false;
            fp2 towerPos = _owner.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
            fp2 targetPos = target.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
            return fpmath.distance(towerPos, targetPos) <= TowerAttackRange;
        }

        public void Clear() { _currentTarget = default; _lockExpireTick = 0; _cooldownUntilTick = 0; }

        public void Capture(ref TowerAttackHandlerSnapshot snapshot)
        {
            snapshot.CurrentTarget = _currentTarget;
            snapshot.LockExpireTick = _lockExpireTick;
            snapshot.CooldownUntilTick = _cooldownUntilTick;
        }

        public void Restore(in TowerAttackHandlerSnapshot snapshot)
        {
            _currentTarget = snapshot.CurrentTarget;
            _lockExpireTick = snapshot.LockExpireTick;
            _cooldownUntilTick = snapshot.CooldownUntilTick;
        }
    }

    [Serializable]
    public struct TowerAttackHandlerSnapshot
    {
        public UnitUid CurrentTarget;
        public int LockExpireTick;
        public int CooldownUntilTick;
    }
}
