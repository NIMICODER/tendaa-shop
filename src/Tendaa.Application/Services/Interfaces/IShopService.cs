using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Services.Interfaces;

public interface IShopService
{
    Task<TendaaResult<ShopInfo>> GetInfoAsync(Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>Dev-only stand-in for <see cref="GetByPhoneNumberIdAsync"/>, used by the dev chat
    /// endpoint. The MVP seeds exactly one shop, so it just needs that one.</summary>
    Task<TendaaResult<Shop>> GetOnlyShopAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves which shop an inbound WhatsApp webhook belongs to, by matching
    /// <see cref="Shop.WhatsAppPhoneNumberId"/> against the webhook payload's
    /// <c>metadata.phone_number_id</c>. <see cref="TendaaResult{T}.NotFound"/> if no shop is
    /// configured with that number.</summary>
    Task<TendaaResult<Shop>> GetByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default);
}
