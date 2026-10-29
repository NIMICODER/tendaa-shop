using Tendaa.Application.Common;
using Tendaa.Application.Models;

namespace Tendaa.Application.Services.Interfaces;

public interface IDeliveryService
{
    /// <summary>Always returns a successful result — an unmatched <paramref name="area"/> still
    /// returns the full zone list via <see cref="DeliveryFeeLookup.AllZones"/> so the agent can
    /// offer it, rather than a bare not-found.</summary>
    Task<TendaaResult<DeliveryFeeLookup>> GetFeeAsync(
        Guid shopId, string area, CancellationToken cancellationToken = default);
}
