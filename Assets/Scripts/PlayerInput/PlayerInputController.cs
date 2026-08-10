using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FrameSyncMoba.PlayerInput
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputController : MonoBehaviour
    {
        [Header("Input System authoring")]
        [SerializeField] private InputActionAsset inputActions;

        private InputActionMap gameplayMap;
        private InputAction pointerPosition;
        private InputAction primaryClick;
        private InputAction secondaryClick;
        private InputAction cancel;
        private InputAction abilityQ;
        private InputAction abilityW;
        private InputAction abilityE;
        private InputAction abilityR;
        private InputAction expandStats;

        private LocalInputEventBuffer buffer;
        private MouseWorldResolver pointerResolver;
        private PlayerCommandRequester commandRequester;
        private bool subscribed;
        private SkillIndicatorDriver indicatorDriver;

        public LocalInputEventBuffer Buffer => buffer;
        public PlayerCommandRequester CommandRequester =>
            commandRequester;

        public void Initialize(
            LocalInputEventBuffer eventBuffer,
            MouseWorldResolver mouseWorldResolver,
            PlayerCommandRequester requester)
        {
            buffer = eventBuffer ?? throw new ArgumentNullException(nameof(eventBuffer));
            pointerResolver = mouseWorldResolver
                ?? throw new ArgumentNullException(nameof(mouseWorldResolver));
            commandRequester = requester ?? throw new ArgumentNullException(nameof(requester));
            CacheActionsOrThrow();
            if (isActiveAndEnabled)
            {
                SubscribeActions();
            }
        }

        /// <summary>
        /// Set the indicator driver for ability aim visual feedback.
        /// </summary>
        public void SetIndicatorDriver(SkillIndicatorDriver driver)
        {
            indicatorDriver = driver;
        }

        private void OnEnable()
        {
            if (inputActions == null || buffer == null) return;
            CacheActionsOrThrow();
            SubscribeActions();
        }

        private void OnDisable()
        {
            UnsubscribeActions();
        }

        private void LateUpdate()
        {
            if (buffer == null || commandRequester == null) return;
            if (pointerResolver != null && pointerPosition != null)
            {
                pointerResolver.LastScreenPosition =
                    pointerPosition.ReadValue<Vector2>();
            }
            commandRequester.ProcessFrame(buffer, pointerResolver);
            UpdateIndicator();
        }

        private void UpdateIndicator()
        {
            if (indicatorDriver == null || pointerResolver == null) return;
            if (commandRequester == null || commandRequester.ControlledUnit == null) return;

            // Show and follow the indicator while the player is locally
            // aiming (E/R) or holding a Focus session (Q hold-release).
            for (byte slot = 0; slot < 4; slot++)
            {
                ref readonly var state = ref commandRequester.GetAbilityState(slot);
                if (state.Kind == LocalAbilityInputStateKind.LocalAiming ||
                    state.Kind == LocalAbilityInputStateKind.GameplayFocusing)
                {
                    // Get the aim kind and cast range for this slot
                    if (commandRequester.TryGetAimInfo(slot, out var aimKind, out var castRange, out var casterPos, out var casterForward))
                    {
                        fp groundRadius = fp.zero;
                        commandRequester.TryGetGroundTargetRadius(
                            slot,
                            out groundRadius);
                        if (!indicatorDriver.IsVisible || indicatorDriver.ActiveKind != aimKind)
                        {
                            indicatorDriver.Show(
                                aimKind,
                                castRange,
                                casterPos,
                                casterForward,
                                true,
                                groundRadius);
                        }

                        Vector2 screenPos = pointerResolver != null
                            ? pointerResolver.LastScreenPosition
                            : Vector2.zero;
                        var cursorWorld = pointerResolver.ResolveGroundPoint(screenPos);
                        if (cursorWorld.HasValue)
                        {
                            indicatorDriver.UpdateCursor(cursorWorld.Value, casterPos, casterForward);
                        }
                    }
                    return;
                }
            }

            // No slot is aiming — hide indicator
            if (indicatorDriver.IsVisible)
            {
                indicatorDriver.Hide();
            }
        }

        private void CacheActionsOrThrow()
        {
            if (inputActions == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(PlayerInputController)} requires an InputActionAsset.");
            }

            gameplayMap = inputActions.FindActionMap("Gameplay", true);
            pointerPosition = gameplayMap.FindAction("PointerPosition", true);
            primaryClick = gameplayMap.FindAction("PrimaryClick", true);
            secondaryClick = gameplayMap.FindAction("SecondaryClick", true);
            cancel = gameplayMap.FindAction("Cancel", true);
            abilityQ = gameplayMap.FindAction("AbilityQ", true);
            abilityW = gameplayMap.FindAction("AbilityW", true);
            abilityE = gameplayMap.FindAction("AbilityE", true);
            abilityR = gameplayMap.FindAction("AbilityR", true);
            expandStats = gameplayMap.FindAction(
                "ExpandStats",
                true);
        }

        private void SubscribeActions()
        {
            if (subscribed) return;
            primaryClick.performed += OnPrimaryClick;
            secondaryClick.performed += OnSecondaryClick;
            cancel.performed += OnCancel;
            abilityQ.performed += OnAbilityQPressed;
            abilityQ.canceled += OnAbilityQReleased;
            abilityW.performed += OnAbilityWPressed;
            abilityW.canceled += OnAbilityWReleased;
            abilityE.performed += OnAbilityEPressed;
            abilityE.canceled += OnAbilityEReleased;
            abilityR.performed += OnAbilityRPressed;
            abilityR.canceled += OnAbilityRReleased;
            expandStats.performed += OnExpandStatsPressed;
            expandStats.canceled += OnExpandStatsReleased;
            subscribed = true;
            gameplayMap.Enable();
        }

        private void UnsubscribeActions()
        {
            if (!subscribed) return;
            primaryClick.performed -= OnPrimaryClick;
            secondaryClick.performed -= OnSecondaryClick;
            cancel.performed -= OnCancel;
            abilityQ.performed -= OnAbilityQPressed;
            abilityQ.canceled -= OnAbilityQReleased;
            abilityW.performed -= OnAbilityWPressed;
            abilityW.canceled -= OnAbilityWReleased;
            abilityE.performed -= OnAbilityEPressed;
            abilityE.canceled -= OnAbilityEReleased;
            abilityR.performed -= OnAbilityRPressed;
            abilityR.canceled -= OnAbilityRReleased;
            expandStats.performed -= OnExpandStatsPressed;
            expandStats.canceled -= OnExpandStatsReleased;
            gameplayMap.Disable();
            subscribed = false;
        }

        private void OnPrimaryClick(InputAction.CallbackContext context) =>
            Push(LocalGameplayInputEventKind.PrimaryClick, 0);

        private void OnSecondaryClick(InputAction.CallbackContext context) =>
            Push(LocalGameplayInputEventKind.SecondaryClick, 0);

        private void OnCancel(InputAction.CallbackContext context) =>
            Push(LocalGameplayInputEventKind.Cancel, 0);

        private void OnAbilityQPressed(InputAction.CallbackContext context) => PushAbilityPressed(0);
        private void OnAbilityQReleased(InputAction.CallbackContext context) => PushAbilityReleased(0);
        private void OnAbilityWPressed(InputAction.CallbackContext context) => PushAbilityPressed(1);
        private void OnAbilityWReleased(InputAction.CallbackContext context) => PushAbilityReleased(1);
        private void OnAbilityEPressed(InputAction.CallbackContext context) => PushAbilityPressed(2);
        private void OnAbilityEReleased(InputAction.CallbackContext context) => PushAbilityReleased(2);
        private void OnAbilityRPressed(InputAction.CallbackContext context) => PushAbilityPressed(3);
        private void OnAbilityRReleased(InputAction.CallbackContext context) => PushAbilityReleased(3);

        private void OnExpandStatsPressed(
            InputAction.CallbackContext context) =>
            PresentationInputState.ExpandStatsHeld = true;

        private void OnExpandStatsReleased(
            InputAction.CallbackContext context) =>
            PresentationInputState.ExpandStatsHeld = false;

        private void PushAbilityPressed(byte slot) =>
            Push(LocalGameplayInputEventKind.AbilityKeyPressed, slot);

        private void PushAbilityReleased(byte slot) =>
            Push(LocalGameplayInputEventKind.AbilityKeyReleased, slot);

        private void Push(LocalGameplayInputEventKind kind, byte slot)
        {
            if (buffer == null) return;
            if (kind == LocalGameplayInputEventKind.PrimaryClick ||
                kind == LocalGameplayInputEventKind.SecondaryClick)
            {
                if (IsPointerOverBlockingUi())
                    return;
            }
            Vector2 screenPosition = pointerPosition != null
                ? pointerPosition.ReadValue<Vector2>()
                : Vector2.zero;
            if (pointerResolver != null) pointerResolver.LastScreenPosition = screenPosition;
            if (!buffer.Push(kind, slot, screenPosition))
            {
                Debug.LogWarning(
                    $"Dropped {kind}: local gameplay input buffer reached "
                    + LocalInputEventBuffer.MaxLocalInputEventsPerUnityFrame + ".",
                    this);
            }
        }

        /// <summary>
        /// Design v1.1 16.4: pointer clicks over blocking UI must not produce
        /// world Commands (no Commit, no Move/Attack). The UI map is handled by
        /// InputSystemUIInputModule on the same pointer.
        /// </summary>
        private static bool IsPointerOverBlockingUi()
        {
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null &&
                eventSystem.IsPointerOverGameObject();
        }
    }
}
