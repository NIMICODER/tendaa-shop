namespace Tendaa.Application.Models;

public sealed record OrderStatusSummary(
    int Number, string Status, decimal GrandTotal, string FormattedGrandTotal, DateTimeOffset CreatedAt);
