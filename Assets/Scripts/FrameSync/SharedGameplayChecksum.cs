using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public static class SharedGameplayChecksum
    {
        public static uint Compute(
            in GameplaySnapshot snapshot,
            GoldIncomeBatchDigest goldDigest,
            CanonicalByteWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.Reset();
            writer.WriteInt32(snapshot.SchemaVersion);
            writer.WriteUInt32(snapshot.RandomState.State);
            WriteMatchRule(writer, snapshot.MatchRuleState);
            WriteUnitWorld(writer, snapshot.UnitWorldState);
            WriteCombat(writer, snapshot.CombatState);
            WriteProjectiles(writer, snapshot.ProjectileState);
            WriteEquipmentShop(writer, snapshot.EquipmentShopState);
            WritePhysics(writer, snapshot.PhysicsState);
            writer.WriteUInt64(goldDigest.Value);
            ArraySegment<byte> bytes = writer.GetWrittenSegment();
            return DeterministicHash32.Compute(bytes.Array, bytes.Offset, bytes.Count);
        }

        private static void WriteMatchRule(
            CanonicalByteWriter writer,
            in MatchRuleRuntimeSnapshot state)
        {
            writer.WriteByte((byte)state.CurrentPhase); writer.WriteInt32(state.PhaseEnterTick);
            writer.WriteInt32(state.RunningStartTick); WriteUnitUid(writer, state.BlueBaseUnitUid);
            WriteUnitUid(writer, state.RedBaseUnitUid); writer.WriteInt32(state.GameOverTick);
            writer.WriteInt32(state.FinishTick); writer.WriteByte(state.WinningTeamId.Value);
            writer.WriteByte((byte)state.EndReason);
            var entries = state.Statistics.Entries ?? new List<MatchStatisticsEntry>();
            writer.WriteInt32(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                WriteUnitUid(writer, entries[i].HeroUnitUid); writer.WriteInt32(entries[i].Kills);
                writer.WriteInt32(entries[i].Deaths); writer.WriteInt32(entries[i].Assists);
            }
        }

        public static uint Compute(
            IReadOnlyList<UnitType> units,
            CanonicalByteWriter writer)
        {
            writer.Reset();
            writer.WriteInt32(units?.Count ?? 0);
            if (units != null)
            {
                for (int i = 0; i < units.Count; i++)
                {
                    WriteUnitUid(writer, units[i].UnitUid);
                    MovementHandler movement = units[i].MovementHandler;
                    WriteFp2(writer, movement.Position);
                    WriteFp2(writer, movement.Velocity);
                    WriteFp2(writer, movement.Facing);
                    writer.WriteFp(movement.MoveSpeed);
                }
            }
            ArraySegment<byte> bytes = writer.GetWrittenSegment();
            return DeterministicHash32.Compute(bytes.Array, bytes.Offset, bytes.Count);
        }

        private static void WriteUnitWorld(CanonicalByteWriter writer, in UnitWorldSnapshot state)
        {
            UnitSnapshot[] units = state.Units ?? Array.Empty<UnitSnapshot>();
            writer.WriteInt32(units.Length);
            for (int i = 0; i < units.Length; i++) WriteUnit(writer, units[i]);
            writer.WriteInt32(state.RuntimeRevision);
            var lifecycleEntries =
                state.PendingUnitLifecycleState.Entries ?? new List<RespawnEntry>();
            writer.WriteInt32(lifecycleEntries.Count);
            for (int i = 0; i < lifecycleEntries.Count; i++)
            {
                WriteUnitUid(writer, lifecycleEntries[i].UnitUid);
                writer.WriteInt32(lifecycleEntries[i].DeathLogicTick);
                writer.WriteInt32(lifecycleEntries[i].RespawnLogicTick);
            }
            writer.WriteInt32(state.MinionSystemState.WaveIndex);
            writer.WriteInt32(state.MinionSystemState.NextWaveLogicTick);
            writer.WriteInt32(state.MinionSystemState.NextTicketCursor);
            MinionTicket[] tickets = state.MinionSystemState.PendingTickets ?? Array.Empty<MinionTicket>();
            writer.WriteInt32(tickets.Length);
            for (int ticketIndex = 0; ticketIndex < tickets.Length; ticketIndex++)
            {
                MinionTicket ticket = tickets[ticketIndex];
                writer.WriteInt32(ticket.SpawnLogicTick);
                writer.WriteByte(ticket.TeamId.Value);
                writer.WriteUInt32(ticket.LaneId);
                writer.WriteInt32(ticket.UnitPrototypeId);
                writer.WriteInt32(ticket.StableEntryIndex);
                WriteFp2(writer, ticket.SpawnPosition);
                WriteFp2(writer, ticket.SpawnForward);
            }
            WriteUidList(writer, state.MinionSystemState.ManagedMinionUids);
            JungleCampSnapshot[] camps = state.JungleCampStates ?? Array.Empty<JungleCampSnapshot>();
            writer.WriteInt32(camps.Length);
            for (int i = 0; i < camps.Length; i++)
            {
                JungleCampSnapshot camp = camps[i];
                writer.WriteInt32(camp.CampId); writer.WriteByte((byte)camp.State);
                WriteUidList(writer, camp.MemberUidsBySlot);
                bool[] alive = camp.MemberAliveBySlot ?? Array.Empty<bool>();
                writer.WriteInt32(alive.Length);
                for (int memberIndex = 0; memberIndex < alive.Length; memberIndex++)
                    writer.WriteBoolean(alive[memberIndex]);
                writer.WriteBoolean(camp.MainMonsterDead);
                WriteUnitUid(writer, camp.PrimaryTargetUid);
                writer.WriteInt32(camp.LastHostileActionLogicTick);
                writer.WriteInt32(camp.NextRespawnLogicTick);
                writer.WriteInt32(camp.ResetBeginLogicTick);
            }
            UnitAIControllerSnapshot[] ais = state.AIControllerStates ?? Array.Empty<UnitAIControllerSnapshot>();
            writer.WriteInt32(ais.Length);
            for (int i = 0; i < ais.Length; i++)
            {
                UnitAIControllerSnapshot ai = ais[i];
                WriteUnitUid(writer, ai.OwnerUnitUid); writer.WriteByte((byte)ai.ControllerKind);
                writer.WriteByte((byte)ai.MinionState);
                writer.WriteInt32(ai.LaneId);
                writer.WriteInt32(ai.MinionNextDecisionLogicTick);
                writer.WriteInt32(ai.MinionTargetLockUntilLogicTick);
                WriteFp2(writer, ai.MinionEngageOrigin);
                WriteUnitUid(writer, ai.MinionPendingAssistTargetUid);
                writer.WriteInt32(ai.MinionPendingAssistExpireLogicTick);
                writer.WriteByte((byte)ai.MonsterState);
                writer.WriteInt32(ai.CampId);
                writer.WriteInt32(ai.MonsterCampSlotIndex);
                writer.WriteInt32(ai.MonsterNextDecisionLogicTick);
                writer.WriteByte((byte)ai.TowerState);
            }
        }

        private static void WriteUnit(CanonicalByteWriter writer, in UnitSnapshot state)
        {
            WriteUnitUid(writer, state.UnitUid); WriteUnitUid(writer, state.OwnerUid);
            writer.WriteByte((byte)state.UnitKind); writer.WriteUInt32(state.UnitSubKindId);
            writer.WriteByte(state.TeamId.Value); writer.WriteInt32(state.UnitPrototypeId);
            writer.WriteByte((byte)state.LifeState);
            writer.WriteBoolean(state.CapabilityState.CanMove); writer.WriteBoolean(state.CapabilityState.CanAttack);
            writer.WriteBoolean(state.CapabilityState.CanCast); writer.WriteBoolean(state.CapabilityState.CanTurn);
            writer.WriteBoolean(state.CapabilityState.IsTargetable);
            writer.WriteByte((byte)state.HitReactionState.ActiveReaction);
            writer.WriteInt32(state.HitReactionState.RemainingTicks); writer.WriteInt32(state.HitReactionState.TotalTicks);
            WriteIntent(writer, state.IntentState);
            WriteFp2(writer, state.PhysicsTransform.Position); WriteFp2(writer, state.PhysicsTransform.PrevPosition);
            WriteFp2(writer, state.PhysicsTransform.Forward); WriteFp2(writer, state.PhysicsTransform.Right);
            writer.WriteByte((byte)state.PhysicsShape.Kind); WriteFp2(writer, state.PhysicsShape.LocalOffset);
            writer.WriteFp(state.PhysicsShape.Radius); writer.WriteFp(state.PhysicsShape.Length);
            writer.WriteFp(state.PhysicsShape.Width); WriteFp2(writer, state.PhysicsShape.HalfExtents);
            writer.WriteBoolean(state.PhysicsShape.SweepFromPrev);
            WriteStats(writer, state.StatState);
            WriteCombatModifiers(writer, state.CombatModifierState);
            WriteMovement(writer, state.MovementState);
            WriteLocomotion(writer, state.LocomotionState);
            WriteAttack(writer, state.AttackState);
            WriteAbility(writer, state.AbilityState);
            WriteBuffs(writer, state.BuffState);
            WriteCrowdControl(writer, state.CCState);
            WriteEquipment(writer, state.EquipmentState);
        }

        private static void WriteStats(CanonicalByteWriter writer, in StatHandlerSnapshot state)
        {
            writer.WriteInt32(state.Level); writer.WriteFp(state.CurrentHealth);
            writer.WriteFp(state.CurrentCastResource); writer.WriteInt32(state.CurrentExperience);
            writer.WriteUInt32(state.NextStatSeq);
            writer.WriteInt32(state.NextShieldInstanceId);
            ShieldInstance[] shields = state.ShieldInstances ?? Array.Empty<ShieldInstance>();
            writer.WriteInt32(shields.Length);
            for (int i = 0; i < shields.Length; i++)
            {
                ShieldInstance shield = shields[i];
                writer.WriteInt32(shield.ShieldInstanceId); writer.WriteByte((byte)shield.ShieldType);
                writer.WriteFp(shield.CurrentValue); writer.WriteFp(shield.MaxValue);
                writer.WriteInt32(shield.StartLogicTick); writer.WriteInt32(shield.ExpireLogicTick);
                WriteUnitUid(writer, shield.SourceUnitUid);
                WriteUnitUid(writer, shield.CrowdControlImmunityHandle.TargetUnitUid);
                writer.WriteInt32(shield.CrowdControlImmunityHandle.ImmunityId);
            }
            StatRuntimeEntrySnapshot[] entries = state.Entries ?? Array.Empty<StatRuntimeEntrySnapshot>();
            writer.WriteInt32(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                StatRuntimeEntrySnapshot entry = entries[i];
                writer.WriteInt32((int)entry.StatId); writer.WriteFp(entry.LevelBaseValue);
                writer.WriteFp(entry.FinalValue); writer.WriteFp(entry.PreviousLogicTickFinalValue);
                writer.WriteBoolean(entry.Dirty);
                StatModifier[] modifiers = entry.Modifiers ?? Array.Empty<StatModifier>();
                writer.WriteInt32(modifiers.Length);
                for (int j = 0; j < modifiers.Length; j++)
                { writer.WriteUInt32(modifiers[j].StatSeq); writer.WriteByte((byte)modifiers[j].Operation); writer.WriteFp(modifiers[j].Value); }
            }
        }

        private static void WriteMovement(CanonicalByteWriter writer, in MovementSnapshot state)
        {
            writer.WriteBoolean(state.Dash.IsActive);
            writer.WriteInt32(state.Dash.StartTick);
            writer.WriteInt32(state.Dash.ConfigId);
            writer.WriteInt32(state.Dash.DurationTicks);
            WriteFp2(writer, state.Dash.StartPosition);
            WriteFp2(writer, state.Dash.Direction);
            WriteFp2(writer, state.Dash.TargetPosition);
            writer.WriteByte((byte)state.Dash.WallPolicy);
            writer.WriteBoolean(state.ForcedMove.IsActive);
            WriteUnitUid(
                writer,
                state.ForcedMove.SourceControlHandle.TargetUnitUid);
            writer.WriteInt32(
                state.ForcedMove.SourceControlHandle.InstanceId);
            writer.WriteInt32(state.ForcedMove.StartTick);
            writer.WriteInt32(state.ForcedMove.DurationTicks);
            WriteFp2(writer, state.ForcedMove.StartPosition);
            WriteFp2(writer, state.ForcedMove.Direction);
            WriteFp2(writer, state.ForcedMove.TargetPosition);
            writer.WriteInt32(state.ForcedMove.ConfigId);
            writer.WriteByte((byte)state.ForcedMove.WallPolicy);
        }

        private static void WriteIntent(CanonicalByteWriter writer, in UnitIntent state)
        {
            writer.WriteByte((byte)state.Kind);
            WriteUnitUid(writer, state.TargetUnit);
            WriteFp2(writer, state.TargetPosition);
            writer.WriteInt32(state.AbilityId);
            writer.WriteByte((byte)state.AbilityVerb);
            WriteAim(writer, state.AbilityAim);
            writer.WriteBoolean(state.AllowChase);
            writer.WriteBoolean(state.AllowReplan);
        }

        private static void WriteCombatModifiers(
            CanonicalByteWriter writer,
            in CombatModifierSetSnapshot state)
        {
            ulong[] ids = state.Ids ?? Array.Empty<ulong>();
            CombatModifierRecord[] records =
                state.Records ?? Array.Empty<CombatModifierRecord>();
            writer.WriteInt32(ids.Length);
            writer.WriteInt32(records.Length);
            for (int i = 0; i < ids.Length; i++)
            {
                writer.WriteUInt64(ids[i]);
            }
            for (int i = 0; i < records.Length; i++)
            {
                writer.WriteBoolean(records[i] != null);
                if (records[i] != null)
                {
                    CombatModifierRecord record = records[i];
                    writer.WriteUInt64(record.Id);
                    writer.WriteByte((byte)record.Domain);
                    writer.WriteByte((byte)record.Scope);
                    writer.WriteByte(
                        (byte)record.Match.SourceTypes);
                    writer.WriteInt32(record.Match.SourceId);
                    writer.WriteInt32(record.Match.RecipeId);
                    writer.WriteByte(
                        (byte)record.Match.DamageTypes);
                    CombatFormulaPatch[] valuePatches =
                        record.ValuePatches ??
                        Array.Empty<CombatFormulaPatch>();
                    writer.WriteInt32(valuePatches.Length);
                    for (int patchIndex = 0;
                         patchIndex < valuePatches.Length;
                         patchIndex++)
                    {
                        CombatFormulaPatch patch =
                            valuePatches[patchIndex];
                        writer.WriteByte((byte)patch.Slot);
                        writer.WriteByte(
                            (byte)patch.Operation);
                        writer.WriteFp(
                            patch.Operand.Constant);
                        CombatOperandTerm[] terms =
                            patch.Operand.Terms ??
                            Array.Empty<CombatOperandTerm>();
                        writer.WriteInt32(terms.Length);
                        for (int termIndex = 0;
                             termIndex < terms.Length;
                             termIndex++)
                        {
                            writer.WriteByte(
                                (byte)terms[termIndex]
                                    .Value.Kind);
                            writer.WriteInt32(
                                terms[termIndex]
                                    .Value.ValueId);
                            writer.WriteFp(
                                terms[termIndex]
                                    .Coefficient);
                        }
                    }
                    CombatPolicyPatch[] policyPatches =
                        record.PolicyPatches ??
                        Array.Empty<CombatPolicyPatch>();
                    writer.WriteInt32(policyPatches.Length);
                    for (int patchIndex = 0;
                         patchIndex < policyPatches.Length;
                         patchIndex++)
                        writer.WriteByte(
                            (byte)policyPatches[
                                patchIndex].Kind);
                }
            }
        }

        private static void WriteLocomotion(
            CanonicalByteWriter writer,
            in LocomotionAgentSnapshot state)
        {
            writer.WriteBoolean(state.HasActiveTask);
            writer.WriteInt32((int)state.Task.Purpose);
            writer.WriteBoolean(state.Task.Target.Position.HasValue);
            if (state.Task.Target.Position.HasValue)
            {
                WriteFp2(writer, state.Task.Target.Position.Value);
            }
            writer.WriteBoolean(state.Task.Target.TargetUid.HasValue);
            if (state.Task.Target.TargetUid.HasValue)
            {
                WriteUnitUid(writer, state.Task.Target.TargetUid.Value);
            }
            writer.WriteFp(state.Task.StopDistance);
            writer.WriteBoolean(state.Task.AllowRVO);
            writer.WriteBoolean(state.Task.AllowRepath);
            writer.WriteInt32((int)state.Task.State);

            writer.WriteInt32((int)state.Route.Kind);
            writer.WriteBoolean(state.Route.NeedRepath);
            writer.WriteInt32(state.Route.NextRepathTick);
            WriteFp2(writer, state.Route.LastPathTargetPosition);
            WriteIntArray(writer, state.Route.AStarPathCellIndices);
            writer.WriteInt32(state.Route.FlowFieldKey);
            WritePathFollower(writer, state.Route.FollowerState);
            WritePathFollower(writer, state.FollowerState);
        }

        private static void WritePathFollower(
            CanonicalByteWriter writer,
            in PathFollowerState state)
        {
            writer.WriteInt32(state.PathCursor);
            writer.WriteBoolean(state.RouteFinished);
            WriteIntArray(writer, state.PathCellIndices);
        }

        private static void WriteAttack(CanonicalByteWriter writer, in AttackSnapshot state)
        {
            WriteUnitUid(writer, state.CurrentTargetUid); writer.WriteInt32(state.AttackStartLogicTick);
            writer.WriteInt32(state.ImpactLogicTick); writer.WriteInt32(state.NextAttackReadyLogicTick);
            writer.WriteBoolean(state.ImpactCommitted); writer.WriteByte(state.AttackSequenceIndex);
            writer.WriteBoolean(state.IsEmpoweredAttack);
            writer.WriteInt32(state.LastSuccessfulAttackLogicTick);
            writer.WriteInt32(state.ResolvedAttackDurationTicks);
            writer.WriteInt32(state.ResolvedWindupTicks);
        }

        private static void WriteAbility(CanonicalByteWriter writer, in AbilityHandlerSnapshot state)
        {
            writer.WriteByte(state.PendingSkillPoints); writer.WriteInt32(state.NextSessionUid);
            writer.WriteBoolean(state.HasFixedPassive); writer.WriteInt32(state.FixedPassiveAbilityId);
            if (state.HasFixedPassive) WritePassiveState(writer, state.FixedPassiveRuntimeState);
            var slots = state.BookSnapshot.SlotSnapshots ?? new List<AbilitySlotSnapshot>();
            writer.WriteInt32(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                writer.WriteByte(slots[i].SlotIndex); writer.WriteByte(slots[i].AllocatedPoints); writer.WriteInt32(slots[i].ActiveAbilityId);
                var runtimes = slots[i].AbilityRuntimes ?? new List<AbilityRuntimeSnapshot>();
                writer.WriteInt32(runtimes.Count);
                for (int j = 0; j < runtimes.Count; j++)
                {
                    AbilityRuntimeSnapshot runtime = runtimes[j];
                    writer.WriteInt32(runtime.AbilityId); writer.WriteInt32(runtime.Level); writer.WriteInt32(runtime.CooldownEndsAtTick);
                    writer.WriteBoolean(runtime.HasPassiveEffectRuntime);
                    if (runtime.HasPassiveEffectRuntime)
                        WritePassiveState(writer, runtime.PassiveEffectRuntimeState);
                    writer.WriteBoolean(runtime.HasActiveSession);
                    if (!runtime.HasActiveSession) continue;
                    AbilitySessionSnapshot session = runtime.ActiveSession;
                    writer.WriteInt32(session.SessionUid); writer.WriteByte(session.CurrentStageKey);
                    writer.WriteInt32(session.StartLogicTick); writer.WriteInt32(session.StageElapsedTicks);
                    WriteAim(writer, session.Aim); writer.WriteBoolean(session.Interrupted); writer.WriteBoolean(session.Cancelled);
                    writer.WriteBoolean(session.CostPaid);
                    var entries = session.Blackboard.Entries ?? new List<AbilityBlackboardEntrySnapshot>();
                    writer.WriteInt32(entries.Count);
                    for (int k = 0; k < entries.Count; k++)
                    { writer.WriteInt32(entries[k].KeyId); writer.WriteByte((byte)entries[k].Kind); writer.WriteFp(entries[k].Number); WriteUnitUid(writer, entries[k].UnitUid); WriteFp2(writer, entries[k].Vector); WriteProjectileUid(writer, entries[k].ProjectileUid); }
                }
            }
        }

        private static void WritePassiveState(
            CanonicalByteWriter writer,
            in AbilityPassiveRuntimeState state)
        {
            writer.WriteInt32(state.StackCount); writer.WriteInt32(state.TriggerCount);
            writer.WriteInt32(state.LastTriggerLogicTick); writer.WriteInt32(state.NextReadyLogicTick);
            WriteUnitUid(writer, state.TargetUnitUid);
            WriteUnitUid(writer, state.StatModifierHandle.OwnerUnitUid);
            writer.WriteInt32((int)state.StatModifierHandle.StatId);
            writer.WriteUInt32(state.StatModifierHandle.StatSeq);
            WriteUnitUid(writer, state.CombatModifierHandle.OwnerUnitUid);
            writer.WriteUInt64(state.CombatModifierHandle.ModifierId);
        }

        private static void WriteBuffs(CanonicalByteWriter writer, in BuffHandlerSnapshot state)
        {
            BuffRuntimeSnapshot[] buffs = state.Buffs ?? Array.Empty<BuffRuntimeSnapshot>();
            writer.WriteInt32(buffs.Length);
            for (int i = 0; i < buffs.Length; i++)
            {
                BuffRuntimeSnapshot buff = buffs[i];
                writer.WriteInt32(buff.ConfigId.Value); WriteUnitUid(writer, buff.SourceUnitUid);
                writer.WriteInt32(buff.RemainingTicks); writer.WriteInt32(buff.CurrentStacks); writer.WriteInt32(buff.ElapsedTicks);
                writer.WriteInt32(buff.PeriodicTimer); writer.WriteByte((byte)buff.RemovalReason); writer.WriteBoolean(buff.IsRemoving);
                var stats = buff.Blackboard.StatHandles ?? new List<BuffStatHandleSnapshot>();
                writer.WriteInt32(stats.Count);
                for (int j = 0; j < stats.Count; j++)
                { writer.WriteString(stats[j].Key); WriteUnitUid(writer, stats[j].Handle.OwnerUnitUid); writer.WriteInt32((int)stats[j].Handle.StatId); writer.WriteUInt32(stats[j].Handle.StatSeq); }
                var combats = buff.Blackboard.CombatHandles ?? new List<BuffCombatHandleSnapshot>();
                writer.WriteInt32(combats.Count);
                for (int j = 0; j < combats.Count; j++)
                { writer.WriteString(combats[j].Key); WriteUnitUid(writer, combats[j].Handle.OwnerUnitUid); writer.WriteUInt64(combats[j].Handle.ModifierId); }
            }
        }

        private static void WriteCrowdControl(CanonicalByteWriter writer, in CrowdControlHandlerSnapshot state)
        {
            var instances = state.Instances ?? new List<CrowdControlConstraint>();
            writer.WriteInt32(instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                writer.WriteInt32(instances[i].InstanceId);
                writer.WriteByte((byte)instances[i].Type);
                writer.WriteInt32(instances[i].StartLogicTick);
                writer.WriteInt32(instances[i].RemainingTicks);
                writer.WriteByte(instances[i].Priority);
                WriteUnitUid(writer, instances[i].SourceUnitUid);
                writer.WriteBoolean(instances[i].IsForcedMove);
                writer.WriteInt32(instances[i].ForcedMoveConfigId);
                WriteFp2(writer, instances[i].ForcedMoveDeltaPerTick);
                writer.WriteByte((byte)instances[i].ForcedMoveWallPolicy);
            }
            var immunities = state.Immunities ?? new List<CrowdControlImmunitySnapshot>();
            writer.WriteInt32(immunities.Count);
            for (int i = 0; i < immunities.Count; i++)
            { writer.WriteInt32(immunities[i].ImmunityId); writer.WriteInt32(immunities[i].RemainingTicks); }
            var unstoppables = state.Unstoppables ?? new List<CrowdControlUnstoppableSnapshot>();
            writer.WriteInt32(unstoppables.Count);
            for (int i = 0; i < unstoppables.Count; i++)
            { writer.WriteInt32(unstoppables[i].UnstoppableId); writer.WriteInt32(unstoppables[i].RemainingTicks); }
            writer.WriteInt32(state.NextInstanceId); writer.WriteInt32(state.NextImmunityId); writer.WriteInt32(state.NextUnstoppableId);
            writer.WriteInt32(state.ActiveForcedMoveHandle.InstanceId);
        }

        private static void WriteEquipment(CanonicalByteWriter writer, in EquipmentHandlerSnapshot state)
        {
            var slots = state.Slots ?? new List<EquipmentSlotSnapshot>();
            writer.WriteInt32(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                writer.WriteBoolean(slots[i].Occupied); writer.WriteInt32(slots[i].EquipmentId);
                writer.WriteInt32(slots[i].StackCount); writer.WriteInt32(slots[i].ChargeCount);
                writer.WriteInt32(slots[i].ReadyTick);
                var handles = slots[i].FixedStatHandles ?? new List<StatModifierHandle>();
                writer.WriteInt32(handles.Count);
                for (int handleIndex = 0; handleIndex < handles.Count; handleIndex++)
                {
                    WriteUnitUid(writer, handles[handleIndex].OwnerUnitUid);
                    writer.WriteInt32((int)handles[handleIndex].StatId);
                    writer.WriteUInt32(handles[handleIndex].StatSeq);
                }
                var effects =
                    slots[i].EffectStates ?? new List<EquipmentEffectRuntimeSnapshot>();
                writer.WriteInt32(effects.Count);
                for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                {
                    var modules =
                        effects[effectIndex].ModuleStates ?? new List<EquipmentEffectModuleRuntimeState>();
                    writer.WriteInt32(modules.Count);
                    for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
                    {
                        writer.WriteInt32(modules[moduleIndex].NextExecuteTick);
                        writer.WriteInt32(modules[moduleIndex].InternalCooldownReadyTick);
                        writer.WriteInt32(modules[moduleIndex].StackCount);
                        writer.WriteInt32(modules[moduleIndex].TimerTicks);
                    }
                }
            }
            var cooldowns =
                state.SharedCooldowns ?? new List<EquipmentSharedCooldownSnapshot>();
            writer.WriteInt32(cooldowns.Count);
            for (int i = 0; i < cooldowns.Count; i++)
            {
                writer.WriteInt32(cooldowns[i].GroupId.Value);
                writer.WriteInt32(cooldowns[i].ReadyTick);
            }
            writer.WriteInt32(state.RuntimeRevision);
        }

        private static void WriteCombat(CanonicalByteWriter writer, in CombatSnapshot state)
        {
            var trackers = state.ContributionTrackers ?? System.Array.Empty<DamageContributionTrackerSnapshot>();
            writer.WriteInt32(trackers.Length);
            for (int i = 0; i < trackers.Length; i++)
            { WriteUnitUid(writer, trackers[i].VictimUnitUid); var records = trackers[i].Records ?? System.Array.Empty<DamageContributionRecordSnapshot>(); writer.WriteInt32(records.Length); for (int j = 0; j < records.Length; j++) { WriteUnitUid(writer, records[j].ContributorHeroUid); writer.WriteInt32(records[j].LastContributionLogicTick); writer.WriteFp(records[j].ContributionValue); writer.WriteInt32(records[j].ExpireLogicTick); } }
            var deferred = state.DeferredRequests ?? System.Array.Empty<DeferredCombatRequest>();
            writer.WriteInt32(deferred.Length);
            for (int i = 0; i < deferred.Length; i++)
            { writer.WriteInt32(deferred[i].ExecuteLogicTick); writer.WriteInt32(deferred[i].SourceLogicTick); writer.WriteUInt32(deferred[i].DeferredSequenceInSourceTick); writer.WriteByte((byte)deferred[i].RequestKind); WriteCombatPayload(writer, deferred[i]); }
        }

        private static void WriteCombatPayload(CanonicalByteWriter writer, in DeferredCombatRequest request)
        {
            switch (request.RequestKind)
            {
                case CombatRequestKind.Shield: WriteUnitUid(writer, request.Shield.SourceUnitUid); WriteUnitUid(writer, request.Shield.TargetUnitUid); writer.WriteFp(request.Shield.BaseValue); writer.WriteByte((byte)request.Shield.ShieldType); break;
                case CombatRequestKind.Damage: WriteCombatHeader(writer, request.Damage.Header); writer.WriteByte((byte)request.Damage.DamageType); writer.WriteFp(request.Damage.BaseDamage); break;
                case CombatRequestKind.Heal: WriteUnitUid(writer, request.Heal.SourceUnitUid); WriteUnitUid(writer, request.Heal.TargetUnitUid); writer.WriteFp(request.Heal.BaseValue); break;
            }
        }

        private static void WriteCombatHeader(
            CanonicalByteWriter writer,
            in CombatRequestHeader header)
        {
            writer.WriteUInt32(header.SequenceInTick);
            writer.WriteInt32(header.SourceLogicTick);
            WriteUnitUid(writer, header.SourceUnitUid);
            WriteUnitUid(writer, header.TargetUnitUid);
            writer.WriteByte((byte)header.SourceDescriptor.SourceType);
            writer.WriteInt32(header.SourceDescriptor.SourceId);
            WriteUnitUid(writer, header.SourceDescriptor.OwnerUnitUid);
            WriteUnitUid(writer, header.SourceDescriptor.EmitterUnitUid);
            writer.WriteInt32(header.RecipeId);
        }

        private static void WriteProjectiles(CanonicalByteWriter writer, in ProjectileWorldSnapshot state)
        {
            var pending = state.PendingSpawns ?? Array.Empty<PendingSpawnRecordSnapshot>();
            writer.WriteInt32(pending.Length);
            for (int i = 0; i < pending.Length; i++)
            {
                WriteProjectileUid(writer, pending[i].Uid);
                writer.WriteInt32(pending[i].DefId);
                WriteUnitUid(writer, pending[i].OwnerUnitUid);
                writer.WriteByte(pending[i].TeamSnapshot.Value);
                WriteSourceDescriptor(writer, pending[i].Source);
                WriteFp2(writer, pending[i].StartPosition);
                WriteFp2(writer, pending[i].Direction);
            }
            var active = state.ActiveProjectiles ?? Array.Empty<ProjectileRuntimeSnapshot>();
            writer.WriteInt32(active.Length);
            for (int i = 0; i < active.Length; i++)
            {
                WriteProjectileUid(writer, active[i].Uid);
                writer.WriteInt32(active[i].DefId);
                WriteUnitUid(writer, active[i].OwnerUnitUid);
                writer.WriteByte(active[i].TeamSnapshot.Value);
                WriteSourceDescriptor(writer, active[i].Source);
                WriteFp2(writer, active[i].PreviousPosition);
                WriteFp2(writer, active[i].Position);
                WriteFp2(writer, active[i].Velocity);
                writer.WriteInt32(active[i].RemainingLifetimeTicks);
                writer.WriteBoolean(active[i].IsActive);
                writer.WriteBoolean(active[i].EndRequested);
                writer.WriteByte((byte)active[i].EndReason);
                writer.WriteInt32(active[i].TotalHitCount);
                writer.WriteInt32(active[i].RemainingPierceCount);
                writer.WriteInt32(active[i].RemainingBounceCount);
                writer.WriteInt32(active[i].NextQueryLogicTick);
                ProjectileHitRecord[] records =
                    active[i].HitRecords ?? Array.Empty<ProjectileHitRecord>();
                writer.WriteInt32(records.Length);
                for (int recordIndex = 0; recordIndex < records.Length; recordIndex++)
                {
                    WriteUnitUid(writer, records[recordIndex].TargetUid);
                    writer.WriteInt32(records[recordIndex].HitCount);
                    writer.WriteInt32(records[recordIndex].LastHitLogicTick);
                }
            }
        }

        private static void WriteSourceDescriptor(
            CanonicalByteWriter writer,
            in SourceDescriptor source)
        {
            writer.WriteByte((byte)source.SourceType);
            writer.WriteInt32(source.SourceId);
            WriteUnitUid(writer, source.OwnerUnitUid);
            WriteUnitUid(writer, source.EmitterUnitUid);
        }

        private static void WriteEquipmentShop(CanonicalByteWriter writer, in EquipmentShopRuntimeSnapshot state)
        {
            var traders = state.CreatedTraders ?? new List<ShopTraderRuntimeSnapshot>();
            writer.WriteInt32(traders.Count);
            for (int i = 0; i < traders.Count; i++)
            {
                writer.WriteInt32(traders[i].Player);
                WriteUnitUid(
                    writer,
                    traders[i].ControlledUnitUid);
                writer.WriteInt32(
                    traders[i].NextOperationSequence);
                var operations =
                    traders[i].OperationLog ??
                    new List<ShopOperationRecord>();
                writer.WriteInt32(operations.Count);
                for (int operationIndex = 0;
                     operationIndex < operations.Count;
                     operationIndex++)
                {
                    ShopOperationRecord operation =
                        operations[operationIndex];
                    writer.WriteInt32(
                        operation.OperationSequence);
                    writer.WriteByte(
                        (byte)operation.OperationType);
                    writer.WriteInt32(operation.Player);
                    WriteUnitUid(
                        writer,
                        operation.ControlledUnitUid);
                    writer.WriteInt32(operation.LogicTick);
                    writer.WriteInt32(operation.GoldDelta);
                    EquipmentSlotChange[] changes =
                        operation.SlotChanges ??
                        Array.Empty<EquipmentSlotChange>();
                    writer.WriteInt32(changes.Length);
                    for (int changeIndex = 0;
                         changeIndex < changes.Length;
                         changeIndex++)
                    {
                        writer.WriteInt32(
                            changes[changeIndex].Slot);
                        WriteTransactionSlotState(
                            writer,
                            changes[changeIndex].Before);
                        WriteTransactionSlotState(
                            writer,
                            changes[changeIndex].After);
                    }
                    writer.WriteBoolean(operation.Reverted);
                    writer.WriteInt32(
                        operation.RevertedLogicTick);
                    writer.WriteInt32(
                        operation.EquipmentRevisionBefore);
                    writer.WriteInt32(
                        operation.EquipmentRevisionAfter);
                }
                WriteIntArray(
                    writer,
                    traders[i].UndoableOperationStack?.ToArray());
            }
        }

        private static void WriteTransactionSlotState(
            CanonicalByteWriter writer,
            in EquipmentTransactionSlotState state)
        {
            writer.WriteBoolean(state.Occupied);
            if (!state.Occupied)
                return;
            writer.WriteInt32(state.EquipmentId);
            writer.WriteInt32(state.StackCount);
            writer.WriteInt32(state.ChargeCount);
            writer.WriteInt32(state.ReadyTick);
            List<EquipmentEffectRuntimeSnapshot> effects =
                state.EffectStates ??
                new List<EquipmentEffectRuntimeSnapshot>();
            writer.WriteInt32(effects.Count);
            for (int effectIndex = 0;
                 effectIndex < effects.Count;
                 effectIndex++)
            {
                List<EquipmentEffectModuleRuntimeState> modules =
                    effects[effectIndex].ModuleStates ??
                    new List<EquipmentEffectModuleRuntimeState>();
                writer.WriteInt32(modules.Count);
                for (int moduleIndex = 0;
                     moduleIndex < modules.Count;
                     moduleIndex++)
                {
                    EquipmentEffectModuleRuntimeState module =
                        modules[moduleIndex];
                    writer.WriteInt32(module.NextExecuteTick);
                    writer.WriteInt32(
                        module.InternalCooldownReadyTick);
                    writer.WriteInt32(module.StackCount);
                    writer.WriteInt32(module.TimerTicks);
                }
            }
        }

        private static void WritePhysics(CanonicalByteWriter writer, in PhysicsRuntimeSnapshot state)
        {
            var pairs = state.CollisionBuffer.PreviousPairs ?? new List<UnitContactPair>();
            writer.WriteInt32(pairs.Count);
            for (int i = 0; i < pairs.Count; i++) { WriteRuntimeUid(writer, pairs[i].MinUid); WriteRuntimeUid(writer, pairs[i].MaxUid); }
        }

        private static void WriteAim(CanonicalByteWriter writer, in AimSnapshot aim) { writer.WriteByte((byte)aim.Kind); WriteUnitUid(writer, aim.TargetUnitUid); WriteFp2(writer, aim.TargetPoint); WriteFp2(writer, aim.Direction); }
        private static void WriteFp2(CanonicalByteWriter writer, fp2 value) { writer.WriteFp(value.x); writer.WriteFp(value.y); }
        private static void WriteUnitUid(CanonicalByteWriter writer, UnitUid uid) { writer.WriteInt32(uid.SpawnLogicTick); writer.WriteInt32(uid.RuntimeEntityPrefabId); writer.WriteByte(uid.SpawnSequenceInTick); }
        private static void WriteRuntimeUid(CanonicalByteWriter writer, RuntimeUidQueryValue uid) { writer.WriteInt32(uid.SpawnLogicTick); writer.WriteInt32(uid.RuntimeEntityPrefabId); writer.WriteByte(uid.SpawnSequenceInTick); }
        private static void WriteProjectileUid(CanonicalByteWriter writer, ProjectileUid uid) { writer.WriteInt32(uid.SpawnLogicTick); writer.WriteInt32(uid.RuntimeEntityPrefabId); writer.WriteByte(uid.SpawnSequenceInTick); }
        private static void WriteUidList(CanonicalByteWriter writer, IList<UnitUid> values) { writer.WriteInt32(values?.Count ?? 0); if (values != null) for (int i = 0; i < values.Count; i++) WriteUnitUid(writer, values[i]); }
        private static void WriteIntArray(CanonicalByteWriter writer, int[] values) { writer.WriteInt32(values?.Length ?? 0); if (values != null) for (int i = 0; i < values.Length; i++) writer.WriteInt32(values[i]); }
    }
}
