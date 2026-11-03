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

    /// <summary>Dashboard: all delivery zones.</summary>
    Task<TendaaResult<IReadOnlyList<DeliveryZoneSummary>>> ListZonesAsync(
        Guid shopId, CancellationToken cancellationToken = default);

    Task<TendaaResult<DeliveryZoneSummary>> CreateZoneAsync(
        Guid shopId, DeliveryZoneInput input, CancellationToken cancellationToken = default);

    Task<TendaaResult<DeliveryZoneSummary>> UpdateZoneAsync(
        Guid shopId, Guid zoneId, DeliveryZoneInput input, CancellationToken cancellationToken = default);

    Task<TendaaResult> DeleteZoneAsync(
        Guid shopId, Guid zoneId, CancellationToken cancellationToken = default);
}
