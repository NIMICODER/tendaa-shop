namespace Tendaa.Application.Models;

public sealed record VariantDashboardItem(
    Guid VariantId,
    string? Size,
    string? Colour,
    int Stock,
    decimal EffectivePrice,
    string FormattedEffectivePrice);

public sealed record ProductDashboardItem(
    Guid ProductId,
    string Name,
    string? Category,
    string? Description,
    decimal Price,
    string FormattedPrice,
    string? PhotoUrl,
    IReadOnlyList<VariantDashboardItem> Variants);

public sealed record VariantInput(
    Guid? VariantId,
    string? Size,
    string? Colour,
    int Stock,
    decimal? PriceOverride);

public sealed record ProductInput(
    string Name,
    string? Category,
    string? Description,
    decimal Price,
    string? PhotoUrl,
    IReadOnlyList<VariantInput> Variants);
