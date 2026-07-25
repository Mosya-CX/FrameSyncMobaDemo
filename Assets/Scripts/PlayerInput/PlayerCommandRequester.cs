using System;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
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
    }

    public enum BakedPlayerAbilityInputMode : byte
    {
        PressCommit = 0,
        LocalAimPrimaryCommit = 1,
        PressFocusReleaseOrPrimaryCommit = 2,
    }

    public readonly struct BakedPlayerAbilityInputProfile
    {
        public readonly BakedPlayerAbilityInputMode Mode;

        public BakedPlayerAbilityInputProfile(BakedPlayerAbilityInputMode mode)
        {
            Mode = mode;
        }
    }

    public interface IPlayerAbilityInputProfileProvider
    {
        bool TryGetProfile(byte slot, out BakedPlayerAbilityInputProfile profile);
        bool TryGetAimKind(byte slot, out AimKind aimKind);
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

    public sealed class PlayerCommandRequester : IPlayerGameplayCommandRequester
    {
        private const int AbilitySlotCount = 4;

        private readonly IGameplayInputGate gate;
        private readonly CommandCollector collector;
        private readonly Func<int> buildLocalTickProvider;
        private readonly Func<int> targetTickProvider;
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
            Func<int> buildLocalTickProvider,
            Func<int> targetTickProvider,
            IPlayerAbilityInputProfileProvider profileProvider = null,
            ILocalAbilityRuntimeView abilityRuntimeView = null)
        {
            this.controlledUnit = controlledUnit;
            this.gate = gate ?? throw new ArgumentNullException(nameof(gate));
            this.collector = collector ?? throw new ArgumentNullException(nameof(collector));
            this.playerSlot = playerSlot;
            this.clientId = clientId;
            this.buildLocalTickProvider = buildLocalTickProvider
                ?? throw new ArgumentNullException(nameof(buildLocalTickProvider));
            this.targetTickProvider = targetTickProvider
                ?? throw new ArgumentNullException(nameof(targetTickProvider));
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
            castRange = (fp)5m; // default range
            casterPos = fp2.zero;
            casterForward = new fp2(fp.zero, fp.one);

            if (profileProvider == null) return false;
            if (!profileProvider.TryGetAimKind(slot, out aimKind)) return false;
            if (aimKind == AimKind.None) return false;

            // Get caster position from controlled unit
            if (controlledUnit?.MovementHandler != null)
            {
                casterPos = controlledUnit.MovementHandler.Snapshot.Position;
                casterForward = controlledUnit.MovementHandler.Snapshot.Facing;
            }

            return true;
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
                        CancelLocalAimOnly();
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

        private void ProcessSecondaryClick(
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver)
        {
            for (int i = 0; i < abilityStates.Length; i++)
            {
                if (abilityStates[i].Kind == LocalAbilityInputStateKind.LocalAiming)
                {
                    abilityStates[i] = default;
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
            ref LocalAbilityInputState state = ref abilityStates[slot];
            if (state.Kind != LocalAbilityInputStateKind.Idle) return;

            BakedPlayerAbilityInputProfile profile = GetProfile(slot);
            switch (profile.Mode)
            {
                case BakedPlayerAbilityInputMode.PressCommit:
                    if (TryBuildAim(slot, inputEvent, pointerResolver, out AimSnapshot pressAim)
                        && RequestCastAbility(
                            slot,
                            AbilitySignalVerb.Commit,
                            pressAim,
                            out GameplayCommandRequestReceipt pressReceipt))
                    {
                        state = CreateState(
                            slot,
                            LocalAbilityInputStateKind.CommitRequested,
                            pressReceipt);
                    }
                    break;

                case BakedPlayerAbilityInputMode.LocalAimPrimaryCommit:
                    state = CreateState(
                        slot,
                        LocalAbilityInputStateKind.LocalAiming,
                        default);
                    break;

                case BakedPlayerAbilityInputMode.PressFocusReleaseOrPrimaryCommit:
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
                    }
                    break;
            }
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

            if (!TryBuildAim(slot, inputEvent, pointerResolver, out AimSnapshot aim)) return;
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

                if (!TryBuildAim(slot, inputEvent, pointerResolver, out AimSnapshot aim)) return;
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
                }
                return;
            }
        }

        private bool TryBuildAim(
            byte slot,
            in LocalGameplayInputEvent inputEvent,
            MouseWorldResolver pointerResolver,
            out AimSnapshot aim)
        {
            AimKind kind = AimKind.None;
            profileProvider?.TryGetAimKind(slot, out kind);
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
                    fp2 direction = groundPoint.Value - controlledUnit.MovementHandler.Snapshot.Position;
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

        private BakedPlayerAbilityInputProfile GetProfile(byte slot)
        {
            if (profileProvider != null
                && profileProvider.TryGetProfile(slot, out BakedPlayerAbilityInputProfile profile))
            {
                return profile;
            }

            return new BakedPlayerAbilityInputProfile(
                BakedPlayerAbilityInputMode.PressCommit);
        }

        private CommandHeader CreateHeader(GameplayCommandKind kind)
        {
            if (nextCommandSeq == uint.MaxValue)
            {
                throw new InvalidOperationException("Player CommandSeq exhausted.");
            }

            return new CommandHeader(
                nextCommandSeq,
                clientId,
                playerSlot,
                controlledUnit.UnitUid,
                targetTickProvider(),
                kind,
                buildLocalTickProvider(),
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
                else if (!hasSession
                    && (state.Kind == LocalAbilityInputStateKind.GameplayFocusing
                        || state.Kind == LocalAbilityInputStateKind.CommitRequested))
                {
                    state = default;
                }
            }
        }

        private void CancelLocalAimOnly()
        {
            for (int i = 0; i < abilityStates.Length; i++)
            {
                if (abilityStates[i].Kind == LocalAbilityInputStateKind.LocalAiming)
                {
                    abilityStates[i] = default;
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
