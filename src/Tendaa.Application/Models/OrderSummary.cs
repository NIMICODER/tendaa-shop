namespace Tendaa.Application.Models;

public sealed record OrderSummaryItem(
    Guid ProductVariantId,
    string ProductName,
    string? Size,
    string? Colour,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string FormattedUnitPrice,
    string FormattedLineTotal);

/// <summary><see cref="SummaryText"/> is the full order summary already formatted in code
/// (items, item total, delivery fee, grand total) — the model relays it rather than composing or
/// totalling it itself (SPEC.md: prices and totals come only from the database).</summary>
public sealed record OrderSummary(
    int Number,
    IReadOnlyList<OrderSummaryItem> Items,
    decimal ItemTotal,
    decimal DeliveryFee,
    decimal GrandTotal,
    string? Area,
    string FormattedItemTotal,
    string FormattedDeliveryFee,
    string FormattedGrandTotal,
    string SummaryText);
