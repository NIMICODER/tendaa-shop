namespace Tendaa.Core.Orders;

/// <summary>Allowed status transitions, each gated by which actor is allowed to make it. The
/// caller passes the actor explicitly — it is never inferred from the model — so an agent tool
/// can never talk its way into confirming a payment even if it tried. See SPEC.md's "Order
/// statuses" and ARCHITECTURE.md's "Order state machine".</summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<(OrderStatus From, OrderStatus To), OrderActor[]> AllowedTransitions =
        new()
        {
            [(OrderStatus.Draft, OrderStatus.AwaitingPayment)] = [OrderActor.Agent],
            [(OrderStatus.Draft, OrderStatus.Cancelled)] = [OrderActor.Agent, OrderActor.Seller],
            [(OrderStatus.AwaitingPayment, OrderStatus.PaymentReview)] = [OrderActor.Agent],
            [(OrderStatus.AwaitingPayment, OrderStatus.Cancelled)] = [OrderActor.Agent, OrderActor.Seller],
            [(OrderStatus.PaymentReview, OrderStatus.Confirmed)] = [OrderActor.Seller],
            [(OrderStatus.PaymentReview, OrderStatus.AwaitingPayment)] = [OrderActor.Seller],
            [(OrderStatus.Confirmed, OrderStatus.Dispatched)] = [OrderActor.Seller],
            [(OrderStatus.Dispatched, OrderStatus.Delivered)] = [OrderActor.Seller, OrderActor.System],
        };

    public static bool CanTransition(OrderStatus from, OrderStatus to, OrderActor actor) =>
        AllowedTransitions.TryGetValue((from, to), out var actors) && actors.Contains(actor);

    /// <exception cref="InvalidOperationException">The transition isn't allowed from the order's
    /// current status, or <paramref name="actor"/> isn't permitted to make it.</exception>
    public static void Transition(Order order, OrderStatus to, OrderActor actor)
    {
        if (!CanTransition(order.Status, to, actor))
        {
            throw new InvalidOperationException(
                $"Actor '{actor}' cannot transition order from '{order.Status}' to '{to}'.");
        }

        order.Status = to;

        var now = DateTimeOffset.UtcNow;
        switch (to)
        {
            case OrderStatus.AwaitingPayment:
                order.AwaitingPaymentAt = now;
                break;
            case OrderStatus.PaymentReview:
                order.PaymentReviewAt = now;
                break;
            case OrderStatus.Confirmed:
                order.ConfirmedAt = now;
                break;
            case OrderStatus.Dispatched:
                order.DispatchedAt = now;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = now;
                break;
            case OrderStatus.Cancelled:
                order.CancelledAt = now;
                break;
            case OrderStatus.Draft:
                break;
        }
    }
}
