namespace Tendaa.Application.Models;

public sealed record ProductSearchItem(
    Guid ProductId,
    string Name,
    string? Category,
    IReadOnlyList<VariantStock> Variants);
