using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class OrderController
{
    private readonly HeroUnit owner;
    private UnitOrder currentOrder;
    private UnitOrder suspendedOrder;
    private readonly Queue<UnitOrder> bufferedOrders = new();
    private readonly Stack<CastOrder> pausedCastStack = new();

    public UnitOrder CurrentOrder => currentOrder;

    public OrderController(HeroUnit owner)
    {
        this.owner = owner;
    }

    public void Submit(UnitOrder order, bool queueIfBusy = false)
    {
        if (owner.DashMotor.IsDashing)
        {
            bufferedOrders.Enqueue(order);
            return;
        }

        if (currentOrder == null)
        {
            StartOrder(order);
            return;
        }

        if (TryResolveCastWindow(order))
            return;

        if (queueIfBusy)
        {
            bufferedOrders.Enqueue(order);
            return;
        }

        if (!currentOrder.CanBeInterruptedBy(order))
        {
            bufferedOrders.Enqueue(order);
            return;
        }

        if (currentOrder is MoveOrder or AttackOrder)
            suspendedOrder = currentOrder;

        currentOrder.Cancel();
        currentOrder.OnExit();

        StartOrder(order);
    }

    public void Tick(fp dt, uint currentTick)
    {
        if (owner.DashMotor.IsDashing)
            return;

        if (currentOrder is CastOrder castOrder)
            castOrder.Tick(dt, currentTick);
        else
            currentOrder?.Tick(dt);

        if (currentOrder == null)
            return;

        if (!currentOrder.IsFinished && !currentOrder.IsCancelled)
            return;

        currentOrder.OnExit();

        if (TryResumePausedCast())
            return;

        if (bufferedOrders.Count > 0)
        {
            StartOrder(bufferedOrders.Dequeue());
            return;
        }

        if (suspendedOrder != null)
        {
            var restore = suspendedOrder;
            suspendedOrder = null;
            StartOrder(restore);
            return;
        }

        currentOrder = null;
    }

    public void ClearSuspendedOrder()
    {
        suspendedOrder = null;
    }

    public void ClearBufferedOrders()
    {
        bufferedOrders.Clear();
    }

    private void StartOrder(UnitOrder order)
    {
        currentOrder = order;
        currentOrder.OnEnter();
    }

    private bool TryResolveCastWindow(UnitOrder newOrder)
    {
        if (currentOrder is not CastOrder currentCast || newOrder is not CastOrder newCast)
            return false;

        var window = currentCast.ResolveCastWindow(newCast.AbilityId);
        if (!window.HasValue)
            return false;

        switch (window.Value)
        {
            case CastWindowType.QueueOnly:
                bufferedOrders.Enqueue(newOrder);
                return true;

            case CastWindowType.ReplaceCurrent:
                currentOrder.Cancel();
                currentOrder.OnExit();
                StartOrder(newOrder);
                return true;

            case CastWindowType.InsertBeforeExecute:
                var snapshot = currentCast.PauseForInsert();
                if (snapshot == null)
                {
                    bufferedOrders.Enqueue(newOrder);
                    return true;
                }

                pausedCastStack.Push(currentCast);
                currentOrder = null;
                StartOrder(newOrder);
                return true;
        }

        return false;
    }

    private bool TryResumePausedCast()
    {
        if (pausedCastStack.Count == 0)
            return false;

        var paused = pausedCastStack.Pop();
        paused.ResumeFromPausedSnapshot();
        currentOrder = paused;
        return true;
    }

    #region Snapshot

    public enum OrderSnapshotType : byte
    {
        None,
        Move,
        Attack,
        Cast,
    }

    [System.Serializable]
    public struct OrderSnapshot
    {
        public OrderSnapshotType Type;

        public fp3 Destination;
        public bool HasDestination;

        public UnitUID TargetId;
        public bool HasTarget;

        public int AbilityId;
        public bool QueueIfBusy;
        public AbilityTriggerContext AbilityContext;

        public bool IsCancelled;
        public bool IsFinished;

        public object ExtraState;
    }

    [System.Serializable]
    public struct OrderControllerSnapshot
    {
        public bool HasCurrentOrder;
        public OrderSnapshot CurrentOrder;

        public bool HasSuspendedOrder;
        public OrderSnapshot SuspendedOrder;

        public OrderSnapshot[] BufferedOrders;
        public OrderSnapshot[] PausedCastOrders;
    }

    public object CaptureState()
    {
        return new OrderControllerSnapshot
        {
            HasCurrentOrder = currentOrder != null,
            CurrentOrder = currentOrder != null ? CaptureOrder(currentOrder) : default,

            HasSuspendedOrder = suspendedOrder != null,
            SuspendedOrder = suspendedOrder != null ? CaptureOrder(suspendedOrder) : default,

            BufferedOrders = CaptureOrderQueue(bufferedOrders),
            PausedCastOrders = CapturePausedCastStack(),
        };
    }

    public void RestoreState(object state)
    {
        var snap = (OrderControllerSnapshot)state;

        currentOrder = snap.HasCurrentOrder ? RestoreOrder(snap.CurrentOrder) : null;
        suspendedOrder = snap.HasSuspendedOrder ? RestoreOrder(snap.SuspendedOrder) : null;

        bufferedOrders.Clear();
        if (snap.BufferedOrders != null)
        {
            for (int i = 0; i < snap.BufferedOrders.Length; i++)
                bufferedOrders.Enqueue(RestoreOrder(snap.BufferedOrders[i]));
        }

        pausedCastStack.Clear();
        if (snap.PausedCastOrders != null)
        {
            // 还原时倒序压栈
            for (int i = snap.PausedCastOrders.Length - 1; i >= 0; i--)
            {
                if (RestoreOrder(snap.PausedCastOrders[i]) is CastOrder cast)
                    pausedCastStack.Push(cast);
            }
        }
    }

    private OrderSnapshot[] CaptureOrderQueue(Queue<UnitOrder> queue)
    {
        if (queue.Count == 0)
            return System.Array.Empty<OrderSnapshot>();

        var arr = new OrderSnapshot[queue.Count];
        int i = 0;
        foreach (var order in queue)
            arr[i++] = CaptureOrder(order);
        return arr;
    }

    private OrderSnapshot[] CapturePausedCastStack()
    {
        if (pausedCastStack.Count == 0)
            return System.Array.Empty<OrderSnapshot>();

        var array = pausedCastStack.ToArray(); // 栈顶在前
        var snaps = new OrderSnapshot[array.Length];
        for (int i = 0; i < array.Length; i++)
            snaps[i] = CaptureOrder(array[i]);
        return snaps;
    }

    private OrderSnapshot CaptureOrder(UnitOrder order)
    {
        switch (order)
        {
            case MoveOrder move:
                return new OrderSnapshot
                {
                    Type = OrderSnapshotType.Move,
                    HasDestination = true,
                    Destination = move.Destination,
                    IsCancelled = move.IsCancelled,
                    IsFinished = move.IsFinished,
                };

            case AttackOrder attack:
                return new OrderSnapshot
                {
                    Type = OrderSnapshotType.Attack,
                    HasTarget = true,
                    TargetId = attack.TargetUid,
                    IsCancelled = attack.IsCancelled,
                    IsFinished = attack.IsFinished,
                };

            case CastOrder cast:
                return new OrderSnapshot
                {
                    Type = OrderSnapshotType.Cast,
                    AbilityId = cast.AbilityId,
                    QueueIfBusy = cast.QueueIfBusy,
                    AbilityContext = cast.CommandContext,
                    IsCancelled = cast.IsCancelled,
                    IsFinished = cast.IsFinished,
                    ExtraState = cast.CaptureOrderState(),
                };
        }

        return default;
    }

    private UnitOrder RestoreOrder(OrderSnapshot snap)
    {
        UnitOrder order = null;

        switch (snap.Type)
        {
            case OrderSnapshotType.Move:
                order = new MoveOrder(owner, snap.Destination);
                break;

            case OrderSnapshotType.Attack:
                order = new AttackOrder(owner, snap.TargetId);
                break;

            case OrderSnapshotType.Cast:
                var cmd = new AbilityCommand
                {
                    ReceiverUnitId = owner.UnitID,
                    AbilityId = snap.AbilityId,
                    QueueIfBusy = snap.QueueIfBusy,
                    Context = snap.AbilityContext,
                };
                var cast = new CastOrder(owner, cmd);
                cast.RestoreOrderState(snap.ExtraState);
                order = cast;
                break;
        }

        if (order != null)
        {
            if (snap.IsCancelled)
                order.Cancel();

            if (snap.IsFinished)
                ForceFinish(order);
        }

        return order;
    }

    private void ForceFinish(UnitOrder order)
    {
        // 因为 IsFinished 的 setter 是 protected，不能直接改
        // 所以这里让完成态在 Tick 中自然结算即可；当前只保留 cancelled 信息。
    }

    #endregion
}