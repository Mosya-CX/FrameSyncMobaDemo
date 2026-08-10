using FrameSyncMoba.Unit;
using FrameSyncMoba.Presentation;
using System.Collections.Generic;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public sealed class UnitAnimationDriver : MonoBehaviour
    {
        [SerializeField] private Animator _animator;

        private UnitPresentationHost _host;
        private UnitAnimationProfile _profile;
        private readonly HashSet<int> _parameterHashes = new HashSet<int>();
        private bool _wasAttacking;
        private int _lastAttackSequence = int.MinValue;
        private int _lastAbilityId;
        private int _lastAbilityStage = -1;
        private LifeState _lastLifeState = (LifeState)byte.MaxValue;
        private bool _wasMoving;
        private bool _wasCasting;
        private bool _hasDrivenFrame;

        private void Awake()
        {
            _host = GetComponent<UnitPresentationHost>();
            if (_animator == null)
                _animator = GetComponentInChildren<Animator>();
            _profile = _host != null ? _host.Profile : null;
            CacheAnimatorParameters();
        }

        private void LateUpdate()
        {
            if (_host == null || _host.OwnerUnit == null || _animator == null)
                return;

            UnitType unit = _host.OwnerUnit;
            if (_profile == null)
                _profile = _host.Profile;
            bool isDead = unit.LifeState == LifeState.Dead
                       || unit.LifeState == LifeState.Respawning;
            SetInteger(
                HashOrDefault(
                    _profile?.LifeStateHash ?? 0,
                    "LifeState"),
                (int)unit.LifeState);
            SetBool(
                HashOrDefault(
                    _profile?.IsControlledHash ?? 0,
                    "IsControlled"),
                unit.ControlledByPlayerSlot >= 0);

            if (unit.LifeState != _lastLifeState)
            {
                // LifeState parameter already drives the Animator transitions
                // (AnyState -> Death, Death -> Idle on respawn). No code-side
                // CrossFade is needed.
                _lastLifeState = unit.LifeState;
            }

            if (isDead)
            {
                SetBool(HashOrDefault(_profile?.IsMovingHash ?? 0, "IsMoving"), false);
                SetFloat(HashOrDefault(_profile?.MoveSpeedHash ?? 0, "MoveSpeed"), 0f);
                SetBool(HashOrDefault(_profile?.IsAttackingHash ?? 0, "IsAttacking"), false);
                SetBool(HashOrDefault(_profile?.IsCastingHash ?? 0, "IsCasting"), false);
                _wasMoving = false;
                _wasCasting = false;
                _wasAttacking = false;
                _hasDrivenFrame = true;
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
            SetBool(HashOrDefault(_profile?.IsMovingHash ?? 0, "IsMoving"), isMoving);
            SetFloat(HashOrDefault(_profile?.MoveSpeedHash ?? 0, "MoveSpeed"), moveSpeed);

            var attack = unit.AttackHandler;
            bool isAttacking = false;
            int attackSequence = 0;
            bool isRecovering = false;
            float attackMotionTime = 0f;
            if (attack != null)
            {
                var attackAnim =
                    attack.GetAnimationSnapshot();
                isAttacking = attackAnim.IsAttacking;
                attackSequence = attackAnim.SequenceIndex;
                if (attackAnim.ImpactCommitted)
                {
                    isRecovering = true;
                    attackMotionTime =
                        0.5f + 0.5f *
                        attackAnim.RecoveryProgress;
                }
                else if (isAttacking)
                    attackMotionTime =
                        0.5f * attackAnim.WindupProgress;
            }
            SetBool(HashOrDefault(_profile?.IsAttackingHash ?? 0, "IsAttacking"), isAttacking);
            SetBool(HashOrDefault(_profile?.IsAttackRecoveringHash ?? 0, "IsAttackRecovering"), isRecovering);
            SetInteger(HashOrDefault(_profile?.AttackSequenceIndexHash ?? 0, "AttackSequenceIndex"), attackSequence);
            SetFloat(HashOrDefault(_profile?.AttackMotionTimeHash ?? 0, "AttackMotionTime"), attackMotionTime);

            if (isAttacking &&
                (!_wasAttacking ||
                 attackSequence != _lastAttackSequence))
            {
                SetTrigger(HashOrDefault(_profile?.AttackStartHash ?? 0, "AttackStart"));
                _lastAttackSequence = attackSequence;
            }
            bool attackEnded =
                _wasAttacking &&
                !isAttacking;

            var abilityHandler = unit.AbilityHandler;
            bool isCasting = false;
            int abilityId = 0;
            int abilityStage = -1;
            int stageElapsedTicks = 0;
            if (abilityHandler != null)
            {
                var activeCasts = abilityHandler.ActiveCasts;
                if (activeCasts != null && activeCasts.Count > 0)
                {
                    // Prefer the first cast entry that actually has an
                    // animation binding. Persistent toggles (e.g. Varus W)
                    // sit in ActiveCasts without a binding and must not
                    // shadow a real cast (E/R/Q release).
                    ActiveAbilityCastInfo? selected = null;
                    for (int ci = 0;
                         ci < activeCasts.Count;
                         ci++)
                    {
                        var candidate = activeCasts[ci];
                        StageAnimationBinding binding =
                            default;
                        bool hasBinding =
                            _profile != null &&
                            _profile.TryGetStageBinding(
                                candidate.AbilityId,
                                candidate.StageKey,
                                out binding);
                        if (_profile == null ||
                            hasBinding)
                        {
                            selected = candidate;
                            break;
                        }
                    }
                    if (selected.HasValue)
                    {
                        isCasting = true;
                        abilityId = selected.Value.AbilityId;
                        abilityStage = selected.Value.StageKey;
                        stageElapsedTicks =
                            selected.Value.StageElapsedTicks;
                    }
                }
            }
            SetBool(HashOrDefault(_profile?.IsCastingHash ?? 0, "IsCasting"), isCasting);
            SetFloat(
                HashOrDefault(
                    _profile?.AbilityStageProgressHash ?? 0,
                    "AbilityStageProgress"),
                stageElapsedTicks);
            if (isCasting &&
                (abilityId != _lastAbilityId ||
                 abilityStage != _lastAbilityStage))
            {
                // Ability casts keep the profile stage binding (heroes use
                // named states today); minions have no casts.
                PlayAbilityStage(
                    abilityId,
                    abilityStage);
                _lastAbilityId = abilityId;
                _lastAbilityStage = abilityStage;
            }
            else if (!isCasting)
            {
                _lastAbilityId = 0;
                _lastAbilityStage = -1;
            }

            if (!isAttacking &&
                !isCasting &&
                (!_hasDrivenFrame ||
                 attackEnded ||
                 _wasCasting ||
                 isMoving != _wasMoving))
            {
                // IsMoving / MoveSpeed parameters drive Idle <-> Move.
            }
            _wasMoving = isMoving;
            _wasCasting = isCasting;
            _wasAttacking = isAttacking;
            _hasDrivenFrame = true;
        }

        private void CacheAnimatorParameters()
        {
            _parameterHashes.Clear();
            if (_animator == null)
                return;
            AnimatorControllerParameter[] parameters =
                _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
                _parameterHashes.Add(
                    parameters[i].nameHash);
        }

        private void PlayAbilityStage(
            int abilityId,
            int stageIndex)
        {
            if (_profile == null ||
                !_profile.TryGetStageBinding(
                    abilityId,
                    stageIndex,
                    out StageAnimationBinding binding) ||
                binding.StateNameHash == 0)
                return;
            _animator.CrossFade(
                binding.StateNameHash,
                0.04f,
                0,
                binding.StartNormalizedTime);
        }

        private static int HashOrDefault(
            int configuredHash,
            string defaultName)
        {
            return configuredHash != 0
                ? configuredHash
                : Animator.StringToHash(defaultName);
        }

        private void SetBool(int hash, bool value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetBool(hash, value);
        }

        private void SetFloat(int hash, float value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetFloat(hash, value);
        }

        private void SetInteger(int hash, int value)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetInteger(hash, value);
        }

        private void SetTrigger(int hash)
        {
            if (_parameterHashes.Contains(hash))
                _animator.SetTrigger(hash);
        }
    }
}
