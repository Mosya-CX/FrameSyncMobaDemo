using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private UnitPresentationHost _host;

        private static readonly int ParamLifeState = Animator.StringToHash("LifeState");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int ParamAttackSpeed = Animator.StringToHash("AttackSpeed");
        private static readonly int ParamAttackSequence = Animator.StringToHash("AttackSequence");
        private static readonly int ParamAttackPhase = Animator.StringToHash("AttackPhase");
        private static readonly int ParamWindupProgress = Animator.StringToHash("WindupProgress");
        private static readonly int ParamIsCasting = Animator.StringToHash("IsCasting");
        private static readonly int ParamCastStage = Animator.StringToHash("CastStage");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        private static readonly int ParamHitReaction = Animator.StringToHash("HitReaction");
        private static readonly int ParamHitReactionKind = Animator.StringToHash("HitReactionKind");
        private static readonly int ParamHitReactionProgress = Animator.StringToHash("HitReactionProgress");

        private void Awake()
        {
            _host = GetComponent<UnitPresentationHost>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
        }

        private void LateUpdate()
        {
            if (_host == null || _host.OwnerUnit == null || _animator == null)
                return;

            UnitType unit = _host.OwnerUnit;

            bool isDead = unit.LifeState == LifeState.Dead
                       || unit.LifeState == LifeState.Respawning;
            _animator.SetBool(ParamIsDead, isDead);
            _animator.SetInteger(ParamLifeState, (int)unit.LifeState);

            if (isDead)
            {
                _animator.SetBool(ParamIsMoving, false);
                _animator.SetFloat(ParamMoveSpeed, 0f);
                _animator.SetBool(ParamIsAttacking, false);
                _animator.SetBool(ParamIsCasting, false);
                return;
            }

            var movement = unit.MovementHandler;
            bool isMoving = false;
            float moveSpeed = 0f;
            if (movement != null)
            {
                isMoving = movement.IsMoving;
                if (isMoving)
                {
                    float sx = (float)movement.Velocity.x;
                    float sy = (float)movement.Velocity.y;
                    moveSpeed = Mathf.Sqrt(sx * sx + sy * sy);
                }
            }
            _animator.SetBool(ParamIsMoving, isMoving);
            _animator.SetFloat(ParamMoveSpeed, moveSpeed);

            var attack = unit.AttackHandler;
            bool isAttacking = false;
            float attackSpeed = 1f;
            int attackSequence = 0;
            float attackPhase = 0f;
            float windupProgress = 0f;
            if (attack != null)
            {
                var attackAnim = attack.GetAnimationSnapshot();
                isAttacking = attackAnim.IsAttacking;
                attackSequence = attackAnim.SequenceIndex;
                windupProgress = attackAnim.WindupProgress;

                if (attackAnim.ImpactCommitted)
                    attackPhase = 0.5f + 0.5f * attackAnim.RecoveryProgress;
                else if (isAttacking)
                    attackPhase = 0.5f * windupProgress;

                var attackSnap = attack.Snapshot;
                if (attackSnap.NextAttackReadyLogicTick > attackSnap.AttackStartLogicTick)
                {
                    int totalTicks = attackSnap.NextAttackReadyLogicTick - attackSnap.AttackStartLogicTick;
                    attackSpeed = totalTicks > 0 ? 1f / totalTicks : 1f;
                }
            }
            _animator.SetBool(ParamIsAttacking, isAttacking);
            _animator.SetFloat(ParamAttackSpeed, attackSpeed);
            _animator.SetInteger(ParamAttackSequence, attackSequence);
            _animator.SetFloat(ParamAttackPhase, attackPhase);
            _animator.SetFloat(ParamWindupProgress, windupProgress);

            var abilityHandler = unit.AbilityHandler;
            bool isCasting = false;
            int castStage = 0;
            if (abilityHandler != null)
            {
                var activeCasts = abilityHandler.ActiveCasts;
                if (activeCasts != null && activeCasts.Count > 0)
                {
                    var first = activeCasts[0];
                    isCasting = true;
                    castStage = first.StageKey;
                }
            }
            _animator.SetBool(ParamIsCasting, isCasting);
            _animator.SetInteger(ParamCastStage, castStage);

            var hit = unit.HitReaction;
            bool hasHitReaction = hit.IsActive;
            float hitReactionProgress = 0f;
            _animator.SetBool(ParamHitReaction, hasHitReaction);
            if (hasHitReaction)
            {
                _animator.SetInteger(ParamHitReactionKind, (int)hit.ActiveReaction);
                if (hit.TotalTicks > 0)
                    hitReactionProgress = 1f - (float)hit.RemainingTicks / (float)hit.TotalTicks;
            }
            _animator.SetFloat(ParamHitReactionProgress, hitReactionProgress);
        }
    }
}
