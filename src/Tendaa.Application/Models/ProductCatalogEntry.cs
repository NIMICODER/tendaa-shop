namespace Tendaa.Application.Models;

/// <summary>One row of the shop's catalog, compact enough to list in a model prompt — not the
/// full <c>ProductDetails</c> shape used elsewhere. See <c>IProductService.GetCatalogAsync</c>
/// and <c>Tendaa.Agent.Media.IMediaAnalyzer.MatchProductPhotoAsync</c> (Phase 4b).</summary>
public sealed record ProductCatalogEntry(Guid ProductId, string Name, string? Category, IReadOnlyList<string> Colours);
