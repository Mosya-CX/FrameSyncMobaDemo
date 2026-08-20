using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

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
        public int HeroRespawnPerMinuteTicks { get; }

        private readonly List<ShieldRequest> _shieldQueue = new List<ShieldRequest>();
        private readonly List<DamageRequest> _damageQueue = new List<DamageRequest>();
        private readonly List<HealRequest> _healQueue = new List<HealRequest>();
        private readonly List<DeferredCombatRequest> _deferredBuffer = new List<DeferredCombatRequest>();
        private ushort _nextDeferredSeq;
        private bool _deferredSeqExhausted;
        private ushort _nextSequenceInTick;
        private bool _sequenceExhausted;
        private int _currentSequenceLogicTick = -1;
        private readonly Dictionary<UnitUid, CombatContributionEventLog> _eventLogs = new Dictionary<UnitUid, CombatContributionEventLog>();
        private readonly List<UnitUid> _eventLogVictimScratch = new List<UnitUid>();
        private readonly List<UnitUid> _pendingDying = new List<UnitUid>();
        private readonly List<DeathResult> _deathResults = new List<DeathResult>();
        private ushort _nextDeathSeq;
        private bool _deathSeqExhausted;

        public int ShieldProcessed { get; private set; }
        public int DamageProcessed { get; private set; }
        public int HealProcessed { get; private set; }
        /// <summary>
        /// Wall-clock seconds over which the unit's HealthRegeneration /
        /// CastResourceRegeneration stats are fully restored (design v13.2
        /// 5: natural regen, LoL-style per-5s values). Configured from
        /// GlobalGameplayData; defaults to 5.
        /// </summary>
        public int NaturalRegenIntervalMilliseconds { get; set; } =
            5000;

        public CombatSystem(
            UnitWorld unitWorld,
            int heroRespawnBaseTicks,
            int heroRespawnPerMinuteTicks)
        {
            if (unitWorld == null)
                throw new System.ArgumentNullException(nameof(unitWorld));
            if (heroRespawnBaseTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnBaseTicks));
            if (heroRespawnPerMinuteTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnPerMinuteTicks));
            _unitWorld = unitWorld;
            HeroRespawnBaseTicks = heroRespawnBaseTicks;
            HeroRespawnPerMinuteTicks = heroRespawnPerMinuteTicks;
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
            ShieldProcessed = 0; DamageProcessed = 0; HealProcessed = 0;
            _nextDeathSeq = 0; _deathSeqExhausted = false;
            _nextDeferredSeq = 0; _deferredSeqExhausted = false;
            _currentSequenceLogicTick = SimulationTickContext.Current.Tick;
            _nextSequenceInTick = 0;
            _sequenceExhausted = false;
            ImportDeferredRequests();
            int t = SimulationTickContext.Current.Tick;
            FillSortedContributionVictims();
            for (int victimIndex = 0;
                 victimIndex < _eventLogVictimScratch.Count;
                 victimIndex++)
            {
                _eventLogs[_eventLogVictimScratch[victimIndex]]
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
            int tickRate = _unitWorld.TickRate;
            if (tickRate <= 0)
            {
                return;
            }
            fp millisecondsPerTick =
                (fp)1000 / (fp)tickRate;
            fp interval =
                NaturalRegenIntervalMilliseconds > 0
                    ? (fp)NaturalRegenIntervalMilliseconds
                    : (fp)5000;
            fp perTickScale =
                millisecondsPerTick / interval;
            var all = _unitWorld.GetAllUnits();
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i]; if (u.LifeState != LifeState.Alive) continue;
                StatHandler st = u.StatHandler; if (st == null) continue;
                fp healthReg =
                    st.GetStat(
                        StatId.HealthRegeneration);
                if (healthReg > fp.zero)
                {
                    fp cur = st.CurrentHealth;
                    fp max =
                        st.GetStat(StatId.MaxHealth);
                    fp nw =
                        cur + healthReg * perTickScale;
                    if (nw > max)
                    {
                        nw = max;
                    }
                    st.SetCurrentHealth(nw);
                }
                fp resourceReg =
                    st.GetStat(
                        StatId.CastResourceRegeneration);
                if (resourceReg > fp.zero)
                {
                    fp cur =
                        st.CurrentCastResource;
                    fp max =
                        st.GetStat(
                            StatId.MaxCastResource);
                    fp nw =
                        cur + resourceReg * perTickScale;
                    if (nw > max)
                    {
                        nw = max;
                    }
                    st.SetCurrentCastResource(nw);
                }
            }
        }

        private void ProcessShield(ShieldRequest r)
        {
            if (!_unitWorld.TryGetUnit(r.TargetUnitUid, out Unit trg)) return;
            if (trg.LifeState != LifeState.Alive && trg.LifeState != LifeState.Dying) return;
            fp amt = r.BaseValue; StatHandler ss = null;
            if (_unitWorld.TryGetUnit(r.SourceUnitUid, out Unit src)) ss = src.StatHandler;
            // 护盾和治疗强度 is one stat (HealPower): both the shield and
            // heal pipelines scale by (1 + value), value starts at 0.
            if (ss != null) { fp sp = ss.GetStat(StatId.HealPower); amt *= (fp.one + sp); }
            if (amt <= fp.zero) return;
            fp applied = trg.StatHandler?.AddShield(
                r.ShieldType, amt, r.DurationTicks, r.SourceUnitUid) ?? fp.zero;
            if (applied <= fp.zero) return;
            RecordEvent(
                r.SourceUnitUid,
                trg,
                CombatContributionKind.Shield,
                applied,
                r.Header.SequenceInTick);
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
                target.UnitKind,
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
                target.UnitKind,
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
                target.UnitKind,
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
                target.UnitKind,
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
                target.UnitKind,
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
            RecordEvent(
                req.SourceUnitUid,
                target,
                CombatContributionKind.Damage,
                shieldAbs + actualLifeDamage,
                req.Header.SequenceInTick);
            var evt = new DamageEventData
            {
                SourceUid = req.SourceUnitUid,
                TargetUid = req.TargetUnitUid,
                Source = req.Header.SourceDescriptor,
                RecipeId = req.Header.RecipeId,
                RawDamage = raw,
                MitigatedDamage = mitigated,
                ActualDamage = actualLifeDamage + shieldAbs,
                DamageType = req.DamageType,
                IsCritical = isCrit,
            };
            CombatEvents.RaiseDamageTaken(evt); CombatEvents.RaiseDamageDealt(evt);
            ApplyStatDrainHeal(
                req,
                evt.ActualDamage);
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
                target.EventBus?.PublishUnitDying(target);
            }
            ApplyHitReaction(target, mitigated);
        }

        /// <summary>
        /// Omnivamp heals the source for a fraction of the settled actual
        /// damage from any source; Life Steal additionally applies to basic
        /// attacks (Unit v27.3 stat semantics). Both stats are decimals
        /// (0.2 = 20%) and are surfaced as modifiers on the source.
        /// </summary>
        private void ApplyStatDrainHeal(
            in DamageRequest req,
            fp actualDamage)
        {
            if (actualDamage <= fp.zero ||
                _unitWorld == null ||
                !_unitWorld.TryGetUnit(
                    req.SourceUnitUid,
                    out Unit source) ||
                source.StatHandler == null ||
                source.LifeState != LifeState.Alive)
            {
                return;
            }
            fp ratio =
                source.StatHandler.GetStat(StatId.Omnivamp);
            if (req.Header.SourceDescriptor.SourceType ==
                CombatSourceType.Attack)
            {
                ratio +=
                    source.StatHandler.GetStat(StatId.LifeSteal);
            }
            fp healAmount = actualDamage * ratio;
            if (healAmount <= fp.zero)
            {
                return;
            }
            SubmitHeal(
                new HealRequest
                {
                    Header = CombatRequestHeader.Create(
                        req.SourceUnitUid,
                        req.SourceUnitUid,
                        req.Header.SourceDescriptor.SourceType,
                        req.Header.SourceDescriptor.SourceId,
                        req.Header.RecipeId,
                        req.Header.SourceDescriptor
                            .OwnerUnitUid),
                    SourceUnitUid = req.SourceUnitUid,
                    TargetUnitUid = req.SourceUnitUid,
                    BaseValue = healAmount,
                });
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
            // Grievous wounds: incoming heals are reduced by the target's
            // HealingReceivedRatio (1 = normal, 0.6 = -40%).
            amt *= stats.GetStat(
                StatId.HealingReceivedRatio);
            if (amt <= fp.zero) return;
            fp cur = stats.CurrentHealth; fp max = stats.GetStat(StatId.MaxHealth);
            fp nw = cur + amt; if (nw > max) nw = max; fp eff = nw - cur;
            stats.SetCurrentHealth(nw);
            if (nw > fp.zero && target.LifeState == LifeState.Dying)
            {
                _unitWorld.RequestRecoverFromDying(target);
                _pendingDying.Remove(req.TargetUnitUid);
            }
            CombatEvents.RaiseHealTaken(new HealEventData { SourceUid = req.SourceUnitUid, TargetUid = req.TargetUnitUid, RawHeal = req.BaseValue, EffectiveHeal = eff });
            if (eff > fp.zero)
            {
                RecordEvent(
                    req.SourceUnitUid,
                    target,
                    CombatContributionKind.Heal,
                    eff,
                    req.Header.SequenceInTick);
            }
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
                    continue;
                }
                var log = _eventLogs.TryGetValue(duid, out var l) ? l : null;
                UnitUid kid = log != null
                    ? log.ResolveKiller(
                        SimulationTickContext.Current.Tick)
                    : default;
                // Assists are a champion-only concept: only hero victims
                // can have assistants, and assisting a minion/monster kill
                // must not trigger assist rewards or assist reactions.
                UnitUid[] assists =
                    System.Array.Empty<UnitUid>();
                if (unit.UnitKind == UnitKind.Hero &&
                    log != null)
                {
                    assists = log.ResolveAssistants(
                        SimulationTickContext.Current.Tick,
                        _unitWorld,
                        unit,
                        kid);
                }
                if (_deathSeqExhausted)
                    throw new DeterministicSimulationException(
                        "Combat death SequenceInTick exhausted.");
                ushort deathSequence = _nextDeathSeq;
                if (_nextDeathSeq == ushort.MaxValue) _deathSeqExhausted = true;
                else _nextDeathSeq++;
                var death = new DeathResult { VictimUid = duid, KillerHeroUid = kid, AssistantHeroUids = assists, DeathSequenceInTick = deathSequence, DeathLogicTick = SimulationTickContext.Current.Tick };
                UnityEngine.Debug.Log(
                    $"[CombatDeath] tick=" +
                    $"{SimulationTickContext.Current.Tick} " +
                    $"victim={duid} " +
                    $"victimKind={unit.UnitKind} " +
                    $"victimTeam={unit.TeamId.Value} " +
                    $"killer={kid} " +
                    $"assistCount={assists.Length}");
                _deathResults.Add(death);
                _unitWorld.ConfirmUnitDeath(unit);
                CombatEvents.RaiseUnitDeath(duid, kid);
                _unitWorld.FinalizeNonHeroDeath(unit);
                if (kid.IsValid()) CombatEvents.RaiseUnitKill(kid, duid);
                if (unit.UnitKind == UnitKind.Hero)
                {
                    for (int assistantIndex = 0;
                         assistantIndex < assists.Length;
                         assistantIndex++)
                    {
                        CombatEvents.RaiseUnitAssist(
                            assists[assistantIndex],
                            duid);
                    }
                }
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
                _eventLogs.Remove(duid);
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
                unit.MovementHandler?.Position ??
                fp2.zero;
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
            UnitKind targetKind,
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
                targetKind,
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
                targetKind,
                sourceStats,
                targetStats,
                ref accumulator,
                ref policies);
            return accumulator.Apply(slotInput);
        }

        public int GetRespawnDelay(Unit unit)
        {
            if (unit == null) throw new System.ArgumentNullException(nameof(unit));
            int tickRate = _unitWorld.TickRate;
            if (tickRate <= 0)
                throw new DeterministicSimulationException(
                    "UnitWorld.TickRate must be set before computing hero respawn delay.");
            int ticksPerMinute = checked(tickRate * 60);
            int elapsedMinutes =
                SimulationTickContext.Current.Tick / ticksPerMinute;
            return checked(
                HeroRespawnBaseTicks +
                (elapsedMinutes * HeroRespawnPerMinuteTicks));
        }

        private static void ApplyHitReaction(Unit target, fp dmg)
        {
            if (target == null || dmg <= fp.zero) return;
            if (target.HitReaction.IsActive)
                return;
            // Plain damage never interrupts. Interrupts (Stagger/Knockback/
            // Interrupt) must come only from crowd control or explicit
            // ability effects; here we only mark a non-interrupting Flinch
            // so presentation can react without cutting attack/movement
            // animation.
            target.HitReaction.Trigger(
                HitReactionKind.Flinch,
                3);
        }

        private void RecordEvent(
            UnitUid sourceUid,
            Unit victim,
            CombatContributionKind kind,
            fp amount,
            ushort sequenceInTick)
        {
            if (victim == null || amount <= fp.zero) return;
            UnitUid contributorHeroUid = ResolveContributorHero(sourceUid, victim);
            if (!_eventLogs.TryGetValue(
                    victim.UnitUid,
                    out CombatContributionEventLog log))
            {
                log = new CombatContributionEventLog(
                    victim.UnitUid,
                    GetAssistContributionDurationTicks());
                _eventLogs.Add(victim.UnitUid, log);
            }
            // Settlement audit: print every effective Damage / Shield / Heal
            // event attributed to a source hero. ResolveContributorHero is
            // kill/assist-oriented (it drops self-heals and friendly
            // shields), so the audit falls back to the raw source-owner hero
            // for those events.
            UnitUid auditHero =
                contributorHeroUid.IsValid()
                    ? contributorHeroUid
                    : ResolveSourceHero(sourceUid);
            if (auditHero.IsValid())
            {
                UnityEngine.Debug.Log(
                    $"[CombatContribution] " +
                    $"tick={SimulationTickContext.Current.Tick} " +
                    $"hero={auditHero} " +
                    $"victim={victim.UnitUid} " +
                    $"kind={kind} amount={amount} " +
                    $"seq={sequenceInTick}");
            }
            if (!contributorHeroUid.IsValid() &&
                kind != CombatContributionKind.Damage)
            {
                // Non-hero shield/heal contributions are irrelevant to
                // killer/assist resolution.
                return;
            }
            log.AddEvent(
                new CombatContributionEvent
                {
                    VictimUnitUid = victim.UnitUid,
                    ContributorHeroUid =
                        contributorHeroUid,
                    Kind = kind,
                    Amount = amount,
                    LogicTick =
                        SimulationTickContext
                            .Current.Tick,
                    SequenceInTick = sequenceInTick,
                });
        }

        /// <summary>
        /// Walks the source owner chain up to the controlling hero without
        /// the kill/assist victim/team filters (used by the settlement audit
        /// log so self-heals and friendly shields are still attributed).
        /// </summary>
        private UnitUid ResolveSourceHero(
            UnitUid sourceUid)
        {
            UnitUid currentUid = sourceUid;
            for (int depth = 0;
                 depth < 16;
                 depth++)
            {
                if (!currentUid.IsValid() ||
                    !_unitWorld.TryGetUnit(
                        currentUid,
                        out Unit source))
                {
                    return default;
                }
                if (source.UnitKind == UnitKind.Hero)
                {
                    return source.UnitUid;
                }
                currentUid = source.OwnerUid;
            }
            return default;
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

        private void FillSortedContributionVictims()
        {
            _eventLogVictimScratch.Clear();
            _eventLogVictimScratch.AddRange(_eventLogs.Keys);
            _eventLogVictimScratch.Sort((left, right) => left.CompareTo(right));
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
                _healQueue.Count != 0 || _pendingDying.Count != 0)
                throw new DeterministicSimulationException("Combat active queues must be empty before Capture.");

            var logs = new List<CombatContributionEventLog>(
                _eventLogs.Values);
            logs.Sort((a, b) =>
                a.VictimUid.CompareTo(b.VictimUid));
            _snapshot.ContributionEventLogs =
                new CombatContributionEventLogSnapshot[
                    logs.Count];
            for (int index = 0;
                 index < logs.Count;
                 index++)
            {
                _snapshot.ContributionEventLogs[index] =
                    logs[index].Capture();
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
            _eventLogs.Clear();
            if (snapshot.ContributionEventLogs != null)
            {
                UnitUid previousVictim = default;
                for (int i = 0;
                     i < snapshot.ContributionEventLogs.Length;
                     i++)
                {
                    CombatContributionEventLogSnapshot logState =
                        snapshot.ContributionEventLogs[i];
                    if (!logState.VictimUnitUid.IsValid() ||
                        (i > 0 && previousVictim.CompareTo(
                            logState.VictimUnitUid) >= 0))
                        throw new DeterministicSimulationException(
                            "Combat event logs are not in canonical VictimUnitUid order.");
                    previousVictim = logState.VictimUnitUid;
                    var log = new CombatContributionEventLog(
                        logState.VictimUnitUid,
                        GetAssistContributionDurationTicks());
                    log.Restore(logState);
                    _eventLogs.Add(
                        logState.VictimUnitUid,
                        log);
                }
            }
        }

        private int GetAssistContributionDurationTicks()
        {
            return DeterministicTimeConversion
                .Legacy30HzTicksToTicks(
                    CombatContributionEventLog
                        .DefaultAssistContributionDurationTicks,
                    _unitWorld.TickRate);
        }

        public void Resolve(in RollbackContext context)
        {
            FillSortedContributionVictims();
            for (int victimIndex = 0;
                 victimIndex < _eventLogVictimScratch.Count;
                 victimIndex++)
            {
                CombatContributionEventLog log =
                    _eventLogs[_eventLogVictimScratch[victimIndex]];
                if (!_unitWorld.TryGetUnit(
                        log.VictimUid,
                        out _))
                {
                    throw new DeterministicSimulationException(
                        $"Missing Combat victim {log.VictimUid} during Resolve.");
                }
                var events = log.Events;
                for (int i = 0;
                     i < events.Count;
                     i++)
                {
                    UnitUid contributor =
                        events[i].ContributorHeroUid;
                    if (!_unitWorld.TryGetUnit(
                            contributor,
                            out Unit contributorUnit) ||
                        contributorUnit.UnitKind !=
                            UnitKind.Hero)
                    {
                        throw new DeterministicSimulationException(
                            $"Missing Combat contributor {contributor} during Resolve.");
                    }
                }
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
