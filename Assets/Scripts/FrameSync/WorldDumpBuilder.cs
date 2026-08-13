using System;
using System.Text;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Renders the complete world state as readable text (real values, not
    /// hashes) so a desync can be pinned down without guesswork.
    /// </summary>
    public static class WorldDumpBuilder
    {
        public static string BuildWorldDump(
            GameplaySnapshot snapshot)
        {
            var sb = new StringBuilder(16384);
            UnitSnapshot[] units =
                snapshot.UnitWorldState.Units ??
                Array.Empty<UnitSnapshot>();
            sb.AppendLine($"WorldDump units={units.Length}");
            for (int u = 0; u < units.Length; u++)
            {
                UnitSnapshot unit = units[u];
                sb.AppendLine(
                    $"Unit {unit.UnitUid} kind={unit.UnitKind} " +
                    $"team={unit.TeamId.Value} life={unit.LifeState} " +
                    $"proto={unit.UnitPrototypeId}");
                var pos = unit.PhysicsTransform.Position;
                sb.AppendLine($"  pos=({pos.x},{pos.y})");
                sb.AppendLine(
                    $"  intent={unit.IntentState.Kind} " +
                    $"cap=(move={unit.CapabilityState.CanMove} " +
                    $"attack={unit.CapabilityState.CanAttack} " +
                    $"cast={unit.CapabilityState.CanCast})");

                StatHandlerSnapshot stats = unit.StatState;
                sb.AppendLine(
                    $"  lvl={stats.Level} hp={stats.CurrentHealth} " +
                    $"mp={stats.CurrentCastResource} xp={stats.CurrentExperience} " +
                    $"nextSeq={stats.NextStatSeq}");
                for (int e = 0;
                     e < (stats.Entries?.Length ?? 0);
                     e++)
                {
                    StatRuntimeEntrySnapshot entry = stats.Entries[e];
                    sb.Append(
                        $"  stat {entry.StatId} base={entry.LevelBaseValue} " +
                        $"final={entry.FinalValue} prev={entry.PreviousLogicTickFinalValue} " +
                        $"dirty={entry.Dirty} mods=[");
                    for (int m = 0;
                         m < (entry.Modifiers?.Length ?? 0);
                         m++)
                    {
                        if (m > 0) sb.Append(", ");
                        sb.Append(
                            $"seq{entry.Modifiers[m].StatSeq}:" +
                            $"{entry.Modifiers[m].Operation}:" +
                            $"{entry.Modifiers[m].Value}");
                    }
                    sb.AppendLine("]");
                }

                BuffHandlerSnapshot buffs = unit.BuffState;
                for (int b = 0;
                     b < (buffs.Buffs?.Length ?? 0);
                     b++)
                {
                    BuffRuntimeSnapshot buff = buffs.Buffs[b];
                    sb.AppendLine(
                        $"  buff id={buff.ConfigId.Value} " +
                        $"src={buff.SourceUnitUid} stacks={buff.CurrentStacks} " +
                        $"remaining={buff.RemainingTicks} " +
                        $"permanent={buff.IsPermanent} removing={buff.IsRemoving}");
                }

                AbilityHandlerSnapshot ability = unit.AbilityState;
                sb.AppendLine(
                    $"  pendingPoints={ability.PendingSkillPoints} " +
                    $"fixedPassive={ability.FixedPassiveAbilityId}");
                var slots = ability.BookSnapshot.SlotSnapshots;
                for (int s = 0;
                     s < (slots?.Count ?? 0);
                     s++)
                {
                    AbilitySlotSnapshot slot = slots[s];
                    sb.Append(
                        $"  slot {slot.SlotIndex} points={slot.AllocatedPoints} " +
                        $"active={slot.ActiveAbilityId} runtimes=[");
                    for (int r = 0;
                         r < (slot.AbilityRuntimes?.Count ?? 0);
                         r++)
                    {
                        if (r > 0) sb.Append(", ");
                        AbilityRuntimeSnapshot runtime = slot.AbilityRuntimes[r];
                        sb.Append(
                            $"id{runtime.AbilityId} lvl{runtime.Level} " +
                            $"cdEnds={runtime.CooldownEndsAtTick}");
                        if (runtime.HasActiveSession)
                        {
                            sb.Append(
                                $" sess{runtime.ActiveSession.SessionUid} " +
                                $"stage{runtime.ActiveSession.CurrentStageKey} " +
                                $"elapsed{runtime.ActiveSession.StageElapsedTicks}");
                        }
                    }
                    sb.AppendLine("]");
                }

                CrowdControlHandlerSnapshot cc = unit.CCState;
                for (int c = 0;
                     c < (cc.Instances?.Count ?? 0);
                     c++)
                {
                    CrowdControlInstance inst = cc.Instances[c];
                    sb.AppendLine(
                        $"  cc id={inst.ControlId.Value} " +
                        $"inst={inst.InstanceId} start={inst.StartTick} " +
                        $"expire={inst.ExpireTick}");
                }

                EquipmentHandlerSnapshot equipment = unit.EquipmentState;
                sb.AppendLine($"  eqRev={equipment.RuntimeRevision}");
                for (int i = 0;
                     i < (equipment.Slots?.Count ?? 0);
                     i++)
                {
                    EquipmentSlotSnapshot slot = equipment.Slots[i];
                    if (!slot.Occupied) continue;
                    sb.Append(
                        $"  eqSlot[{i}] id={slot.EquipmentId} " +
                        $"stack={slot.StackCount} charge={slot.ChargeCount} " +
                        $"ready={slot.ReadyTick} handles=[");
                    for (int h = 0;
                         h < (slot.FixedStatHandles?.Count ?? 0);
                         h++)
                    {
                        if (h > 0) sb.Append(", ");
                        sb.Append(
                            $"{slot.FixedStatHandles[h].StatId}:" +
                            $"{slot.FixedStatHandles[h].StatSeq}");
                    }
                    sb.AppendLine("]");
                }

                MovementSnapshot movement = unit.MovementState;
                sb.AppendLine(
                    $"  dash={movement.Dash.IsActive} " +
                    $"forcedMove={movement.ForcedMove.IsActive}");
                LocomotionAgentSnapshot loco = unit.LocomotionState;
                sb.AppendLine(
                    $"  loco={loco.HasActiveTask} " +
                    $"purpose={loco.Task.Purpose} " +
                    $"state={loco.Task.State} " +
                    $"needRepath={loco.Route.NeedRepath} " +
                    $"cursor={loco.FollowerState.PathCursor}");
                AttackSnapshot attack = unit.AttackState;
                sb.AppendLine(
                    $"  attackTarget={attack.CurrentTargetUid} " +
                    $"start={attack.AttackStartLogicTick} " +
                    $"impact={attack.ImpactLogicTick} " +
                    $"nextReady={attack.NextAttackReadyLogicTick} " +
                    $"seq={attack.AttackSequenceIndex}");
            }

            EquipmentShopRuntimeSnapshot shop =
                snapshot.EquipmentShopState;
            var traders = shop.CreatedTraders;
            sb.AppendLine($"Shop traders={traders?.Count ?? 0}");
            if (traders != null)
            {
                for (int t = 0; t < traders.Count; t++)
                {
                    ShopTraderRuntimeSnapshot trader = traders[t];
                    sb.AppendLine(
                        $"  p={trader.Player} unit={trader.ControlledUnitUid} " +
                        $"nextSeq={trader.NextOperationSequence} " +
                        $"undo=[{(trader.UndoableOperationStack == null ? "" : string.Join(",", trader.UndoableOperationStack))}]");
                    var ops = trader.OperationLog;
                    for (int o = 0;
                         o < (ops?.Count ?? 0);
                         o++)
                    {
                        ShopOperationRecord op = ops[o];
                        sb.AppendLine(
                            $"    op seq={op.OperationSequence} " +
                            $"type={(byte)op.OperationType} tick={op.LogicTick} " +
                            $"gold={op.GoldDelta} reverted={op.Reverted} " +
                            $"rev={op.EquipmentRevisionBefore}->{op.EquipmentRevisionAfter}");
                    }
                }
            }
            return sb.ToString();
        }
    }
}
