using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Money;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class DeliveryService(IRepository<DeliveryZone> deliveryZones, IRepository<Shop> shops) : IDeliveryService
{
    public async Task<TendaaResult<DeliveryFeeLookup>> GetFeeAsync(
        Guid shopId, string area, CancellationToken cancellationToken = default)
    {
        var zones = await deliveryZones.Query()
            .Where(z => z.ShopId == shopId)
            .ToListAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

        var normalizedArea = area.Trim();
        var matched = zones.FirstOrDefault(z =>
            normalizedArea.Contains(z.Name, StringComparison.OrdinalIgnoreCase)
            || z.Name.Contains(normalizedArea, StringComparison.OrdinalIgnoreCase));

        var lookup = new DeliveryFeeLookup(
            matched is null ? null : ToSummary(matched, currency),
            zones.Select(z => ToSummary(z, currency)).ToList());

        return TendaaResult<DeliveryFeeLookup>.Success(lookup);
    }

    private static DeliveryZoneSummary ToSummary(DeliveryZone zone, string currency) =>
        new(zone.Id, zone.Name, zone.Fee, zone.DeliveryTimeText, MoneyFormatter.Format(zone.Fee, currency));
}
