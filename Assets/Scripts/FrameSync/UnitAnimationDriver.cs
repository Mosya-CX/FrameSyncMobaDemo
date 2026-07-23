using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// MonoBehaviour that reads deterministic Gameplay state each frame
    /// and drives the owner Unit's Animator accordingly.
    ///
    /// Does NOT write root Transform (PhysicsEntity2D owns that).
    /// Does NOT read AbilityCastEvent or other presentation events.
    /// </summary>
    public sealed class UnitAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private UnitPresentationHost _host;

        private static readonly int ParamLifeState = Animator.StringToHash("LifeState");
        private static readonly int ParamIsMoving = Animator.StringToHash("IsMoving");
        private static readonly int ParamMoveSpeed = Animator.StringToHash("MoveSpeed");
        private static readonly int ParamIsAttacking = Animator.StringToHash("IsAttacking");
        private static readonly int ParamAttackSpeed = Animator.StringToHash("AttackSpeed");
        private static readonly int ParamIsCasting = Animator.StringToHash("IsCasting");
        private static readonly int ParamCastStage = Animator.StringToHash("CastStage");
        private static readonly int ParamIsDead = Animator.StringToHash("IsDead");
        private static readonly int ParamHitReaction = Animator.StringToHash("HitReaction");
        private static readonly int ParamHitReactionKind = Animator.StringToHash("HitReactionKind");

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

            // LifeState / death
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

            // Movement
            var movement = unit.MovementHandler;
            bool isMoving = false;
            float moveSpeed = 0f;
            if (movement != null)
            {
                var snap = movement.Snapshot;
                isMoving = snap.IsMoving;
                if (isMoving)
                {
                    float sx = (float)snap.Velocity.x;
                    float sy = (float)snap.Velocity.y;
                    moveSpeed = Mathf.Sqrt(sx * sx + sy * sy);
                }
            }
            _animator.SetBool(ParamIsMoving, isMoving);
            _animator.SetFloat(ParamMoveSpeed, moveSpeed);

            // Attack
            var attack = unit.AttackHandler;
            bool isAttacking = false;
            float attackSpeed = 1f;
            if (attack != null)
            {
                var attackSnap = attack.Snapshot;
                int now = Deterministic.SimulationTickContext.Current.Tick;
                isAttacking = attackSnap.CurrentTargetUid.IsValid()
                           && now >= attackSnap.AttackStartLogicTick
                           && now < attackSnap.NextAttackReadyLogicTick
                           && !attackSnap.ImpactCommitted;
                if (attackSnap.NextAttackReadyLogicTick > attackSnap.AttackStartLogicTick)
                {
                    int totalTicks = attackSnap.NextAttackReadyLogicTick - attackSnap.AttackStartLogicTick;
                    attackSpeed = totalTicks > 0 ? 1f / totalTicks : 1f;
                }
            }
            _animator.SetBool(ParamIsAttacking, isAttacking);
            _animator.SetFloat(ParamAttackSpeed, attackSpeed);

            // Ability cast — deferred: AbilityHandler does not expose ActiveSession publicly.
            // When AbilityCastView or a public session getter is added, wire here.
            _animator.SetBool(ParamIsCasting, false);
            _animator.SetInteger(ParamCastStage, 0);

            // Hit reaction
            var hit = unit.HitReaction;
            bool hasHitReaction = hit.IsActive;
            _animator.SetBool(ParamHitReaction, hasHitReaction);
            if (hasHitReaction)
            {
                _animator.SetInteger(ParamHitReactionKind, (int)hit.ActiveReaction);
            }
        }
    }
}
