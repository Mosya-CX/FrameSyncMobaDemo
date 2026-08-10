using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unified numerical state entry point for one Unit (Unit v27.3 section 5.1).
    /// Owns stat definitions, level-growth base values, runtime modifiers,
    /// final value calculation, and tick-to-tick change queries.
    /// Implements IRollback&lt;StatHandlerSnapshot&gt; (section 5.9).
    /// </summary>
    public sealed class StatHandler : UnitHandler, IRollback<StatHandlerSnapshot>
    {
        private StatDefinitionTable definitionTable;
        private UnitUid ownerUid;
        private fp statGrowthC;
        private fp statGrowthD;
        private LevelExperienceConfig levelExperienceConfig;
        private fp currentHealth;
        private fp currentCastResource;
        private int currentExperience;

        private readonly Dictionary<StatId, StatRuntimeEntry> entries = new Dictionary<StatId, StatRuntimeEntry>();
        private readonly Dictionary<StatId, StatConfig> configs = new Dictionary<StatId, StatConfig>();
        private readonly List<ShieldInstance> shieldInstances = new List<ShieldInstance>(4);

        private uint nextStatSeq = 1;
        private int nextShieldInstanceId = 1;
        private int level;

        private struct StatConfig
        {
            public fp BaseValue;
            public fp GrowthValue;
            public StatDefinition Definition;
        }

        internal void InitializeRuntime(
            StatDefinitionTable definitionTable,
            StatPreset preset,
            UnitUid ownerUid,
            int level,
            fp statGrowthC,
            fp statGrowthD,
            LevelExperienceConfig levelExperienceConfig = null)
        {
            this.definitionTable = definitionTable ?? throw new ArgumentNullException(nameof(definitionTable));
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            this.ownerUid = ownerUid;
            this.levelExperienceConfig = levelExperienceConfig ?? LevelExperienceConfig.Disabled;
            this.level = levelExperienceConfig?.InitialLevel ?? level;
            this.statGrowthC = statGrowthC;
            this.statGrowthD = statGrowthD;
            currentExperience = this.levelExperienceConfig.InitialExperience;
            entries.Clear();
            configs.Clear();
            shieldInstances.Clear();
            nextStatSeq = 1;
            nextShieldInstanceId = 1;

            for (int i = 0; i < preset.Stats.Count; i++)
            {
                StatPresetEntry pe = preset.Stats[i];

                if (!definitionTable.TryGet(pe.StatId, out StatDefinition def))
                {
                    throw new ArgumentException(
                        $"StatPreset references StatId {pe.StatId} which is not in the StatDefinitionTable.",
                        nameof(preset));
                }

                if (!def.SupportsLevelGrowth && pe.GrowthValue != default)
                {
                    throw new ArgumentException(
                        $"StatId {pe.StatId} does not support level growth but has a non-zero GrowthValue.",
                        nameof(preset));
                }

                var config = new StatConfig
                {
                    BaseValue = pe.BaseValue,
                    GrowthValue = pe.GrowthValue,
                    Definition = def,
                };
                configs[pe.StatId] = config;

                var entry = new StatRuntimeEntry { Dirty = true };
                entries[pe.StatId] = entry;
            }

            currentHealth = GetStat(StatId.MaxHealth);
            currentCastResource = HasStat(StatId.MaxCastResource)
                ? GetStat(StatId.MaxCastResource)
                : fp.zero;
        }

        public UnitUid OwnerUid => ownerUid;
        public fp CurrentHealth => currentHealth;
        public fp CurrentCastResource => currentCastResource;
        public int CurrentExperience => currentExperience;
        public int MaxLevel => levelExperienceConfig?.MaxLevel ?? 1;
        public bool CanLevelUp => levelExperienceConfig != null &&
            levelExperienceConfig.CanLevelUp && level < levelExperienceConfig.MaxLevel;
        public int ExperienceRequiredForNextLevel => CanLevelUp
            ? levelExperienceConfig.GetXpForNextLevel(level)
            : int.MaxValue;
        public IReadOnlyList<ShieldInstance> ShieldInstances => shieldInstances;

        public fp CurrentShield
        {
            get
            {
                fp total = fp.zero;
                for (int i = 0; i < shieldInstances.Count; i++)
                    total += shieldInstances[i].CurrentValue;
                return total;
            }
        }

        public fp AddShield(
            ShieldType shieldType,
            fp value,
            int durationTicks,
            UnitUid sourceUnitUid)
        {
            if (value <= fp.zero || durationTicks < 0 || !sourceUnitUid.IsValid())
                return fp.zero;
            if (nextShieldInstanceId == int.MaxValue)
                throw new DeterministicSimulationException("Shield instance ID exhausted.");

            int currentTick = SimulationTickContext.Current.Tick;
            var instance = new ShieldInstance
            {
                ShieldInstanceId = nextShieldInstanceId++,
                ShieldType = shieldType,
                CurrentValue = value,
                MaxValue = value,
                StartLogicTick = currentTick,
                ExpireLogicTick = durationTicks == 0
                    ? int.MaxValue
                    : checked(currentTick + durationTicks),
                SourceUnitUid = sourceUnitUid,
            };

            if (shieldType == ShieldType.Black && Owner?.CrowdControl != null)
            {
                int immunityDuration = durationTicks == 0 ? int.MaxValue : durationTicks;
                instance.CrowdControlImmunityHandle = Owner.CrowdControl.AddImmunity(
                    new CrowdControlImmunitySpec(
                        new CrowdControlTagQuery(
                            new CrowdControlTagMask(
                                CrowdControlDefinition.ControlTagBits.Control),
                            default,
                            default),
                        immunityDuration,
                        blockCount: 0,
                        priority: 0));
            }

            shieldInstances.Add(instance);
            return value;
        }

        public fp AbsorbShields(ref fp remainingDamage, DamageType damageType)
        {
            return AbsorbShields(
                ref remainingDamage,
                damageType,
                false,
                false);
        }

        public fp AbsorbShields(
            ref fp remainingDamage,
            DamageType damageType,
            bool ignorePhysicalShield,
            bool ignoreMagicShield)
        {
            fp absorbed = fp.zero;
            for (int i = 0; i < shieldInstances.Count && remainingDamage > fp.zero;)
            {
                ShieldInstance instance = shieldInstances[i];
                if ((ignorePhysicalShield &&
                     instance.ShieldType ==
                     ShieldType.Physical) ||
                    (ignoreMagicShield &&
                     (instance.ShieldType ==
                          ShieldType.Magic ||
                      instance.ShieldType ==
                          ShieldType.Black)))
                {
                    i++;
                    continue;
                }
                if (!MatchesDamage(instance.ShieldType, damageType))
                {
                    i++;
                    continue;
                }

                fp amount = instance.CurrentValue < remainingDamage
                    ? instance.CurrentValue
                    : remainingDamage;
                instance.CurrentValue -= amount;
                remainingDamage -= amount;
                absorbed += amount;

                if (instance.CurrentValue <= fp.zero)
                {
                    RemoveShieldAt(i, instance);
                    continue;
                }

                shieldInstances[i] = instance;
                i++;
            }
            return absorbed;
        }

        public void ExpireShields(int currentLogicTick)
        {
            for (int i = shieldInstances.Count - 1; i >= 0; i--)
            {
                ShieldInstance instance = shieldInstances[i];
                if (instance.ExpireLogicTick != int.MaxValue &&
                    currentLogicTick >= instance.ExpireLogicTick)
                    RemoveShieldAt(i, instance);
            }
        }

        public void ClearShields()
        {
            for (int i = shieldInstances.Count - 1; i >= 0; i--)
                RemoveShieldAt(i, shieldInstances[i]);
        }

        public int Level
        {
            get => level;
            set
            {
                if (level != value)
                {
                    level = value;
                    foreach (var kvp in entries)
                    {
                        kvp.Value.Dirty = true;
                    }
                }
            }
        }

        public void SetCurrentHealth(fp value)
        {
            fp maximum = GetStat(StatId.MaxHealth);
            if (value < fp.zero) value = fp.zero;
            if (value > maximum) value = maximum;
            currentHealth = value;
        }

        public void SetCurrentCastResource(fp value)
        {
            fp maximum = HasStat(StatId.MaxCastResource)
                ? GetStat(StatId.MaxCastResource)
                : fp.zero;
            if (value < fp.zero) value = fp.zero;
            if (value > maximum) value = maximum;
            currentCastResource = value;
        }

        public ExperienceGainResult AddExperience(int amount)
        {
            if (amount <= 0 || !CanLevelUp)
                return ExperienceGainResult.None;

            int previousLevel = level;
            int previousExperience = currentExperience;
            currentExperience = checked(currentExperience + amount);
            int levelsGained = 0;

            while (CanLevelUp)
            {
                int required = levelExperienceConfig.GetXpForNextLevel(level);
                if (required == int.MaxValue || currentExperience < required)
                    break;

                fp previousMaxHealth = GetStat(StatId.MaxHealth);
                fp previousMaxResource = HasStat(StatId.MaxCastResource)
                    ? GetStat(StatId.MaxCastResource)
                    : fp.zero;
                currentExperience -= required;
                Level = level + 1;
                fp newMaxHealth = GetStat(StatId.MaxHealth);
                fp newMaxResource = HasStat(StatId.MaxCastResource)
                    ? GetStat(StatId.MaxCastResource)
                    : fp.zero;
                currentHealth = ApplyLevelUpCurrentValueRule(
                    currentHealth, previousMaxHealth, newMaxHealth,
                    levelExperienceConfig.HealthOnLevelUp);
                currentCastResource = ApplyLevelUpCurrentValueRule(
                    currentCastResource, previousMaxResource, newMaxResource,
                    levelExperienceConfig.CastResourceOnLevelUp);
                  levelsGained++;
              }

              // Grant skill points for each level gained
              if (levelsGained > 0 && Owner?.AbilityHandler != null)
              {
                  for (int i = 0; i < levelsGained; i++)
                      Owner.AbilityHandler?.GrantSkillPoint();
              }

              if (levelsGained > 0 &&
                  Owner?.BuffHandler != null)
              {
                  Owner.BuffHandler.OnLevelUp(
                      previousLevel,
                      level);
              }

            return new ExperienceGainResult
            {
                LeveledUp = levelsGained != 0,
                PreviousLevel = previousLevel,
                NewLevel = level,
                LevelsGained = levelsGained,
                SkillPointsGained = levelsGained,
                PreviousExperience = previousExperience,
                CurrentExperience = currentExperience,
            };
        }

        public StatModifierHandle AddModifier(
            StatId statId,
            StatModifierOperation operation,
            fp value)
        {
            if (!configs.ContainsKey(statId))
            {
                if (!definitionTable.TryGet(statId, out StatDefinition def))
                {
                    throw new ArgumentException(
                        $"StatId {statId} is not a valid stat.", nameof(statId));
                }

                var config = new StatConfig
                {
                    BaseValue = def.DefaultBaseValue,
                    GrowthValue = default,
                    Definition = def,
                };
                configs[statId] = config;
                entries[statId] = new StatRuntimeEntry { Dirty = true };
            }

            StatRuntimeEntry entry = entries[statId];
            uint seq = nextStatSeq;

            if (nextStatSeq == uint.MaxValue)
            {
                throw new DeterministicSimulationException(
                    "StatSeq overflow: maximum sequence reached.");
            }
            nextStatSeq++;

            entry.Modifiers.Add(new StatModifier
            {
                StatSeq = seq,
                Operation = operation,
                Value = value,
            });
            entry.Dirty = true;

            return new StatModifierHandle(ownerUid, statId, seq);
        }

        public bool SetModifierValue(StatModifierHandle handle, fp newValue)
        {
            if (handle.OwnerUnitUid != ownerUid)
            {
                return false;
            }

            if (!entries.TryGetValue(handle.StatId, out StatRuntimeEntry entry))
            {
                return false;
            }

            for (int i = 0; i < entry.Modifiers.Count; i++)
            {
                if (entry.Modifiers[i].StatSeq == handle.StatSeq)
                {
                    StatModifier mod = entry.Modifiers[i];
                    mod.Value = newValue;
                    entry.Modifiers[i] = mod;
                    entry.Dirty = true;
                    return true;
                }
            }

            return false;
        }

        public bool RemoveModifier(StatModifierHandle handle)
        {
            if (handle.OwnerUnitUid != ownerUid)
            {
                return false;
            }

            if (!entries.TryGetValue(handle.StatId, out StatRuntimeEntry entry))
            {
                return false;
            }

            for (int i = 0; i < entry.Modifiers.Count; i++)
            {
                if (entry.Modifiers[i].StatSeq == handle.StatSeq)
                {
                    entry.Modifiers.RemoveAt(i);
                    entry.Dirty = true;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetModifier(StatModifierHandle handle, out StatModifierView view)
        {
            view = default;

            if (handle.OwnerUnitUid != ownerUid)
            {
                return false;
            }

            if (!entries.TryGetValue(handle.StatId, out StatRuntimeEntry entry))
            {
                return false;
            }

            for (int i = 0; i < entry.Modifiers.Count; i++)
            {
                StatModifier mod = entry.Modifiers[i];
                if (mod.StatSeq == handle.StatSeq)
                {
                    view = new StatModifierView(
                        handle.StatId, mod.StatSeq, mod.Operation, mod.Value);
                    return true;
                }
            }

            return false;
        }

        public fp GetStat(StatId statId)
        {
            // Uninitialized runtime (Inspector/Odin preview, authoring view):
            // no definition table exists yet, so every stat resolves to zero
            // instead of throwing.
            if (definitionTable == null)
            {
                return fp.zero;
            }
            if (entries.TryGetValue(statId, out StatRuntimeEntry entry))
            {
                if (entry.Dirty)
                {
                    Recompute(statId, entry);
                }
                return entry.FinalValue;
            }

            if (configs.TryGetValue(statId, out StatConfig config))
            {
                return config.Definition.DefaultBaseValue;
            }

            if (definitionTable.TryGet(statId, out StatDefinition def))
            {
                return def.DefaultBaseValue;
            }

            throw new ArgumentException(
                $"StatId {statId} is not a valid stat.", nameof(statId));
        }

        public StatChange GetChangeThisTick(StatId statId)
        {
            if (!entries.TryGetValue(statId, out StatRuntimeEntry entry))
            {
                return default;
            }

            if (entry.Dirty)
            {
                Recompute(statId, entry);
            }

            fp delta = entry.FinalValue - entry.PreviousLogicTickFinalValue;
            return new StatChange(delta != default, delta);
        }

        public void ClearModifiers()
        {
            foreach (var kvp in entries)
            {
                kvp.Value.Modifiers.Clear();
                kvp.Value.Dirty = true;
            }
        }

        public override void ResetForPool()
        {
            ClearShields();
            entries.Clear();
            configs.Clear();
            definitionTable = null;
            ownerUid = default;
            statGrowthC = default;
            statGrowthD = default;
            levelExperienceConfig = null;
            currentHealth = default;
            currentCastResource = default;
            currentExperience = 0;
            nextStatSeq = 1;
            nextShieldInstanceId = 1;
            level = 0;
        }

        public override void ClearForDeath() => ClearShields();

        public override void ClearForRespawn() => ClearShields();

#if UNITY_EDITOR
        // ---- Editor-only live runtime view (Odin; never in builds) ----
        // Odin Inspector repaints live in Play Mode, so the key values below
        // update in real time without any per-frame enumeration. The full
        // StatId table is opt-in (folded away by default).

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-100)]
        [LabelText("Runtime Level")]
        public int EditorLevel => level;

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-99)]
        [LabelText("Runtime Health")]
        public string EditorHealth =>
            FormatEditorStat(currentHealth) +
            " / " +
            FormatEditorStat(
                GetStat(StatId.MaxHealth));

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-98)]
        [LabelText("Runtime Resource")]
        public string EditorResource =>
            FormatEditorStat(
                currentCastResource) +
            " / " +
            FormatEditorStat(
                GetStat(
                    StatId.MaxCastResource));

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-97)]
        [LabelText("Runtime Experience")]
        public int EditorExperience =>
            currentExperience;

        [ShowInInspector]
        [ReadOnly]
        [PropertyOrder(-96)]
        [LabelText("Runtime Shields")]
        public string EditorShields =>
            FormatEditorStat(
                CurrentShield);

        [ShowInInspector]
        [PropertyOrder(-95)]
        [FoldoutGroup("All Stats")]
        [LabelText("Show All Stats")]
        public bool EditorShowAllStats;

        [ShowInInspector]
        [ReadOnly]
        [FoldoutGroup("All Stats")]
        [ShowIf("EditorShowAllStats")]
        [LabelText("Final Values")]
        public List<string> EditorAllStats
        {
            get
            {
                var values = new List<string>();
                Array all =
                    Enum.GetValues(
                        typeof(StatId));
                var ids =
                    new List<StatId>(
                        all.Length);
                for (int i = 0;
                     i < all.Length;
                     i++)
                {
                    ids.Add(
                        (StatId)all.GetValue(
                            i));
                }
                ids.Sort(
                    (left, right) =>
                        left.CompareTo(right));
                for (int i = 0;
                     i < ids.Count;
                     i++)
                {
                    values.Add(
                        ids[i] + ": " +
                        FormatEditorStat(
                            GetStat(ids[i])));
                }
                return values;
            }
        }

        private static string FormatEditorStat(
            fp value)
        {
            return value.ToString();
        }
#endif


        /// <summary>
        /// Directly sets a calculated long-term stat value. Current health,
        /// cast resource and experience use their dedicated APIs instead.
        /// </summary>
        public void SetStat(StatId statId, fp value)
        {
            if (entries.TryGetValue(statId, out StatRuntimeEntry entry))
            {
                entry.FinalValue = value;
                entry.Dirty = false;
            }
            else if (configs.TryGetValue(statId, out StatConfig config))
            {
                var newEntry = new StatRuntimeEntry { FinalValue = value, Dirty = false };
                entries[statId] = newEntry;
            }
            else if (definitionTable.TryGet(statId, out StatDefinition def))
            {
                var newConfig = new StatConfig
                {
                    BaseValue = def.DefaultBaseValue,
                    GrowthValue = default,
                    Definition = def,
                };
                configs[statId] = newConfig;
                entries[statId] = new StatRuntimeEntry { FinalValue = value, Dirty = false };
            }
        }
        public void FinalizeTick()
        {
            var orderedStatIds = new List<StatId>(entries.Keys);
            orderedStatIds.Sort((left, right) => left.CompareTo(right));
            for (int statIndex = 0; statIndex < orderedStatIds.Count; statIndex++)
            {
                StatId statId = orderedStatIds[statIndex];
                StatRuntimeEntry entry = entries[statId];
                if (entry.Dirty)
                {
                    Recompute(statId, entry);
                }
                entry.PreviousLogicTickFinalValue = entry.FinalValue;
            }
            SetCurrentHealth(currentHealth);
            SetCurrentCastResource(currentCastResource);
        }

        /// <summary>
        /// Captures all cross-Tick state into a snapshot (Unit v27.3 §5.9.1).
        /// Does not capture static config (definitionTable, presets, growth params).
        /// </summary>
        public void Capture(ref StatHandlerSnapshot state)
        {
            state.Level = level;
            state.CurrentHealth = currentHealth;
            state.CurrentCastResource = currentCastResource;
            state.CurrentExperience = currentExperience;
            state.NextStatSeq = nextStatSeq;
            state.NextShieldInstanceId = nextShieldInstanceId;
            var shieldList = new List<ShieldInstance>(shieldInstances.Count);
            for (int i = 0; i < shieldInstances.Count; i++)
                shieldList.Add(shieldInstances[i]);
            state.ShieldInstances = shieldList.ToArray();

            var captureStatIds = new List<StatId>(entries.Keys);
            captureStatIds.Sort((left, right) => left.CompareTo(right));
            var entryList = new List<StatRuntimeEntrySnapshot>(entries.Count);
            for (int statIndex = 0; statIndex < captureStatIds.Count; statIndex++)
            {
                StatId statId = captureStatIds[statIndex];
                StatRuntimeEntry entry = entries[statId];
                StatModifier[] modifiers =
                    entry.Modifiers.ToArray();
                Array.Sort(
                    modifiers,
                    (left, right) =>
                        left.StatSeq.CompareTo(
                            right.StatSeq));

                entryList.Add(new StatRuntimeEntrySnapshot
                {
                    StatId = statId,
                    LevelBaseValue = entry.LevelBaseValue,
                    FinalValue = entry.FinalValue,
                    PreviousLogicTickFinalValue = entry.PreviousLogicTickFinalValue,
                    Dirty = entry.Dirty,
                    Modifiers = modifiers,
                });
            }
            state.Entries = entryList.ToArray();
        }

        /// <summary>
        /// Directly replaces all internal state from a snapshot (Unit v27.3 §5.9.2).
        /// Does NOT call AddModifier/SetModifierValue/RemoveModifier/ClearModifiers.
        /// Does NOT trigger events.
        /// </summary>
        public void Restore(in StatHandlerSnapshot state)
        {
            level = state.Level;
            currentHealth = state.CurrentHealth;
            currentCastResource = state.CurrentCastResource;
            currentExperience = state.CurrentExperience;
            nextStatSeq = state.NextStatSeq;
            nextShieldInstanceId = state.NextShieldInstanceId;

            entries.Clear();
            shieldInstances.Clear();

            int previousShieldInstanceId = 0;
            if (state.ShieldInstances != null)
            {
                for (int i = 0; i < state.ShieldInstances.Length; i++)
                {
                    ShieldInstance instance = state.ShieldInstances[i];
                    if (instance.ShieldInstanceId <= previousShieldInstanceId ||
                        instance.CurrentValue <= fp.zero ||
                        instance.CurrentValue > instance.MaxValue)
                        throw new DeterministicSimulationException(
                            "StatHandler snapshot contains invalid or non-canonical shields.");
                    previousShieldInstanceId = instance.ShieldInstanceId;
                    shieldInstances.Add(instance);
                }
            }

            for (int i = 0; i < state.Entries.Length; i++)
            {
                StatRuntimeEntrySnapshot snap = state.Entries[i];

                if (!configs.ContainsKey(snap.StatId))
                {
                    // Runtime-added stats (e.g. HealingReceivedRatio applied
                    // by a Buff) are not part of the unit preset. A
                    // rollback-reconstructed StatHandler only has preset
                    // configs after InitializeRuntime, so the restored entry
                    // must lazily recreate its config exactly like
                    // AddModifier does; otherwise FinalizeTick -> Recompute
                    // throws KeyNotFoundException on the next tick.
                    if (!definitionTable.TryGet(snap.StatId, out StatDefinition def))
                    {
                        throw new DeterministicSimulationException(
                            $"StatHandler snapshot references StatId {snap.StatId} " +
                            "which is not in the StatDefinitionTable.");
                    }
                    configs[snap.StatId] = new StatConfig
                    {
                        BaseValue = def.DefaultBaseValue,
                        GrowthValue = default,
                        Definition = def,
                    };
                }

                var entry = new StatRuntimeEntry
                {
                    LevelBaseValue = snap.LevelBaseValue,
                    FinalValue = snap.FinalValue,
                    PreviousLogicTickFinalValue = snap.PreviousLogicTickFinalValue,
                    Dirty = snap.Dirty,
                };

                if (snap.Modifiers != null)
                {
                    for (int j = 0; j < snap.Modifiers.Length; j++)
                    {
                        entry.Modifiers.Add(snap.Modifiers[j]);
                    }
                }

                entries[snap.StatId] = entry;
            }
        }

        /// <summary>
        /// Resolve phase (Unit v27.3 §7.15). StatHandler has no external object
        /// references to resolve — handles contain only value-type data.
        /// </summary>
        public void Resolve(in RollbackContext context)
        {
            for (int i = 0; i < shieldInstances.Count; i++)
            {
                ShieldInstance instance = shieldInstances[i];
                if (Owner?.World == null ||
                    !Owner.World.TryGetUnit(instance.SourceUnitUid, out _))
                    throw new DeterministicSimulationException(
                        $"Shield {instance.ShieldInstanceId} references missing source {instance.SourceUnitUid}.");
                if (instance.CrowdControlImmunityHandle.IsValid &&
                    instance.CrowdControlImmunityHandle.TargetUnitUid != ownerUid)
                    throw new DeterministicSimulationException(
                        $"Shield {instance.ShieldInstanceId} has an invalid immunity owner.");
            }
        }

        /// <summary>
        /// Rebuild phase (Unit v27.3 §7.15). Marks all entries Dirty so that
        /// GetStat recomputes from restored state.
        /// </summary>
        public void Rebuild(in RollbackContext context)
        {
            foreach (var kvp in entries)
            {
                kvp.Value.Dirty = true;
            }
        }

        private void Recompute(StatId statId, StatRuntimeEntry entry)
        {
            StatConfig config = configs[statId];

            int L = Math.Max(level - 1, 0);
            fp Lfp = L;
            fp levelGrowth = config.GrowthValue * Lfp * (statGrowthC + statGrowthD * Lfp);
            entry.LevelBaseValue = config.BaseValue + levelGrowth;

            fp flatSum = default;
            fp baseRatioSum = default;
            fp finalRatioSum = default;

            for (int i = 0; i < entry.Modifiers.Count; i++)
            {
                StatModifier mod = entry.Modifiers[i];
                switch (mod.Operation)
                {
                    case StatModifierOperation.FlatAdd:
                        flatSum += mod.Value;
                        break;
                    case StatModifierOperation.BaseRatioAdd:
                        baseRatioSum += mod.Value;
                        break;
                    case StatModifierOperation.FinalRatioAdd:
                        finalRatioSum += mod.Value;
                        break;
                }
            }

            fp beforeFinalRatio = entry.LevelBaseValue * (fp.one + baseRatioSum) + flatSum;
            fp unclampedFinalValue = beforeFinalRatio * (fp.one + finalRatioSum);

            fp finalValue = unclampedFinalValue;
            if (config.Definition.HasMinValue && finalValue < config.Definition.MinValue)
            {
                finalValue = config.Definition.MinValue;
            }
            if (config.Definition.HasMaxValue && finalValue > config.Definition.MaxValue)
            {
                finalValue = config.Definition.MaxValue;
            }

            entry.FinalValue = finalValue;
            entry.Dirty = false;
        }

        private bool HasStat(StatId statId) =>
            configs.ContainsKey(statId) || definitionTable.TryGet(statId, out _);

        private static fp ApplyLevelUpCurrentValueRule(
            fp current,
            fp previousMaximum,
            fp newMaximum,
            LevelUpCurrentValueRule rule)
        {
            fp value;
            switch (rule)
            {
                case LevelUpCurrentValueRule.KeepCurrent:
                    value = current;
                    break;
                case LevelUpCurrentValueRule.AddMaximumDelta:
                    value = current + (newMaximum - previousMaximum);
                    break;
                case LevelUpCurrentValueRule.PreserveRatio:
                    value = previousMaximum > fp.zero
                        ? current * newMaximum / previousMaximum
                        : fp.zero;
                    break;
                case LevelUpCurrentValueRule.Refill:
                    value = newMaximum;
                    break;
                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported level-up current-value rule {rule}.");
            }

            if (value < fp.zero) return fp.zero;
            return value > newMaximum ? newMaximum : value;
        }

        private void RemoveShieldAt(int index, in ShieldInstance instance)
        {
            if (instance.CrowdControlImmunityHandle.IsValid && Owner?.CrowdControl != null)
                Owner.CrowdControl.RemoveImmunity(instance.CrowdControlImmunityHandle);
            shieldInstances.RemoveAt(index);
        }

        private static bool MatchesDamage(ShieldType shieldType, DamageType damageType)
        {
            switch (shieldType)
            {
                case ShieldType.White:
                    return true;
                case ShieldType.Physical:
                    return damageType == DamageType.Physical;
                case ShieldType.Magic:
                case ShieldType.Black:
                    return damageType == DamageType.Magic;
                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported ShieldType {shieldType}.");
            }
        }
    }

    public struct ExperienceGainResult
    {
        public bool LeveledUp;
        public int PreviousLevel;
        public int NewLevel;
        public int LevelsGained;
        public int SkillPointsGained;
        public int PreviousExperience;
        public int CurrentExperience;

        public static readonly ExperienceGainResult None = default;
    }
}
