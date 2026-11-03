using Tendaa.Application.Common;
using Tendaa.Application.Models;

namespace Tendaa.Application.Services.Interfaces;

public interface IProductService
{
    Task<TendaaResult<IReadOnlyList<ProductSearchItem>>> SearchAsync(
        Guid shopId, string query, CancellationToken cancellationToken = default);

    Task<TendaaResult<ProductDetails>> GetDetailsAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>The shop's whole catalog, compact enough to pass into a product-photo-match
    /// prompt (Phase 4b) — not paged, since the MVP's catalogs are small enough to list in full.</summary>
    Task<TendaaResult<IReadOnlyList<ProductCatalogEntry>>> GetCatalogAsync(
        Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: full catalog for editing (not paged — MVP catalogs are small).</summary>
    Task<TendaaResult<IReadOnlyList<ProductDashboardItem>>> ListForDashboardAsync(
        Guid shopId, CancellationToken cancellationToken = default);

    Task<TendaaResult<ProductDashboardItem>> CreateAsync(
        Guid shopId, ProductInput input, CancellationToken cancellationToken = default);

    /// <summary>Updates scalar fields and merges variants: entries with an id update in place,
    /// entries without one are added, variants missing from the input are deleted.</summary>
    Task<TendaaResult<ProductDashboardItem>> UpdateAsync(
        Guid shopId, Guid productId, ProductInput input, CancellationToken cancellationToken = default);

    Task<TendaaResult> DeleteAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default);
}
