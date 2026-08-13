using System;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Dispatches fixed UnitEventBus routes to equipment effect modules in
    /// stable Slot→Effect→Module order (Equipment/Gold v12 §3.5, §3.12).
    /// </summary>
    public sealed class EquipmentEffectDispatch
    {
        private readonly Unit _owner;
        private bool _repeatRequested;

        internal DamageEventData LastDamageDealt;
        internal HealEventData LastHealDealt;
        internal UnitUid LastKillVictimUid;
        internal OnHitEventData LastOnHit;

        public EquipmentEffectDispatch(Unit owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public void Advance()
        {
            DispatchForTiming(EquipmentEffectInvokeTiming.Tick);
        }

        internal void RequestRepeatedOnHit()
        {
            if (!LastOnHit.IsRepeated)
                _repeatRequested = true;
        }

        /// <summary>
        /// Asks every equipped effect module for an empowered-strike recipe
        /// that is ready for <paramref name="target"/> (stable Slot to Effect
        /// to Module order). Returns the first ready recipe; the caller uses
        /// it to build the basic-attack damage request.
        /// </summary>
        internal bool TryResolveEmpoweredAttackRecipe(
            Unit target,
            out int recipeId)
        {
            recipeId = 0;
            if (target == null)
            {
                return false;
            }
            EquipmentHandler handler =
                _owner.EquipmentHandler;
            if (handler == null)
            {
                return false;
            }
            for (int slot = 0;
                 slot < EquipmentHandler.SlotCount;
                 slot++)
            {
                EquipmentInstance instance =
                    handler.GetSlot(slot);
                if (instance?.Definition?.Effects == null ||
                    instance.EffectRuntimes == null)
                {
                    continue;
                }
                for (int fxIdx = 0;
                     fxIdx < instance.Definition.Effects.Length;
                     fxIdx++)
                {
                    EquipmentEffectDef effect =
                        instance.Definition.Effects[fxIdx];
                    if (effect?.Modules == null)
                    {
                        continue;
                    }
                    for (int modIdx = 0;
                         modIdx < effect.Modules.Length;
                         modIdx++)
                    {
                        if (effect.Modules[modIdx] is
                            IEmpoweredAttackProvider provider &&
                            provider.IsReadyForTarget(
                                _owner,
                                target))
                        {
                            recipeId =
                                provider.EmpoweredRecipeId;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        internal void DispatchInstanceForTiming(
            EquipmentInstance instance,
            EquipmentEffectInvokeTiming timing,
            AimSnapshot target = default)
        {
            if (instance?.Definition?.Effects == null ||
                instance.EffectRuntimes == null)
                return;
            if (instance.Definition.Effects.Length !=
                instance.EffectRuntimes.Length)
                throw new DeterministicSimulationException(
                    $"Equipment {instance.Definition.Id} effect runtime count mismatch.");

            for (int fxIdx = 0;
                 fxIdx < instance.Definition.Effects.Length;
                 fxIdx++)
            {
                EquipmentEffectDef effect =
                    instance.Definition.Effects[fxIdx];
                EquipmentEffectRuntime runtime =
                    instance.EffectRuntimes[fxIdx];
                if (effect?.Modules == null)
                    continue;
                if (runtime?.ModuleStates == null ||
                    runtime.ModuleStates.Length != effect.Modules.Length)
                    throw new DeterministicSimulationException(
                        $"Equipment {instance.Definition.Id} effect {fxIdx} module-state count mismatch.");

                for (int modIdx = 0;
                     modIdx < effect.Modules.Length;
                     modIdx++)
                {
                    EquipmentEffectModule module = effect.Modules[modIdx];
                    if (module == null || !HasTiming(module, timing))
                        continue;

                    ref EquipmentEffectModuleRuntimeState state =
                        ref runtime.ModuleStates[modIdx];
                    var context = new EquipmentEffectExecutionContext
                    {
                        Owner = _owner,
                        Instance = instance,
                        Dispatch = this,
                        Timing = timing,
                        Target = target,
                        OnHit = LastOnHit,
                    };
                    if (module.CanExecute(ref context, ref state))
                        module.Execute(ref context, ref state);
                }
            }
        }

        private void DispatchForTiming(EquipmentEffectInvokeTiming timing)
        {
            EquipmentHandler handler = _owner.EquipmentHandler;
            if (handler == null)
                return;

            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
                DispatchInstanceForTiming(handler.GetSlot(slot), timing);
        }

        private static bool HasTiming(
            EquipmentEffectModule module,
            EquipmentEffectInvokeTiming timing)
        {
            EquipmentEffectInvokeTiming[] timings = module.InvokeTimings;
            if (timings == null)
                return false;
            for (int i = 0; i < timings.Length; i++)
                if (timings[i] == timing)
                    return true;
            return false;
        }

        public void OnDamageTaken(in DamageEventData data)
        {
            DispatchForTiming(EquipmentEffectInvokeTiming.DamageTaken);
        }

        public void OnDamageDealt(in DamageEventData data)
        {
            LastDamageDealt = data;
            DispatchForTiming(EquipmentEffectInvokeTiming.DamageDealt);
        }

        public void OnHealTaken(in HealEventData data)
        {
            DispatchForTiming(EquipmentEffectInvokeTiming.HealTaken);
        }

        public void OnHealDealt(in HealEventData data)
        {
            LastHealDealt = data;
            DispatchForTiming(EquipmentEffectInvokeTiming.HealDealt);
        }

        public void OnUnitDying(Unit unit)
        {
            DispatchForTiming(EquipmentEffectInvokeTiming.UnitDying);
        }

        public void OnUnitDeath(Unit unit)
        {
            DispatchForTiming(EquipmentEffectInvokeTiming.UnitDeath);
        }

        public void OnUnitKill(Unit unit)
        {
            LastKillVictimUid = unit?.UnitUid ?? default;
            DispatchForTiming(EquipmentEffectInvokeTiming.UnitKill);
        }

        public void OnHitDealt(in OnHitEventData data)
        {
            LastOnHit = data;
            _repeatRequested = false;
            DispatchForTiming(EquipmentEffectInvokeTiming.OnHitDealt);

            if (data.IsRepeated || !_repeatRequested)
                return;

            OnHitEventData repeated = data;
            repeated.IsRepeated = true;
            _owner.EventBus.PublishOnHit(repeated);
        }
    }
}
