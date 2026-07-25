using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Dispatches fixed UnitEventBus routes to equipment effect modules
    /// in stable Slot→Effect→Module order (Equipment/Gold v12 §3.5, §3.12).
    /// </summary>
    public sealed class EquipmentEffectDispatch
    {
        private readonly Unit _owner;

        // Context data for event-driven modules. Set by event handlers before dispatch.
        internal DamageEventData LastDamageDealt;
        internal HealEventData LastHealDealt;
        internal UnitUid LastKillVictimUid;
        internal OnHitEventData LastOnHit;

        public EquipmentEffectDispatch(Unit owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Per-Tick advance for Tick-timed effect modules.</summary>
        public void Advance()
        {
            var handler = _owner.EquipmentHandler;
            if (handler == null) return;

            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
            {
                var inst = handler.GetSlot(slot);
                if (inst?.Definition?.Effects == null) continue;

                for (int fxIdx = 0; fxIdx < inst.Definition.Effects.Length; fxIdx++)
                {
                    var fxDef = inst.Definition.Effects[fxIdx];
                    if (fxDef?.Modules == null) continue;

                    for (int modIdx = 0; modIdx < fxDef.Modules.Length; modIdx++)
                    {
                        var mod = fxDef.Modules[modIdx];
                        if (mod == null) continue;

                        bool hasTickTiming = false;
                        if (mod.InvokeTimings != null)
                        {
                            for (int t = 0; t < mod.InvokeTimings.Length; t++)
                            {
                                if (mod.InvokeTimings[t] == EquipmentEffectInvokeTiming.Tick)
                                { hasTickTiming = true; break; }
                            }
                        }

                        if (hasTickTiming && mod.CanExecute())
                            mod.Execute(_owner, inst);
                    }
                }
            }
        }

        // ---- Event handlers (iterate Slot→Effect→Module) ----

        private void DispatchForTiming(EquipmentEffectInvokeTiming timing)
        {
            var handler = _owner.EquipmentHandler;
            if (handler == null) return;

            for (int slot = 0; slot < EquipmentHandler.SlotCount; slot++)
            {
                var inst = handler.GetSlot(slot);
                if (inst?.Definition?.Effects == null) continue;

                for (int fxIdx = 0; fxIdx < inst.Definition.Effects.Length; fxIdx++)
                {
                    var fxDef = inst.Definition.Effects[fxIdx];
                    if (fxDef?.Modules == null) continue;

                    for (int modIdx = 0; modIdx < fxDef.Modules.Length; modIdx++)
                    {
                        var mod = fxDef.Modules[modIdx];
                        if (mod == null || mod.InvokeTimings == null) continue;

                        for (int t = 0; t < mod.InvokeTimings.Length; t++)
                        {
                            if (mod.InvokeTimings[t] == timing && mod.CanExecute())
                                mod.Execute(_owner, inst);
                        }
                    }
                }
            }
        }

        public void OnDamageTaken(in DamageEventData data) => DispatchForTiming(EquipmentEffectInvokeTiming.DamageTaken);
        public void OnDamageDealt(in DamageEventData data) { LastDamageDealt = data; DispatchForTiming(EquipmentEffectInvokeTiming.DamageDealt); }
        public void OnHealTaken(in HealEventData data) => DispatchForTiming(EquipmentEffectInvokeTiming.HealTaken);
        public void OnHealDealt(in HealEventData data) { LastHealDealt = data; DispatchForTiming(EquipmentEffectInvokeTiming.HealDealt); }
        public void OnUnitDying(Unit unit) => DispatchForTiming(EquipmentEffectInvokeTiming.UnitDying);
        public void OnUnitDeath(Unit unit) => DispatchForTiming(EquipmentEffectInvokeTiming.UnitDeath);
        public void OnUnitKill(Unit unit) { LastKillVictimUid = unit?.UnitUid ?? default; DispatchForTiming(EquipmentEffectInvokeTiming.UnitKill); }
        public void OnHitDealt(in OnHitEventData data) { LastOnHit = data; DispatchForTiming(EquipmentEffectInvokeTiming.OnHitDealt); }
    }
}
