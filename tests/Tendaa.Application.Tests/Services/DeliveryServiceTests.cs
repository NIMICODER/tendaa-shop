using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class DeliveryServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

    private DeliveryService CreateService() => new(
        CreateRepository<DeliveryZone>(), CreateRepository<Shop>(), CreateUnitOfWork());

    private async Task SeedZonesAsync()
    {
        Db.DeliveryZones.AddRange(
            new DeliveryZone { Id = Guid.NewGuid(), ShopId = _shopId, Name = "Lekki", Fee = 1500m, DeliveryTimeText = "Same day" },
            new DeliveryZone { Id = Guid.NewGuid(), ShopId = _shopId, Name = "Ajah", Fee = 2500m, DeliveryTimeText = "1 to 2 business days" });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
    }

    [Fact]
    public async Task GetFeeAsync_AreaMatchesZoneName_ReturnsMatchedZoneAndAllZones()
    {
        await SeedZonesAsync();

        var result = await CreateService().GetFeeAsync(_shopId, "Lekki");

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Data!.Matched);
        Assert.Equal("Lekki", result.Data!.Matched!.Name);
        Assert.Equal(1500m, result.Data!.Matched!.Fee);
        Assert.Equal(2, result.Data!.AllZones.Count);
    }

    [Fact]
    public async Task GetFeeAsync_AreaDoesNotMatchAnyZone_ReturnsNullMatchButAllZones()
    {
        await SeedZonesAsync();

        var result = await CreateService().GetFeeAsync(_shopId, "Surulere");

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data!.Matched);
        Assert.Equal(2, result.Data!.AllZones.Count);
    }

    [Fact]
    public async Task ListZonesAsync_ReturnsAllZones()
    {
        await SeedZonesAsync();

        var result = await CreateService().ListZonesAsync(_shopId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task CreateZoneAsync_ValidInput_PersistsAndReturnsSummary()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().CreateZoneAsync(
            _shopId, new DeliveryZoneInput("Ikoyi", 1500m, "Same day"));

        Assert.True(result.IsSuccessful);
        Assert.Equal("Ikoyi", result.Data!.Name);
        Assert.Equal("₦1,500", result.Data!.FormattedFee);

        var persisted = await Db.DeliveryZones.SingleAsync(z => z.Name == "Ikoyi");
        Assert.Equal(1500m, persisted.Fee);
    }

    [Fact]
    public async Task CreateZoneAsync_EmptyName_ReturnsBadRequest()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().CreateZoneAsync(
            _shopId, new DeliveryZoneInput("  ", 1500m, null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UpdateZoneAsync_UpdatesFields()
    {
        await SeedZonesAsync();
        var zoneId = await Db.DeliveryZones.Where(z => z.Name == "Lekki").Select(z => z.Id).SingleAsync();

        var result = await CreateService().UpdateZoneAsync(
            _shopId, zoneId, new DeliveryZoneInput("Lekki Phase 1", 2000m, "Next day"));

        Assert.True(result.IsSuccessful);
        Assert.Equal("Lekki Phase 1", result.Data!.Name);
        Assert.Equal(2000m, result.Data!.Fee);
    }

    [Fact]
    public async Task UpdateZoneAsync_MissingZone_ReturnsNotFound()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().UpdateZoneAsync(
            _shopId, Guid.NewGuid(), new DeliveryZoneInput("Nowhere", 0m, null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task DeleteZoneAsync_RemovesZone()
    {
        await SeedZonesAsync();
        var zoneId = await Db.DeliveryZones.Where(z => z.Name == "Lekki").Select(z => z.Id).SingleAsync();

        var result = await CreateService().DeleteZoneAsync(_shopId, zoneId);

        Assert.True(result.IsSuccessful);
        Assert.Null(await Db.DeliveryZones.FindAsync(zoneId));
        Assert.Equal(1, await Db.DeliveryZones.CountAsync());
    }

    [Fact]
    public async Task DeleteZoneAsync_MissingZone_ReturnsNotFound()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().DeleteZoneAsync(_shopId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }
}
