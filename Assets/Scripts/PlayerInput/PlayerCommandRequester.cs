using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.PlayerInput
{
    public interface IPlayerGameplayCommandRequester
    {
        bool RequestMove(in fp2 targetPoint);
        bool RequestAttack(in UnitUid targetUnitUid);
        bool RequestCastAbility(
            byte slot,
            AbilitySignalVerb signal,
            in AimSnapshot aim,
            out GameplayCommandRequestReceipt receipt);
        bool RequestAllocateAbilitySkillPoint(byte slot);
    }

    public interface IPlayerShopCommandRequester
    {
        bool RequestEquipmentPurchase(int equipmentId);
        bool RequestEquipmentSell(byte sourceSlot);
        bool RequestEquipmentUndo();
    }

    public interface IPlayerAbilityInputProfileProvider
    {
        bool TryGetTemplate(
            byte slot,
            out InputMappingTemplate template);
        bool TryGetAimKind(byte slot, out AimKind aimKind);
    }

    public interface IPlayerAbilityAimProfileProvider
    {
        bool TryGetAimConfiguration(
            byte slot,
            out AimKind aimKind,
            out fp castRange);
    }

    public interface ILocalAbilityRuntimeView
    {
        bool HasActiveSession(UnitUid ownerUid, byte slot);
        bool IsWaitingForCommit(UnitUid ownerUid, byte slot);
    }

    public readonly struct GameplayCommandRequestReceipt
    {
        public readonly int TargetTick;
        public readonly uint CommandSeq;

        public GameplayCommandRequestReceipt(int targetTick, uint commandSeq)
        {
            TargetTick = targetTick;
            CommandSeq = commandSeq;
        }
    }

    public enum LocalAbilityInputStateKind : byte
    {
        Idle = 0,
        LocalAiming = 1,
        FocusRequested = 2,
        GameplayFocusing = 3,
        CommitRequested = 4,
    }

    public struct LocalAbilityInputState
    {
        public LocalAbilityInputStateKind Kind;
        public byte Slot;
        public UnitUid ControlledUnitUidAtBegin;
        public GameplayCommandRequestReceipt LastRequestReceipt;
    }

    public sealed class PlayerCommandRequester :
        IPlayerGameplayCommandRequester,
        IPlayerShopCommandRequester,
        FrameSyncMoba.Unit.IEquipmentShopCommandSubmitter
    {
        private const int AbilitySlotCount = 4;

        private readonly IGameplayInputGate gate;
        private readonly CommandCollector collector;
        private readonly CommandTargetTickResolver targetTickResolver;
        private readonly IPlayerAbilityInputProfileProvider profileProvider;
        private readonly ILocalAbilityRuntimeView abilityRuntimeView;
        private readonly LocalAbilityInputState[] abilityStates =
            new LocalAbilityInputState[AbilitySlotCount];

        private UnitType controlledUnit;
        private readonly int playerSlot;
        private readonly ulong clientId;
        private uint nextCommandSeq = 1;

        public PlayerCommandRequester(
            UnitType controlledUnit,
            IGameplayInputGate gate,
            CommandCollector collector,
            int playerSlot,
            ulong clientId,
            CommandTargetTickResolver targetTickResolver,
            IPlayerAbilityInputProfileProvider profileProvider = null,
            ILocalAbilityRuntimeView abilityRuntimeView = null)
        {
            this.controlledUnit = controlledUnit;
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
            this.playerSlot = playerSlot;
            this.clientId = clientId;
            this.targetTickResolver = targetTickResolver
                ?? throw new ArgumentNullException(nameof(targetTickResolver));
            this.profileProvider = profileProvider;
            this.abilityRuntimeView = abilityRuntimeView;
        }

        public UnitType ControlledUnit => controlledUnit;
        public uint NextCommandSeq => nextCommandSeq;

        public ref readonly LocalAbilityInputState GetAbilityState(byte slot)
        {
            ValidateSlot(slot);
            return ref abilityStates[slot];
        }

        /// <summary>
        /// Query the aim configuration for an ability slot.
        /// Used by the indicator driver to show the correct indicator shape.
        /// </summary>
        public bool TryGetAimInfo(
            byte slot,
            out AimKind aimKind,
            out fp castRange,
            out fp2 casterPos,
            out fp2 casterForward)
        {
            aimKind = AimKind.None;
            castRange = fp.zero;
            casterPos = fp2.zero;
            casterForward = new fp2(fp.zero, fp.one);

            if (!(profileProvider is
                    IPlayerAbilityAimProfileProvider aimProvider) ||
                !aimProvider.TryGetAimConfiguration(
                    slot,
                    out aimKind,
                    out castRange))
            {
                return false;
            }
            if (aimKind == AimKind.None) return false;

            // Get caster position from controlled unit
            if (controlledUnit?.MovementHandler != null)
            {
                casterPos = controlledUnit.MovementHandler.Position;
                casterForward = controlledUnit.MovementHandler.Facing;
            }

            return true;
        }

        /// <summary>
        /// Resolve the presentation-only ground circle radius for an aiming
        /// ability (Point/Unit aim): the first area-stage radius (e.g.
        /// AreaDamageStageDef) or the GroundTarget cast-model radius. Returns
        /// false when the slot has no usable radius.
        /// </summary>
        public bool TryGetGroundTargetRadius(
            byte slot,
            out fp radius)
        {
            radius = fp.zero;
            AbilityDef definition =
                controlledUnit?.AbilityHandler
                    ?.GetAbilityDef(slot);
            CastModelDef model =
                definition?.CastModel;
            if (model == null)
            {
                return false;
            }
            CollectAreaRadius(model, ref radius);
            if (radius > fp.zero)
            {
                return true;
            }
            if (model is GroundTargetCastModelDef ground)
            {
                radius = ground.Radius;
                return radius > fp.zero;
            }
            return false;
        }

        private static void CollectAreaRadius(
            CastModelDef model,
            ref fp radius)
        {
            switch (model)
            {
                case CommitCastModelDef commit:
                    CollectStageRadius(
                        commit.Cast,
                        ref radius);
                    break;
                case HoldReleaseCastModelDef hold:
                    CollectStageRadius(
                        hold.Hold,
                        ref radius);
                    CollectStageRadius(
                        hold.Release,
                        ref radius);
                    break;
                case ChannelCastModelDef channel:
                    CollectStageRadius(
                        channel.Channel,
                        ref radius);
                    break;
                case ActiveSignalCastModelDef active:
                    CollectStageRadius(
                        active.Active,
                        ref radius);
                    break;
                case ToggleCastModelDef toggle:
                    CollectStageRadius(
                        toggle.Active,
                        ref radius);
                    break;
            }
        }

        private static void CollectStageRadius(
            in CastStage stage,
            ref fp radius)
        {
            if (radius > fp.zero)
            {
                return;
            }
            if (stage.Def is AreaDamageStageDef area)
            {
                radius = area.Radius;
            }
        }

        public void SetControlledUnit(UnitType unit)
        {
            if (controlledUnit == unit) return;
            controlledUnit = unit;
            ClearLocalAbilityStates();
        }

        public void ProcessFrame(
            LocalInputEventBuffer buffer,
            MouseWorldResolver pointerResolver)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            SynchronizeAbilityRuntimeView();

            while (buffer.TryDequeue(out LocalGameplayInputEvent inputEvent))
            {
                switch (inputEvent.Kind)
                {
                    case LocalGameplayInputEventKind.SecondaryClick:
                        ProcessSecondaryClick(inputEvent, pointerResolver);
                        break;

                    case LocalGameplayInputEventKind.PrimaryClick:
                        ProcessPrimaryCommit(inputEvent, pointerResolver);
                        break;

                    case LocalGameplayInputEventKind.AbilityKeyPressed:
                        ProcessAbilityPressed(inputEvent, pointerResolver);
                        break;

                    case LocalGameplayInputEventKind.AbilityKeyReleased:
                        ProcessAbilityReleased(inputEvent, pointerResolver);
                        break;

                    case LocalGameplayInputEventKind.Cancel:
                        ProcessCancel();
                        break;
                }
            }
        }

        public bool RequestMove(in fp2 targetPoint)
        {
            if (controlledUnit == null || !controlledUnit.UnitUid.IsValid()) return false;
            if (!gate.IsMoveAllowed(controlledUnit)) return false;

            CommandHeader header = CreateHeader(GameplayCommandKind.Move);
            collector.Collect(GameplayCommand.CreateMove(header, targetPoint));
            AdvanceSequence();
            return true;
        }

        public bool RequestAttack(in UnitUid targetUnitUid)
        {
            if (controlledUnit == null || !controlledUnit.UnitUid.IsValid()) return false;
            if (!targetUnitUid.IsValid() || targetUnitUid == controlledUnit.UnitUid) return false;
            if (!gate.IsAttackAllowed(controlledUnit)) return false;

            CommandHeader header = CreateHeader(GameplayCommandKind.Attack);
            collector.Collect(GameplayCommand.CreateAttack(header, targetUnitUid));
            AdvanceSequence();
            return true;
        }

        public bool RequestCastAbility(
            byte slot,
            AbilitySignalVerb signal,
            in AimSnapshot aim,
            out GameplayCommandRequestReceipt receipt)
        {
            receipt = default;
            if (slot >= AbilitySlotCount) return false;
            if (controlledUnit == null || !controlledUnit.UnitUid.IsValid()) return false;
            if (!gate.IsAbilityAllowed(controlledUnit, slot)) return false;

            CommandHeader header = CreateHeader(GameplayCommandKind.CastAbility);
            collector.Collect(GameplayCommand.CreateCastAbility(header, slot, signal, aim));
            receipt = new GameplayCommandRequestReceipt(header.TargetTick, header.CommandSeq);
            AdvanceSequence();
            return true;
        }

        public bool RequestEquipmentPurchase(int equipmentId)
        {
            if (!CanRequestForControlledUnit() ||
                equipmentId <= 0)
                return false;
            CommandHeader header =
                CreateHeader(GameplayCommandKind.EquipmentShop);
            collector.Collect(
                GameplayCommand.CreateEquipmentPurchase(
                    header,
                    equipmentId));
            AdvanceSequence();
            return true;
        }

        public bool RequestEquipmentSell(byte sourceSlot)
        {
            if (!CanRequestForControlledUnit() ||
                sourceSlot >= EquipmentHandler.SlotCount)
                return false;
            CommandHeader header =
                CreateHeader(GameplayCommandKind.EquipmentShop);
            collector.Collect(
                GameplayCommand.CreateEquipmentSell(
                    header,
                    sourceSlot));
            AdvanceSequence();
            return true;
        }

        public bool RequestEquipmentUndo()
        {
            if (!CanRequestForControlledUnit())
                return false;
            CommandHeader header =
                CreateHeader(GameplayCommandKind.EquipmentShop);
            collector.Collect(
                GameplayCommand.CreateEquipmentUndo(header));
            AdvanceSequence();
            return true;
        }

        public bool RequestAllocateAbilitySkillPoint(
            byte slot)
        {
            if (!CanRequestForControlledUnit() ||
                slot >= AbilitySlotCount)
                return false;
            CommandHeader header =
                CreateHeader(
                    GameplayCommandKind
                        .AllocateAbilitySkillPoint);
            collector.Collect(
                GameplayCommand
                    .CreateAllocateAbilitySkillPoint(
                        header,
                        slot));
            AdvanceSequence();
            return true;
        }

        /// <summary>
        /// Submits a deterministic debug command (GameScene testing only).
        /// </summary>
        public bool RequestDebugCommand(
            byte op,
            int value)
        {
            if (!CanRequestForControlledUnit())
                return false;
            CommandHeader header =
                CreateHeader(
                    GameplayCommandKind.Debug);
            collector.Collect(
                GameplayCommand
                    .CreateDebugCommand(
                        header,
                        op,
                        value));
            AdvanceSequence();
            return true;
        }

        void FrameSyncMoba.Unit
            .IEquipmentShopCommandSubmitter
            .SubmitPurchase(
                int playerSlot,
                int targetEquipmentId)
        {
            EnsureSubmitterSlotMatches(playerSlot);
            if (!RequestEquipmentPurchase(
                    targetEquipmentId))
                throw new InvalidOperationException(
                    "Shop purchase submission failed after RequestCheck passed.");
        }

        void FrameSyncMoba.Unit
            .IEquipmentShopCommandSubmitter
            .SubmitSell(
                int playerSlot,
                int sourceSlot)
        {
            EnsureSubmitterSlotMatches(playerSlot);
            if (!RequestEquipmentSell(
                    (byte)sourceSlot))
                throw new InvalidOperationException(
                    "Shop sell submission failed after RequestCheck passed.");
        }

        void FrameSyncMoba.Unit
            .IEquipmentShopCommandSubmitter
            .SubmitUndo(
                int playerSlot)
        {
            EnsureSubmitterSlotMatches(playerSlot);
            if (!RequestEquipmentUndo())
                throw new InvalidOperationException(
                    "Shop undo submission failed after RequestCheck passed.");
        }

        private void EnsureSubmitterSlotMatches(
            int playerSlot)
        {
            if (!CanRequestForControlledUnit() ||
                controlledUnit.ControlledByPlayerSlot !=
                    playerSlot)
            {
                throw new InvalidOperationException(
                    $"Shop submitter player slot {playerSlot} does not match the controlled unit.");
            }
        }

        private bool CanRequestForControlledUnit()
        {
            return controlledUnit != null &&
                   controlledUnit.UnitUid.IsValid();
        }

        private void ProcessSecondaryClick(
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver)
        {
            for (int i = 0; i < abilityStates.Length; i++)
            {
                if (abilityStates[i].Kind !=
                    LocalAbilityInputStateKind.LocalAiming)
                    continue;
                if (TryGetBinding(
                        abilityStates[i].Slot,
                        InputTrigger.SecondaryClick,
                        out InputBinding binding) &&
                    binding.Translation ==
                        InputTranslation.CancelLocalAim)
                {
                    abilityStates[i] = default;
                    Debug.Log(
                        $"[Input] SecondaryClick closed local aim on slot {i}.");
                    return;
                }
            }

            if (controlledUnit == null || pointerResolver == null) return;
            UnitUid? target = pointerResolver.ResolveUnitTarget(inputEvent.ScreenPositionAtEvent);
            if (target.HasValue && RequestAttack(target.Value)) return;
            fp2? point = pointerResolver.ResolveGroundPoint(inputEvent.ScreenPositionAtEvent);
            if (point.HasValue)
            {
                RequestMove(point.Value);
            }
        }

        private void ProcessAbilityPressed(
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver)
        {
            byte slot = inputEvent.AbilitySlot;
            if (slot >= AbilitySlotCount || controlledUnit == null) return;
            // Unlearned abilities (level 0) must not produce indicators or
            // cast commands (design v15.2 1.12 LockMask covers the HUD).
            if (!IsAbilityLearned(slot))
            {
                Debug.Log(
                    $"[Input] Press slot {slot} ignored: " +
                    $"ability level 0 (not learned).");
                return;
            }
            ref LocalAbilityInputState state = ref abilityStates[slot];
            if (state.Kind != LocalAbilityInputStateKind.Idle) return;

            if (!TryGetBinding(
                    slot,
                    InputTrigger.AbilityKeyPressed,
                    out InputBinding binding))
            {
                Debug.Log(
                    $"[Input] Press slot {slot}: template has no AbilityKeyPressed binding, no action.");
                return;
            }

            switch (binding.Translation)
            {
                case InputTranslation.LocalAimOnly:
                    state = CreateState(
                        slot,
                        LocalAbilityInputStateKind.LocalAiming,
                        default);
                    Debug.Log(
                        $"[Input] Press slot {slot}: LocalAimOnly -> LocalAiming (no command).");
                    break;

                case InputTranslation.Focus:
                    if (RequestCastAbility(
                        slot,
                        AbilitySignalVerb.Focus,
                        AimSnapshot.None,
                        out GameplayCommandRequestReceipt focusReceipt))
                    {
                        state = CreateState(
                            slot,
                            LocalAbilityInputStateKind.FocusRequested,
                            focusReceipt);
                        Debug.Log(
                            $"[Input] Press slot {slot}: Focus -> FocusRequested (seq {focusReceipt.CommandSeq}).");
                    }
                    break;

                case InputTranslation.Commit:
                    if (TryBuildAim(
                            slot,
                            inputEvent,
                            pointerResolver,
                            binding.CaptureAim,
                            out AimSnapshot pressAim) &&
                        RequestCastAbility(
                            slot,
                            AbilitySignalVerb.Commit,
                            pressAim,
                            out GameplayCommandRequestReceipt
                                pressReceipt))
                    {
                        state = CreateState(
                            slot,
                            LocalAbilityInputStateKind.CommitRequested,
                            pressReceipt);
                        Debug.Log(
                            $"[Input] Press slot {slot}: Commit -> CommitRequested (seq {pressReceipt.CommandSeq}).");
                    }
                    break;

                case InputTranslation.Cancel:
                    if (RequestCastAbility(
                        slot,
                        AbilitySignalVerb.Cancel,
                        AimSnapshot.None,
                        out GameplayCommandRequestReceipt cancelReceipt))
                    {
                        state = default;
                        Debug.Log(
                            $"[Input] Press slot {slot}: Cancel -> Idle (seq {cancelReceipt.CommandSeq}).");
                    }
                    break;

                default:
                    Debug.Log(
                        $"[Input] Press slot {slot}: translation {binding.Translation}, no action.");
                    break;
            }
        }

        private bool IsAbilityLearned(byte slot)
        {
            int level =
                controlledUnit?.AbilityHandler
                    ?.GetAbilityLevel(slot) ?? 0;
            return level > 0;
        }

        private void ProcessAbilityReleased(
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver)
        {
            byte slot = inputEvent.AbilitySlot;
            if (slot >= AbilitySlotCount) return;
            ref LocalAbilityInputState state = ref abilityStates[slot];
            if (state.Kind != LocalAbilityInputStateKind.FocusRequested
                && state.Kind != LocalAbilityInputStateKind.GameplayFocusing)
            {
                return;
            }

            if (!TryGetBinding(
                    slot,
                    InputTrigger.AbilityKeyReleased,
                    out InputBinding binding))
            {
                Debug.Log(
                    $"[Input] Release slot {slot}: template has no AbilityKeyReleased binding, no action.");
                return;
            }

            switch (binding.Translation)
            {
                case InputTranslation.Commit:
                    if (TryBuildAim(
                            slot,
                            inputEvent,
                            pointerResolver,
                            binding.CaptureAim,
                            out AimSnapshot aim) &&
                        RequestCastAbility(
                            slot,
                            AbilitySignalVerb.Commit,
                            aim,
                            out GameplayCommandRequestReceipt
                                receipt))
                    {
                        state = CreateState(
                            slot,
                            LocalAbilityInputStateKind.CommitRequested,
                            receipt);
                        Debug.Log(
                            $"[Input] Release slot {slot}: Commit -> CommitRequested (seq {receipt.CommandSeq}).");
                    }
                    break;

                case InputTranslation.Cancel:
                    if (RequestCastAbility(
                        slot,
                        AbilitySignalVerb.Cancel,
                        AimSnapshot.None,
                        out GameplayCommandRequestReceipt cancelReceipt))
                    {
                        state = default;
                        Debug.Log(
                            $"[Input] Release slot {slot}: Cancel -> Idle (seq {cancelReceipt.CommandSeq}).");
                    }
                    break;

                default:
                    Debug.Log(
                        $"[Input] Release slot {slot}: translation {binding.Translation}, no action.");
                    break;
            }
        }

        private void ProcessPrimaryCommit(
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver)
        {
            for (byte slot = 0; slot < AbilitySlotCount; slot++)
            {
                ref LocalAbilityInputState state = ref abilityStates[slot];
                if (state.Kind != LocalAbilityInputStateKind.LocalAiming
                    && state.Kind != LocalAbilityInputStateKind.FocusRequested
                    && state.Kind != LocalAbilityInputStateKind.GameplayFocusing)
                {
                    continue;
                }
                if (!TryGetBinding(
                        slot,
                        InputTrigger.PrimaryClick,
                        out InputBinding binding) ||
                    binding.Translation !=
                        InputTranslation.Commit)
                {
                    continue;
                }

                if (!TryBuildAim(
                        slot,
                        inputEvent,
                        pointerResolver,
                        binding.CaptureAim,
                        out AimSnapshot aim))
                    return;
                if (RequestCastAbility(
                    slot,
                    AbilitySignalVerb.Commit,
                    aim,
                    out GameplayCommandRequestReceipt receipt))
                {
                    state = CreateState(
                        slot,
                        LocalAbilityInputStateKind.CommitRequested,
                        receipt);
                    Debug.Log(
                        $"[Input] PrimaryClick slot {slot}: Commit -> CommitRequested (seq {receipt.CommandSeq}).");
                }
                return;
            }
        }

        private bool TryBuildAim(
            byte slot,
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver,
            bool captureAim,
            out AimSnapshot aim)
        {
            AimKind kind = AimKind.None;
            profileProvider?.TryGetAimKind(slot, out kind);
            if (!captureAim)
            {
                aim = AimSnapshot.None;
                return true;
            }
            switch (kind)
            {
                case AimKind.None:
                    aim = AimSnapshot.None;
                    return true;
                case AimKind.Self:
                    aim = AimSnapshot.Self;
                    return true;
                case AimKind.Point:
                    fp2? point = pointerResolver?.ResolveGroundPoint(inputEvent.ScreenPositionAtEvent);
                    aim = point.HasValue ? AimSnapshot.ForPoint(point.Value) : default;
                    return point.HasValue;
                case AimKind.Unit:
                    UnitUid? target = pointerResolver?.ResolveUnitTarget(inputEvent.ScreenPositionAtEvent);
                    aim = target.HasValue ? AimSnapshot.ForUnit(target.Value) : default;
                    return target.HasValue;
                case AimKind.Direction:
                    fp2? groundPoint = pointerResolver?.ResolveGroundPoint(inputEvent.ScreenPositionAtEvent);
                    if (!groundPoint.HasValue || controlledUnit?.MovementHandler == null)
                    {
                        aim = default;
                        return false;
                    }
            fp2 direction = groundPoint.Value - controlledUnit.MovementHandler.Position;
                    try
                    {
                        aim = AimSnapshot.ForDirection(direction);
                        return true;
                    }
                    catch (ArgumentException)
                    {
                        aim = default;
                        return false;
                    }
                default:
                    aim = default;
                    return false;
            }
        }

        private bool TryGetBinding(
            byte slot,
            InputTrigger trigger,
            out InputBinding binding)
        {
            InputMappingTemplate template =
                GetTemplate(slot);
            if (template != null &&
                template.TryGet(trigger, out binding))
            {
                return true;
            }
            binding = default;
            return false;
        }

        private InputMappingTemplate GetTemplate(byte slot)
        {
            if (profileProvider != null &&
                profileProvider.TryGetTemplate(
                    slot,
                    out InputMappingTemplate template))
            {
                return template;
            }
            return AbilityInputMapping.DefaultPressCommit;
        }

        private CommandHeader CreateHeader(GameplayCommandKind kind)
        {
            if (nextCommandSeq == uint.MaxValue)
            {
                throw new InvalidOperationException("Player CommandSeq exhausted.");
            }

            int targetTick =
                targetTickResolver.ResolveTargetTick(out int buildLocalTick);
            return new CommandHeader(
                nextCommandSeq,
                clientId,
                playerSlot,
                controlledUnit.UnitUid,
                targetTick,
                kind,
                buildLocalTick,
                0);
        }

        private void AdvanceSequence()
        {
            nextCommandSeq++;
        }

        private void SynchronizeAbilityRuntimeView()
        {
            if (abilityRuntimeView == null || controlledUnit == null) return;
            for (byte slot = 0; slot < AbilitySlotCount; slot++)
            {
                ref LocalAbilityInputState state = ref abilityStates[slot];
                bool hasSession = abilityRuntimeView.HasActiveSession(
                    controlledUnit.UnitUid, slot);
                bool waiting = abilityRuntimeView.IsWaitingForCommit(
                    controlledUnit.UnitUid, slot);

                if (waiting
                    && state.Kind == LocalAbilityInputStateKind.FocusRequested)
                {
                    state.Kind = LocalAbilityInputStateKind.GameplayFocusing;
                }
                else if (waiting
                    && hasSession
                    && state.Kind ==
                        LocalAbilityInputStateKind.CommitRequested)
                {
                    // Commit was not accepted by Gameplay while the Session
                    // still waits: recover to Focusing (design v1.1 17.4).
                    state.Kind =
                        LocalAbilityInputStateKind.GameplayFocusing;
                }
                else if (!hasSession
                    && (state.Kind == LocalAbilityInputStateKind.GameplayFocusing
                        || state.Kind == LocalAbilityInputStateKind.CommitRequested))
                {
                    state = default;
                }
            }
        }

        private void ProcessCancel()
        {
            for (int i = 0; i < abilityStates.Length; i++)
            {
                if (abilityStates[i].Kind !=
                    LocalAbilityInputStateKind.LocalAiming)
                    continue;

                byte slot = abilityStates[i].Slot;
                if (!TryGetBinding(
                        slot,
                        InputTrigger.Cancel,
                        out InputBinding binding))
                    continue;

                switch (binding.Translation)
                {
                    case InputTranslation.CancelLocalAim:
                        abilityStates[i] = default;
                        Debug.Log(
                            $"[Input] Escape closed local aim on slot {slot}.");
                        break;
                    case InputTranslation.Cancel:
                        if (RequestCastAbility(
                            slot,
                            AbilitySignalVerb.Cancel,
                            AimSnapshot.None,
                            out GameplayCommandRequestReceipt
                                cancelReceipt))
                        {
                            abilityStates[i] = default;
                            Debug.Log(
                                $"[Input] Escape slot {slot}: Cancel -> Idle (seq {cancelReceipt.CommandSeq}).");
                        }
                        break;
                    default:
                        Debug.Log(
                            $"[Input] Escape slot {slot}: translation {binding.Translation}, no action.");
                        break;
                }
            }
        }

        private void ClearLocalAbilityStates()
        {
            Array.Clear(abilityStates, 0, abilityStates.Length);
        }

        private LocalAbilityInputState CreateState(
            byte slot,
            LocalAbilityInputStateKind kind,
            GameplayCommandRequestReceipt receipt)
        {
            return new LocalAbilityInputState
            {
                Kind = kind,
                Slot = slot,
                ControlledUnitUidAtBegin = controlledUnit?.UnitUid ?? default,
                LastRequestReceipt = receipt,
            };
        }

        private static void ValidateSlot(byte slot)
        {
            if (slot >= AbilitySlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
