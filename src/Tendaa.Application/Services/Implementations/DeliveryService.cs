using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Money;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class DeliveryService(
    IRepository<DeliveryZone> deliveryZones, IRepository<Shop> shops, IUnitOfWork unitOfWork) : IDeliveryService
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

    public async Task<TendaaResult<IReadOnlyList<DeliveryZoneSummary>>> ListZonesAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var zones = await deliveryZones.Query()
            .Where(z => z.ShopId == shopId)
            .ToListAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

        IReadOnlyList<DeliveryZoneSummary> items = zones
            .OrderBy(z => z.Name)
            .Select(z => ToSummary(z, currency))
            .ToList();

        return TendaaResult<IReadOnlyList<DeliveryZoneSummary>>.Success(items);
    }

    public async Task<TendaaResult<DeliveryZoneSummary>> CreateZoneAsync(
        Guid shopId, DeliveryZoneInput input, CancellationToken cancellationToken = default)
    {
        if (Validate(input) is { } error)
        {
            return TendaaResult<DeliveryZoneSummary>.BadRequest(error);
        }

        var zone = new DeliveryZone
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Name = input.Name.Trim(),
            Fee = input.Fee,
            DeliveryTimeText = Clean(input.DeliveryTimeText) ?? "",
        };

        await deliveryZones.AddAsync(zone, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        return TendaaResult<DeliveryZoneSummary>.Success(ToSummary(zone, shop?.Currency ?? "NGN"));
    }

    public async Task<TendaaResult<DeliveryZoneSummary>> UpdateZoneAsync(
        Guid shopId, Guid zoneId, DeliveryZoneInput input, CancellationToken cancellationToken = default)
    {
        if (Validate(input) is { } error)
        {
            return TendaaResult<DeliveryZoneSummary>.BadRequest(error);
        }

        var zone = await deliveryZones.Query()
            .FirstOrDefaultAsync(z => z.ShopId == shopId && z.Id == zoneId, cancellationToken);
        if (zone is null)
        {
            return TendaaResult<DeliveryZoneSummary>.NotFound();
        }

        zone.Name = input.Name.Trim();
        zone.Fee = input.Fee;
        zone.DeliveryTimeText = Clean(input.DeliveryTimeText) ?? "";

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        return TendaaResult<DeliveryZoneSummary>.Success(ToSummary(zone, shop?.Currency ?? "NGN"));
    }

    public async Task<TendaaResult> DeleteZoneAsync(
        Guid shopId, Guid zoneId, CancellationToken cancellationToken = default)
    {
        var zone = await deliveryZones.Query()
            .FirstOrDefaultAsync(z => z.ShopId == shopId && z.Id == zoneId, cancellationToken);
        if (zone is null)
        {
            return TendaaResult.NotFound();
        }

        deliveryZones.Remove(zone);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TendaaResult.Success();
    }

    private static string? Validate(DeliveryZoneInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Zone name is required.";
        }

        if (input.Fee < 0)
        {
            return "Fee cannot be negative.";
        }

        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DeliveryZoneSummary ToSummary(DeliveryZone zone, string currency) =>
        new(zone.Id, zone.Name, zone.Fee, zone.DeliveryTimeText, MoneyFormatter.Format(zone.Fee, currency));
}
