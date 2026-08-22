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
        /// <summary>
        /// When true, authority/server ticks and client replay mismatches log
        /// per-segment (and per-unit handler) checksum hashes so a divergence
        /// can be localized to the exact subsystem. Diagnostics only; never
        /// affects simulation.
        /// </summary>
        public static bool DetailedLoggingEnabled;

        public readonly struct ChecksumSegment
        {
            public readonly string Label;
            public readonly uint Hash;

            public ChecksumSegment(string label, uint hash)
            {
                Label = label;
                Hash = hash;
            }
        }

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
                writer.WriteInt32(entries[i].CreepKills);
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
            var disposalEntries =
                state.PendingUnitLifecycleState.DisposalEntries ??
                new List<DeathDisposalEntry>();
            writer.WriteInt32(disposalEntries.Count);
            for (int i = 0; i < disposalEntries.Count; i++)
            {
                WriteUnitUid(writer, disposalEntries[i].UnitUid);
                writer.WriteInt32(disposalEntries[i].DeathLogicTick);
                writer.WriteInt32(disposalEntries[i].DisposeLogicTick);
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
                writer.WriteInt32(ai.MinionLastThreatRefreshLogicTick);
                writer.WriteInt32(ai.MinionNextDecisionLogicTick);
                writer.WriteInt32(ai.MinionTargetLockUntilLogicTick);
                WriteFp2(writer, ai.MinionEngageOrigin);
                WriteUnitUid(writer, ai.MinionPendingAssistTargetUid);
                writer.WriteInt32(ai.MinionPendingAssistExpireLogicTick);
                MinionThreatSnapshotEntry[] threats =
                    ai.MinionThreatTable ??
                    Array.Empty<MinionThreatSnapshotEntry>();
                writer.WriteInt32(threats.Length);
                for (int threatIndex = 0;
                     threatIndex < threats.Length;
                     threatIndex++)
                {
                    WriteUnitUid(writer, threats[threatIndex].Uid);
                    writer.WriteInt32(threats[threatIndex].Threat);
                }
                writer.WriteByte((byte)ai.MonsterState);
                writer.WriteInt32(ai.CampId);
                writer.WriteInt32(ai.MonsterCampSlotIndex);
                writer.WriteInt32(ai.MonsterNextDecisionLogicTick);
                writer.WriteByte((byte)ai.TowerState);
            }
        }

        private static void WriteUnit(CanonicalByteWriter writer, in UnitSnapshot state)
        {
            WriteUnitBase(writer, state);
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

        private static void WriteUnitBase(CanonicalByteWriter writer, in UnitSnapshot state)
        {
            WriteUnitUid(writer, state.UnitUid); WriteUnitUid(writer, state.OwnerUid);
            writer.WriteByte((byte)state.UnitKind); writer.WriteUInt32(state.UnitSubKindId);
            writer.WriteByte(state.TeamId.Value); writer.WriteInt32(state.UnitPrototypeId);
            WriteFp2(writer, state.RespawnPosition);
            writer.WriteByte((byte)state.LifeState);
            writer.WriteBoolean(state.CapabilityState.CanMove); writer.WriteBoolean(state.CapabilityState.CanAttack);
            writer.WriteBoolean(state.CapabilityState.CanCast); writer.WriteBoolean(state.CapabilityState.CanTurn);
            writer.WriteBoolean(state.CapabilityState.IsTargetable);
            writer.WriteByte((byte)state.HitReactionState.ActiveReaction);
            writer.WriteInt32(state.HitReactionState.RemainingTicks); writer.WriteInt32(state.HitReactionState.TotalTicks);
            WriteIntent(writer, state.IntentState);
            WriteActionRuntimeSet(writer, state.ActionRuntimeState);
            WriteFp2(writer, state.PhysicsTransform.Position); WriteFp2(writer, state.PhysicsTransform.PrevPosition);
            WriteFp2(writer, state.PhysicsTransform.Forward); WriteFp2(writer, state.PhysicsTransform.Right);
            writer.WriteByte((byte)state.PhysicsShape.Kind); WriteFp2(writer, state.PhysicsShape.LocalOffset);
            writer.WriteFp(state.PhysicsShape.Radius); writer.WriteFp(state.PhysicsShape.Length);
            writer.WriteFp(state.PhysicsShape.Width); WriteFp2(writer, state.PhysicsShape.HalfExtents);
            writer.WriteBoolean(state.PhysicsShape.SweepFromPrev);
        }

        private static void WriteActionRuntimeSet(
            CanonicalByteWriter writer,
            in ActionRuntimeSetSnapshot state)
        {
            WriteActionRuntimeSlot(writer, state.Main);
            WriteActionRuntimeSlot(writer, state.Base);
        }

        private static void WriteActionRuntimeSlot(
            CanonicalByteWriter writer,
            in ActionRuntimeSlotSnapshot state)
        {
            writer.WriteBoolean(state.IsOccupied);
            writer.WriteByte((byte)state.Slot);
            writer.WriteByte((byte)state.Kind);
            writer.WriteByte((byte)state.Phase);
            writer.WriteUInt32((ushort)state.OccupiedResources);
            writer.WriteBoolean(state.Interruptible);
            writer.WriteBoolean(state.BlocksVoluntaryMove);
            writer.WriteBoolean(state.IsControlAction);
            WriteUnitUid(writer, state.TargetUnitUid);
            writer.WriteByte(state.AbilitySlot);
        }

        private static void WriteStats(CanonicalByteWriter writer, in StatHandlerSnapshot state)
        {
            WriteStatLevel(writer, state);
            WriteStatVitals(writer, state);
            WriteStatSeq(writer, state);
            WriteStatShields(writer, state);
            WriteStatEntries(writer, state);
        }

        private static void WriteStatLevel(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state)
        {
            writer.WriteInt32(state.Level);
        }

        private static void WriteStatVitals(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state)
        {
            writer.WriteFp(state.CurrentHealth);
            writer.WriteFp(state.CurrentCastResource);
            writer.WriteInt32(state.CurrentExperience);
        }

        private static void WriteStatSeq(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state)
        {
            writer.WriteUInt32(state.NextStatSeq);
        }

        private static void WriteStatShields(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state)
        {
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
        }

        private static void WriteStatEntries(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state)
        {
            StatRuntimeEntrySnapshot[] entries = state.Entries ?? Array.Empty<StatRuntimeEntrySnapshot>();
            writer.WriteInt32(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                StatRuntimeEntrySnapshot entry = entries[i];
                writer.WriteInt32((int)entry.StatId);
                WriteStatEntryBase(writer, entry);
                WriteStatEntryFinal(writer, entry);
                WriteStatEntryPrev(writer, entry);
                WriteStatEntryDirty(writer, entry);
                WriteStatEntryModifiers(writer, entry);
            }
        }

        private static void WriteStatEntryBase(
            CanonicalByteWriter writer,
            in StatRuntimeEntrySnapshot entry)
        {
            writer.WriteFp(entry.LevelBaseValue);
        }

        private static void WriteStatEntryFinal(
            CanonicalByteWriter writer,
            in StatRuntimeEntrySnapshot entry)
        {
            writer.WriteFp(entry.FinalValue);
        }

        private static void WriteStatEntryPrev(
            CanonicalByteWriter writer,
            in StatRuntimeEntrySnapshot entry)
        {
            writer.WriteFp(entry.PreviousLogicTickFinalValue);
        }

        private static void WriteStatEntryDirty(
            CanonicalByteWriter writer,
            in StatRuntimeEntrySnapshot entry)
        {
            writer.WriteBoolean(entry.Dirty);
        }

        private static void WriteStatEntryModifiers(
            CanonicalByteWriter writer,
            in StatRuntimeEntrySnapshot entry)
        {
                StatModifier[] modifiers = entry.Modifiers ?? Array.Empty<StatModifier>();
                writer.WriteInt32(modifiers.Length);
                for (int j = 0; j < modifiers.Length; j++)
                { writer.WriteUInt32(modifiers[j].StatSeq); writer.WriteByte((byte)modifiers[j].Operation); writer.WriteFp(modifiers[j].Value); }
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
            WriteUnitUid(writer, state.RampTargetUnitUid);
            writer.WriteInt32(state.RampHitCount);
            WriteProjectileUid(writer, state.PendingProjectileUid);
            WriteUnitUid(writer, state.LockedTargetUnitUid);
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
                    {
                        writer.WriteInt32(entries[k].KeyId); writer.WriteByte((byte)entries[k].Kind); writer.WriteFp(entries[k].Number);
                        WriteUnitUid(writer, entries[k].UnitUid); WriteFp2(writer, entries[k].Vector);
                        WriteProjectileUid(writer, entries[k].ProjectileUid);
                        WriteUnitUid(writer, entries[k].StatModifierHandle.OwnerUnitUid);
                        writer.WriteInt32((int)entries[k].StatModifierHandle.StatId);
                        writer.WriteUInt32(entries[k].StatModifierHandle.StatSeq);
                        WriteUnitUid(writer, entries[k].CrowdControlHandle.TargetUnitUid);
                        writer.WriteInt32(entries[k].CrowdControlHandle.InstanceId);
                    }
                }
            }
        }

        private static void WritePassiveState(
            CanonicalByteWriter writer,
            in AbilityPassiveRuntimeState state)
        {
            writer.WriteInt32(state.AbilityLevel);
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
                writer.WriteByte((byte)buff.SourceType);
                writer.WriteInt32(buff.SourceConfigId);
                writer.WriteInt32(buff.RemainingTicks); writer.WriteInt32(buff.CurrentStacks); writer.WriteInt32(buff.ElapsedTicks);
                writer.WriteInt32(buff.PeriodicTimer); writer.WriteByte((byte)buff.RemovalReason); writer.WriteBoolean(buff.IsRemoving);
                var slots = buff.Blackboard.Slots ?? new List<BuffValueSnapshot>();
                writer.WriteInt32(slots.Count);
                for (int j = 0; j < slots.Count; j++)
                {
                    BuffValueSnapshot slot = slots[j];
                    writer.WriteInt32(slot.SlotId.Value);
                    writer.WriteByte((byte)slot.Value.Kind);
                    switch (slot.Value.Kind)
                    {
                        case BuffValueKind.Int:
                            writer.WriteInt32(slot.Value.IntValue);
                            break;
                        case BuffValueKind.Bool:
                            writer.WriteBoolean(slot.Value.BoolValue);
                            break;
                        case BuffValueKind.Fp:
                            writer.WriteFp(slot.Value.FpValue);
                            break;
                        case BuffValueKind.Fp2:
                            writer.WriteFp(slot.Value.Fp2Value.x);
                            writer.WriteFp(slot.Value.Fp2Value.y);
                            break;
                        case BuffValueKind.UnitUid:
                            WriteUnitUid(writer, slot.Value.UnitUidValue);
                            break;
                        case BuffValueKind.StableConfigId:
                            writer.WriteInt32(slot.Value.ConfigIdValue);
                            break;
                        case BuffValueKind.StatModifierHandle:
                            WriteUnitUid(writer, slot.Value.StatHandle.OwnerUnitUid);
                            writer.WriteInt32((int)slot.Value.StatHandle.StatId);
                            writer.WriteUInt32(slot.Value.StatHandle.StatSeq);
                            break;
                        case BuffValueKind.CombatModifierHandle:
                            WriteUnitUid(writer, slot.Value.CombatHandle.OwnerUnitUid);
                            writer.WriteUInt64(slot.Value.CombatHandle.ModifierId);
                            break;
                    }
                }
            }
        }

        private static void WriteCrowdControl(CanonicalByteWriter writer, in CrowdControlHandlerSnapshot state)
        {
            var instances = state.Instances ?? new List<CrowdControlInstance>();
            writer.WriteInt32(instances.Count);
            for (int i = 0; i < instances.Count; i++)
            {
                writer.WriteInt32(instances[i].InstanceId);
                writer.WriteInt32(instances[i].ControlId.Value);
                writer.WriteInt32(instances[i].StartTick);
                writer.WriteInt32(instances[i].ExpireTick);
                WriteParamBlock(writer, instances[i].Params);
            }
            var immunities = state.Immunities ?? new List<CrowdControlImmunity>();
            writer.WriteInt32(immunities.Count);
            for (int i = 0; i < immunities.Count; i++)
            {
                writer.WriteInt32(immunities[i].ImmunityId);
                writer.WriteInt64(unchecked((long)immunities[i].Query.All.Bits));
                writer.WriteInt64(unchecked((long)immunities[i].Query.Any.Bits));
                writer.WriteInt64(unchecked((long)immunities[i].Query.None.Bits));
                writer.WriteInt32(immunities[i].ExpireTick);
                writer.WriteInt32(immunities[i].RemainingBlocks);
                writer.WriteInt32(immunities[i].Priority);
            }
            var unstoppables = state.Unstoppables ?? new List<CrowdControlUnstoppable>();
            writer.WriteInt32(unstoppables.Count);
            for (int i = 0; i < unstoppables.Count; i++)
            {
                writer.WriteInt32(unstoppables[i].UnstoppableId);
                writer.WriteInt32(unstoppables[i].ExpireTick);
            }
            writer.WriteInt32(state.NextInstanceId); writer.WriteInt32(state.NextImmunityId); writer.WriteInt32(state.NextUnstoppableId);
            WriteUnitUid(writer, state.ActiveForcedMoveHandle.TargetUnitUid);
            writer.WriteInt32(state.ActiveForcedMoveHandle.InstanceId);
            writer.WriteUInt32((uint)state.PendingSignals);
            var signalTicks = state.SignalEffectiveTicks ?? new int[(int)CrowdControlSignalType.Count];
            writer.WriteInt32(signalTicks.Length);
            for (int i = 0; i < signalTicks.Length; i++)
            {
                writer.WriteInt32(signalTicks[i]);
            }
        }

        private static void WriteParamBlock(
            CanonicalByteWriter writer,
            in CrowdControlParamBlock block)
        {
            for (int i = 0; i < 64; i++)
            {
                writer.WriteByte(
                    block.Data.ReadByte(i));
            }
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
                        writer.WriteInt32(modules[moduleIndex].TriggerCount);
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
            var logs = state.ContributionEventLogs ?? System.Array.Empty<CombatContributionEventLogSnapshot>();
            writer.WriteInt32(logs.Length);
            for (int i = 0; i < logs.Length; i++)
            {
                WriteUnitUid(writer, logs[i].VictimUnitUid);
                WriteUnitUid(writer, logs[i].LastHitContributorUid);
                var events = logs[i].Events ?? System.Array.Empty<CombatContributionEventSnapshot>();
                writer.WriteInt32(events.Length);
                for (int j = 0; j < events.Length; j++)
                {
                    WriteUnitUid(writer, events[j].ContributorHeroUid);
                    writer.WriteByte((byte)events[j].Kind);
                    writer.WriteFp(events[j].Amount);
                    writer.WriteInt32(events[j].LogicTick);
                    writer.WriteInt32(
                        events[j].SequenceInTick);
                }
            }
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
                writer.WriteInt32(
                    pending[i].MaxLifetimeTicksOverride);
                WriteOnHitDamageOverride(
                    writer,
                    pending[i].OnHitDamageOverride);
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
                WriteOnHitDamageOverride(
                    writer,
                    active[i].OnHitDamageOverride);
            }
        }

        private static void WriteOnHitDamageOverride(
            CanonicalByteWriter writer,
            ProjectileOnHitDamage[] effects)
        {
            ProjectileOnHitDamage[] list =
                effects ??
                Array.Empty<ProjectileOnHitDamage>();
            writer.WriteInt32(list.Length);
            for (int i = 0; i < list.Length; i++)
            {
                writer.WriteFp(list[i].Amount);
                writer.WriteByte((byte)list[i].DamageType);
                writer.WriteFp(list[i].DamageRatio);
                writer.WriteFp(list[i].MissingHpRatio);
                writer.WriteFp(list[i].FalloffPerHitPercent);
                writer.WriteFp(list[i].MinDamageRatio);
                writer.WriteInt32(list[i].RecipeId);
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
                    writer.WriteInt32(module.TriggerCount);
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

        /// <summary>
        /// Diagnostics: top-level checksum segments, each hashed with an
        /// independent writer. Does not change the canonical overall hash.
        /// </summary>
        public static ChecksumSegment[] ComputeSegmentHashes(
            in GameplaySnapshot snapshot,
            GoldIncomeBatchDigest goldDigest)
        {
            var writer = new CanonicalByteWriter(
                new byte[65536]);
            var segments = new List<ChecksumSegment>();

            writer.Reset();
            writer.WriteInt32(snapshot.SchemaVersion);
            writer.WriteUInt32(snapshot.RandomState.State);
            WriteMatchRule(writer, snapshot.MatchRuleState);
            segments.Add(new ChecksumSegment(
                "Schema+Random+MatchRule",
                Hash(writer)));

            writer.Reset();
            WriteUnitWorld(writer, snapshot.UnitWorldState);
            segments.Add(new ChecksumSegment(
                "UnitWorld",
                Hash(writer)));

            writer.Reset();
            WriteCombat(writer, snapshot.CombatState);
            segments.Add(new ChecksumSegment(
                "Combat",
                Hash(writer)));

            writer.Reset();
            WriteProjectiles(writer, snapshot.ProjectileState);
            segments.Add(new ChecksumSegment(
                "Projectiles",
                Hash(writer)));

            writer.Reset();
            WriteEquipmentShop(
                writer,
                snapshot.EquipmentShopState);
            segments.Add(new ChecksumSegment(
                "EquipmentShop",
                Hash(writer)));

            writer.Reset();
            WritePhysics(writer, snapshot.PhysicsState);
            segments.Add(new ChecksumSegment(
                "Physics",
                Hash(writer)));

            writer.Reset();
            writer.WriteUInt64(goldDigest.Value);
            segments.Add(new ChecksumSegment(
                "GoldDigest",
                Hash(writer)));

            return segments.ToArray();
        }

        /// <summary>
        /// Diagnostics: per-handler checksum hashes for one unit snapshot.
        /// </summary>
        public static ChecksumSegment[] ComputeUnitHandlerHashes(
            in UnitSnapshot state)
        {
            var writer = new CanonicalByteWriter(
                new byte[65536]);
            var segments = new List<ChecksumSegment>();

            writer.Reset();
            WriteUnitBase(writer, state);
            segments.Add(new ChecksumSegment(
                "Base",
                Hash(writer)));

            writer.Reset();
            WriteStatLevel(writer, state.StatState);
            segments.Add(new ChecksumSegment(
                "StatLevel",
                Hash(writer)));

            writer.Reset();
            WriteStatVitals(writer, state.StatState);
            segments.Add(new ChecksumSegment(
                "StatVitals",
                Hash(writer)));

            writer.Reset();
            WriteStatSeq(writer, state.StatState);
            segments.Add(new ChecksumSegment(
                "StatSeq",
                Hash(writer)));

            writer.Reset();
            WriteStatShields(writer, state.StatState);
            segments.Add(new ChecksumSegment(
                "StatShields",
                Hash(writer)));

            writer.Reset();
            WriteStatEntrySegment(
                writer,
                state.StatState,
                StatEntryField.Base);
            segments.Add(new ChecksumSegment(
                "StatEntryBase",
                Hash(writer)));

            writer.Reset();
            WriteStatEntrySegment(
                writer,
                state.StatState,
                StatEntryField.Final);
            segments.Add(new ChecksumSegment(
                "StatEntryFinal",
                Hash(writer)));

            writer.Reset();
            WriteStatEntrySegment(
                writer,
                state.StatState,
                StatEntryField.Prev);
            segments.Add(new ChecksumSegment(
                "StatEntryPrev",
                Hash(writer)));

            writer.Reset();
            WriteStatEntrySegment(
                writer,
                state.StatState,
                StatEntryField.Dirty);
            segments.Add(new ChecksumSegment(
                "StatEntryDirty",
                Hash(writer)));

            writer.Reset();
            WriteStatEntrySegment(
                writer,
                state.StatState,
                StatEntryField.Modifiers);
            segments.Add(new ChecksumSegment(
                "StatEntryModifiers",
                Hash(writer)));

            writer.Reset();
            WriteCombatModifiers(
                writer,
                state.CombatModifierState);
            segments.Add(new ChecksumSegment(
                "CombatModifiers",
                Hash(writer)));

            writer.Reset();
            WriteMovement(writer, state.MovementState);
            segments.Add(new ChecksumSegment(
                "Movement",
                Hash(writer)));

            writer.Reset();
            WriteLocomotion(writer, state.LocomotionState);
            segments.Add(new ChecksumSegment(
                "Locomotion",
                Hash(writer)));

            writer.Reset();
            WriteAttack(writer, state.AttackState);
            segments.Add(new ChecksumSegment(
                "Attack",
                Hash(writer)));

            writer.Reset();
            WriteAbility(writer, state.AbilityState);
            segments.Add(new ChecksumSegment(
                "Ability",
                Hash(writer)));

            writer.Reset();
            WriteBuffs(writer, state.BuffState);
            segments.Add(new ChecksumSegment(
                "Buffs",
                Hash(writer)));

            writer.Reset();
            WriteCrowdControl(writer, state.CCState);
            segments.Add(new ChecksumSegment(
                "CrowdControl",
                Hash(writer)));

            writer.Reset();
            WriteEquipment(writer, state.EquipmentState);
            segments.Add(new ChecksumSegment(
                "Equipment",
                Hash(writer)));

            return segments.ToArray();
        }

        private static uint Hash(CanonicalByteWriter writer)
        {
            ArraySegment<byte> bytes =
                writer.GetWrittenSegment();
            return DeterministicHash32.Compute(
                bytes.Array,
                bytes.Offset,
                bytes.Count);
        }

        private enum StatEntryField : byte
        {
            Base,
            Final,
            Prev,
            Dirty,
            Modifiers,
        }

        private static void WriteStatEntrySegment(
            CanonicalByteWriter writer,
            in StatHandlerSnapshot state,
            StatEntryField field)
        {
            StatRuntimeEntrySnapshot[] entries =
                state.Entries ??
                Array.Empty<StatRuntimeEntrySnapshot>();
            writer.WriteInt32(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                StatRuntimeEntrySnapshot entry =
                    entries[i];
                writer.WriteInt32((int)entry.StatId);
                switch (field)
                {
                    case StatEntryField.Base:
                        WriteStatEntryBase(writer, entry);
                        break;
                    case StatEntryField.Final:
                        WriteStatEntryFinal(writer, entry);
                        break;
                    case StatEntryField.Prev:
                        WriteStatEntryPrev(writer, entry);
                        break;
                    case StatEntryField.Dirty:
                        WriteStatEntryDirty(writer, entry);
                        break;
                    case StatEntryField.Modifiers:
                        WriteStatEntryModifiers(writer, entry);
                        break;
                }
            }
        }
    }
}
