using Tendaa.Application.Common;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Services.Interfaces;

public interface ICustomerService
{
    /// <summary>Finds the customer by (ShopId, BsuId), or creates one. Updates
    /// <paramref name="phone"/>/<paramref name="username"/>/<paramref name="name"/> only when a
    /// non-null value is passed — never clears a previously known value. See ARCHITECTURE.md's
    /// "Customer identity" section.</summary>
    Task<TendaaResult<Customer>> FindOrCreateByBsuIdAsync(
        Guid shopId,
        string bsuId,
        string? phone = null,
        string? username = null,
        string? name = null,
        CancellationToken cancellationToken = default);
}
