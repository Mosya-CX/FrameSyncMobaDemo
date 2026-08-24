using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Varus R "Corruption Vines" buff behavior:
    /// - applies Blight stacks to the owner at 0.2s / 0.8s / 1.4s;
    /// - every Tick scans nearby enemy heroes; a hero that stays inside the
    ///   spread radius for ContactTicks (1s) is infected, and several heroes
    ///   can be infected at once;
    /// - an infected hero receives the same R damage + root + vine effects
    ///   and continues spreading from itself;
    /// - each R cast infects a hero at most once (vine + per-cast marker);
    ///   a later R cast starts a fresh spread (markers are not permanent).
    /// Deterministic; presentation reads the buff source to draw spread
    /// lines. (Hero design R section.)
    /// </summary>
    public sealed class CorruptionVineSpreadBuffEffect :
        BuffEffect
    {
        public int BlightBuffConfigId = 9001;
        public int VineBuffConfigId = 9113;
        /// <summary>
        /// Lightweight invisible tag key used to deduplicate infections from
        /// one R cast. The tag Uid carries the caster + ability + Tick, so a
        /// second R cast replaces it and starts an independent spread.
        /// </summary>
        public string SpreadTagKey = "VarusR.Vine";
        /// <summary>Tag lifetime in Ticks (6s window covers the whole spread
        /// chain; R cooldown is far longer).</summary>
        public DurationAuthoring SpreadTagDuration;
        [HideInInspector] public int SpreadTagTicks = 180;
        /// <summary>
        /// Internal per-candidate "inside spread radius" contact timer buff.
        /// Hidden from the HUD; its ElapsedTicks drives the 1s requirement.
        /// </summary>
        public int TimerBuffConfigId = 9115;
        /// <summary>How many consecutive Ticks a hero must stay in range
        /// before it is infected (30 Ticks = 1s at 30 tps).</summary>
        public DurationAuthoring ContactDuration;
        [HideInInspector] public int ContactTicks = 30;
        public fp SpreadRadius = (fp)5.5m;
        public DurationAuthoring BlightStackDelay1;
        public DurationAuthoring BlightStackDelay2;
        public DurationAuthoring BlightStackDelay3;
        [HideInInspector] public int BlightStackAtTick1 = 6;
        [HideInInspector] public int BlightStackAtTick2 = 24;
        [HideInInspector] public int BlightStackAtTick3 = 42;
        public BuffStateSlotId ElapsedTicksSlot;
        public BuffStateSlotId CasterUnitUidSlot =
            new BuffStateSlotId(2);
        /// <summary>Spread damage by R level (design R: 150/250/350).</summary>
        public int[] SpreadDamageBaseByLevel =
        {
            150, 250, 350, 350, 350,
        };
        public fp SpreadAbilityPowerRatio =
            (fp)1m;
        public int SpreadDamageType = 1;
        public int SpreadRecipeId = 1;
        /// <summary>R ability id used as the damage source (detonates Blight
        /// and is attributed to the original R caster).</summary>
        public int SpreadSourceAbilityId = 10014;
        public int SpreadCrowdControlId = 102;
        public DurationAuthoring SpreadCrowdControlDuration;
        [HideInInspector] public int SpreadCrowdControlTicks = 60;

        public override void BakeTime(int tickRate)
        {
            SpreadTagTicks = Bake(
                SpreadTagDuration, SpreadTagTicks, tickRate);
            ContactTicks = Bake(
                ContactDuration, ContactTicks, tickRate);
            BlightStackAtTick1 = Bake(
                BlightStackDelay1, BlightStackAtTick1, tickRate);
            BlightStackAtTick2 = Bake(
                BlightStackDelay2, BlightStackAtTick2, tickRate);
            BlightStackAtTick3 = Bake(
                BlightStackDelay3, BlightStackAtTick3, tickRate);
            SpreadCrowdControlTicks = Bake(
                SpreadCrowdControlDuration,
                SpreadCrowdControlTicks,
                tickRate);
        }

        private static int Bake(
            in DurationAuthoring duration,
            int legacyTicks,
            int tickRate) =>
            duration.IsAuthored
                ? duration.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(legacyTicks, tickRate);

        private readonly List<Unit> _resultScratch =
            new List<Unit>();
        private readonly List<PhysicsEntity2D>
            _gridScratch =
                new List<PhysicsEntity2D>();

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = ElapsedTicksSlot,
                        Kind = BuffValueKind.Int,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = CasterUnitUidSlot,
                        Kind = BuffValueKind.UnitUid,
                    },
                };

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
            if (ElapsedTicksSlot.IsValid)
            {
                runtime.Blackboard.WriteInt(
                    ElapsedTicksSlot,
                    0);
            }
            ResolveOriginalCaster(owner, runtime);
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnTick(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.World?.RangeQuery == null ||
                owner.BuffHandler == null ||
                !ElapsedTicksSlot.IsValid)
            {
                return;
            }
            if (!runtime.ShouldExecutePeriodic())
            {
                return;
            }

            int elapsed =
                runtime.Blackboard
                    .ReadIntOrDefault(
                        ElapsedTicksSlot) +
                1;
            runtime.Blackboard.WriteInt(
                ElapsedTicksSlot,
                elapsed);

            if (elapsed == BlightStackAtTick1 ||
                elapsed == BlightStackAtTick2 ||
                elapsed == BlightStackAtTick3)
            {
                ApplyBlight(owner, runtime);
            }

            Spread(owner, runtime);
        }

        private void ApplyBlight(
            Unit owner,
            BuffRuntime runtime)
        {
            if (BlightBuffConfigId <= 0 ||
                owner.World.BuffDefinitions == null)
            {
                return;
            }
            var configId =
                new BuffConfigId(
                    BlightBuffConfigId);
            if (!owner.World.BuffDefinitions.TryGet(
                    configId,
                    out BuffDefinition definition))
            {
                return;
            }
            // Chain spread infects new heroes through the immediate
            // spreader, whose vine buff SourceUnitUid is not the original R
            // caster. Blight must always be attributed to the original
            // caster, otherwise its Ability damage can never detonate it
            // (AbilityHitStackDetonationBuffEffect compares SourceUid).
            UnitUid casterUid =
                runtime.Blackboard.ReadUnitUidOrDefault(
                    CasterUnitUidSlot);
            if (!casterUid.IsValid())
            {
                casterUid = runtime.SourceUnitUid;
            }
            if (!casterUid.IsValid())
            {
                return;
            }
            owner.BuffHandler.Apply(
                configId,
                definition,
                BuffSource.Create(
                    casterUid,
                    BuffSourceType.Ability,
                    0));
        }

        /// <summary>
        /// Records the original R caster for this infection chain. The
        /// immediate source may be another infected unit (chain spread), so
        /// walk one hop: if the source already carries the vine buff, inherit
        /// its stored caster; otherwise the immediate source is the caster.
        /// </summary>
        private void ResolveOriginalCaster(
            Unit owner,
            BuffRuntime runtime)
        {
            if (!CasterUnitUidSlot.IsValid ||
                owner?.World == null)
            {
                return;
            }
            UnitUid immediate =
                runtime.SourceUnitUid;
            if (!immediate.IsValid())
            {
                return;
            }
            UnitUid original = immediate;
            if (owner.World.TryGetUnit(
                    immediate,
                    out Unit sourceUnit) &&
                sourceUnit != null &&
                sourceUnit.BuffHandler != null &&
                VineBuffConfigId > 0 &&
                sourceUnit.BuffHandler.TryGetRuntime(
                    new BuffConfigId(
                        VineBuffConfigId),
                    out BuffRuntime sourceVine))
            {
                UnitUid inherited =
                    sourceVine.Blackboard
                        .ReadUnitUidOrDefault(
                            CasterUnitUidSlot);
                if (inherited.IsValid())
                {
                    original = inherited;
                }
            }
            runtime.Blackboard.WriteUnitUid(
                CasterUnitUidSlot,
                original);
        }

        private void Spread(
            Unit owner,
            BuffRuntime runtime)
        {
            fp2 center =
                owner.PhysicsEntity != null
                    ? owner.PhysicsEntity
                        .Transform2D.Position
                    : fp2.zero;
            var desc = new RangeQueryDesc
            {
                Shape = FrameSyncMoba.Physics
                    .PhysicsShape2D.CreateCircle(
                    fp2.zero,
                    SpreadRadius),
                Transform = new PhysicsTransform2D(
                    center,
                    center,
                    fp2.zero,
                    fp2.zero),
                TargetFilter = new UnitTargetFilter
                {
                    TeamRule =
                        TeamQueryRule.EnemyOnly,
                    UnitKindMask =
                        UnitKindMask.Hero,
                    LifeStateMask =
                        UnitLifeStateMask.AliveOnly,
                },
                SortMode =
                    RangeQuerySortMode
                        .DistanceThenUid,
                MaxResult = 0,
            };

            // The spread always infects heroes whose team differs from the
            // original R caster, never the caster's own camp.
            UnitUid casterUid =
                runtime.Blackboard
                    .ReadUnitUidOrDefault(
                        CasterUnitUidSlot);
            UnitUid requesterUid = owner.UnitUid;
            TeamId requesterTeam = owner.TeamId;
            if (casterUid.IsValid() &&
                owner.World.TryGetUnit(
                    casterUid,
                    out Unit caster) &&
                caster != null)
            {
                requesterUid = casterUid;
                requesterTeam = caster.TeamId;
            }

            _resultScratch.Clear();
            _gridScratch.Clear();
            owner.World.RangeQuery.Query(
                desc,
                requesterUid,
                requesterTeam,
                _resultScratch,
                _gridScratch);

            if (VineBuffConfigId <= 0 ||
                owner.World.BuffDefinitions == null)
            {
                return;
            }
            var vineId =
                new BuffConfigId(
                    VineBuffConfigId);
            if (!owner.World.BuffDefinitions.TryGet(
                    vineId,
                    out BuffDefinition vineDef))
            {
                return;
            }

            RemoveStaleTimers(owner);

            for (int i = 0;
                 i < _resultScratch.Count;
                 i++)
            {
                Unit target = _resultScratch[i];
                if (target == null ||
                    target.LifeState !=
                        LifeState.Alive ||
                    target.BuffHandler == null ||
                    target.BuffHandler.HasBuff(
                        vineId) ||
                    target.HasTag(
                        SpreadTagKey))
                {
                    continue;
                }

                if (TimerBuffConfigId > 0 &&
                    target.BuffHandler.HasBuff(
                        new BuffConfigId(
                            TimerBuffConfigId)))
                {
                    var timerId =
                        new BuffConfigId(
                            TimerBuffConfigId);
                    if (target.BuffHandler
                            .TryGetRuntime(
                                timerId,
                                out BuffRuntime
                                    timer) &&
                        timer.SourceUnitUid ==
                            owner.UnitUid)
                    {
                        if (timer.ElapsedTicks >=
                            ContactTicks)
                        {
                            target.BuffHandler.Remove(
                                timerId);
                            Infect(
                                target,
                                owner,
                                runtime,
                                vineId,
                                vineDef);
                        }
                        continue;
                    }
                    // A different vine owns this contact timer: take it over
                    // so this vine starts its own full 1s contact count.
                    target.BuffHandler.Remove(
                        timerId);
                    ApplyTimer(
                        target,
                        owner,
                        runtime);
                    continue;
                }

                ApplyTimer(
                    target,
                    owner,
                    runtime);
            }
        }

        /// <summary>
        /// A hero that leaves the spread radius before ContactTicks loses its
        /// contact timer (owned by this vine); it must stay inside again for
        /// a full second to be infected. Only timers created by this vine are
        /// touched - another vine's candidates keep their own countdown.
        /// </summary>
        private void RemoveStaleTimers(
            Unit owner)
        {
            if (TimerBuffConfigId <= 0 ||
                owner.World == null)
            {
                return;
            }
            var timerId =
                new BuffConfigId(
                    TimerBuffConfigId);
            var units =
                owner.World.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                Unit unit = units[i];
                if (unit == null ||
                    unit.BuffHandler == null ||
                    !unit.BuffHandler.HasBuff(
                        timerId))
                {
                    continue;
                }
                if (!unit.BuffHandler.TryGetRuntime(
                        timerId,
                        out BuffRuntime timer) ||
                    timer.SourceUnitUid !=
                        owner.UnitUid)
                {
                    continue;
                }
                if (!_resultScratch.Contains(
                        unit))
                {
                    unit.BuffHandler.Remove(
                        timerId);
                }
            }
        }

        private void ApplyTimer(
            Unit target,
            Unit owner,
            BuffRuntime runtime)
        {
            if (TimerBuffConfigId <= 0 ||
                target?.World?.BuffDefinitions ==
                    null)
            {
                return;
            }
            var timerId =
                new BuffConfigId(
                    TimerBuffConfigId);
            if (!target.World.BuffDefinitions
                    .TryGet(
                        timerId,
                        out BuffDefinition
                            timerDef))
            {
                return;
            }
            target.BuffHandler.Apply(
                timerId,
                timerDef,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Ability,
                    0));
        }

        /// <summary>
        /// Applies the same R effects to a newly infected hero: magic damage
        /// (attributed to the original R caster, detonates Blight), a 2s
        /// root, and the vine buff itself.
        /// </summary>
        private void Infect(
            Unit target,
            Unit spreader,
            BuffRuntime runtime,
            BuffConfigId vineId,
            BuffDefinition vineDef)
        {
            ApplySpreadDamage(
                target,
                runtime);
            ApplySpreadCrowdControl(
                target);
            ApplySpreadTag(
                target,
                runtime);
            target.BuffHandler.Apply(
                vineId,
                vineDef,
                BuffSource.Create(
                    spreader.UnitUid,
                    BuffSourceType.Ability,
                    0));
        }

        private void ApplySpreadDamage(
            Unit target,
            BuffRuntime runtime)
        {
            if (target?.World?.CombatSystem ==
                    null ||
                SpreadDamageBaseByLevel == null ||
                SpreadDamageBaseByLevel.Length ==
                    0)
            {
                return;
            }
            UnitUid casterUid =
                runtime.Blackboard
                    .ReadUnitUidOrDefault(
                        CasterUnitUidSlot);
            if (!casterUid.IsValid())
            {
                casterUid =
                    runtime.SourceUnitUid;
            }
            if (!casterUid.IsValid() ||
                !target.World.TryGetUnit(
                    casterUid,
                    out Unit caster) ||
                caster == null)
            {
                return;
            }

            int level =
                Mathf.Max(
                    1,
                    caster.AbilityHandler?
                        .GetActiveRuntime(3)
                        ?.Level ?? 1);
            int index =
                Mathf.Min(
                    level - 1,
                    SpreadDamageBaseByLevel
                        .Length - 1);
            fp baseDamage =
                (fp)SpreadDamageBaseByLevel[
                    index];
            fp ap =
                caster.StatHandler != null
                    ? caster.StatHandler
                        .GetStat(
                            StatId
                                .AbilityPower)
                    : fp.zero;
            fp amount =
                baseDamage +
                ap * SpreadAbilityPowerRatio;
            if (amount <= fp.zero)
            {
                return;
            }

            var request =
                new DamageRequest
                {
                    Header =
                        CombatRequestHeader.Create(
                            casterUid,
                            target.UnitUid,
                            CombatSourceType.Ability,
                            SpreadSourceAbilityId,
                            SpreadRecipeId,
                            originActionId:
                                new OriginActionId(
                                    caster.GameplayParticipantId,
                                    CombatSourceType.Ability,
                                    SpreadSourceAbilityId,
                                    SimulationTickContext.Current.Tick,
                                    CombatFairnessKey
                                        .ParticipantLocalSequence(
                                            target.GameplayParticipantId,
                                            SpreadRecipeId)),
                            effectOrdinal:
                                CombatFairnessKey.ComposeEffectOrdinal(
                                    SpreadRecipeId,
                                    0)),
                    DamageType =
                        (DamageType)
                        SpreadDamageType,
                    BaseDamage = amount,
                };
            target.World.CombatSystem
                .SubmitDamage(request);
        }

        private void ApplySpreadCrowdControl(
            Unit target)
        {
            if (SpreadCrowdControlId <= 0 ||
                target?.CrowdControl == null)
            {
                return;
            }
            target.CrowdControl.Add(
                new CrowdControlId(
                    SpreadCrowdControlId),
                    SpreadCrowdControlTicks,
                default);
        }

        private void ApplySpreadTag(
            Unit target,
            BuffRuntime runtime)
        {
            if (string.IsNullOrEmpty(
                    SpreadTagKey) ||
                target == null)
            {
                return;
            }
            UnitUid casterUid =
                runtime.Blackboard
                    .ReadUnitUidOrDefault(
                        CasterUnitUidSlot);
            if (!casterUid.IsValid())
            {
                casterUid =
                    runtime.SourceUnitUid;
            }
            if (!casterUid.IsValid())
            {
                return;
            }
            target.AddTag(
                SpreadTagKey,
                SpreadTagTicks,
                new UnitTagUid(
                    casterUid,
                    (byte)BuffSourceType
                        .Ability,
                    SpreadSourceAbilityId,
                    SimulationTickContext
                        .Current.Tick));
        }
    }
}
