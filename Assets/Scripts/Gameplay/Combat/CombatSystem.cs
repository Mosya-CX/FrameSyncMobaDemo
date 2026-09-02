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
        private readonly List<EvaluatedDamage> _damageBatchScratch =
            new List<EvaluatedDamage>();
        private readonly List<EvaluatedHeal> _healBatchScratch =
            new List<EvaluatedHeal>();
        private readonly List<HeroDamageContribution> _heroDamageScratch =
            new List<HeroDamageContribution>();
        private readonly List<ShieldResultEmission> _shieldEmissionScratch =
            new List<ShieldResultEmission>();
        private readonly List<HealResultEmission> _healEmissionScratch =
            new List<HealResultEmission>();
        private readonly List<DamageResultEmission> _damageEmissionScratch =
            new List<DamageResultEmission>();
        private readonly List<UnitUid> _dyingEmissionScratch =
            new List<UnitUid>();
        private readonly Dictionary<UnitUid, UnitUid> _lethalBatchKillers =
            new Dictionary<UnitUid, UnitUid>();
        private readonly Dictionary<UnitUid, fp> _waveStartHealth =
            new Dictionary<UnitUid, fp>();
        private ushort _nextDeathSeq;
        private bool _deathSeqExhausted;
        private const int MaxSettlementWavesPerTick = 256;
        private const ulong KillerTieDomain = 0x434F4D4241544B49UL;
        private uint _initialMatchSeed;
        private bool _hasConfiguredInitialMatchSeed;
        private bool _isCombatTickActive;

        public int ShieldProcessed { get; private set; }
        public int DamageProcessed { get; private set; }
        public int HealProcessed { get; private set; }
        public uint InitialMatchSeed => _initialMatchSeed;
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
            int heroRespawnPerMinuteTicks,
            uint initialMatchSeed = 1u)
        {
            if (unitWorld == null)
                throw new System.ArgumentNullException(nameof(unitWorld));
            if (heroRespawnBaseTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnBaseTicks));
            if (heroRespawnPerMinuteTicks < 0)
                throw new System.ArgumentOutOfRangeException(nameof(heroRespawnPerMinuteTicks));
            if (initialMatchSeed == 0u)
                throw new System.ArgumentOutOfRangeException(nameof(initialMatchSeed));
            _unitWorld = unitWorld;
            _initialMatchSeed = initialMatchSeed;
            HeroRespawnBaseTicks = heroRespawnBaseTicks;
            HeroRespawnPerMinuteTicks = heroRespawnPerMinuteTicks;
            _snapshot = CombatSnapshot.Default;
        }

        public void ConfigureInitialMatchSeed(uint initialMatchSeed)
        {
            if (initialMatchSeed == 0u)
                throw new System.ArgumentOutOfRangeException(
                    nameof(initialMatchSeed));
            if (_hasConfiguredInitialMatchSeed)
            {
                if (_initialMatchSeed == initialMatchSeed) return;
                throw new DeterministicSimulationException(
                    "Combat InitialMatchSeed cannot change after bootstrap configuration.");
            }
            if (_isCombatTickActive ||
                _shieldQueue.Count != 0 ||
                _damageQueue.Count != 0 ||
                _healQueue.Count != 0)
            {
                throw new DeterministicSimulationException(
                    "Combat match seed must be configured outside an active Combat Tick with empty request queues.");
            }
            _initialMatchSeed = initialMatchSeed;
            _hasConfiguredInitialMatchSeed = true;
        }
        public void SubmitShield(ShieldRequest request)
        {
            if (!request.IsValid) return;
            ValidateActiveSubmission();
            if (_unitWorld.TryGetUnit(
                    request.TargetUnitUid,
                    out Unit shieldTarget) &&
                !StructureEffectPolicy.AllowsExternalEffect(
                    shieldTarget,
                    request.SourceUnitUid))
            {
                return;
            }
            _shieldQueue.Add(request);
        }

        public bool SubmitDamage(DamageRequest request)
        {
            ValidateDamageEffectOrdinal(request, "submitted");
            if (!request.IsValid) return false;
            ValidateActiveSubmission();
            if (_unitWorld.TryGetUnit(
                    request.Header.TargetUnitUid,
                    out Unit damageTarget) &&
                !StructureEffectPolicy.AllowsDamage(
                    damageTarget,
                    request.Header))
            {
                // A valid request rejected by the structure policy is a
                // consumed no-op. Several formal producers treat false as an
                // invalid request and must not turn content misconfiguration
                // into a deterministic match failure.
                return true;
            }
            _damageQueue.Add(request);
            return true;
        }

        public void SubmitHeal(HealRequest request)
        {
            if (!request.IsValid) return;
            ValidateActiveSubmission();
            if (_unitWorld.TryGetUnit(
                    request.TargetUnitUid,
                    out Unit healTarget) &&
                !StructureEffectPolicy.AllowsExternalEffect(
                    healTarget,
                    request.SourceUnitUid))
            {
                return;
            }
            _healQueue.Add(request);
        }

        public void BeginTick()
        {
            _isCombatTickActive = true;
            _shieldQueue.Clear(); _damageQueue.Clear(); _healQueue.Clear();
            _pendingDying.Clear(); _deathResults.Clear();
            _damageBatchScratch.Clear();
            _healBatchScratch.Clear();
            _heroDamageScratch.Clear();
            _shieldEmissionScratch.Clear();
            _healEmissionScratch.Clear();
            _damageEmissionScratch.Clear();
            _dyingEmissionScratch.Clear();
            _lethalBatchKillers.Clear();
            _waveStartHealth.Clear();
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
            int wave = 0;
            while (_shieldQueue.Count != 0 ||
                   _healQueue.Count != 0 ||
                   _damageQueue.Count != 0)
            {
                if (wave >= MaxSettlementWavesPerTick)
                {
                    throw new DeterministicSimulationException(
                        "Combat settlement wave limit exceeded.");
                }
                wave++;

                int shieldCount = _shieldQueue.Count;
                int healCount = _healQueue.Count;
                int damageCount = _damageQueue.Count;
                CaptureWaveStartHealth(damageCount);
                _shieldQueue.Sort(0, shieldCount, ShieldRequestComparer.Instance);
                _healQueue.Sort(0, healCount, HealRequestComparer.Instance);
                _damageQueue.Sort(0, damageCount, DamageRequestComparer.Instance);

                SealShieldRequests(shieldCount);
                SealHealRequests(healCount);
                SealDamageRequests(damageCount);

                for (int i = 0; i < shieldCount; i++)
                {
                    ProcessShield(_shieldQueue[i]);
                    ShieldProcessed++;
                }
                ProcessHealBatch(healCount);
                HealProcessed += healCount;
                ProcessDamageBatch(damageCount);
                DamageProcessed += damageCount;
                EmitWaveResults();
                _waveStartHealth.Clear();

                _shieldQueue.RemoveRange(0, shieldCount);
                _healQueue.RemoveRange(0, healCount);
                _damageQueue.RemoveRange(0, damageCount);
            }
            ResolveDying();
            _shieldQueue.Clear();
            _damageQueue.Clear();
            _healQueue.Clear();
            _pendingDying.Clear();
            _lethalBatchKillers.Clear();
            _waveStartHealth.Clear();
        }

        public void EndTick()
        {
            ValidateCaptureState();
            FreezeSnapshot();
            _isCombatTickActive = false;
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
            if (_lethalBatchKillers.Count != 0)
                throw new DeterministicSimulationException("Combat lethal-batch killer scratch must be empty before Capture.");
            if (_waveStartHealth.Count != 0)
                throw new DeterministicSimulationException("Combat wave-start health scratch must be empty before Capture.");
            if (_damageBatchScratch.Count != 0 ||
                _healBatchScratch.Count != 0 ||
                _heroDamageScratch.Count != 0 ||
                _shieldEmissionScratch.Count != 0 ||
                _healEmissionScratch.Count != 0 ||
                _damageEmissionScratch.Count != 0 ||
                _dyingEmissionScratch.Count != 0)
            {
                throw new DeterministicSimulationException(
                    "Combat settlement scratch must be empty before Capture.");
            }
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
            if (d.HasValue)
                ValidateDamageEffectOrdinal(d.Value, "deferred");
            if (ShouldConsumeDeferredStructureRequest(k, s, d, h))
                return;
            if (_deferredSeqExhausted) throw new DeterministicSimulationException("Deferred seq exhausted.");
            ushort seq = _nextDeferredSeq; if (_nextDeferredSeq == ushort.MaxValue) _deferredSeqExhausted = true; else _nextDeferredSeq++;
            var req = new DeferredCombatRequest { ExecuteLogicTick = et, SourceLogicTick = st, DeferredSequenceInSourceTick = seq, RequestKind = k };
            if (s.HasValue) req.Shield = s.Value; if (d.HasValue) req.Damage = d.Value; if (h.HasValue) req.Heal = h.Value;
            _deferredBuffer.Add(req);
        }

        private bool ShouldConsumeDeferredStructureRequest(
            CombatRequestKind kind,
            ShieldRequest? shield,
            DamageRequest? damage,
            HealRequest? heal)
        {
            switch (kind)
            {
                case CombatRequestKind.Shield:
                    if (!shield.HasValue || !shield.Value.IsValid)
                        return false;
                    return _unitWorld.TryGetUnit(
                            shield.Value.TargetUnitUid,
                            out Unit shieldTarget) &&
                        !StructureEffectPolicy.AllowsExternalEffect(
                            shieldTarget,
                            shield.Value.SourceUnitUid);
                case CombatRequestKind.Damage:
                    if (!damage.HasValue || !damage.Value.IsValid)
                        return false;
                    return _unitWorld.TryGetUnit(
                            damage.Value.TargetUnitUid,
                            out Unit damageTarget) &&
                        !StructureEffectPolicy.AllowsDamage(
                            damageTarget,
                            damage.Value.Header);
                case CombatRequestKind.Heal:
                    if (!heal.HasValue || !heal.Value.IsValid)
                        return false;
                    return _unitWorld.TryGetUnit(
                            heal.Value.TargetUnitUid,
                            out Unit healTarget) &&
                        !StructureEffectPolicy.AllowsExternalEffect(
                            healTarget,
                            heal.Value.SourceUnitUid);
                default:
                    return false;
            }
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

        private void SealShieldRequests(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ShieldRequest request = _shieldQueue[i];
                request.Header = AllocateActiveHeader(request.Header);
                _shieldQueue[i] = request;
            }
        }

        private void SealHealRequests(int count)
        {
            for (int i = 0; i < count; i++)
            {
                HealRequest request = _healQueue[i];
                request.Header = AllocateActiveHeader(request.Header);
                _healQueue[i] = request;
            }
        }

        private void SealDamageRequests(int count)
        {
            for (int i = 0; i < count; i++)
            {
                DamageRequest request = _damageQueue[i];
                request.Header = AllocateActiveHeader(request.Header);
                _damageQueue[i] = request;
            }
        }

        private void CaptureWaveStartHealth(int damageCount)
        {
            _waveStartHealth.Clear();
            for (int i = 0; i < damageCount; i++)
            {
                UnitUid targetUid = _damageQueue[i].TargetUnitUid;
                if (_waveStartHealth.ContainsKey(targetUid)) continue;
                if (_unitWorld.TryGetUnit(targetUid, out Unit target) &&
                    target.StatHandler != null)
                {
                    _waveStartHealth.Add(
                        targetUid,
                        target.StatHandler.CurrentHealth);
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
            _shieldEmissionScratch.Add(new ShieldResultEmission
            {
                Request = r,
                AppliedAmount = applied,
                EventAmount = amt,
            });
        }

        private void EmitShieldResult(in ShieldResultEmission result)
        {
            ShieldRequest r = result.Request;
            if (!_unitWorld.TryGetUnit(r.TargetUnitUid, out Unit trg)) return;
            RecordEvent(
                r.SourceUnitUid,
                trg,
                CombatContributionKind.Shield,
                result.AppliedAmount,
                r.Header.SequenceInTick);
            CombatEvents.RaiseShieldApplied(new ShieldEventData { SourceUid = r.SourceUnitUid, TargetUid = r.TargetUnitUid, ShieldAmount = result.EventAmount, ShieldType = r.ShieldType });
            CombatEvents.OnCombatParticipationUnit?.Invoke(r.SourceUnitUid, r.TargetUnitUid, CombatParticipationFlags.ShieldGranted | CombatParticipationFlags.ShieldReceived);
        }

        private void ProcessDamageBatch(int count)
        {
            int index = 0;
            while (index < count)
            {
                UnitUid targetUid = _damageQueue[index].TargetUnitUid;
                int end = index + 1;
                while (end < count &&
                       _damageQueue[end].TargetUnitUid == targetUid)
                    end++;

                _damageBatchScratch.Clear();
                if (_unitWorld.TryGetUnit(targetUid, out Unit target) &&
                    (target.LifeState == LifeState.Alive ||
                     target.LifeState == LifeState.Dying) &&
                    target.StatHandler != null)
                {
                    for (int i = index; i < end; i++)
                    {
                        if (TryEvaluateDamage(
                                _damageQueue[i],
                                target,
                                out EvaluatedDamage evaluated))
                            _damageBatchScratch.Add(evaluated);
                    }
                    if (_damageBatchScratch.Count != 0)
                        CommitDamageBatch(target);
                }
                index = end;
            }
            _damageBatchScratch.Clear();
        }

        private bool TryEvaluateDamage(
            DamageRequest req,
            Unit target,
            out EvaluatedDamage evaluated)
        {
            evaluated = default;
            StatHandler stats = target.StatHandler;
            fp targetBatchStartHealth =
                _waveStartHealth.TryGetValue(
                    target.UnitUid,
                    out fp capturedHealth)
                    ? capturedHealth
                    : stats.CurrentHealth;
            _unitWorld.TryGetUnit(req.SourceUnitUid, out Unit sourceUnit);
            StatHandler sourceStats = sourceUnit?.StatHandler;
            CombatModifierSet sourceModifiers = sourceUnit?.CombatModifiers;
            CombatModifierSet targetModifiers = target.CombatModifiers;
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
                targetBatchStartHealth,
                ref policies);
            bool isCrit = false;
            if (!policies.ForbidCrit &&
                sourceStats != null)
            {
                bool shouldCrit = policies.ForceCrit;
                if (!shouldCrit)
                {
                    fp critChance = sourceStats.GetStat(
                        StatId.CriticalStrikeChance);
                    shouldCrit = critChance > fp.zero &&
                        CombatFairnessKey.RollCrit(
                            _initialMatchSeed,
                            req.Header.OriginActionId,
                            target.GameplayParticipantId,
                            req.Header.EffectOrdinal,
                            critChance);
                }
                if (shouldCrit)
                {
                    fp critDamage = sourceStats.GetStat(
                        StatId.CriticalStrikeDamage);
                    if (critDamage <= fp.zero) critDamage = (fp)2;
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
                targetBatchStartHealth,
                ref policies);
            fp resistance = fp.zero;
            if (req.DamageType == DamageType.Physical)
                resistance = stats.GetStat(StatId.Armor);
            else if (req.DamageType == DamageType.Magic)
                resistance = stats.GetStat(StatId.MagicResistance);
            resistance = ApplyDamageModifierSlot(
                sourceModifiers,
                targetModifiers,
                req,
                CombatFormulaSlot.DefenseInput,
                req.BaseDamage,
                resistance,
                target.UnitKind,
                sourceStats,
                stats,
                targetBatchStartHealth,
                ref policies);
            fp mitigated = CalculateResistedDamage(raw, resistance);
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
                targetBatchStartHealth,
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
                targetBatchStartHealth,
                ref policies);
            if (mitigated <= fp.zero) return false;

            evaluated = new EvaluatedDamage
            {
                Request = req,
                RawDamage = raw,
                MitigatedDamage = mitigated,
                RemainingDamage = mitigated,
                Policies = policies,
                IsCritical = isCrit,
            };
            return true;
        }

        private void CommitDamageBatch(Unit target)
        {
            StatHandler stats = target.StatHandler;
            AllocateShieldGroup(
                target,
                DamageAllocationGroup.PhysicalSpecific,
                stats.GetSpecificShieldTotal(DamageType.Physical));
            AllocateShieldGroup(
                target,
                DamageAllocationGroup.MagicSpecific,
                stats.GetSpecificShieldTotal(DamageType.Magic));
            AllocateShieldGroup(
                target,
                DamageAllocationGroup.White,
                stats.GetWhiteShieldTotal());

            fp availableLife = stats.CurrentHealth;
            AllocateLifeDamage(target, availableLife);
            fp totalLifeDamage = fp.zero;
            for (int i = 0; i < _damageBatchScratch.Count; i++)
                totalLifeDamage += _damageBatchScratch[i].ActualLifeDamage;
            fp finalHealth = availableLife - totalLifeDamage;
            if (finalHealth < fp.zero) finalHealth = fp.zero;
            stats.SetCurrentHealth(finalHealth);

            if (finalHealth <= fp.zero &&
                target.LifeState == LifeState.Alive)
            {
                _lethalBatchKillers[target.UnitUid] =
                    ResolveLethalBatchKiller(target);
            }
            else if (finalHealth > fp.zero)
            {
                _lethalBatchKillers.Remove(target.UnitUid);
            }

            for (int i = 0; i < _damageBatchScratch.Count; i++)
            {
                _damageEmissionScratch.Add(new DamageResultEmission
                {
                    TargetUnitUid = target.UnitUid,
                    Result = _damageBatchScratch[i],
                });
            }

            if (finalHealth <= fp.zero &&
                target.LifeState == LifeState.Alive &&
                !_dyingEmissionScratch.Contains(target.UnitUid))
                _dyingEmissionScratch.Add(target.UnitUid);
        }

        private void AllocateShieldGroup(
            Unit target,
            DamageAllocationGroup group,
            fp availableShield)
        {
            fp allocated = AllocateDamageAmount(
                group,
                availableShield,
                allocateLifeDamage: false,
                target.UnitUid);
            if (allocated <= fp.zero) return;

            fp consumed = group == DamageAllocationGroup.White
                ? target.StatHandler.ConsumeWhiteShields(allocated)
                : target.StatHandler.ConsumeSpecificShields(
                    group == DamageAllocationGroup.PhysicalSpecific
                        ? DamageType.Physical
                        : DamageType.Magic,
                    allocated);
            if (consumed != allocated)
            {
                throw new DeterministicSimulationException(
                    "Combat batch shield allocation did not conserve shield value.");
            }
        }

        private void AllocateLifeDamage(Unit target, fp availableLife)
        {
            AllocateDamageAmount(
                DamageAllocationGroup.Life,
                availableLife,
                allocateLifeDamage: true,
                target.UnitUid);
        }

        private fp AllocateDamageAmount(
            DamageAllocationGroup group,
            fp available,
            bool allocateLifeDamage,
            UnitUid targetUid)
        {
            if (available <= fp.zero) return fp.zero;
            fp totalWeight = fp.zero;
            for (int i = 0; i < _damageBatchScratch.Count; i++)
            {
                EvaluatedDamage result = _damageBatchScratch[i];
                if (IsEligibleForAllocation(result, group))
                    totalWeight += result.RemainingDamage;
            }
            if (totalWeight <= fp.zero) return fp.zero;

            fp allocatable = available < totalWeight
                ? available
                : totalWeight;
            fp allocated = fp.zero;
            for (int i = 0; i < _damageBatchScratch.Count; i++)
            {
                EvaluatedDamage result = _damageBatchScratch[i];
                if (!IsEligibleForAllocation(result, group)) continue;
                fp share = allocatable * result.RemainingDamage /
                           totalWeight;
                ApplyDamageShare(
                    ref result,
                    share,
                    allocateLifeDamage);
                allocated += share;
                _damageBatchScratch[i] = result;
            }

            fp remainder = allocatable - allocated;
            while (remainder > fp.zero)
            {
                int bestRemainderIndex = -1;
                ulong bestRemainderScore = ulong.MaxValue;
                for (int i = 0; i < _damageBatchScratch.Count; i++)
                {
                    EvaluatedDamage candidate = _damageBatchScratch[i];
                    if (!IsEligibleForAllocation(candidate, group) ||
                        candidate.RemainingDamage <= fp.zero)
                        continue;
                    ulong score = ComputeRequestTieScore(
                        candidate.Request,
                        targetUid,
                        (ulong)group);
                    if (score < bestRemainderScore)
                    {
                        bestRemainderScore = score;
                        bestRemainderIndex = i;
                    }
                }
                if (bestRemainderIndex < 0)
                {
                    throw new DeterministicSimulationException(
                        "Combat batch damage remainder could not be conserved.");
                }
                EvaluatedDamage result =
                    _damageBatchScratch[bestRemainderIndex];
                fp share = result.RemainingDamage < remainder
                    ? result.RemainingDamage
                    : remainder;
                ApplyDamageShare(
                    ref result,
                    share,
                    allocateLifeDamage);
                _damageBatchScratch[bestRemainderIndex] = result;
                allocated += share;
                remainder -= share;
            }
            return allocated;
        }

        private static bool IsEligibleForAllocation(
            in EvaluatedDamage result,
            DamageAllocationGroup group)
        {
            if (result.RemainingDamage <= fp.zero) return false;
            switch (group)
            {
                case DamageAllocationGroup.PhysicalSpecific:
                    return result.Request.DamageType == DamageType.Physical &&
                           !result.Policies.IgnoreAllShield &&
                           !result.Policies.IgnorePhysicalShield;
                case DamageAllocationGroup.MagicSpecific:
                    return result.Request.DamageType == DamageType.Magic &&
                           !result.Policies.IgnoreAllShield &&
                           !result.Policies.IgnoreMagicShield;
                case DamageAllocationGroup.White:
                    return !result.Policies.IgnoreAllShield;
                case DamageAllocationGroup.Life:
                    return true;
                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported Combat allocation group {group}.");
            }
        }

        private static void ApplyDamageShare(
            ref EvaluatedDamage result,
            fp share,
            bool allocateLifeDamage)
        {
            if (share <= fp.zero) return;
            if (allocateLifeDamage)
                result.ActualLifeDamage += share;
            else
                result.ShieldAbsorbed += share;
            result.RemainingDamage -= share;
            if (result.RemainingDamage < fp.zero)
                result.RemainingDamage = fp.zero;
        }

        private UnitUid ResolveLethalBatchKiller(Unit target)
        {
            _heroDamageScratch.Clear();
            for (int i = 0; i < _damageBatchScratch.Count; i++)
            {
                EvaluatedDamage result = _damageBatchScratch[i];
                if (result.ActualLifeDamage <= fp.zero) continue;
                UnitUid heroUid = ResolveContributorHero(
                    result.Request.SourceUnitUid,
                    target);
                if (!heroUid.IsValid()) continue;
                int existingIndex = -1;
                for (int h = 0; h < _heroDamageScratch.Count; h++)
                {
                    if (_heroDamageScratch[h].HeroUid == heroUid)
                    {
                        existingIndex = h;
                        break;
                    }
                }
                if (existingIndex < 0)
                {
                    _heroDamageScratch.Add(
                        new HeroDamageContribution
                        {
                            HeroUid = heroUid,
                            ActualLifeDamage = result.ActualLifeDamage,
                        });
                }
                else
                {
                    HeroDamageContribution contribution =
                        _heroDamageScratch[existingIndex];
                    contribution.ActualLifeDamage +=
                        result.ActualLifeDamage;
                    _heroDamageScratch[existingIndex] = contribution;
                }
            }

            UnitUid winner = default;
            fp bestDamage = fp.zero;
            ulong bestScore = ulong.MaxValue;
            int tick = SimulationTickContext.Current.Tick;
            for (int i = 0; i < _heroDamageScratch.Count; i++)
            {
                HeroDamageContribution candidate = _heroDamageScratch[i];
                ulong score = ComputeKillerTieScore(
                    tick,
                    target.UnitUid,
                    candidate.HeroUid);
                if (candidate.ActualLifeDamage > bestDamage ||
                    (candidate.ActualLifeDamage == bestDamage &&
                     (score < bestScore ||
                      (score == bestScore &&
                       (!winner.IsValid() ||
                        candidate.HeroUid.CompareTo(winner) < 0)))))
                {
                    bestDamage = candidate.ActualLifeDamage;
                    bestScore = score;
                    winner = candidate.HeroUid;
                }
            }
            _heroDamageScratch.Clear();
            return winner;
        }

        private void EmitDamageResult(
            Unit target,
            in EvaluatedDamage result)
        {
            DamageRequest req = result.Request;
            fp actualDamage =
                result.ShieldAbsorbed + result.ActualLifeDamage;
            if (MatchEventTracker != null)
            {
                MatchEventTracker.RecordDamage(
                    req.TargetUnitUid,
                    req.SourceUnitUid,
                    (int)actualDamage,
                    0,
                    SimulationTickContext.Current.Tick,
                    true);
            }
            SubmitHitVfx(
                req.TargetUnitUid,
                req.SourceUnitUid,
                req.Header.SequenceInTick);
            RecordEvent(
                req.SourceUnitUid,
                target,
                CombatContributionKind.Damage,
                actualDamage,
                req.Header.SequenceInTick);
            var evt = new DamageEventData
            {
                SourceUid = req.SourceUnitUid,
                TargetUid = req.TargetUnitUid,
                Source = req.Header.SourceDescriptor,
                OriginActionId = req.Header.OriginActionId,
                EffectOrdinal = req.Header.EffectOrdinal,
                RecipeId = req.Header.RecipeId,
                RawDamage = result.RawDamage,
                MitigatedDamage = result.MitigatedDamage,
                ActualDamage = actualDamage,
                DamageType = req.DamageType,
                IsCritical = result.IsCritical,
            };
            CombatEvents.RaiseDamageTaken(evt);
            CombatEvents.RaiseDamageDealt(evt);
            ApplyStatDrainHeal(req, result.ActualLifeDamage);
            if (req.Header.SourceDescriptor.SourceType ==
                CombatSourceType.Attack)
            {
                CombatEvents.RaiseOnHit(new OnHitEventData
                {
                    SourceUid = req.SourceUnitUid,
                    TargetUid = req.TargetUnitUid,
                    OriginActionId = req.Header.OriginActionId,
                    EffectOrdinal = req.Header.EffectOrdinal,
                    DamageType = req.DamageType,
                    IsCritical = result.IsCritical,
                });
            }
            CombatEvents.OnCombatParticipationUnit?.Invoke(
                req.SourceUnitUid,
                req.TargetUnitUid,
                CombatParticipationFlags.DamageDealt |
                CombatParticipationFlags.DamageTaken);
            ApplyHitReaction(target, result.MitigatedDamage);
        }

        private ulong ComputeRequestTieScore(
            in DamageRequest request,
            UnitUid targetUid,
            ulong domain)
        {
            return DeterministicHash64.Compute(
                _initialMatchSeed,
                unchecked((uint)SimulationTickContext.Current.Tick),
                PackTraversalNeutralUid(targetUid),
                PackTraversalNeutralUid(request.SourceUnitUid),
                KillerTieDomain ^ domain ^
                unchecked((uint)request.Header.RecipeId));
        }

        private ulong ComputeKillerTieScore(
            int deathLogicTick,
            UnitUid victimUid,
            UnitUid candidateHeroUid)
        {
            return DeterministicHash64.Compute(
                _initialMatchSeed,
                unchecked((uint)deathLogicTick),
                PackTraversalNeutralUid(victimUid),
                PackTraversalNeutralUid(candidateHeroUid),
                KillerTieDomain);
        }

        private static ulong PackTraversalNeutralUid(UnitUid uid)
        {
            unchecked
            {
                // RuntimeEntityPrefabId is intentionally excluded. It is an
                // authored technical/content identity and can correlate with
                // side-specific prefab numbering. Spawn Tick + globally
                // allocated sequence identify the runtime entity for neutral
                // scoring; a full-score collision still falls back to UnitUid.
                ulong value = (uint)uid.SpawnLogicTick;
                return (value * 0xC2B2AE3D27D4EB4FUL) ^
                       uid.SpawnSequenceInTick;
            }
        }
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
                (source.LifeState != LifeState.Alive &&
                 source.LifeState != LifeState.Dying))
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

        private void ProcessHealBatch(int count)
        {
            int index = 0;
            while (index < count)
            {
                UnitUid targetUid = _healQueue[index].TargetUnitUid;
                int end = index + 1;
                while (end < count &&
                       _healQueue[end].TargetUnitUid == targetUid)
                    end++;

                _healBatchScratch.Clear();
                if (_unitWorld.TryGetUnit(targetUid, out Unit target) &&
                    (target.LifeState == LifeState.Alive ||
                     target.LifeState == LifeState.Dying) &&
                    target.StatHandler != null)
                {
                    for (int i = index; i < end; i++)
                    {
                        HealRequest request = _healQueue[i];
                        fp amount = request.BaseValue;
                        if (_unitWorld.TryGetUnit(
                                request.SourceUnitUid,
                                out Unit source) &&
                            source.StatHandler != null)
                        {
                            amount *= fp.one +
                                source.StatHandler.GetStat(
                                    StatId.HealPower);
                        }
                        amount *= target.StatHandler.GetStat(
                            StatId.HealingReceivedRatio);
                        if (amount > fp.zero)
                        {
                            _healBatchScratch.Add(new EvaluatedHeal
                            {
                                Request = request,
                                EvaluatedAmount = amount,
                            });
                        }
                    }
                    CommitHealBatch(target);
                }
                index = end;
            }
            _healBatchScratch.Clear();
        }

        private void CommitHealBatch(Unit target)
        {
            StatHandler stats = target.StatHandler;
            fp current = stats.CurrentHealth;
            fp maximum = stats.GetStat(StatId.MaxHealth);
            fp capacity = maximum - current;
            if (capacity < fp.zero) capacity = fp.zero;
            fp totalRequested = fp.zero;
            for (int i = 0; i < _healBatchScratch.Count; i++)
                totalRequested += _healBatchScratch[i].EvaluatedAmount;
            fp totalEffective = capacity < totalRequested
                ? capacity
                : totalRequested;
            fp allocated = fp.zero;
            for (int i = 0; i < _healBatchScratch.Count; i++)
            {
                EvaluatedHeal result = _healBatchScratch[i];
                fp share = totalRequested > fp.zero
                    ? totalEffective * result.EvaluatedAmount /
                      totalRequested
                    : fp.zero;
                result.EffectiveAmount = share;
                allocated += share;
                _healBatchScratch[i] = result;
            }
            fp remainder = totalEffective - allocated;
            while (remainder > fp.zero)
            {
                int remainderIndex = -1;
                ulong remainderScore = ulong.MaxValue;
                for (int i = 0; i < _healBatchScratch.Count; i++)
                {
                    EvaluatedHeal candidate = _healBatchScratch[i];
                    fp candidateCapacity = candidate.EvaluatedAmount -
                                           candidate.EffectiveAmount;
                    if (candidateCapacity <= fp.zero) continue;
                    ulong score = DeterministicHash64.Compute(
                        _initialMatchSeed,
                        unchecked((uint)SimulationTickContext.Current.Tick),
                        PackTraversalNeutralUid(target.UnitUid),
                        PackTraversalNeutralUid(candidate.Request.SourceUnitUid),
                        KillerTieDomain ^ 0x4845414CUL);
                    if (score < remainderScore)
                    {
                        remainderScore = score;
                        remainderIndex = i;
                    }
                }
                if (remainderIndex < 0)
                {
                    throw new DeterministicSimulationException(
                        "Combat batch heal remainder could not be conserved.");
                }
                EvaluatedHeal result = _healBatchScratch[remainderIndex];
                fp remainingCapacity = result.EvaluatedAmount -
                                       result.EffectiveAmount;
                fp share = remainingCapacity < remainder
                    ? remainingCapacity
                    : remainder;
                result.EffectiveAmount += share;
                _healBatchScratch[remainderIndex] = result;
                remainder -= share;
            }

            fp newHealth = current + totalEffective;
            if (newHealth > maximum) newHealth = maximum;
            stats.SetCurrentHealth(newHealth);
            if (newHealth > fp.zero &&
                target.LifeState == LifeState.Dying)
            {
                _unitWorld.RequestRecoverFromDying(target);
                _pendingDying.Remove(target.UnitUid);
                _lethalBatchKillers.Remove(target.UnitUid);
            }

            for (int i = 0; i < _healBatchScratch.Count; i++)
            {
                EvaluatedHeal result = _healBatchScratch[i];
                _healEmissionScratch.Add(new HealResultEmission
                {
                    TargetUnitUid = target.UnitUid,
                    Result = result,
                });
            }
        }

        private void EmitWaveResults()
        {
            for (int i = 0; i < _shieldEmissionScratch.Count; i++)
                EmitShieldResult(_shieldEmissionScratch[i]);
            for (int i = 0; i < _healEmissionScratch.Count; i++)
                EmitHealResult(_healEmissionScratch[i]);
            for (int i = 0; i < _damageEmissionScratch.Count; i++)
            {
                DamageResultEmission emission = _damageEmissionScratch[i];
                if (_unitWorld.TryGetUnit(
                        emission.TargetUnitUid,
                        out Unit target))
                    EmitDamageResult(target, emission.Result);
            }
            for (int i = 0; i < _dyingEmissionScratch.Count; i++)
            {
                UnitUid targetUid = _dyingEmissionScratch[i];
                if (!_unitWorld.TryGetUnit(targetUid, out Unit target) ||
                    target.LifeState != LifeState.Alive ||
                    target.StatHandler == null ||
                    target.StatHandler.CurrentHealth > fp.zero)
                    continue;
                _unitWorld.RequestEnterDying(target);
                if (!_pendingDying.Contains(targetUid))
                    _pendingDying.Add(targetUid);
                target.EventBus?.PublishUnitDying(target);
            }
            _shieldEmissionScratch.Clear();
            _healEmissionScratch.Clear();
            _damageEmissionScratch.Clear();
            _dyingEmissionScratch.Clear();
        }

        private void EmitHealResult(in HealResultEmission emission)
        {
            if (!_unitWorld.TryGetUnit(
                    emission.TargetUnitUid,
                    out Unit target))
                return;
            EvaluatedHeal result = emission.Result;
            HealRequest request = result.Request;
            var eventData = new HealEventData
            {
                SourceUid = request.SourceUnitUid,
                TargetUid = request.TargetUnitUid,
                RawHeal = request.BaseValue,
                EffectiveHeal = result.EffectiveAmount,
            };
            CombatEvents.RaiseHealTaken(eventData);
            if (result.EffectiveAmount > fp.zero)
            {
                RecordEvent(
                    request.SourceUnitUid,
                    target,
                    CombatContributionKind.Heal,
                    result.EffectiveAmount,
                    request.Header.SequenceInTick);
            }
            CombatEvents.OnCombatParticipationUnit?.Invoke(
                request.SourceUnitUid,
                request.TargetUnitUid,
                CombatParticipationFlags.HealDealt |
                CombatParticipationFlags.HealTaken);
            CombatEvents.RaiseHealDealt(eventData);
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
                UnitUid kid = _lethalBatchKillers.TryGetValue(
                    duid,
                    out UnitUid lethalBatchKiller)
                    ? lethalBatchKiller
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
            fp targetBatchStartHealth,
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
                targetBatchStartHealth,
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
                targetBatchStartHealth,
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
            _shieldQueue.Clear();
            _damageQueue.Clear();
            _healQueue.Clear();
            _pendingDying.Clear();
            _deathResults.Clear();
            _damageBatchScratch.Clear();
            _healBatchScratch.Clear();
            _heroDamageScratch.Clear();
            _shieldEmissionScratch.Clear();
            _healEmissionScratch.Clear();
            _damageEmissionScratch.Clear();
            _dyingEmissionScratch.Clear();
            _lethalBatchKillers.Clear();
            _waveStartHealth.Clear();
            ShieldProcessed = 0;
            DamageProcessed = 0;
            HealProcessed = 0;
            _nextDeathSeq = 0;
            _deathSeqExhausted = false;
            _nextDeferredSeq = 0;
            _deferredSeqExhausted = false;
            _nextSequenceInTick = 0;
            _sequenceExhausted = false;
            _currentSequenceLogicTick = -1;
            _isCombatTickActive = false;
            _deferredBuffer.Clear();
            if (snapshot.DeferredRequests != null)
            {
                for (int i = 0; i < snapshot.DeferredRequests.Length; i++)
                {
                    DeferredCombatRequest deferred =
                        snapshot.DeferredRequests[i];
                    if (deferred.RequestKind == CombatRequestKind.Damage)
                        ValidateDamageEffectOrdinal(
                            deferred.Damage,
                            "restored deferred");
                    _deferredBuffer.Add(deferred);
                }
            }
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

        private static void ValidateDamageEffectOrdinal(
            in DamageRequest request,
            string boundary)
        {
            if (request.Header.EffectOrdinal < 0)
                throw new DeterministicSimulationException(
                    $"Combat {boundary} DamageRequest has negative EffectOrdinal " +
                    $"{request.Header.EffectOrdinal}.");
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

        private void ValidateActiveSubmission()
        {
            int tick = SimulationTickContext.Current.Tick;
            if (_currentSequenceLogicTick != tick)
            {
                throw new DeterministicSimulationException(
                    "Combat request submitted outside the active Combat Tick.");
            }
        }

        private static int CompareUnitUid(UnitUid left, UnitUid right) =>
            left.CompareTo(right);

        private sealed class ShieldRequestComparer :
            IComparer<ShieldRequest>
        {
            public static readonly ShieldRequestComparer Instance =
                new ShieldRequestComparer();

            public int Compare(ShieldRequest left, ShieldRequest right)
            {
                int comparison = CompareUnitUid(
                    left.TargetUnitUid,
                    right.TargetUnitUid);
                if (comparison != 0) return comparison;
                comparison = left.ShieldType.CompareTo(right.ShieldType);
                if (comparison != 0) return comparison;
                comparison = left.DurationTicks.CompareTo(right.DurationTicks);
                if (comparison != 0) return comparison;
                comparison = left.BaseValue.RawValue.CompareTo(
                    right.BaseValue.RawValue);
                if (comparison != 0) return comparison;
                return CompareUnitUid(
                    left.SourceUnitUid,
                    right.SourceUnitUid);
            }
        }

        private sealed class HealRequestComparer : IComparer<HealRequest>
        {
            public static readonly HealRequestComparer Instance =
                new HealRequestComparer();

            public int Compare(HealRequest left, HealRequest right)
            {
                int comparison = CompareUnitUid(
                    left.TargetUnitUid,
                    right.TargetUnitUid);
                if (comparison != 0) return comparison;
                comparison = left.BaseValue.RawValue.CompareTo(
                    right.BaseValue.RawValue);
                if (comparison != 0) return comparison;
                return CompareUnitUid(
                    left.SourceUnitUid,
                    right.SourceUnitUid);
            }
        }

        private sealed class DamageRequestComparer :
            IComparer<DamageRequest>
        {
            public static readonly DamageRequestComparer Instance =
                new DamageRequestComparer();

            public int Compare(DamageRequest left, DamageRequest right)
            {
                int comparison = CompareUnitUid(
                    left.TargetUnitUid,
                    right.TargetUnitUid);
                if (comparison != 0) return comparison;
                comparison = left.Header.SourceDescriptor.SourceType.CompareTo(
                    right.Header.SourceDescriptor.SourceType);
                if (comparison != 0) return comparison;
                comparison = left.Header.SourceDescriptor.SourceId.CompareTo(
                    right.Header.SourceDescriptor.SourceId);
                if (comparison != 0) return comparison;
                comparison = left.Header.RecipeId.CompareTo(
                    right.Header.RecipeId);
                if (comparison != 0) return comparison;
                comparison = left.DamageType.CompareTo(right.DamageType);
                if (comparison != 0) return comparison;
                comparison = left.BaseDamage.RawValue.CompareTo(
                    right.BaseDamage.RawValue);
                if (comparison != 0) return comparison;
                comparison = left.Header.OriginActionId.CompareTo(
                    right.Header.OriginActionId);
                if (comparison != 0) return comparison;
                comparison = left.Header.EffectOrdinal.CompareTo(
                    right.Header.EffectOrdinal);
                if (comparison != 0) return comparison;
                return CompareUnitUid(
                    left.SourceUnitUid,
                    right.SourceUnitUid);
            }
        }

        private enum DamageAllocationGroup : byte
        {
            PhysicalSpecific = 0,
            MagicSpecific = 1,
            White = 2,
            Life = 3,
        }

        private struct EvaluatedDamage
        {
            public DamageRequest Request;
            public CombatPolicyResolution Policies;
            public fp RawDamage;
            public fp MitigatedDamage;
            public fp RemainingDamage;
            public fp ShieldAbsorbed;
            public fp ActualLifeDamage;
            public bool IsCritical;
        }

        private struct HeroDamageContribution
        {
            public UnitUid HeroUid;
            public fp ActualLifeDamage;
        }

        private struct EvaluatedHeal
        {
            public HealRequest Request;
            public fp EvaluatedAmount;
            public fp EffectiveAmount;
        }

        private struct ShieldResultEmission
        {
            public ShieldRequest Request;
            public fp AppliedAmount;
            public fp EventAmount;
        }

        private struct HealResultEmission
        {
            public UnitUid TargetUnitUid;
            public EvaluatedHeal Result;
        }

        private struct DamageResultEmission
        {
            public UnitUid TargetUnitUid;
            public EvaluatedDamage Result;
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
