using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class DeliveryService(IRepository<DeliveryZone> deliveryZones) : IDeliveryService
{
    public async Task<TendaaResult<DeliveryFeeLookup>> GetFeeAsync(
        Guid shopId, string area, CancellationToken cancellationToken = default)
    {
        var zones = await deliveryZones.Query()
            .Where(z => z.ShopId == shopId)
            .ToListAsync(cancellationToken);

        var normalizedArea = area.Trim();
        var matched = zones.FirstOrDefault(z =>
            normalizedArea.Contains(z.Name, StringComparison.OrdinalIgnoreCase)
            || z.Name.Contains(normalizedArea, StringComparison.OrdinalIgnoreCase));

        var lookup = new DeliveryFeeLookup(
            matched is null ? null : ToSummary(matched),
            zones.Select(ToSummary).ToList());

        return TendaaResult<DeliveryFeeLookup>.Success(lookup);
    }

    private static DeliveryZoneSummary ToSummary(DeliveryZone zone) =>
        new(zone.Id, zone.Name, zone.Fee, zone.DeliveryTimeText);
}
