using System.Collections.Generic;
using System.Text;
using FrameSyncMoba.Unit;

namespace FrameSyncMoba.FrameSync
{
    /// <summary>
    /// Diagnostic-only formatter that expands Equipment slots and the
    /// EquipmentShop trader state into readable lines. Used by the checksum
    /// detail printers on the server and the client so a desync can be
    /// pinned down to an exact field without rebuilding the world.
    /// </summary>
    internal static class ChecksumDiagnosticFormatter
    {
        public static void AppendWorldState(
            List<string> lines,
            in GameplaySnapshot snapshot)
        {
            UnitWorldSnapshot world = snapshot.UnitWorldState;
            UnitSnapshot[] units = world.Units ??
                System.Array.Empty<UnitSnapshot>();
            var respawns = world.PendingUnitLifecycleState.Entries;
            var disposals =
                world.PendingUnitLifecycleState.DisposalEntries;
            ProjectileRuntimeSnapshot[] projectiles =
                snapshot.ProjectileState.ActiveProjectiles ??
                System.Array.Empty<ProjectileRuntimeSnapshot>();
            PendingSpawnRecordSnapshot[] pendingProjectiles =
                snapshot.ProjectileState.PendingSpawns ??
                System.Array.Empty<PendingSpawnRecordSnapshot>();
            lines.Add(
                $"  World schema={snapshot.SchemaVersion} " +
                $"random={snapshot.RandomState.State} " +
                $"units={units.Length} revision={world.RuntimeRevision} " +
                $"respawns={respawns?.Count ?? 0} " +
                $"disposals={disposals?.Count ?? 0} " +
                $"ai={world.AIControllerStates?.Length ?? 0} " +
                $"projectiles={projectiles.Length} " +
                $"pendingProjectiles={pendingProjectiles.Length}");
            lines.Add(
                $"  Minions wave={world.MinionSystemState.WaveIndex} " +
                $"nextWave={world.MinionSystemState.NextWaveLogicTick} " +
                $"ticketCursor={world.MinionSystemState.NextTicketCursor} " +
                $"tickets={world.MinionSystemState.PendingTickets?.Length ?? 0} " +
                $"managed={world.MinionSystemState.ManagedMinionUids?.Length ?? 0}");

            if (respawns != null)
            {
                for (int i = 0; i < respawns.Count; i++)
                {
                    RespawnEntry entry = respawns[i];
                    lines.Add(
                        $"  Respawn[{i}] unit={entry.UnitUid} " +
                        $"death={entry.DeathLogicTick} " +
                        $"at={entry.RespawnLogicTick}");
                }
            }
            if (disposals != null)
            {
                for (int i = 0; i < disposals.Count; i++)
                {
                    DeathDisposalEntry entry = disposals[i];
                    lines.Add(
                        $"  Disposal[{i}] unit={entry.UnitUid} " +
                        $"death={entry.DeathLogicTick} " +
                        $"at={entry.DisposeLogicTick}");
                }
            }
        }

        public static void AppendUnitState(
            List<string> lines,
            in UnitSnapshot unit)
        {
            UnitIntent intent = unit.IntentState;
            ActionRuntimeSlotSnapshot main =
                unit.ActionRuntimeState.Main;
            ActionRuntimeSlotSnapshot baseAction =
                unit.ActionRuntimeState.Base;
            AttackSnapshot attack = unit.AttackState;
            var transform = unit.PhysicsTransform;
            LocomotionAgentSnapshot locomotion =
                unit.LocomotionState;

            lines.Add(
                $"    identity participant={unit.GameplayParticipantId} " +
                $"prototype={unit.UnitPrototypeId} team={unit.TeamId.Value} " +
                $"life={unit.LifeState}");
            lines.Add(
                $"    pose=({transform.Position.x},{transform.Position.y}) " +
                $"prev=({transform.PrevPosition.x},{transform.PrevPosition.y}) " +
                $"forward=({transform.Forward.x},{transform.Forward.y})");
            lines.Add(
                $"    intent={intent.Kind} target={intent.TargetUnit} " +
                $"point=({intent.TargetPosition.x},{intent.TargetPosition.y}) " +
                $"ability={intent.AbilityId}/{intent.AbilityVerb} " +
                $"chase={intent.AllowChase} replan={intent.AllowReplan}");
            lines.Add(
                $"    main={FormatAction(main)} " +
                $"base={FormatAction(baseAction)}");
            lines.Add(
                $"    attack target={attack.CurrentTargetUid} " +
                $"start={attack.AttackStartLogicTick} " +
                $"impact={attack.ImpactLogicTick} " +
                $"ready={attack.NextAttackReadyLogicTick} " +
                $"committed={attack.ImpactCommitted} " +
                $"seq={attack.AttackSequenceIndex} " +
                $"last={attack.LastSuccessfulAttackLogicTick}");
            lines.Add(
                $"    locomotion active={locomotion.HasActiveTask} " +
                $"purpose={locomotion.Task.Purpose} " +
                $"state={locomotion.Task.State} " +
                $"cursor={locomotion.FollowerState.PathCursor} " +
                $"finished={locomotion.FollowerState.RouteFinished} " +
                $"repath={locomotion.Route.NeedRepath}");
        }

        private static string FormatAction(
            in ActionRuntimeSlotSnapshot action)
        {
            if (!action.IsOccupied)
                return "empty";
            return
                $"{action.Kind}/{action.Phase}" +
                $"[resources={action.OccupiedResources}," +
                $"target={action.TargetUnitUid}," +
                $"abilitySlot={action.AbilitySlot}]";
        }

        public static void AppendEquipmentSlots(
            List<string> lines,
            in UnitSnapshot unit)
        {
            EquipmentHandlerSnapshot equipment = unit.EquipmentState;
            var slots = equipment.Slots;
            lines.Add(
                $"    eqSlots={(slots == null ? 0 : slots.Count)} " +
                $"rev={equipment.RuntimeRevision}");
            if (slots == null)
            {
                return;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlotSnapshot slot = slots[i];
                if (!slot.Occupied)
                {
                    continue;
                }
                var handleText = new StringBuilder();
                if (slot.FixedStatHandles != null)
                {
                    for (int h = 0;
                         h < slot.FixedStatHandles.Count;
                         h++)
                    {
                        if (h > 0) handleText.Append(',');
                        handleText.Append(
                            slot.FixedStatHandles[h].StatId);
                        handleText.Append(':');
                        handleText.Append(
                            slot.FixedStatHandles[h].StatSeq);
                    }
                }
                lines.Add(
                    $"    eqSlot[{i}] id={slot.EquipmentId} " +
                    $"stack={slot.StackCount} charge={slot.ChargeCount} " +
                    $"ready={slot.ReadyTick} handles=[{handleText}]");
                if (slot.EffectStates == null)
                {
                    continue;
                }
                for (int e = 0; e < slot.EffectStates.Count; e++)
                {
                    var modules =
                        slot.EffectStates[e].ModuleStates;
                    if (modules == null)
                    {
                        continue;
                    }
                    for (int m = 0; m < modules.Count; m++)
                    {
                        var module = modules[m];
                        lines.Add(
                            $"    eqSlot[{i}] eff[{e}]mod[{m}] " +
                            $"next={module.NextExecuteTick} " +
                            $"icd={module.InternalCooldownReadyTick} " +
                            $"stack={module.StackCount} " +
                            $"trigger={module.TriggerCount}");
                    }
                }
            }
        }

        public static void AppendShopState(
            List<string> lines,
            in EquipmentShopRuntimeSnapshot shop)
        {
            var traders = shop.CreatedTraders;
            lines.Add(
                $"  Shop traders={traders?.Count ?? 0}");
            if (traders == null)
            {
                return;
            }
            for (int t = 0; t < traders.Count; t++)
            {
                ShopTraderRuntimeSnapshot trader = traders[t];
                var undoStack = trader.UndoableOperationStack;
                lines.Add(
                    $"    trader p={trader.Player} " +
                    $"unit={trader.ControlledUnitUid} " +
                    $"nextSeq={trader.NextOperationSequence} " +
                    $"undo=[{(undoStack == null ? "" : string.Join(",", undoStack))}]");
                var operations = trader.OperationLog;
                if (operations == null)
                {
                    continue;
                }
                for (int o = 0; o < operations.Count; o++)
                {
                    ShopOperationRecord operation = operations[o];
                    var changes = operation.SlotChanges;
                    var changeText = new StringBuilder();
                    if (changes != null)
                    {
                        for (int c = 0; c < changes.Length; c++)
                        {
                            if (c > 0) changeText.Append(';');
                            changeText.Append(
                                $"slot{changes[c].Slot}:" +
                                $"{changes[c].Before.EquipmentId}->" +
                                $"{changes[c].After.EquipmentId}");
                        }
                    }
                    lines.Add(
                        $"      op seq={operation.OperationSequence} " +
                        $"type={(byte)operation.OperationType} " +
                        $"tick={operation.LogicTick} " +
                        $"gold={operation.GoldDelta} " +
                        $"rev={operation.EquipmentRevisionBefore}->" +
                        $"{operation.EquipmentRevisionAfter} " +
                        $"reverted={operation.Reverted}@" +
                        $"{operation.RevertedLogicTick} " +
                        $"changes=[{changeText}]");
                }
            }
        }
    }
}
