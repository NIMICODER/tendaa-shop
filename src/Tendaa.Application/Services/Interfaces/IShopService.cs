using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Services.Interfaces;

public interface IShopService
{
    Task<TendaaResult<ShopInfo>> GetInfoAsync(Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>Dev-only stand-in for Phase 3's real "which shop does this WhatsApp number
    /// belong to" resolution. The MVP seeds exactly one shop, so the dev chat endpoint just needs
    /// that one.</summary>
    Task<TendaaResult<Shop>> GetOnlyShopAsync(CancellationToken cancellationToken = default);
}
