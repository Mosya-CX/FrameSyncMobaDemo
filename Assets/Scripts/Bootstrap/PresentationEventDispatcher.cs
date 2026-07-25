using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Consumes deterministic VisualEventOutput streams at tick-end
    /// and dispatches to registered presentation handlers.
    /// Deduplicates events by (tick, sourceUid, eventKey) for rollback safety.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationEventDispatcher : MonoBehaviour
    {
        private readonly List<IVfxHandler> _vfxHandlers = new List<IVfxHandler>();
        private readonly List<ISfxHandler> _sfxHandlers = new List<ISfxHandler>();
        private readonly HashSet<ulong> _dedupSet = new HashSet<ulong>();

        /// <summary>
        /// Register a handler that consumes VfxEvents.
        /// </summary>
        public void RegisterVfxHandler(IVfxHandler handler)
        {
            if (handler != null && !_vfxHandlers.Contains(handler))
                _vfxHandlers.Add(handler);
        }

        /// <summary>
        /// Register a handler that consumes SfxEvents.
        /// </summary>
        public void RegisterSfxHandler(ISfxHandler handler)
        {
            if (handler != null && !_sfxHandlers.Contains(handler))
                _sfxHandlers.Add(handler);
        }

        /// <summary>
        /// Called after deterministic tick completes.
        /// Consumes VFX and SFX event streams and dispatches to handlers.
        /// </summary>
        public void DispatchCurrentFrame()
        {
            _dedupSet.Clear();

            var vfxEvents = VisualEventOutput.ConsumeVfxEvents();
            for (int i = 0; i < vfxEvents.Count; i++)
            {
                VfxEvent evt = vfxEvents[i];
                if (!TryDedup(evt.Id)) continue;
                for (int j = 0; j < _vfxHandlers.Count; j++)
                    _vfxHandlers[j].OnVfxEvent(evt);
            }

            var sfxEvents = VisualEventOutput.ConsumeSfxEvents();
            for (int i = 0; i < sfxEvents.Count; i++)
            {
                SfxEvent evt = sfxEvents[i];
                if (!TryDedup(evt.Id)) continue;
                for (int j = 0; j < _sfxHandlers.Count; j++)
                    _sfxHandlers[j].OnSfxEvent(evt);
            }
        }

        private bool TryDedup(PresentationEventId id)
        {
            // Compact dedup key: combine tick (32bit) + sourceUid hash (32bit) into ulong
            ulong key = ((ulong)(uint)id.SourceLogicTick << 32)
                      | ((uint)id.SourceRuntimeUid.GetHashCode() & 0xFFFFFFFF);
            return _dedupSet.Add(key);
        }

        private void OnDestroy()
        {
            _vfxHandlers.Clear();
            _sfxHandlers.Clear();
        }
    }

    /// <summary>
    /// Handler for VfxEvent consumption.
    /// </summary>
    public interface IVfxHandler
    {
        void OnVfxEvent(in VfxEvent evt);
    }

    /// <summary>
    /// Handler for SfxEvent consumption.
    /// </summary>
    public interface ISfxHandler
    {
        void OnSfxEvent(in SfxEvent evt);
    }
}
