using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Application.Services.Interfaces;

public interface IOrderService
{
    /// <summary>Creates the customer's draft order if none exists, otherwise replaces its items
    /// with <paramref name="items"/>. Never advances past <see cref="OrderStatus.Draft"/> — that's
    /// <see cref="ConfirmOrderSummaryAsync"/>'s job, and only for <see cref="OrderActor.Agent"/>.</summary>
    Task<TendaaResult<UpsertDraftOrderResult>> UpsertDraftOrderAsync(
        Guid shopId,
        Guid customerId,
        IReadOnlyList<OrderItemInput> items,
        string? customerName,
        string? area,
        string? address,
        CancellationToken cancellationToken = default);

    Task<TendaaResult<OrderSummary>> GetOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Draft -> AwaitingPayment, via <c>OrderStateMachine</c> with
    /// <see cref="OrderActor.Agent"/>. Throws if the order isn't a fully-formed draft (no items,
    /// no delivery zone) — the agent should have confirmed the summary with the customer first.</summary>
    Task<TendaaResult<ConfirmOrderResult>> ConfirmOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default);

    Task<TendaaResult<IReadOnlyList<OrderStatusSummary>>> GetMyOrdersAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default);
}
