namespace Tendaa.Application.Models;

public sealed record ProductDetails(
    Guid ProductId,
    string Name,
    string? Category,
    string? Description,
    IReadOnlyList<VariantStock> Variants);
