using Tendaa.Application.Common;
using Tendaa.Application.Models;

namespace Tendaa.Application.Services.Interfaces;

public interface IProductService
{
    Task<TendaaResult<IReadOnlyList<ProductSearchItem>>> SearchAsync(
        Guid shopId, string query, CancellationToken cancellationToken = default);

    Task<TendaaResult<ProductDetails>> GetDetailsAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default);
}
