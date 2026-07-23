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
            MatchStatisticsEntry[] entries = state.Statistics.Entries ?? Array.Empty<MatchStatisticsEntry>();
            writer.WriteInt32(entries.Length);
            for (int i = 0; i < entries.Length; i++)
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
                    ref readonly MovementSnapshot movement = ref units[i].MovementHandler.Snapshot;
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
            List<UnitSnapshot> units = state.Units ?? new List<UnitSnapshot>();
            writer.WriteInt32(units.Count);
            for (int i = 0; i < units.Count; i++) WriteUnit(writer, units[i]);
            writer.WriteInt32(state.RuntimeRevision);
            List<RespawnEntry> lifecycleEntries =
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
            List<MinionTicket> tickets = state.MinionSystemState.PendingTickets ?? new List<MinionTicket>();
            writer.WriteInt32(tickets.Count);
            for (int ticketIndex = 0; ticketIndex < tickets.Count; ticketIndex++)
            {
                MinionTicket ticket = tickets[ticketIndex];
                WriteUnitUid(writer, ticket.UnitUid); writer.WriteInt32(ticket.SpawnLogicTick);
                writer.WriteInt32(ticket.LaneId); writer.WriteBoolean(ticket.IsSpawned);
            }
            WriteUidList(writer, state.MinionSystemState.ManagedMinionUids);
            List<JungleCampSnapshot> camps = state.JungleCampStates ?? new List<JungleCampSnapshot>();
            writer.WriteInt32(camps.Count);
            for (int i = 0; i < camps.Count; i++)
            {
                JungleCampSnapshot camp = camps[i];
                writer.WriteInt32(camp.CampId); writer.WriteByte((byte)camp.State);
                WriteUidList(writer, camp.MemberUidsBySlot);
                List<bool> alive = camp.MemberAliveBySlot ?? new List<bool>();
                writer.WriteInt32(alive.Count);
                for (int memberIndex = 0; memberIndex < alive.Count; memberIndex++)
                    writer.WriteBoolean(alive[memberIndex]);
                writer.WriteBoolean(camp.MainMonsterDead);
                WriteUnitUid(writer, camp.PrimaryTargetUid);
                writer.WriteInt32(camp.LastHostileActionLogicTick);
                writer.WriteInt32(camp.NextRespawnLogicTick);
                writer.WriteInt32(camp.ResetBeginLogicTick);
            }
            List<UnitAIControllerSnapshot> ais = state.AIControllerStates ?? new List<UnitAIControllerSnapshot>();
            writer.WriteInt32(ais.Count);
            for (int i = 0; i < ais.Count; i++)
            {
                UnitAIControllerSnapshot ai = ais[i];
                WriteUnitUid(writer, ai.OwnerUnitUid); writer.WriteByte((byte)ai.ControllerKind);
                writer.WriteByte((byte)ai.MinionState); writer.WriteInt32(ai.LaneId); WriteUnitUid(writer, ai.MinionTargetUid);
                writer.WriteByte((byte)ai.MonsterState); writer.WriteInt32(ai.CampId); WriteUnitUid(writer, ai.MonsterTargetUid);
                writer.WriteByte((byte)ai.TowerState); WriteUnitUid(writer, ai.TowerTargetUid);
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
            WriteFp2(writer, state.PhysicsTransform.Position); WriteFp2(writer, state.PhysicsTransform.PrevPosition);
            WriteFp2(writer, state.PhysicsTransform.Forward); WriteFp2(writer, state.PhysicsTransform.Right);
            writer.WriteByte((byte)state.PhysicsShape.Kind); WriteFp2(writer, state.PhysicsShape.LocalOffset);
            writer.WriteFp(state.PhysicsShape.Radius); writer.WriteFp(state.PhysicsShape.Length);
            writer.WriteFp(state.PhysicsShape.Width); WriteFp2(writer, state.PhysicsShape.HalfExtents);
            writer.WriteBoolean(state.PhysicsShape.SweepFromPrev);
            WriteStats(writer, state.StatState);
            WriteMovement(writer, state.MovementState);
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
            List<ShieldInstance> shields = state.ShieldInstances ?? new List<ShieldInstance>();
            writer.WriteInt32(shields.Count);
            for (int i = 0; i < shields.Count; i++)
            {
                ShieldInstance shield = shields[i];
                writer.WriteInt32(shield.ShieldInstanceId); writer.WriteByte((byte)shield.ShieldType);
                writer.WriteFp(shield.CurrentValue); writer.WriteFp(shield.MaxValue);
                writer.WriteInt32(shield.StartLogicTick); writer.WriteInt32(shield.ExpireLogicTick);
                WriteUnitUid(writer, shield.SourceUnitUid);
                WriteUnitUid(writer, shield.CrowdControlImmunityHandle.TargetUnitUid);
                writer.WriteInt32(shield.CrowdControlImmunityHandle.ImmunityId);
            }
            List<StatRuntimeEntrySnapshot> entries = state.Entries ?? new List<StatRuntimeEntrySnapshot>();
            writer.WriteInt32(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                StatRuntimeEntrySnapshot entry = entries[i];
                writer.WriteInt32((int)entry.StatId); writer.WriteFp(entry.LevelBaseValue);
                writer.WriteFp(entry.FinalValue); writer.WriteFp(entry.PreviousLogicTickFinalValue);
                writer.WriteBoolean(entry.Dirty);
                List<StatModifier> modifiers = entry.Modifiers ?? new List<StatModifier>();
                writer.WriteInt32(modifiers.Count);
                for (int j = 0; j < modifiers.Count; j++)
                { writer.WriteUInt32(modifiers[j].StatSeq); writer.WriteByte((byte)modifiers[j].Operation); writer.WriteFp(modifiers[j].Value); }
            }
        }

        private static void WriteMovement(CanonicalByteWriter writer, in MovementSnapshot state)
        {
            WriteFp2(writer, state.Position); WriteFp2(writer, state.Velocity); WriteFp2(writer, state.Facing);
            writer.WriteFp(state.MoveSpeed); writer.WriteBoolean(state.IsMoving); WriteFp2(writer, state.TargetDirection);
            writer.WriteInt32(state.CurrentWaypointIndex);
            WriteIntArray(writer, state.SnapshotPathCellIndices);
        }

        private static void WriteAttack(CanonicalByteWriter writer, in AttackSnapshot state)
        {
            WriteUnitUid(writer, state.CurrentTargetUid); writer.WriteInt32(state.AttackStartLogicTick);
            writer.WriteInt32(state.ImpactLogicTick); writer.WriteInt32(state.NextAttackReadyLogicTick);
            writer.WriteBoolean(state.ImpactCommitted); writer.WriteByte(state.AttackSequenceIndex);
        }

        private static void WriteAbility(CanonicalByteWriter writer, in AbilityHandlerSnapshot state)
        {
            writer.WriteByte(state.PendingSkillPoints); writer.WriteInt32(state.NextSessionUid);
            writer.WriteBoolean(state.HasFixedPassive); writer.WriteInt32(state.FixedPassiveAbilityId);
            if (state.HasFixedPassive) WritePassiveState(writer, state.FixedPassiveRuntimeState);
            AbilitySlotSnapshot[] slots = state.BookSnapshot.SlotSnapshots ?? Array.Empty<AbilitySlotSnapshot>();
            writer.WriteInt32(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                writer.WriteByte(slots[i].SlotIndex); writer.WriteByte(slots[i].AllocatedPoints); writer.WriteInt32(slots[i].ActiveAbilityId);
                AbilityRuntimeSnapshot[] runtimes = slots[i].AbilityRuntimes ?? Array.Empty<AbilityRuntimeSnapshot>();
                writer.WriteInt32(runtimes.Length);
                for (int j = 0; j < runtimes.Length; j++)
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
                    AbilityBlackboardEntrySnapshot[] entries = session.Blackboard.Entries ?? Array.Empty<AbilityBlackboardEntrySnapshot>();
                    writer.WriteInt32(entries.Length);
                    for (int k = 0; k < entries.Length; k++)
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
            List<BuffRuntimeSnapshot> buffs = state.Buffs ?? new List<BuffRuntimeSnapshot>();
            writer.WriteInt32(buffs.Count);
            for (int i = 0; i < buffs.Count; i++)
            {
                BuffRuntimeSnapshot buff = buffs[i];
                writer.WriteInt32(buff.ConfigId.Value); WriteUnitUid(writer, buff.SourceUnitUid);
                writer.WriteInt32(buff.RemainingTicks); writer.WriteInt32(buff.CurrentStacks); writer.WriteInt32(buff.ElapsedTicks);
                writer.WriteInt32(buff.PeriodicTimer); writer.WriteByte((byte)buff.RemovalReason); writer.WriteBoolean(buff.IsRemoving);
                BuffStatHandleSnapshot[] stats = buff.Blackboard.StatHandles ?? Array.Empty<BuffStatHandleSnapshot>();
                writer.WriteInt32(stats.Length);
                for (int j = 0; j < stats.Length; j++)
                { writer.WriteString(stats[j].Key); WriteUnitUid(writer, stats[j].Handle.OwnerUnitUid); writer.WriteInt32((int)stats[j].Handle.StatId); writer.WriteUInt32(stats[j].Handle.StatSeq); }
                BuffCombatHandleSnapshot[] combats = buff.Blackboard.CombatHandles ?? Array.Empty<BuffCombatHandleSnapshot>();
                writer.WriteInt32(combats.Length);
                for (int j = 0; j < combats.Length; j++)
                { writer.WriteString(combats[j].Key); WriteUnitUid(writer, combats[j].Handle.OwnerUnitUid); writer.WriteUInt64(combats[j].Handle.ModifierId); }
            }
        }

        private static void WriteCrowdControl(CanonicalByteWriter writer, in CrowdControlHandlerSnapshot state)
        {
            CrowdControlConstraint[] instances = state.Instances ?? Array.Empty<CrowdControlConstraint>();
            writer.WriteInt32(instances.Length);
            for (int i = 0; i < instances.Length; i++)
            { writer.WriteInt32(instances[i].InstanceId); writer.WriteByte((byte)instances[i].Type); writer.WriteInt32(instances[i].StartLogicTick); writer.WriteInt32(instances[i].RemainingTicks); writer.WriteByte(instances[i].Priority); WriteUnitUid(writer, instances[i].SourceUnitUid); writer.WriteBoolean(instances[i].IsForcedMove); WriteFp2(writer, instances[i].ForcedMoveDeltaPerTick); }
            CrowdControlImmunitySnapshot[] immunities = state.Immunities ?? Array.Empty<CrowdControlImmunitySnapshot>();
            writer.WriteInt32(immunities.Length);
            for (int i = 0; i < immunities.Length; i++)
            { writer.WriteInt32(immunities[i].ImmunityId); writer.WriteInt32(immunities[i].RemainingTicks); }
            CrowdControlUnstoppableSnapshot[] unstoppables = state.Unstoppables ?? Array.Empty<CrowdControlUnstoppableSnapshot>();
            writer.WriteInt32(unstoppables.Length);
            for (int i = 0; i < unstoppables.Length; i++)
            { writer.WriteInt32(unstoppables[i].UnstoppableId); writer.WriteInt32(unstoppables[i].RemainingTicks); }
            writer.WriteInt32(state.NextInstanceId); writer.WriteInt32(state.NextImmunityId); writer.WriteInt32(state.NextUnstoppableId);
            writer.WriteInt32(state.ActiveForcedMoveHandle.InstanceId);
        }

        private static void WriteEquipment(CanonicalByteWriter writer, in EquipmentHandlerSnapshot state)
        {
            EquipmentSlotSnapshot[] slots = state.Slots ?? Array.Empty<EquipmentSlotSnapshot>();
            writer.WriteInt32(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                writer.WriteBoolean(slots[i].Occupied); writer.WriteInt32(slots[i].EquipmentId);
                writer.WriteInt32(slots[i].StackCount); writer.WriteInt32(slots[i].ChargeCount);
                writer.WriteInt32(slots[i].ReadyTick);
                StatModifierHandle[] handles = slots[i].FixedStatHandles ?? Array.Empty<StatModifierHandle>();
                writer.WriteInt32(handles.Length);
                for (int handleIndex = 0; handleIndex < handles.Length; handleIndex++)
                {
                    WriteUnitUid(writer, handles[handleIndex].OwnerUnitUid);
                    writer.WriteInt32((int)handles[handleIndex].StatId);
                    writer.WriteUInt32(handles[handleIndex].StatSeq);
                }
                EquipmentEffectRuntimeSnapshot[] effects =
                    slots[i].EffectStates ?? Array.Empty<EquipmentEffectRuntimeSnapshot>();
                writer.WriteInt32(effects.Length);
                for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    EquipmentEffectModuleRuntimeState[] modules =
                        effects[effectIndex].ModuleStates ?? Array.Empty<EquipmentEffectModuleRuntimeState>();
                    writer.WriteInt32(modules.Length);
                    for (int moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
                    {
                        writer.WriteInt32(modules[moduleIndex].NextExecuteTick);
                        writer.WriteInt32(modules[moduleIndex].InternalCooldownReadyTick);
                        writer.WriteInt32(modules[moduleIndex].StackCount);
                        writer.WriteInt32(modules[moduleIndex].TimerTicks);
                    }
                }
            }
            List<EquipmentSharedCooldownSnapshot> cooldowns =
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
            DamageContributionTrackerSnapshot[] trackers = state.ContributionTrackers ?? Array.Empty<DamageContributionTrackerSnapshot>();
            writer.WriteInt32(trackers.Length);
            for (int i = 0; i < trackers.Length; i++)
            { WriteUnitUid(writer, trackers[i].VictimUnitUid); DamageContributionRecordSnapshot[] records = trackers[i].Records ?? Array.Empty<DamageContributionRecordSnapshot>(); writer.WriteInt32(records.Length); for (int j = 0; j < records.Length; j++) { WriteUnitUid(writer, records[j].ContributorHeroUid); writer.WriteInt32(records[j].LastContributionLogicTick); writer.WriteFp(records[j].ContributionValue); writer.WriteInt32(records[j].ExpireLogicTick); } }
            DeferredCombatRequest[] deferred = state.DeferredRequests ?? Array.Empty<DeferredCombatRequest>();
            writer.WriteInt32(deferred.Length);
            for (int i = 0; i < deferred.Length; i++)
            { writer.WriteInt32(deferred[i].ExecuteLogicTick); writer.WriteInt32(deferred[i].SourceLogicTick); writer.WriteUInt32(deferred[i].DeferredSequenceInSourceTick); writer.WriteByte((byte)deferred[i].RequestKind); WriteCombatPayload(writer, deferred[i]); }
        }

        private static void WriteCombatPayload(CanonicalByteWriter writer, in DeferredCombatRequest request)
        {
            switch (request.RequestKind)
            {
                case CombatRequestKind.Shield: WriteUnitUid(writer, request.Shield.SourceUnitUid); WriteUnitUid(writer, request.Shield.TargetUnitUid); writer.WriteFp(request.Shield.BaseValue); writer.WriteByte((byte)request.Shield.ShieldType); break;
                case CombatRequestKind.Damage: WriteUnitUid(writer, request.Damage.SourceUnitUid); WriteUnitUid(writer, request.Damage.TargetUnitUid); writer.WriteByte((byte)request.Damage.DamageType); writer.WriteFp(request.Damage.BaseDamage); writer.WriteByte(request.Damage.AttackSequenceIndex); break;
                case CombatRequestKind.Heal: WriteUnitUid(writer, request.Heal.SourceUnitUid); WriteUnitUid(writer, request.Heal.TargetUnitUid); writer.WriteFp(request.Heal.BaseValue); break;
            }
        }

        private static void WriteProjectiles(CanonicalByteWriter writer, in ProjectileWorldSnapshot state)
        {
            List<PendingSpawnRecordSnapshot> pending = state.PendingSpawns ?? new List<PendingSpawnRecordSnapshot>(); writer.WriteInt32(pending.Count);
            for (int i = 0; i < pending.Count; i++) { WriteProjectileUid(writer, pending[i].Uid); writer.WriteInt32(pending[i].DefId); WriteUnitUid(writer, pending[i].OwnerUnitUid); writer.WriteByte(pending[i].TeamSnapshot.Value); WriteFp2(writer, pending[i].StartPosition); WriteFp2(writer, pending[i].Direction); }
            List<ProjectileRuntimeSnapshot> active = state.ActiveProjectiles ?? new List<ProjectileRuntimeSnapshot>(); writer.WriteInt32(active.Count);
            for (int i = 0; i < active.Count; i++) { WriteProjectileUid(writer, active[i].Uid); writer.WriteInt32(active[i].DefId); WriteUnitUid(writer, active[i].OwnerUnitUid); writer.WriteByte(active[i].TeamSnapshot.Value); WriteFp2(writer, active[i].PreviousPosition); WriteFp2(writer, active[i].Position); WriteFp2(writer, active[i].Velocity); writer.WriteInt32(active[i].RemainingLifetimeTicks); writer.WriteBoolean(active[i].IsActive); writer.WriteInt32(active[i].HitCount); WriteUidList(writer, active[i].HitTargets); }
        }

        private static void WriteEquipmentShop(CanonicalByteWriter writer, in EquipmentShopRuntimeSnapshot state)
        {
            List<ShopTraderRuntimeSnapshot> traders = state.CreatedTraders ?? new List<ShopTraderRuntimeSnapshot>(); writer.WriteInt32(traders.Count);
            for (int i = 0; i < traders.Count; i++) { writer.WriteInt32(traders[i].Player); WriteUnitUid(writer, traders[i].ControlledUnitUid); writer.WriteInt32(traders[i].NextOperationSequence); ShopOperationRecord[] ops = traders[i].OperationLog ?? Array.Empty<ShopOperationRecord>(); writer.WriteInt32(ops.Length); for (int j = 0; j < ops.Length; j++) { writer.WriteInt32(ops[j].OperationSequence); writer.WriteByte((byte)ops[j].OperationType); writer.WriteInt32(ops[j].Player); WriteUnitUid(writer, ops[j].ControlledUnitUid); writer.WriteInt32(ops[j].LogicTick); writer.WriteInt32(ops[j].GoldDelta); writer.WriteBoolean(ops[j].Reverted); writer.WriteInt32(ops[j].RevertedLogicTick); writer.WriteInt32(ops[j].EquipmentRevisionBefore); writer.WriteInt32(ops[j].EquipmentRevisionAfter); } WriteIntArray(writer, traders[i].UndoableOperationStack); }
        }

        private static void WritePhysics(CanonicalByteWriter writer, in PhysicsRuntimeSnapshot state)
        {
            UnitContactPair[] pairs = state.CollisionBuffer.PreviousPairs ?? Array.Empty<UnitContactPair>(); writer.WriteInt32(pairs.Length);
            for (int i = 0; i < pairs.Length; i++) { WriteRuntimeUid(writer, pairs[i].MinUid); WriteRuntimeUid(writer, pairs[i].MaxUid); }
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
