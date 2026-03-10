using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public sealed class OrderController
{
    private readonly HeroUnit owner;
    private UnitOrder currentOrder;
    private UnitOrder suspendedOrder;
    private readonly Queue<UnitOrder> bufferedOrders = new();

    public UnitOrder CurrentOrder => currentOrder;

    public OrderController(HeroUnit owner)
    {
        this.owner = owner;
    }

    public void Submit(UnitOrder order, bool queueIfBusy = false)
    {
        if (currentOrder == null)
        {
            StartOrder(order);
            return;
        }

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

    public void Tick(fp dt)
    {
        currentOrder?.Tick(dt);

        if (currentOrder == null)
            return;

        if (!currentOrder.IsFinished && !currentOrder.IsCancelled)
            return;

        currentOrder.OnExit();

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
}