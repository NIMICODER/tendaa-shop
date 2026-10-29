namespace Tendaa.Application.Models;

public sealed record OrderSummaryItem(
    Guid ProductVariantId,
    string ProductName,
    string? Size,
    string? Colour,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderSummary(
    int Number,
    IReadOnlyList<OrderSummaryItem> Items,
    decimal ItemTotal,
    decimal DeliveryFee,
    decimal GrandTotal,
    string? Area);
