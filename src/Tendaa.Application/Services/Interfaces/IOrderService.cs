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

    /// <summary>Finds the customer's current <see cref="OrderStatus.AwaitingPayment"/> order,
    /// records a <c>PaymentProof</c> row, and runs <c>PaymentVerifier</c>'s deterministic check —
    /// transitioning the order to <see cref="OrderStatus.PaymentReview"/> as
    /// <see cref="OrderActor.System"/> on a match. Takes plain extracted fields, not a
    /// <c>Tendaa.Agent</c> type — this layer never references <c>Agent</c> (see CLAUDE.md's
    /// dependency direction). Returns <see cref="TendaaResult{T}.NotFound"/> semantics via
    /// <c>OrderFound: false</c> when there's no such order, never an exception.</summary>
    Task<TendaaResult<ReceiptCheckResult>> CheckReceiptAsync(
        Guid shopId,
        Guid customerId,
        string mediaId,
        decimal? extractedAmount,
        DateOnly? transferDate,
        string? reference,
        double confidence,
        string extractedFieldsJson,
        CancellationToken cancellationToken = default);

    /// <summary>Dashboard: paged orders, newest first, optionally filtered by status.</summary>
    Task<TendaaResult<OrderListPage>> ListAsync(
        Guid shopId, OrderStatus? statusFilter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: order counts for every status (including zeros), for the board's
    /// stat cards and tab badges.</summary>
    Task<TendaaResult<IReadOnlyList<OrderStatusCount>>> GetStatusCountsAsync(
        Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: full order detail — items with product names, totals, delivery, and
    /// every payment proof with its parsed extracted fields.</summary>
    Task<TendaaResult<OrderDetail>> GetDetailAsync(
        Guid shopId, Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: seller confirms the given receipt — PaymentReview -> Confirmed via
    /// <c>OrderStateMachine</c> with <see cref="OrderActor.Seller"/>, stock decremented, customer
    /// notified when inside WhatsApp's 24-hour window (see <see cref="ICustomerMessenger"/>).
    /// The result message says whether the customer was notified.</summary>
    Task<TendaaResult<OrderDetail>> ConfirmPaymentAsync(
        Guid shopId, Guid paymentProofId, string confirmedBy, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: seller rejects the given receipt — PaymentReview -> AwaitingPayment
    /// with the reason recorded, customer asked to resend proof (same 24-hour rule as
    /// <see cref="ConfirmPaymentAsync"/>).</summary>
    Task<TendaaResult<OrderDetail>> RejectPaymentAsync(
        Guid shopId, Guid paymentProofId, string reason, string rejectedBy, CancellationToken cancellationToken = default);
}
