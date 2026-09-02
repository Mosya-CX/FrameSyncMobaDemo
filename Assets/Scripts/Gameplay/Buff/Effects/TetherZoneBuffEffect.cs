using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Keeps a target inside an impact-anchored trapezoid. Escaping removes
    /// the effect without settlement; surviving until expiry repeats damage
    /// and pulls the target to the anchor over an exact configured duration.
    /// </summary>
    [Serializable]
    public sealed class TetherZoneBuffEffect : BuffEffect
    {
        public int AreaProjectileDefId;
        public AbilityLevelValue BaseDamageByAbilityLevel;
        public AbilityLevelValue AttackDamageRatioByAbilityLevel;
        public AbilityLevelValue SlowRatioByAbilityLevel;
        public DurationAuthoring PullDuration;
        [HideInInspector] public int PullDurationTicks;
        public byte PullPriority;
        public int RecipeId;
        public BuffStateSlotId AnchorSlot;
        public BuffStateSlotId ForwardSlot;
        public BuffStateSlotId EscapedSlot;
        public BuffStateSlotId SlowHandleSlot;
        public BuffStateSlotId ProjectileSpawnTickSlot;
        public BuffStateSlotId ProjectilePrefabIdSlot;
        public BuffStateSlotId ProjectileSequenceSlot;

        public override void BakeTime(int tickRate)
        {
            PullDurationTicks = PullDuration.IsAuthored
                ? PullDuration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        PullDurationTicks,
                        tickRate);
        }

        public override BuffStateSlotDefinition[] RequiredSlotDefinitions =>
            new[]
            {
                new BuffStateSlotDefinition
                {
                    SlotId = AnchorSlot,
                    Kind = BuffValueKind.Fp2,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = ForwardSlot,
                    Kind = BuffValueKind.Fp2,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = EscapedSlot,
                    Kind = BuffValueKind.Bool,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = SlowHandleSlot,
                    Kind = BuffValueKind.StatModifierHandle,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = ProjectileSpawnTickSlot,
                    Kind = BuffValueKind.Int,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = ProjectilePrefabIdSlot,
                    Kind = BuffValueKind.Int,
                },
                new BuffStateSlotDefinition
                {
                    SlotId = ProjectileSequenceSlot,
                    Kind = BuffValueKind.Int,
                },
            };

        public override void OnAdded(BuffRuntime runtime, Unit owner)
        {
            InitializeAnchor(runtime, owner);
            ApplyOrUpdateSlow(runtime, owner);
        }

        public override void OnReapplied(BuffRuntime runtime, Unit owner)
        {
            InitializeAnchor(runtime, owner);
            ApplyOrUpdateSlow(runtime, owner);
        }

        public override void OnTick(BuffRuntime runtime, Unit owner)
        {
            if (owner == null || owner.LifeState != LifeState.Alive)
            {
                MarkEscaped(runtime);
                return;
            }
            if (!TryGetZone(
                    runtime,
                    owner,
                    out ProjectileContainmentZone zone,
                    out fp2 anchor,
                    out fp2 forward))
            {
                MarkEscaped(runtime);
                return;
            }
            fp radius = owner.PhysicsEntity.Shape.Kind ==
                Physics.PhysicsShapeKind.Circle
                    ? owner.PhysicsEntity.Shape.Radius
                    : fp.zero;
            if (!zone.Contains(
                    anchor,
                    forward,
                    owner.PhysicsEntity.Transform2D.Position,
                    radius))
                MarkEscaped(runtime);
        }

        public override void OnRemoved(BuffRuntime runtime, Unit owner)
        {
            ReleaseSlow(runtime, owner);
            fp2 anchor = ResolveAnchor(runtime, owner);
            ReleaseAreaProjectile(runtime, owner);
            if (runtime.Blackboard.ReadBoolOrDefault(EscapedSlot) ||
                runtime.RemovalReason != RemovalReason.DurationExpired ||
                owner == null ||
                owner.LifeState != LifeState.Alive ||
                owner.World?.CombatSystem == null ||
                !owner.World.TryGetUnit(runtime.SourceUnitUid, out Unit source))
            {
                return;
            }

            int abilityLevel = source.AbilityHandler?
                .GetAbilityLevelById(runtime.Source.SourceConfigId) ?? 0;
            fp damage = BaseDamageByAbilityLevel.Resolve(abilityLevel) +
                source.StatHandler.GetStat(StatId.AttackDamage) *
                AttackDamageRatioByAbilityLevel.Resolve(abilityLevel);
            var request = new DamageRequest
            {
                Header = CombatRequestHeader.Create(
                    source.UnitUid,
                    owner.UnitUid,
                    CombatSourceType.Ability,
                    runtime.Source.SourceConfigId,
                    RecipeId,
                    originActionId:
                        BuildOriginActionId(
                            runtime,
                            source,
                            owner),
                    effectOrdinal:
                        CombatFairnessKey.ComposeEffectOrdinal(
                            runtime.ConfigId.Value,
                            1)),
                BaseDamage = damage,
                DamageType = DamageType.Physical,
            };
            if (!owner.World.CombatSystem.SubmitDamage(request))
                throw new DeterministicSimulationException(
                    $"Tether Buff {runtime.ConfigId.Value} repeat damage was rejected.");

            fp2 towardAnchor =
                anchor - owner.PhysicsEntity.Transform2D.Position;
            fp distance = fpmath.sqrt(fpmath.lengthsq(towardAnchor));
            if (distance <= fp.zero || PullDurationTicks <= 0 ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    towardAnchor,
                    out fp2 direction,
                    out _))
            {
                return;
            }
            var parameters = new CrowdControlParamWriter();
            parameters.SetFp2(ControlParamKeys.Direction, direction);
            parameters.SetFp(ControlParamKeys.Distance, distance);
            parameters.SetInt(ControlParamKeys.MoveTicks, PullDurationTicks);
            parameters.SetShort(
                ControlParamKeys.ForcedMovePriority,
                (short)PullPriority);
            if (owner.CrowdControl != null ||
                owner.UnitKind == UnitKind.Structure)
            {
                StructureEffectPolicy.TryApplyCrowdControl(
                    owner,
                    source.UnitUid,
                    CrowdControlIds.KnockBack,
                    PullDurationTicks,
                    parameters);
            }
        }

        public override void ClearForDeath(BuffRuntime runtime, Unit owner)
        {
            ReleaseSlow(runtime, owner);
            ReleaseAreaProjectile(runtime, owner);
        }

        public override void ClearForDespawn(BuffRuntime runtime, Unit owner)
        {
            ReleaseSlow(runtime, owner);
            ReleaseAreaProjectile(runtime, owner);
        }

        private void InitializeAnchor(BuffRuntime runtime, Unit owner)
        {
            ReleaseAreaProjectile(runtime, owner);
            if (owner == null ||
                owner.World == null ||
                owner.World.ProjectileWorld == null ||
                !owner.World.TryGetUnit(runtime.SourceUnitUid, out Unit source))
            {
                MarkEscaped(runtime);
                return;
            }
            fp2 anchor = owner.PhysicsEntity.Transform2D.Position;
            fp2 sourceToTarget =
                anchor - source.PhysicsEntity.Transform2D.Position;
            if (!Physics.PhysicsGeometry2D.TryCreateFacing(
                    sourceToTarget,
                    out fp2 forward,
                    out _))
            {
                forward = owner.PhysicsEntity.Transform2D.Forward;
            }
            runtime.Blackboard.WriteFp2(AnchorSlot, anchor);
            runtime.Blackboard.WriteFp2(ForwardSlot, forward);
            runtime.Blackboard.WriteBool(EscapedSlot, false);
            var request = new ProjectileSpawnRequest(
                AreaProjectileDefId,
                source.UnitUid,
                source.TeamId,
                new SourceDescriptor
                {
                    SourceType = CombatSourceType.Ability,
                    SourceId = runtime.Source.SourceConfigId,
                    OwnerUnitUid = source.UnitUid,
                    EmitterUnitUid = source.UnitUid,
                },
                BuildOriginActionId(runtime, source, owner),
                anchor,
                forward);
            ProjectileUid uid =
                owner.World.ProjectileWorld.RequestSpawn(request);
            if (!uid.IsValid)
                throw new DeterministicSimulationException(
                    $"Tether Buff {runtime.ConfigId.Value} failed to spawn area projectile {AreaProjectileDefId}.");
            WriteProjectileUid(runtime, uid);
        }

        private static OriginActionId BuildOriginActionId(
            BuffRuntime runtime,
            Unit source,
            Unit owner) =>
            new OriginActionId(
                source.GameplayParticipantId,
                CombatSourceType.Ability,
                runtime.Source.SourceConfigId,
                SimulationTickContext.Current.Tick -
                    runtime.ElapsedTicks,
                CombatFairnessKey.ParticipantLocalSequence(
                    owner.GameplayParticipantId,
                    runtime.ConfigId.Value));

        private bool TryGetZone(
            BuffRuntime runtime,
            Unit owner,
            out ProjectileContainmentZone zone,
            out fp2 anchor,
            out fp2 forward)
        {
            anchor = runtime.Blackboard.ReadFp2OrDefault(AnchorSlot);
            forward = runtime.Blackboard.ReadFp2OrDefault(ForwardSlot);
            zone = default;
            ProjectileWorld world = owner?.World?.ProjectileWorld;
            ProjectileDef def = world?.DefRegistry?.FindById(
                AreaProjectileDefId);
            if (def == null || !def.ContainmentZone.IsValid)
                return false;
            zone = def.ContainmentZone;
            ProjectileUid uid = ReadProjectileUid(runtime);
            if (uid.IsValid && world.TryGet(uid, out ProjectileRuntime area))
            {
                anchor = area.Position;
                forward = area.PhysicsEntity.Transform2D.Forward;
            }
            return true;
        }

        private fp2 ResolveAnchor(BuffRuntime runtime, Unit owner)
        {
            fp2 anchor = runtime.Blackboard.ReadFp2OrDefault(AnchorSlot);
            ProjectileWorld world = owner?.World?.ProjectileWorld;
            ProjectileUid uid = ReadProjectileUid(runtime);
            if (uid.IsValid &&
                world != null &&
                world.TryGet(uid, out ProjectileRuntime area))
            {
                anchor = area.Position;
            }
            return anchor;
        }

        private void ReleaseAreaProjectile(
            BuffRuntime runtime,
            Unit owner)
        {
            ProjectileUid uid = ReadProjectileUid(runtime);
            if (uid.IsValid)
                owner?.World?.ProjectileWorld?.RequestEnd(uid);
            WriteProjectileUid(runtime, ProjectileUid.Invalid);
        }

        private ProjectileUid ReadProjectileUid(BuffRuntime runtime)
        {
            int prefabId = runtime.Blackboard.ReadIntOrDefault(
                ProjectilePrefabIdSlot);
            if (prefabId <= 0)
                return ProjectileUid.Invalid;
            return new ProjectileUid(
                runtime.Blackboard.ReadIntOrDefault(
                    ProjectileSpawnTickSlot),
                prefabId,
                checked((byte)runtime.Blackboard.ReadIntOrDefault(
                    ProjectileSequenceSlot)));
        }

        private void WriteProjectileUid(
            BuffRuntime runtime,
            ProjectileUid uid)
        {
            runtime.Blackboard.WriteInt(
                ProjectileSpawnTickSlot,
                uid.IsValid ? uid.SpawnLogicTick : 0);
            runtime.Blackboard.WriteInt(
                ProjectilePrefabIdSlot,
                uid.IsValid ? uid.RuntimeEntityPrefabId : 0);
            runtime.Blackboard.WriteInt(
                ProjectileSequenceSlot,
                uid.IsValid ? uid.SpawnSequenceInTick : 0);
        }

        private void ApplyOrUpdateSlow(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler == null)
                return;
            int abilityLevel = 0;
            if (owner.World != null &&
                owner.World.TryGetUnit(runtime.SourceUnitUid, out Unit source))
            {
                abilityLevel = source.AbilityHandler?
                    .GetAbilityLevelById(runtime.Source.SourceConfigId) ?? 0;
            }
            fp ratio = -SlowRatioByAbilityLevel.Resolve(abilityLevel);
            if (runtime.Blackboard.TryGetStatHandle(
                    SlowHandleSlot,
                    out StatModifierHandle handle))
            {
                owner.StatHandler.SetModifierValue(handle, ratio);
            }
            else
            {
                handle = owner.StatHandler.AddModifier(
                    StatId.MoveSpeed,
                    StatModifierOperation.FinalRatioAdd,
                    ratio);
                runtime.Blackboard.WriteStatHandle(SlowHandleSlot, handle);
            }
        }

        private void ReleaseSlow(BuffRuntime runtime, Unit owner)
        {
            if (owner?.StatHandler != null &&
                runtime.Blackboard.TryGetStatHandle(
                    SlowHandleSlot,
                    out StatModifierHandle handle) &&
                handle.IsValid)
            {
                owner.StatHandler.RemoveModifier(handle);
            }
            runtime.Blackboard.WriteStatHandle(SlowHandleSlot, default);
        }

        private void MarkEscaped(BuffRuntime runtime)
        {
            runtime.Blackboard.WriteBool(EscapedSlot, true);
            runtime.SetRemainingTicks(0);
        }
    }
}
