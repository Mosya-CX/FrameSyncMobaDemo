using System;
using System.Collections.Generic;
using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Consumes deterministic VisualEventOutput streams at tick-end
    /// and dispatches to registered presentation handlers.
    /// Keeps independent, bounded VFX and SFX one-shot histories keyed by the
    /// complete PresentationEventId.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PresentationEventDispatcher : MonoBehaviour
    {
        private readonly List<IVfxHandler> _vfxHandlers = new List<IVfxHandler>();
        private readonly List<ISfxHandler> _sfxHandlers = new List<ISfxHandler>();
        private const int DefaultHistoryWindowTicks = 512;
        private readonly PresentationEventHistory _vfxHistory =
            new PresentationEventHistory(
                DefaultHistoryWindowTicks);
        private readonly PresentationEventHistory _sfxHistory =
            new PresentationEventHistory(
                DefaultHistoryWindowTicks);

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
            var vfxEvents = VisualEventOutput.ConsumeVfxEvents();
            for (int i = 0; i < vfxEvents.Count; i++)
            {
                VfxEvent evt = vfxEvents[i];
                if (!_vfxHistory.TryConsume(
                        evt.Id))
                    continue;
                for (int j = 0; j < _vfxHandlers.Count; j++)
                    _vfxHandlers[j].OnVfxEvent(evt);
            }

            var sfxEvents = VisualEventOutput.ConsumeSfxEvents();
            for (int i = 0; i < sfxEvents.Count; i++)
            {
                SfxEvent evt = sfxEvents[i];
                if (!_sfxHistory.TryConsume(
                        evt.Id))
                    continue;
                for (int j = 0; j < _sfxHandlers.Count; j++)
                    _sfxHandlers[j].OnSfxEvent(evt);
            }
        }

        public void ResetForNewMatch()
        {
            _vfxHistory.Clear();
            _sfxHistory.Clear();
        }

        private void OnDestroy()
        {
            _vfxHandlers.Clear();
            _sfxHandlers.Clear();
            ResetForNewMatch();
        }
    }

    internal sealed class PresentationEventHistory
    {
        private readonly int historyWindowTicks;
        private readonly HashSet<PresentationEventId> consumed =
            new HashSet<PresentationEventId>();
        private readonly List<PresentationEventId> entries =
            new List<PresentationEventId>();
        private int newestObservedTick = int.MinValue;

        public PresentationEventHistory(
            int historyWindowTicks)
        {
            if (historyWindowTicks <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(historyWindowTicks));
            this.historyWindowTicks =
                historyWindowTicks;
        }

        public bool TryConsume(
            in PresentationEventId id)
        {
            if (id.SourceLogicTick >
                newestObservedTick)
            {
                newestObservedTick =
                    id.SourceLogicTick;
                Prune();
            }
            if (!consumed.Add(id))
                return false;
            entries.Add(id);
            return true;
        }

        public void Clear()
        {
            consumed.Clear();
            entries.Clear();
            newestObservedTick =
                int.MinValue;
        }

        private void Prune()
        {
            int oldestRetainedTick =
                newestObservedTick -
                historyWindowTicks;
            for (int i = entries.Count - 1;
                 i >= 0;
                 i--)
            {
                PresentationEventId id =
                    entries[i];
                if (id.SourceLogicTick >=
                    oldestRetainedTick)
                    continue;
                entries.RemoveAt(i);
                consumed.Remove(id);
            }
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
