using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class CombatSystem : IRollback<CombatSnapshot>
    {
        public MatchEventTracker MatchEventTracker { get; set; }
        private readonly UnitWorld _unitWorld;
        private CombatSnapshot _snapshot;

        public DeathEffectDispatcher DeathEffectDispatcher { get; set; }
        public RespawnTimer RespawnTimer { get; set; }
        public int HeroRespawnBaseTicks { get; }
        public int HeroRespawnPerLevelTicks { get; }

        private readonly List<ShieldRequest> _shieldQueue = new List<ShieldRequest>();
        private readonly List<DamageRequest> _damageQueue = new List<DamageRequest>();
        private readonly List<HealRequest> _healQueue = new List<HealRequest>();
        private readonly List<DeferredCombatRequest> _deferredBuffer = new List<DeferredCombatRequest>();
        private ushort _nextDeferredSeq;
        private bool _deferredSeqExhausted;
        private ushort _nextSequenceInTick;
        private bool _sequenceExhausted;
        private int _currentSequenceLogicTick = -1;
        private readonly Dictionary<UnitUid, DamageContributionTracker> _contributionTrackers = new Dictionary<UnitUid, DamageContributionTracker>();
        private readonly Dictionary<UnitUid, UnitUid> _pendingKillerHeroUids =
            new Dictionary<UnitUid, UnitUid>();
        private readonly List<UnitUid> _contributionVictimScratch = new List<UnitUid>();
        private readonly List<UnitUid> _pendingDying = new List<UnitUid>();
        private readonly List<DeathResult> _deathResults = new List<DeathResult>();
        private ushort _nextDeathSeq;
        private bool _deathSeqExhausted;

        public int ShieldProcessed { get; private set; }
        public int DamageProcessed { get; private set; }
        public int HealProcessed { get; private set; }

        public CombatSystem(
            UnitWorld unitWorld,
            int heroRespawnBaseTicks,
            int heroRespawnPerLevelTicks)
        {
            if (unitWorld == null)
                throw new System.ArgumentNullException(nameof(unitWorld));
            if (heroRespawnBaseTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnBaseTicks));
            if (heroRespawnPerLevelTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnPerLevelTicks));
            _unitWorld = unitWorld;
            HeroRespawnBaseTicks = heroRespawnBaseTicks;
            HeroRespawnPerLevelTicks = heroRespawnPerLevelTicks;
            _snapshot = CombatSnapshot.Default;
        }
        public void SubmitShield(ShieldRequest request)
        {
            if (!request.IsValid) return;
            request.Header = AllocateActiveHeader();
            _shieldQueue.Add(request);
        }

        public bool SubmitDamage(DamageRequest request)
        {
            if (!request.IsValid) return false;
            request.Header = AllocateActiveHeader(request.Header);
            _damageQueue.Add(request);
            return true;
        }

        public void SubmitHeal(HealRequest request)
        {
            if (!request.IsValid) return;
            request.Header = AllocateActiveHeader();
            _healQueue.Add(request);
        }

        public void BeginTick()
        {
            _shieldQueue.Clear(); _damageQueue.Clear(); _healQueue.Clear();
            _pendingDying.Clear(); _deathResults.Clear();
            _pendingKillerHeroUids.Clear();
            ShieldProcessed = 0; DamageProcessed = 0; HealProcessed = 0;
            _nextDeathSeq = 0; _deathSeqExhausted = false;
            _nextDeferredSeq = 0; _deferredSeqExhausted = false;
            _currentSequenceLogicTick = SimulationTickContext.Current.Tick;
            _nextSequenceInTick = 0;
            _sequenceExhausted = false;
            ImportDeferredRequests();
            int t = SimulationTickContext.Current.Tick;
            FillSortedContributionVictims();
            for (int trackerIndex = 0;
                 trackerIndex < _contributionVictimScratch.Count;
                 trackerIndex++)
            {
                _contributionTrackers[_contributionVictimScratch[trackerIndex]]
                    .PruneExpired(t);
            }
            IReadOnlyList<Unit> units = _unitWorld.GetAllUnits();
            for (int i = 0; i < units.Count; i++)
                units[i].StatHandler?.ExpireShields(t);
        }

        public void SettleActiveRequests()
        {
            ExecuteNaturalRegen();
            int shieldIndex = 0;
            int damageIndex = 0;
            int healIndex = 0;
            while (shieldIndex < _shieldQueue.Count ||
                   damageIndex < _damageQueue.Count ||
                   healIndex < _healQueue.Count)
            {
                ushort shieldSequence = shieldIndex < _shieldQueue.Count
                    ? _shieldQueue[shieldIndex].Header.SequenceInTick
                    : ushort.MaxValue;
                ushort damageSequence = damageIndex < _damageQueue.Count
                    ? _damageQueue[damageIndex].Header.SequenceInTick
                    : ushort.MaxValue;
                ushort healSequence = healIndex < _healQueue.Count
                    ? _healQueue[healIndex].Header.SequenceInTick
                    : ushort.MaxValue;

                if (shieldIndex < _shieldQueue.Count &&
                    shieldSequence <= damageSequence && shieldSequence <= healSequence)
                {
                    ProcessShield(_shieldQueue[shieldIndex++]);
                    ShieldProcessed++;
                }
                else if (damageIndex < _damageQueue.Count && damageSequence <= healSequence)
                {
                    ProcessDamage(_damageQueue[damageIndex++]);
                    DamageProcessed++;
                }
                else
                {
                    ProcessHeal(_healQueue[healIndex++]);
                    HealProcessed++;
                }
            }
            ResolveDying();
            _shieldQueue.Clear();
            _damageQueue.Clear();
            _healQueue.Clear();
            _pendingDying.Clear();
            _pendingKillerHeroUids.Clear();
        }

        public void EndTick()
        {
            ValidateCaptureState();
            FreezeSnapshot();
        }

        /// <summary>
        /// Verifies that all per-Tick transient state is cleared before Capture.
        /// (Snapshot Appendix v7.2 section 7.3 -- Capture assertions)
        /// </summary>
        private void ValidateCaptureState()
        {
            if (_shieldQueue.Count != 0)
                throw new DeterministicSimulationException("Combat active ShieldQueue must be empty before Capture.");
            if (_damageQueue.Count != 0)
                throw new DeterministicSimulationException("Combat active DamageQueue must be empty before Capture.");
            if (_healQueue.Count != 0)
                throw new DeterministicSimulationException("Combat active HealQueue must be empty before Capture.");
            if (_pendingDying.Count != 0)
                throw new DeterministicSimulationException("Combat PendingDying must be empty before Capture.");
            // Verify all DeferredRequests target future ticks
            for (int i = 0; i < _deferredBuffer.Count; i++)
            {
                var dr = _deferredBuffer[i];
                if (dr.ExecuteLogicTick != SimulationTickContext.Current.Tick + 1)
                    throw new DeterministicSimulationException(
                        $"Deferred request ExecuteLogicTick={dr.ExecuteLogicTick} " +
                        $"must equal CurrentTick+1={SimulationTickContext.Current.Tick + 1}.");
            }
        }

        public IReadOnlyList<DeathResult> DeathResults => _deathResults;

        public bool HasDeferredRequestFrom(UnitUid sourceUnitUid)
        {
            for (int i = 0; i < _deferredBuffer.Count; i++)
            {
                DeferredCombatRequest request = _deferredBuffer[i];
                UnitUid source = request.RequestKind switch
                {
                    CombatRequestKind.Shield => request.Shield.SourceUnitUid,
                    CombatRequestKind.Damage => request.Damage.SourceUnitUid,
                    CombatRequestKind.Heal => request.Heal.SourceUnitUid,
                    _ => default,
                };
                if (source == sourceUnitUid) return true;
            }
            return false;
        }

        public void DeferRequest(CombatRequestKind k, ShieldRequest? s, DamageRequest? d, HealRequest? h, int et, int st)
        {
            if (_deferredSeqExhausted) throw new DeterministicSimulationException("Deferred seq exhausted.");
            ushort seq = _nextDeferredSeq; if (_nextDeferredSeq == ushort.MaxValue) _deferredSeqExhausted = true; else _nextDeferredSeq++;
            var req = new DeferredCombatRequest { ExecuteLogicTick = et, SourceLogicTick = st, DeferredSequenceInSourceTick = seq, RequestKind = k };
            if (s.HasValue) req.Shield = s.Value; if (d.HasValue) req.Damage = d.Value; if (h.HasValue) req.Heal = h.Value;
            _deferredBuffer.Add(req);
        }

        private void ExecuteNaturalRegen()
        {
            var all = _unitWorld.GetAllUnits();
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i]; if (u.LifeState != LifeState.Alive) continue;
                StatHandler st = u.StatHandler; if (st == null) continue;
                fp reg = st.GetStat(StatId.HealthRegeneration); if (reg <= fp.zero) continue;
                fp cur = st.CurrentHealth; fp max = st.GetStat(StatId.MaxHealth);
                fp nw = cur + reg; if (nw > max) nw = max; st.SetCurrentHealth(nw);
            }
        }

        private void ProcessShield(ShieldRequest r)
        {
            if (!_unitWorld.TryGetUnit(r.TargetUnitUid, out Unit trg)) return;
            if (trg.LifeState != LifeState.Alive && trg.LifeState != LifeState.Dying) return;
            fp amt = r.BaseValue; StatHandler ss = null;
            if (_unitWorld.TryGetUnit(r.SourceUnitUid, out Unit src)) ss = src.StatHandler;
            if (ss != null) { fp sp = ss.GetStat(StatId.ShieldPower); amt *= (fp.one + sp); }
            if (amt <= fp.zero) return;
            fp applied = trg.StatHandler?.AddShield(
                r.ShieldType, amt, r.DurationTicks, r.SourceUnitUid) ?? fp.zero;
            if (applied <= fp.zero) return;
            CombatEvents.RaiseShieldApplied(new ShieldEventData { SourceUid = r.SourceUnitUid, TargetUid = r.TargetUnitUid, ShieldAmount = amt, ShieldType = r.ShieldType });
            CombatEvents.OnCombatParticipationUnit?.Invoke(r.SourceUnitUid, r.TargetUnitUid, CombatParticipationFlags.ShieldGranted | CombatParticipationFlags.ShieldReceived);
        }

        private void ProcessDamage(DamageRequest req)
        {
            if (!_unitWorld.TryGetUnit(req.TargetUnitUid, out Unit target)) return;
            if (target.LifeState != LifeState.Alive && target.LifeState != LifeState.Dying) return;
            StatHandler stats = target.StatHandler; if (stats == null) return;
            _unitWorld.TryGetUnit(
                req.SourceUnitUid,
                out Unit sourceUnit);
            StatHandler sourceStats =
                sourceUnit?.StatHandler;
            CombatModifierSet sourceModifiers =
                sourceUnit?.CombatModifiers;
            CombatModifierSet targetModifiers =
                target.CombatModifiers;
            var policies = new CombatPolicyResolution();
            fp raw = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.CoreValue,
                req.BaseDamage,
                req.BaseDamage,
                sourceStats,
                stats,
                ref policies);
            bool isCrit = false;
            if (!policies.ForbidCrit &&
                sourceStats != null &&
                _unitWorld.RandomService != null)
            {
                bool shouldCrit = policies.ForceCrit;
                if (!shouldCrit)
                {
                    fp critChance =
                        sourceStats.GetStat(
                            StatId.CriticalStrikeChance);
                    shouldCrit =
                        critChance > fp.zero &&
                        _unitWorld.RandomService.Chance01(
                            critChance);
                }
                if (shouldCrit)
                {
                    fp critDamage =
                        sourceStats.GetStat(
                            StatId.CriticalStrikeDamage);
                    if (critDamage <= fp.zero)
                        critDamage = (fp)2;
                    raw *= critDamage;
                    isCrit = true;
                }
            }
            raw = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.PreDefenseValue,
                req.BaseDamage,
                raw,
                sourceStats,
                stats,
                ref policies);
            fp res = fp.zero;
            if (req.DamageType == DamageType.Physical) res = stats.GetStat(StatId.Armor);
            else if (req.DamageType == DamageType.Magic) res = stats.GetStat(StatId.MagicResistance);
            res = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.DefenseInput,
                req.BaseDamage,
                res,
                sourceStats,
                stats,
                ref policies);
            fp mitigated =
                CalculateResistedDamage(raw, res);
            mitigated = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.PostDefenseValue,
                req.BaseDamage,
                mitigated,
                sourceStats,
                stats,
                ref policies);
            mitigated = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.FinalValue,
                req.BaseDamage,
                mitigated,
                sourceStats,
                stats,
                ref policies);
            if (mitigated <= fp.zero) return;
            fp afterShields = mitigated; fp shieldAbs = fp.zero;
            if (!policies.IgnoreAllShield)
                shieldAbs = stats.AbsorbShields(
                    ref afterShields,
                    req.DamageType,
                    policies.IgnorePhysicalShield,
                    policies.IgnoreMagicShield);
            fp cur = stats.CurrentHealth;
            fp actualLifeDamage = afterShields > cur ? cur : afterShields;
            fp nw = cur - actualLifeDamage;
            stats.SetCurrentHealth(nw);
            // Death recap tracking (per Combat Design v13.2 section 8)
            if (MatchEventTracker != null)
            {
                MatchEventTracker.RecordDamage(
                    req.TargetUnitUid,
                    req.SourceUnitUid,
                    (int)(shieldAbs + actualLifeDamage),
                    0 /* DamageRequest has no AbilityId */,
                    SimulationTickContext.Current.Tick,
                    0 /* DamageRequest has no AbilityId */ == 0);
            }
            SubmitHitVfx(
                req.TargetUnitUid,
                req.SourceUnitUid,
                req.Header.SequenceInTick);
            RecordContribution(
                req.SourceUnitUid,
                target,
                shieldAbs + actualLifeDamage);
            var evt = new DamageEventData { SourceUid = req.SourceUnitUid, TargetUid = req.TargetUnitUid, RawDamage = raw, MitigatedDamage = mitigated, ActualDamage = actualLifeDamage + shieldAbs, DamageType = req.DamageType, IsCritical = isCrit };
            CombatEvents.RaiseDamageTaken(evt); CombatEvents.RaiseDamageDealt(evt);
            // Fire on-hit event for attack damage
            if (req.Header.SourceDescriptor.SourceType == CombatSourceType.Attack)
            {
                CombatEvents.RaiseOnHit(new OnHitEventData
                {
                    SourceUid = req.SourceUnitUid,
                    TargetUid = req.TargetUnitUid,
                    DamageType = req.DamageType,
                    IsCritical = isCrit,
                });
            }
            CombatEvents.OnCombatParticipationUnit?.Invoke(req.SourceUnitUid, req.TargetUnitUid, CombatParticipationFlags.DamageDealt | CombatParticipationFlags.DamageTaken);
            if (nw <= fp.zero && target.LifeState == LifeState.Alive)
            {
                _unitWorld.RequestEnterDying(target);
                if (!_pendingDying.Contains(req.TargetUnitUid))
                    _pendingDying.Add(req.TargetUnitUid);
                _pendingKillerHeroUids[req.TargetUnitUid] =
                    ResolveContributorHero(req.SourceUnitUid, target);
                target.EventBus?.PublishUnitDying(target);
            }
            ApplyHitReaction(target, mitigated);
        }

        private void SubmitHitVfx(
            UnitUid targetUid,
            UnitUid sourceUid,
            ushort requestSequence)
        {
            if (!_unitWorld.TryGetUnit(targetUid, out Unit target)) return;
            int tick = SimulationTickContext.Current.Tick;
            VisualEventOutput.SubmitVfx(new VfxEvent
            {
                Id = new PresentationEventId
                {
                    SourceLogicTick = tick,
                    SourceKind =
                        PresentationSourceKind.Unit,
                    SourceRuntimeUid = sourceUid,
                    EventSequence =
                        requestSequence,
                    EventKey =
                        PresentationEventKeys
                            .CombatHit,
                },
                VfxDefId =
                    PresentationEventKeys.CombatHit,
                WorldPosition = target.MovementHandler?.Position ?? fp2.zero,
                AttachToUnit = targetUid,
                DurationScale = fp.one,
            });
        }

        private void ProcessHeal(HealRequest req)
        {
            if (!_unitWorld.TryGetUnit(req.TargetUnitUid, out Unit target)) return;
            if (target.LifeState != LifeState.Alive && target.LifeState != LifeState.Dying) return;
            StatHandler stats = target.StatHandler; if (stats == null) return;
            fp amt = req.BaseValue;
            if (_unitWorld.TryGetUnit(req.SourceUnitUid, out Unit src) && src.StatHandler != null) { fp hp = src.StatHandler.GetStat(StatId.HealPower); amt *= (fp.one + hp); }
            if (amt <= fp.zero) return;
            fp cur = stats.CurrentHealth; fp max = stats.GetStat(StatId.MaxHealth);
            fp nw = cur + amt; if (nw > max) nw = max; fp eff = nw - cur;
            stats.SetCurrentHealth(nw);
            if (nw > fp.zero && target.LifeState == LifeState.Dying)
            {
                _unitWorld.RequestRecoverFromDying(target);
                _pendingDying.Remove(req.TargetUnitUid);
                _pendingKillerHeroUids.Remove(req.TargetUnitUid);
            }
            CombatEvents.RaiseHealTaken(new HealEventData { SourceUid = req.SourceUnitUid, TargetUid = req.TargetUnitUid, RawHeal = req.BaseValue, EffectiveHeal = eff });
            CombatEvents.OnCombatParticipationUnit?.Invoke(req.SourceUnitUid, req.TargetUnitUid, CombatParticipationFlags.HealDealt | CombatParticipationFlags.HealTaken);
            CombatEvents.RaiseHealDealt(new HealEventData { SourceUid = req.SourceUnitUid, TargetUid = req.TargetUnitUid, RawHeal = req.BaseValue, EffectiveHeal = eff });
        }

        private void ResolveDying()
        {
            for (int i = 0; i < _pendingDying.Count; i++)
            {
                UnitUid duid = _pendingDying[i];
                if (!_unitWorld.TryGetUnit(duid, out Unit unit)) continue;
                if (unit.LifeState != LifeState.Dying) continue;
                if (unit.StatHandler != null &&
                    unit.StatHandler.CurrentHealth > fp.zero)
                {
                    _unitWorld.RequestRecoverFromDying(unit);
                    _pendingKillerHeroUids.Remove(duid);
                    continue;
                }
                var trk = _contributionTrackers.TryGetValue(duid, out var t) ? t : null;
                UnitUid kid = _pendingKillerHeroUids.TryGetValue(duid, out UnitUid frozenKiller)
                    ? frozenKiller
                    : default;
                UnitUid[] assists = FreezeAssistantHeroUids(trk, kid, unit);
                if (_deathSeqExhausted)
                    throw new DeterministicSimulationException(
                        "Combat death SequenceInTick exhausted.");
                ushort deathSequence = _nextDeathSeq;
                if (_nextDeathSeq == ushort.MaxValue) _deathSeqExhausted = true;
                else _nextDeathSeq++;
                var death = new DeathResult { VictimUid = duid, KillerHeroUid = kid, AssistantHeroUids = assists, DeathSequenceInTick = deathSequence, DeathLogicTick = SimulationTickContext.Current.Tick };
                _deathResults.Add(death);
                _unitWorld.ConfirmUnitDeath(unit);
                CombatEvents.RaiseUnitDeath(duid, kid);
                _unitWorld.FinalizeNonHeroDeath(unit);
                if (kid.IsValid()) CombatEvents.RaiseUnitKill(kid, duid);
                for (int assistantIndex = 0; assistantIndex < assists.Length; assistantIndex++)
                    CombatEvents.RaiseUnitAssist(assists[assistantIndex], duid);
                SubmitDeathPresentation(
                    unit,
                    deathSequence);
                unit.ClearForDeath();
                DeathEffectDispatcher?.DispatchDeathEffects(death);
                if (unit.UnitKind == UnitKind.Hero)
                {
                    RespawnTimer?.RegisterDeath(
                        duid,
                        SimulationTickContext.Current.Tick,
                        GetRespawnDelay(unit));
                }
                _contributionTrackers.Remove(duid);
                _pendingKillerHeroUids.Remove(duid);
            }
        }

        private void SubmitDeathPresentation(
            Unit unit,
            ushort deathSequence)
        {
            if (unit?.MovementHandler == null) return;
            int tick = SimulationTickContext.Current.Tick;
            var eventId = new PresentationEventId
            {
                SourceLogicTick = tick,
                SourceKind =
                    PresentationSourceKind.Unit,
                SourceRuntimeUid =
                    unit.UnitUid,
                EventSequence =
                    deathSequence,
                EventKey =
                    PresentationEventKeys
                        .CombatDeath,
            };
            fp2 position =
                unit.MovementHandler.Position;
            VisualEventOutput.SubmitVfx(new VfxEvent
            {
                Id = eventId,
                VfxDefId =
                    PresentationEventKeys
                        .CombatDeath,
                WorldPosition = position,
                DurationScale = fp.one,
            });
            VisualEventOutput.SubmitSfx(new SfxEvent
            {
                Id = eventId,
                SfxDefId =
                    PresentationEventKeys
                        .CombatDeath,
                Anchor = SfxAnchor.World,
                WorldPosition = position,
                PitchScale = fp.one,
                VolumeScale = fp.one,
            });
        }

        private static fp ApplyDamageModifierSlot(
            CombatModifierSet sourceModifiers,
            CombatModifierSet targetModifiers,
            in DamageRequest request,
            CombatFormulaSlot slot,
            fp baseValue,
            fp slotInput,
            StatHandler sourceStats,
            StatHandler targetStats,
            ref CombatPolicyResolution policies)
        {
            CombatFormulaAccumulator accumulator =
                CombatFormulaAccumulator.Create();
            sourceModifiers?.AccumulateDamage(
                CombatModifierScope.Outgoing,
                request.Header,
                request.DamageType,
                slot,
                baseValue,
                slotInput,
                sourceStats,
                targetStats,
                ref accumulator,
                ref policies);
            targetModifiers?.AccumulateDamage(
                CombatModifierScope.Incoming,
                request.Header,
                request.DamageType,
                slot,
                baseValue,
                slotInput,
                sourceStats,
                targetStats,
                ref accumulator,
                ref policies);
            return accumulator.Apply(slotInput);
        }

        public int GetRespawnDelay(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            return checked(
                HeroRespawnBaseTicks +
                ((unit.Level - 1) * HeroRespawnPerLevelTicks));
        }

        private static void ApplyHitReaction(Unit target, fp dmg)
        {
            if (target == null || dmg <= fp.zero) return;
            if (!target.HitReaction.IsActive) { target.HitReaction.Trigger(HitReactionKind.Flinch, 3); fp maxHp = target.StatHandler?.GetStat(StatId.MaxHealth) ?? fp.one; if (dmg > maxHp * (fp)0.1m) target.HitReaction.Trigger(HitReactionKind.Stagger, 6); }
        }

        private void RecordContribution(UnitUid sourceUid, Unit victim, fp damage)
        {
            if (victim == null || damage <= fp.zero) return;
            UnitUid contributorHeroUid = ResolveContributorHero(sourceUid, victim);
            if (!contributorHeroUid.IsValid()) return;
            if (!_contributionTrackers.TryGetValue(
                    victim.UnitUid,
                    out DamageContributionTracker tracker))
            {
                tracker = new DamageContributionTracker(victim.UnitUid);
                _contributionTrackers.Add(victim.UnitUid, tracker);
            }
            tracker.AddContribution(
                contributorHeroUid,
                damage,
                SimulationTickContext.Current.Tick);
        }

        private UnitUid ResolveContributorHero(UnitUid sourceUid, Unit victim)
        {
            UnitUid currentUid = sourceUid;
            for (int depth = 0; depth < 16; depth++)
            {
                if (!currentUid.IsValid() ||
                    !_unitWorld.TryGetUnit(currentUid, out Unit source))
                    return default;
                if (source.UnitKind == UnitKind.Hero)
                {
                    return source.UnitUid != victim.UnitUid &&
                           source.TeamId != victim.TeamId
                        ? source.UnitUid
                        : default;
                }
                currentUid = source.OwnerUid;
            }
            throw new DeterministicSimulationException(
                $"Combat source owner chain exceeded 16 units from {sourceUid}.");
        }

        private UnitUid[] FreezeAssistantHeroUids(
            DamageContributionTracker tracker,
            UnitUid killerHeroUid,
            Unit victim)
        {
            if (tracker == null) return System.Array.Empty<UnitUid>();
            tracker.PruneExpired(SimulationTickContext.Current.Tick);
            List<DamageContributionRecord> records = tracker.GetContributorsByUid();
            var assistants = new List<UnitUid>(records.Count);
            for (int i = 0; i < records.Count; i++)
            {
                UnitUid candidate = records[i].ContributorHeroUid;
                if (candidate == killerHeroUid) continue;
                if (_unitWorld.TryGetUnit(candidate, out Unit hero) &&
                    hero.UnitKind == UnitKind.Hero &&
                    hero.TeamId != victim.TeamId)
                    assistants.Add(candidate);
            }
            return assistants.ToArray();
        }

        private void FillSortedContributionVictims()
        {
            _contributionVictimScratch.Clear();
            _contributionVictimScratch.AddRange(_contributionTrackers.Keys);
            _contributionVictimScratch.Sort((left, right) => left.CompareTo(right));
        }

        private void ImportDeferredRequests()
        {
            int tick = SimulationTickContext.Current.Tick;
            _deferredBuffer.Sort(CompareDeferredRequests);
            for (int i = 0; i < _deferredBuffer.Count;)
            {
                DeferredCombatRequest request = _deferredBuffer[i];
                if (request.ExecuteLogicTick < tick || request.ExecuteLogicTick > tick + 1)
                {
                    throw new DeterministicSimulationException(
                        $"Deferred Combat request has illegal execute Tick {request.ExecuteLogicTick} at Tick {tick}.");
                }
                if (request.ExecuteLogicTick != tick)
                {
                    i++;
                    continue;
                }

                switch (request.RequestKind)
                {
                    case CombatRequestKind.Shield: SubmitShield(request.Shield); break;
                    case CombatRequestKind.Damage: SubmitDamage(request.Damage); break;
                    case CombatRequestKind.Heal: SubmitHeal(request.Heal); break;
                    default:
                        throw new DeterministicSimulationException(
                            $"Deferred Combat request has invalid kind {request.RequestKind}.");
                }
                _deferredBuffer.RemoveAt(i);
            }
        }

        private void FreezeSnapshot()
        {
            if (_shieldQueue.Count != 0 || _damageQueue.Count != 0 ||
                _healQueue.Count != 0 || _pendingDying.Count != 0 ||
                _pendingKillerHeroUids.Count != 0)
                throw new DeterministicSimulationException("Combat active queues must be empty before Capture.");

            var trackers = new List<DamageContributionTracker>(_contributionTrackers.Values);
            trackers.Sort((a, b) => a.VictimUid.CompareTo(b.VictimUid));
            _snapshot.ContributionTrackers = new DamageContributionTrackerSnapshot[trackers.Count];
            for (int index = 0; index < trackers.Count; index++)
            {
                DamageContributionTracker tracker = trackers[index];
                var contributors = tracker.GetContributorsByUid();
                var records = new DamageContributionRecordSnapshot[contributors.Count];
                for (int i = 0; i < contributors.Count; i++)
                {
                    DamageContributionRecord record = contributors[i];
                    records[i] = new DamageContributionRecordSnapshot
                    {
                        ContributorHeroUid = record.ContributorHeroUid,
                        LastContributionLogicTick = record.LastContributionLogicTick,
                        ContributionValue = record.ContributionValue,
                        ExpireLogicTick = record.ExpireLogicTick,
                    };
                }
                _snapshot.ContributionTrackers[index] = new DamageContributionTrackerSnapshot
                {
                    VictimUnitUid = tracker.VictimUid,
                    Records = records,
                };
            }
            _deferredBuffer.Sort(CompareDeferredRequests);
            _snapshot.DeferredRequests = _deferredBuffer.ToArray();
        }

        public void Capture(ref CombatSnapshot snapshot) { FreezeSnapshot(); snapshot = _snapshot; }
        public void Restore(in CombatSnapshot snapshot)
        {
            _snapshot = snapshot;
            _deferredBuffer.Clear();
            if (snapshot.DeferredRequests != null) _deferredBuffer.AddRange(snapshot.DeferredRequests);
            _contributionTrackers.Clear();
            _pendingKillerHeroUids.Clear();
            if (snapshot.ContributionTrackers != null)
            {
                UnitUid previousVictim = default;
                for (int i = 0; i < snapshot.ContributionTrackers.Length; i++)
                {
                    DamageContributionTrackerSnapshot trackerState = snapshot.ContributionTrackers[i];
                    if (!trackerState.VictimUnitUid.IsValid() ||
                        (i > 0 && previousVictim.CompareTo(
                            trackerState.VictimUnitUid) >= 0))
                        throw new DeterministicSimulationException(
                            "Combat contribution trackers are not in canonical VictimUnitUid order.");
                    previousVictim = trackerState.VictimUnitUid;
                    var tracker = new DamageContributionTracker(trackerState.VictimUnitUid);
                    if (trackerState.Records != null)
                    {
                        UnitUid previousContributor = default;
                        for (int j = 0; j < trackerState.Records.Length; j++)
                        {
                            UnitUid contributor =
                                trackerState.Records[j].ContributorHeroUid;
                            if (j > 0 && previousContributor.CompareTo(contributor) >= 0)
                                throw new DeterministicSimulationException(
                                    "Combat contribution records are not in canonical ContributorHeroUid order.");
                            previousContributor = contributor;
                            tracker.RestoreRecord(trackerState.Records[j]);
                        }
                    }
                    _contributionTrackers.Add(trackerState.VictimUnitUid, tracker);
                }
            }
        }

        public void Resolve(in RollbackContext context)
        {
            FillSortedContributionVictims();
            for (int trackerIndex = 0;
                 trackerIndex < _contributionVictimScratch.Count;
                 trackerIndex++)
            {
                DamageContributionTracker tracker =
                    _contributionTrackers[_contributionVictimScratch[trackerIndex]];
                if (!_unitWorld.TryGetUnit(tracker.VictimUid, out _))
                    throw new DeterministicSimulationException($"Missing Combat victim {tracker.VictimUid} during Resolve.");
                var records = tracker.GetContributorsByUid();
                for (int i = 0; i < records.Count; i++)
                    if (!_unitWorld.TryGetUnit(records[i].ContributorHeroUid, out Unit contributor) ||
                        contributor.UnitKind != UnitKind.Hero)
                        throw new DeterministicSimulationException($"Missing Combat contributor {records[i].ContributorHeroUid} during Resolve.");
            }
        }

        public void Rebuild(in RollbackContext context) { }
        public static fp CalculateResistedDamage(fp b, fp r) { if (r < fp.zero) r = fp.zero; return b * (fp)100 / ((fp)100 + r); }

        private CombatRequestHeader AllocateActiveHeader(
            CombatRequestHeader submittedHeader = default)
        {
            int tick = SimulationTickContext.Current.Tick;
            if (_currentSequenceLogicTick != tick)
                throw new DeterministicSimulationException("Combat request submitted outside the active Combat Tick.");
            if (_sequenceExhausted)
                throw new DeterministicSimulationException("Combat request SequenceInTick exhausted.");
            ushort sequence = _nextSequenceInTick;
            if (_nextSequenceInTick == ushort.MaxValue) _sequenceExhausted = true;
            else _nextSequenceInTick++;
            submittedHeader.SequenceInTick = sequence;
            submittedHeader.SourceLogicTick = tick;
            return submittedHeader;
        }

        private static int CompareDeferredRequests(DeferredCombatRequest a, DeferredCombatRequest b)
        {
            int comparison = a.ExecuteLogicTick.CompareTo(b.ExecuteLogicTick);
            if (comparison != 0) return comparison;
            comparison = a.SourceLogicTick.CompareTo(b.SourceLogicTick);
            if (comparison != 0) return comparison;
            return a.DeferredSequenceInSourceTick.CompareTo(b.DeferredSequenceInSourceTick);
        }
    }

}
