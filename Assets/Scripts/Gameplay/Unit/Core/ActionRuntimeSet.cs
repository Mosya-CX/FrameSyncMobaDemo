using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public sealed class ActionRuntimeSet
    {
        private readonly List<IActionRuntime> _runtimes = new List<IActionRuntime>(4);
        private IActionRuntime _mainAction;
        private ActionKind _mainKind;

        public int Count => _runtimes.Count;
        public IActionRuntime MainAction => _mainAction;
        public ActionKind MainKind => _mainKind;

        public void Add(IActionRuntime runtime)
        {
            if (runtime == null) return;
            _runtimes.Add(runtime);
            if (runtime.Kind != ActionKind.None)
            {
                CancelMainAction();
                _mainAction = runtime;
                _mainKind = runtime.Kind;
            }
        }

        public void Remove(IActionRuntime runtime)
        {
            if (runtime == null) return;
            _runtimes.Remove(runtime);
            if (ReferenceEquals(_mainAction, runtime))
            {
                _mainAction = null;
                _mainKind = ActionKind.None;
            }
        }

        private void CancelMainAction()
        {
            if (_mainAction != null)
            {
                _mainAction.Cancel();
                _mainAction = null;
                _mainKind = ActionKind.None;
            }
        }

        public bool HasKind(ActionKind kind)
        {
            for (int i = 0; i < _runtimes.Count; i++)
                if (_runtimes[i].Kind == kind) return true;
            return false;
        }

        public IActionRuntime TryGet(ActionKind kind)
        {
            for (int i = 0; i < _runtimes.Count; i++)
                if (_runtimes[i].Kind == kind) return _runtimes[i];
            return null;
        }

        public ReservationState BuildReservation()
        {
            ReservationState state = ReservationState.Empty;
            ActionKind highest = ActionKind.None;
            int highestPriority = -1;
            for (int i = 0; i < _runtimes.Count; i++)
            {
                var rt = _runtimes[i];
                switch (rt.Kind)
                {
                    case ActionKind.Move: state.MoveReserved = true; break;
                    case ActionKind.Attack: state.AttackReserved = true; break;
                    case ActionKind.Cast: state.CastReserved = true; break;
                }
                if ((int)rt.Kind > highestPriority)
                {
                    highestPriority = (int)rt.Kind;
                    highest = rt.Kind;
                }
            }
            state.HighestReservedKind = highest;
            return state;
        }

        public void CancelAll()
        {
            for (int i = _runtimes.Count - 1; i >= 0; i--)
                _runtimes[i].Cancel();
            _runtimes.Clear();
            _mainAction = null;
            _mainKind = ActionKind.None;
        }

        public void CancelByKind(ActionKind kind)
        {
            for (int i = _runtimes.Count - 1; i >= 0; i--)
            {
                if (_runtimes[i].Kind == kind)
                {
                    _runtimes[i].Cancel();
                    if (ReferenceEquals(_mainAction, _runtimes[i]))
                    {
                        _mainAction = null;
                        _mainKind = ActionKind.None;
                    }
                    _runtimes.RemoveAt(i);
                }
            }
        }

        public void Tick()
        {
            for (int i = _runtimes.Count - 1; i >= 0; i--)
            {
                if (_runtimes[i].IsFinished)
                {
                    if (ReferenceEquals(_mainAction, _runtimes[i]))
                    {
                        _mainAction = null;
                        _mainKind = ActionKind.None;
                    }
                    _runtimes.RemoveAt(i);
                }
                else
                {
                    _runtimes[i].Tick();
                }
            }
        }

        public void ClearWithoutCancel()
        {
            _runtimes.Clear();
            _mainAction = null;
            _mainKind = ActionKind.None;
        }
    }

    public interface IActionRuntime
    {
        ActionKind Kind { get; }
        bool IsFinished { get; }
        void Tick();
        void Cancel();
    }
}
