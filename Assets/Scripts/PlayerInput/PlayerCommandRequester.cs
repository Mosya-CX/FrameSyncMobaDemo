using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
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
        /// <summary>
        /// Whether the slot may open a local aim indicator right now: the
        /// ability is ready (learned, not on cooldown, no session) or the
        /// active session can accept a real next-stage Commit immediately.
        /// </summary>
        bool CanOpenLocalAim(UnitUid ownerUid, byte slot);
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
        public bool AwaitingAcceptedExecution;
    }

    public sealed class PlayerCommandRequester :
        IPlayerGameplayCommandRequester,
        IPlayerShopCommandRequester,
        FrameSyncMoba.Unit.IEquipmentShopCommandSubmitter
    {
        private const int AbilitySlotCount = 4;
        private const int AbilityRequestDiagnosticCapacity = 64;

        private readonly IGameplayInputGate gate;
        private readonly CommandCollector collector;
        private readonly CommandTargetTickResolver targetTickResolver;
        private readonly IPlayerAbilityInputProfileProvider profileProvider;
        private readonly ILocalAbilityRuntimeView abilityRuntimeView;
        private readonly Func<int> completedGameplayTickProvider;
        private readonly LocalAbilityInputState[] abilityStates =
            new LocalAbilityInputState[AbilitySlotCount];
        private readonly AbilityRequestDiagnostic[]
            abilityRequestDiagnostics =
                new AbilityRequestDiagnostic[
                    AbilityRequestDiagnosticCapacity];
        private int nextAbilityRequestDiagnosticIndex;

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
            ILocalAbilityRuntimeView abilityRuntimeView = null,
            Func<int> completedGameplayTickProvider = null)
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
            this.completedGameplayTickProvider =
                completedGameplayTickProvider;
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
                LocalAbilityInputStateKind stateBefore =
                    inputEvent.AbilitySlot < AbilitySlotCount
                        ? abilityStates[inputEvent.AbilitySlot].Kind
                        : LocalAbilityInputStateKind.Idle;
                Debug.Log(
                    $"[InputTrace] eventSeq={inputEvent.LocalEventSequence} " +
                    $"kind={inputEvent.Kind} slot={inputEvent.AbilitySlot} " +
                    $"screen={inputEvent.ScreenPositionAtEvent} " +
                    $"stateBefore={stateBefore} " +
                    $"states={DescribeAbilityStates()} " +
                    $"bufferRemaining={buffer.Count}");
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
                LocalAbilityInputStateKind stateAfter =
                    inputEvent.AbilitySlot < AbilitySlotCount
                        ? abilityStates[inputEvent.AbilitySlot].Kind
                        : LocalAbilityInputStateKind.Idle;
                Debug.Log(
                    $"[InputTrace] eventSeq={inputEvent.LocalEventSequence} " +
                    $"outcome stateAfter={stateAfter} " +
                    $"states={DescribeAbilityStates()} " +
                    $"nextCommandSeq={nextCommandSeq}");
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
            Debug.Log(
                $"[AbilityCommandRequest] unit={header.ControlledUnitUid} " +
                $"slot={slot} verb={signal} seq={header.CommandSeq} " +
                $"targetTick={header.TargetTick} buildTick={header.BuildLocalTick} " +
                $"localState={abilityStates[slot].Kind} aim={aim.Kind}");
            RecordAbilityRequest(header, slot, signal);
            AdvanceSequence();
            return true;
        }

        /// <summary>
        /// Correlates a locally submitted ability Command with the exact
        /// canonical command list observed at TickCompleted. It reconciles
        /// local-only receipt/input presentation state but never changes
        /// Gameplay state. It is intentionally called for both prediction and
        /// ClientReplay so a packaged log can distinguish a real physical
        /// input event from a rollback re-execution of the same CommandSeq.
        /// </summary>
        public void ObserveCompletedGameplayTick(
            int tick,
            IReadOnlyList<GameplayCommand> commands,
            uint checksum)
        {
            if (commands == null ||
                controlledUnit == null ||
                !controlledUnit.UnitUid.IsValid())
            {
                return;
            }

            for (int i = 0; i < commands.Count; i++)
            {
                GameplayCommand command = commands[i];
                if (command.Kind != GameplayCommandKind.CastAbility ||
                    command.ControlledUnitUid != controlledUnit.UnitUid)
                {
                    continue;
                }

                int requestIndex =
                    FindAbilityRequestDiagnostic(command.CommandSeq);

                bool hasSession = abilityRuntimeView != null &&
                    abilityRuntimeView.HasActiveSession(
                        controlledUnit.UnitUid,
                        command.AbilitySlot);
                bool waitingForCommit = abilityRuntimeView != null &&
                    abilityRuntimeView.IsWaitingForCommit(
                        controlledUnit.UnitUid,
                        command.AbilitySlot);
                LocalAbilityInputStateKind localState =
                    command.AbilitySlot < AbilitySlotCount
                        ? abilityStates[command.AbilitySlot].Kind
                        : LocalAbilityInputStateKind.Idle;
                if (command.AbilitySlot < AbilitySlotCount)
                {
                    ref LocalAbilityInputState state =
                        ref abilityStates[command.AbilitySlot];
                    if (state.Kind != LocalAbilityInputStateKind.Idle &&
                        state.ControlledUnitUidAtBegin ==
                            command.ControlledUnitUid &&
                        state.LastRequestReceipt.CommandSeq ==
                            command.CommandSeq)
                    {
                        state.LastRequestReceipt =
                            new GameplayCommandRequestReceipt(
                                command.TargetTick,
                                command.CommandSeq);
                        state.AwaitingAcceptedExecution = false;
                        if (command.AbilityVerb ==
                                AbilitySignalVerb.Focus &&
                            (hasSession || waitingForCommit))
                        {
                            state.Kind =
                                LocalAbilityInputStateKind
                                    .GameplayFocusing;
                        }
                        localState = state.Kind;
                    }
                }
                AbilityRequestDiagnostic request =
                    requestIndex >= 0
                        ? abilityRequestDiagnostics[requestIndex]
                        : default;
                string requestTarget = requestIndex >= 0
                    ? request.RequestTargetTick.ToString()
                    : "<untracked>";
                if (requestIndex >= 0)
                {
                    ref AbilityRequestDiagnostic trackedRequest =
                        ref abilityRequestDiagnostics[requestIndex];
                    trackedRequest.LastObservationTick = tick;
                    trackedRequest.LastObservationMode =
                        SimulationTickContext.Current.ExecutionMode;
                }
                Debug.Log(
                    $"[AbilityCommandObserved] mode={SimulationTickContext.Current.ExecutionMode} " +
                    $"tick={tick} checksum=0x{checksum:X8} " +
                    $"unit={command.ControlledUnitUid} seq={command.CommandSeq} " +
                    $"slot={command.AbilitySlot} verb={command.AbilityVerb} " +
                    $"requestTarget={requestTarget} " +
                    $"actualTarget={command.TargetTick} " +
                    $"buildTick={command.Header.BuildLocalTick} " +
                    $"localState={localState} session={hasSession} " +
                    $"waiting={waitingForCommit} tracked={requestIndex >= 0}");
            }

            for (int i = 0;
                i < abilityRequestDiagnostics.Length;
                i++)
            {
                ref AbilityRequestDiagnostic request =
                    ref abilityRequestDiagnostics[i];
                if (!request.Active ||
                    request.UnitUid != controlledUnit.UnitUid ||
                    request.MissingAtTargetLogged ||
                    tick != request.RequestTargetTick)
                {
                    continue;
                }

                bool observed = request.LastObservationTick == tick &&
                    request.LastObservationMode ==
                        SimulationTickContext.Current.ExecutionMode;
                if (observed)
                    continue;

                request.MissingAtTargetLogged = true;
                LocalAbilityInputStateKind localState =
                    request.Slot < AbilitySlotCount
                        ? abilityStates[request.Slot].Kind
                        : LocalAbilityInputStateKind.Idle;
                Debug.LogWarning(
                    $"[AbilityCommandMissing] mode={SimulationTickContext.Current.ExecutionMode} " +
                    $"tick={tick} unit={request.UnitUid} " +
                    $"seq={request.CommandSeq} slot={request.Slot} " +
                    $"verb={request.Verb} requestTarget={request.RequestTargetTick} " +
                    $"buildTick={request.BuildLocalTick} localState={localState} " +
                    "no matching CastAbility command at its requested Tick.");
            }
        }

        /// <summary>
        /// Updates a local request receipt when the server accepts the same
        /// CommandSeq on a different Tick. If rollback already retired the
        /// stale local latch, the exact tracked Focus/Commit request is
        /// reconstructed from the accepted command. This local presentation
        /// state never participates in Gameplay rollback or checksums.
        /// </summary>
        public void ObserveAcceptedGameplayCommands(
            IReadOnlyList<GameplayCommand> commands)
        {
            if (commands == null || controlledUnit == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                GameplayCommand command = commands[i];
                if (command.Kind != GameplayCommandKind.CastAbility ||
                    command.ControlledUnitUid != controlledUnit.UnitUid ||
                    command.AbilitySlot >= AbilitySlotCount)
                {
                    continue;
                }

                ref LocalAbilityInputState state =
                    ref abilityStates[command.AbilitySlot];
                int requestTarget;
                bool recoveredFromIdle = false;
                if (state.Kind == LocalAbilityInputStateKind.Idle)
                {
                    int requestIndex =
                        FindAbilityRequestDiagnostic(command.CommandSeq);
                    if (requestIndex < 0)
                        continue;

                    AbilityRequestDiagnostic request =
                        abilityRequestDiagnostics[requestIndex];
                    if (!request.MissingAtTargetLogged ||
                        command.TargetTick <= request.RequestTargetTick ||
                        request.LastObservationTick >= command.TargetTick ||
                        request.Slot != command.AbilitySlot ||
                        request.Verb != command.AbilityVerb ||
                        !TryGetAcceptedRecoveryState(
                            command.AbilityVerb,
                            out LocalAbilityInputStateKind recoveryKind))
                    {
                        continue;
                    }

                    requestTarget = request.RequestTargetTick;
                    state = CreateState(
                        command.AbilitySlot,
                        recoveryKind,
                        new GameplayCommandRequestReceipt(
                            command.TargetTick,
                            command.CommandSeq));
                    recoveredFromIdle = true;
                }
                else if (state.ControlledUnitUidAtBegin !=
                        command.ControlledUnitUid ||
                    state.LastRequestReceipt.CommandSeq !=
                        command.CommandSeq)
                {
                    continue;
                }
                else
                {
                    requestTarget =
                        state.LastRequestReceipt.TargetTick;
                }
                state.LastRequestReceipt =
                    new GameplayCommandRequestReceipt(
                        command.TargetTick,
                        command.CommandSeq);
                int completedGameplayTick =
                    completedGameplayTickProvider != null
                        ? completedGameplayTickProvider()
                        : SimulationTickContext.Current.Tick;
                bool acceptedTickAlreadyCompleted =
                    command.TargetTick <= completedGameplayTick;
                state.AwaitingAcceptedExecution =
                    !acceptedTickAlreadyCompleted;
                Debug.Log(
                    $"[AbilityCommandAccepted] unit={command.ControlledUnitUid} " +
                    $"seq={command.CommandSeq} slot={command.AbilitySlot} " +
                    $"verb={command.AbilityVerb} requestTarget={requestTarget} " +
                    $"acceptedTarget={command.TargetTick} state={state.Kind} " +
                    $"completedTick={completedGameplayTick} " +
                    $"awaitingExecution={state.AwaitingAcceptedExecution} " +
                    $"recoveredFromIdle={recoveredFromIdle}");
            }
        }

        private static bool TryGetAcceptedRecoveryState(
            AbilitySignalVerb verb,
            out LocalAbilityInputStateKind stateKind)
        {
            switch (verb)
            {
                case AbilitySignalVerb.Focus:
                    stateKind =
                        LocalAbilityInputStateKind.FocusRequested;
                    return true;
                case AbilitySignalVerb.Commit:
                    stateKind =
                        LocalAbilityInputStateKind.CommitRequested;
                    return true;
                default:
                    stateKind = LocalAbilityInputStateKind.Idle;
                    return false;
            }
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
            Debug.Log(
                $"[InputTrace] SecondaryClick eventSeq={inputEvent.LocalEventSequence} " +
                $"statesBefore={DescribeAbilityStates()} " +
                $"pointer={(pointerResolver != null ? "ready" : "null")}");
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
                        $"[Input] SecondaryClick closed local aim on slot {i} " +
                        $"eventSeq={inputEvent.LocalEventSequence}.");
                    return;
                }
            }

            if (controlledUnit == null || pointerResolver == null)
            {
                Debug.Log(
                    $"[InputTrace] SecondaryClick eventSeq={inputEvent.LocalEventSequence} " +
                    "did not submit Move/Attack: controlled unit or pointer resolver is null.");
                return;
            }
            UnitUid? target = pointerResolver.ResolveUnitTarget(inputEvent.ScreenPositionAtEvent);
            if (target.HasValue)
            {
                bool attackSubmitted = RequestAttack(target.Value);
                Debug.Log(
                    $"[InputTrace] SecondaryClick eventSeq={inputEvent.LocalEventSequence} " +
                    $"unitTarget={target.Value} attackSubmitted={attackSubmitted} " +
                    $"statesAfterAttack={DescribeAbilityStates()}");
                if (attackSubmitted) return;
            }
            fp2? point = pointerResolver.ResolveGroundPoint(inputEvent.ScreenPositionAtEvent);
            if (point.HasValue)
            {
                bool moveSubmitted = RequestMove(point.Value);
                Debug.Log(
                    $"[InputTrace] SecondaryClick eventSeq={inputEvent.LocalEventSequence} " +
                    $"groundTarget={point.Value} moveSubmitted={moveSubmitted} " +
                    $"statesAfterMove={DescribeAbilityStates()}");
            }
            else
            {
                Debug.Log(
                    $"[InputTrace] SecondaryClick eventSeq={inputEvent.LocalEventSequence} " +
                    "resolved neither a unit target nor a ground point.");
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
            if (state.Kind != LocalAbilityInputStateKind.Idle)
            {
                Debug.Log(
                    $"[InputTrace] AbilityKeyPressed eventSeq={inputEvent.LocalEventSequence} " +
                    $"slot={slot} ignored because state={state.Kind}.");
                return;
            }

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
                    if (!CanOpenLocalAim(slot))
                    {
                        Debug.Log(
                            $"[Input] Press slot {slot}: " +
                            "LocalAimOnly ignored (ability on cooldown " +
                            "or current session cannot accept the next " +
                            "Commit).");
                        return;
                    }
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

        private bool CanOpenLocalAim(byte slot)
        {
            if (abilityRuntimeView != null)
            {
                return abilityRuntimeView.CanOpenLocalAim(
                    controlledUnit.UnitUid,
                    slot);
            }
            return controlledUnit?.AbilityHandler
                    ?.CanOpenLocalAim(slot) ??
                true;
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
                Debug.Log(
                    $"[InputTrace] AbilityKeyReleased eventSeq={inputEvent.LocalEventSequence} " +
                    $"slot={slot} ignored because state={state.Kind}.");
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
            bool foundEligibleSlot = false;
            for (byte slot = 0; slot < AbilitySlotCount; slot++)
            {
                ref LocalAbilityInputState state = ref abilityStates[slot];
                if (state.Kind != LocalAbilityInputStateKind.LocalAiming
                    && state.Kind != LocalAbilityInputStateKind.FocusRequested
                    && state.Kind != LocalAbilityInputStateKind.GameplayFocusing)
                {
                    continue;
                }
                foundEligibleSlot = true;
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
                {
                    Debug.Log(
                        $"[InputTrace] PrimaryClick eventSeq={inputEvent.LocalEventSequence} " +
                        $"slot={slot} could not build aim; state remains {state.Kind}.");
                    return;
                }
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
                else
                {
                    Debug.Log(
                        $"[InputTrace] PrimaryClick eventSeq={inputEvent.LocalEventSequence} " +
                        $"slot={slot} RequestCastAbility returned false; state remains {state.Kind}.");
                }
                return;
            }
            if (!foundEligibleSlot)
            {
                Debug.Log(
                    $"[InputTrace] PrimaryClick eventSeq={inputEvent.LocalEventSequence} " +
                    "had no LocalAiming/FocusRequested/GameplayFocusing slot.");
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
                    if (!point.HasValue)
                    {
                        Debug.Log(
                            $"[InputTrace] TryBuildAim failed eventSeq={inputEvent.LocalEventSequence} " +
                            $"slot={slot} kind={kind} reason=ground-point-unresolved.");
                    }
                    return point.HasValue;
                case AimKind.Unit:
                    UnitUid? target = pointerResolver?.ResolveUnitTarget(inputEvent.ScreenPositionAtEvent);
                    aim = target.HasValue ? AimSnapshot.ForUnit(target.Value) : default;
                    if (!target.HasValue)
                    {
                        Debug.Log(
                            $"[InputTrace] TryBuildAim failed eventSeq={inputEvent.LocalEventSequence} " +
                            $"slot={slot} kind={kind} reason=unit-target-unresolved.");
                    }
                    return target.HasValue;
                case AimKind.Direction:
                    fp2? groundPoint = pointerResolver?.ResolveGroundPoint(inputEvent.ScreenPositionAtEvent);
                    if (!groundPoint.HasValue || controlledUnit?.MovementHandler == null)
                    {
                        aim = default;
                        Debug.Log(
                            $"[InputTrace] TryBuildAim failed eventSeq={inputEvent.LocalEventSequence} " +
                            $"slot={slot} kind={kind} " +
                            $"reason={(groundPoint.HasValue ? "movement-handler-null" : "ground-point-unresolved")}.");
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
                        Debug.Log(
                            $"[InputTrace] TryBuildAim failed eventSeq={inputEvent.LocalEventSequence} " +
                            $"slot={slot} kind={kind} reason=zero-direction " +
                            $"groundPoint={groundPoint.Value} caster={controlledUnit.MovementHandler.Position}.");
                        return false;
                    }
                default:
                    aim = default;
                    Debug.Log(
                        $"[InputTrace] TryBuildAim failed eventSeq={inputEvent.LocalEventSequence} " +
                        $"slot={slot} kind={kind} reason=unsupported-aim-kind.");
                    return false;
            }
        }

        private string DescribeAbilityStates()
        {
            string description = "[";
            bool hasState = false;
            for (int i = 0; i < abilityStates.Length; i++)
            {
                LocalAbilityInputState state = abilityStates[i];
                if (state.Kind == LocalAbilityInputStateKind.Idle)
                    continue;
                if (hasState)
                    description += ",";
                description +=
                    $"{i}:{state.Kind}/target={state.LastRequestReceipt.TargetTick}/seq={state.LastRequestReceipt.CommandSeq}";
                hasState = true;
            }
            return hasState ? description + "]" : "[]";
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
            // The deterministic tick published by the last completed
            // simulation Tick. A Command whose Receipt.TargetTick has been
            // executed is observable through the runtime view; until then
            // the local state stays pending so duplicate input is
            // suppressed (Player Input v1.1 17.4).
            int currentTick = completedGameplayTickProvider != null
                ? completedGameplayTickProvider()
                : SimulationTickContext.Current.Tick;
            for (byte slot = 0; slot < AbilitySlotCount; slot++)
            {
                ref LocalAbilityInputState state = ref abilityStates[slot];
                if (state.Kind == LocalAbilityInputStateKind.Idle ||
                    state.Kind == LocalAbilityInputStateKind.LocalAiming)
                {
                    continue;
                }
                bool hasSession = abilityRuntimeView.HasActiveSession(
                    controlledUnit.UnitUid, slot);
                bool waiting = abilityRuntimeView.IsWaitingForCommit(
                    controlledUnit.UnitUid, slot);
                bool targetReached =
                    currentTick >=
                    state.LastRequestReceipt.TargetTick;
                LocalAbilityInputStateKind beforeKind = state.Kind;
                int requestTargetTick =
                    state.LastRequestReceipt.TargetTick;

                switch (state.Kind)
                {
                    case LocalAbilityInputStateKind.FocusRequested:
                        // Player Input v1.1 17.4: only after the Receipt
                        // TargetTick may the runtime view decide the fate of
                        // the Focus Command.
                        if (!targetReached)
                        {
                            break;
                        }
                        if (hasSession || waiting)
                        {
                            state.Kind =
                                LocalAbilityInputStateKind
                                    .GameplayFocusing;
                        }
                        else if (!state.AwaitingAcceptedExecution)
                        {
                            state = default;
                        }
                        break;

                    case LocalAbilityInputStateKind.GameplayFocusing:
                        if (!hasSession &&
                            !state.AwaitingAcceptedExecution)
                        {
                            state = default;
                        }
                        break;

                    case LocalAbilityInputStateKind.CommitRequested:
                        // Player Input v1.1 17.4: only after the Receipt
                        // TargetTick may the runtime view decide the fate of
                        // the Commit Command.
                        if (!targetReached)
                        {
                            break;
                        }
                        if (state.AwaitingAcceptedExecution)
                        {
                            break;
                        }
                        if (waiting && hasSession)
                        {
                            // Commit was not accepted by Gameplay while the
                            // Session still waits: recover to Focusing.
                            state.Kind =
                                LocalAbilityInputStateKind
                                    .GameplayFocusing;
                        }
                        else
                        {
                            // Session ended, or the session advanced past
                            // the waiting stage (e.g. a sequential recast
                            // window): the local state returns to Idle so
                            // the next key press can open the next stage.
                            state = default;
                        }
                        break;
                }

                if (state.Kind != beforeKind)
                {
                    Debug.Log(
                        $"[AbilityLocalState] unit={controlledUnit.UnitUid} " +
                        $"slot={slot} completedTick={currentTick} " +
                        $"targetTick={requestTargetTick} " +
                        $"session={hasSession} waiting={waiting} " +
                        $"state={beforeKind}->{state.Kind}");
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
            Array.Clear(
                abilityRequestDiagnostics,
                0,
                abilityRequestDiagnostics.Length);
            nextAbilityRequestDiagnosticIndex = 0;
        }

        private void RecordAbilityRequest(
            in CommandHeader header,
            byte slot,
            AbilitySignalVerb verb)
        {
            int index = nextAbilityRequestDiagnosticIndex;
            nextAbilityRequestDiagnosticIndex =
                (nextAbilityRequestDiagnosticIndex + 1) %
                abilityRequestDiagnostics.Length;
            ref AbilityRequestDiagnostic existing =
                ref abilityRequestDiagnostics[index];
            if (existing.Active &&
                !existing.MissingAtTargetLogged)
            {
                Debug.LogWarning(
                    $"[AbilityCommandDiagnostic] request ring overwrote " +
                    $"seq={existing.CommandSeq} slot={existing.Slot} " +
                    $"targetTick={existing.RequestTargetTick}.");
            }
            existing = new AbilityRequestDiagnostic
            {
                Active = true,
                UnitUid = header.ControlledUnitUid,
                Slot = slot,
                Verb = verb,
                CommandSeq = header.CommandSeq,
                RequestTargetTick = header.TargetTick,
                BuildLocalTick = header.BuildLocalTick,
                MissingAtTargetLogged = false,
                LastObservationTick = -1,
                LastObservationMode = default,
            };
        }

        private int FindAbilityRequestDiagnostic(uint commandSeq)
        {
            for (int i = 0;
                i < abilityRequestDiagnostics.Length;
                i++)
            {
                AbilityRequestDiagnostic request =
                    abilityRequestDiagnostics[i];
                if (request.Active &&
                    request.CommandSeq == commandSeq &&
                    controlledUnit != null &&
                    request.UnitUid == controlledUnit.UnitUid)
                {
                    return i;
                }
            }
            return -1;
        }

        private struct AbilityRequestDiagnostic
        {
            public bool Active;
            public UnitUid UnitUid;
            public byte Slot;
            public AbilitySignalVerb Verb;
            public uint CommandSeq;
            public int RequestTargetTick;
            public int BuildLocalTick;
            public bool MissingAtTargetLogged;
            public int LastObservationTick;
            public ExecutionMode LastObservationMode;
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
                AwaitingAcceptedExecution = false,
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
