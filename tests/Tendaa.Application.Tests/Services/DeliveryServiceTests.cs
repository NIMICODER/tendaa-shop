using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class DeliveryServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

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
        var service = new DeliveryService(CreateRepository<DeliveryZone>(), CreateRepository<Shop>());

        var result = await service.GetFeeAsync(_shopId, "Lekki");

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
        var service = new DeliveryService(CreateRepository<DeliveryZone>(), CreateRepository<Shop>());

        var result = await service.GetFeeAsync(_shopId, "Surulere");

        Assert.True(result.IsSuccessful);
        Assert.Null(result.Data!.Matched);
        Assert.Equal(2, result.Data!.AllZones.Count);
    }
}
