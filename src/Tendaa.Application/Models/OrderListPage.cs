using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Application.Models;

public sealed record OrderListItem(
    Guid OrderId,
    int Number,
    OrderStatus Status,
    string CustomerName,
    int ItemCount,
    decimal GrandTotal,
    string FormattedGrandTotal,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaymentReviewAt);

public sealed record OrderListPage(
    IReadOnlyList<OrderListItem> Items,
    int TotalCount,
    int Page,
    int PageSize);
