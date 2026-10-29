namespace Tendaa.Application.Models;

public sealed record VariantStock(
    Guid ProductVariantId, string? Size, string? Colour, int Stock, decimal EffectivePrice, string FormattedEffectivePrice);
