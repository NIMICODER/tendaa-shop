using Tendaa.Core.Orders;

namespace Tendaa.Core.Tests.Orders;

public class OrderStateMachineTests
{
    private static Order NewOrder(OrderStatus status) => new() { Number = 1, Status = status };

    public static TheoryData<OrderStatus, OrderStatus, OrderActor> AllowedTransitions =>
        new()
        {
            { OrderStatus.Draft, OrderStatus.AwaitingPayment, OrderActor.Agent },
            { OrderStatus.Draft, OrderStatus.Cancelled, OrderActor.Agent },
            { OrderStatus.Draft, OrderStatus.Cancelled, OrderActor.Seller },
            { OrderStatus.AwaitingPayment, OrderStatus.PaymentReview, OrderActor.Agent },
            { OrderStatus.AwaitingPayment, OrderStatus.Cancelled, OrderActor.Agent },
            { OrderStatus.AwaitingPayment, OrderStatus.Cancelled, OrderActor.Seller },
            { OrderStatus.PaymentReview, OrderStatus.Confirmed, OrderActor.Seller },
            { OrderStatus.PaymentReview, OrderStatus.AwaitingPayment, OrderActor.Seller },
            { OrderStatus.Confirmed, OrderStatus.Dispatched, OrderActor.Seller },
            { OrderStatus.Dispatched, OrderStatus.Delivered, OrderActor.Seller },
            { OrderStatus.Dispatched, OrderStatus.Delivered, OrderActor.System },
        };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Transition_WithAllowedActor_Succeeds(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        var order = NewOrder(from);

        OrderStateMachine.Transition(order, to, actor);

        Assert.Equal(to, order.Status);
        Assert.True(OrderStateMachine.CanTransition(from, to, actor));
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Transition_SetsTimestamp_ForTargetStatus(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        var order = NewOrder(from);

        OrderStateMachine.Transition(order, to, actor);

        DateTimeOffset? timestamp = to switch
        {
            OrderStatus.AwaitingPayment => order.AwaitingPaymentAt,
            OrderStatus.PaymentReview => order.PaymentReviewAt,
            OrderStatus.Confirmed => order.ConfirmedAt,
            OrderStatus.Dispatched => order.DispatchedAt,
            OrderStatus.Delivered => order.DeliveredAt,
            OrderStatus.Cancelled => order.CancelledAt,
            _ => throw new InvalidOperationException($"Unexpected target status {to}."),
        };
        Assert.NotNull(timestamp);
    }

    public static IEnumerable<object[]> AllStatusPairsAndActors()
    {
        var statuses = Enum.GetValues<OrderStatus>();
        var actors = Enum.GetValues<OrderActor>();
        foreach (var from in statuses)
        {
            foreach (var to in statuses)
            {
                if (from == to)
                {
                    continue;
                }

                foreach (var actor in actors)
                {
                    yield return [from, to, actor];
                }
            }
        }
    }

    /// <summary>Exhaustively covers the "every forbidden transition" half of Phase 1's done-when
    /// check: every (from, to, actor) combination not explicitly listed in
    /// <see cref="AllowedTransitions"/> must be rejected, independent of whatever
    /// <see cref="OrderStateMachine"/>'s own table says — this catches an accidental extra entry
    /// in production code, not just a missing one.</summary>
    [Theory]
    [MemberData(nameof(AllStatusPairsAndActors))]
    public void Transition_Rejects_EverythingNotExplicitlyAllowed(OrderStatus from, OrderStatus to, OrderActor actor)
    {
        var isAllowed = AllowedTransitions.Any(row =>
            (OrderStatus)row[0]! == from && (OrderStatus)row[1]! == to && (OrderActor)row[2]! == actor);
        if (isAllowed)
        {
            return;
        }

        var order = NewOrder(from);

        var exception = Record.Exception(() => OrderStateMachine.Transition(order, to, actor));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal(from, order.Status);
    }

    [Fact]
    public void Agent_CannotConfirmPayment_Throws()
    {
        var order = NewOrder(OrderStatus.PaymentReview);

        var exception = Assert.Throws<InvalidOperationException>(
            () => OrderStateMachine.Transition(order, OrderStatus.Confirmed, OrderActor.Agent));

        Assert.Contains("Agent", exception.Message);
        Assert.Equal(OrderStatus.PaymentReview, order.Status);
    }
}
