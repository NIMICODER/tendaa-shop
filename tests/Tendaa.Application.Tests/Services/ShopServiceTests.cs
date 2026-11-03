using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class ShopServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

    private ShopService CreateService() => new(CreateRepository<Shop>(), CreateUnitOfWork());

    private async Task SeedShopAsync()
    {
        Db.Shops.Add(new Shop
        {
            Id = _shopId, Name = "Lagos Kicks", Country = "Nigeria", Currency = "NGN",
            TimeZone = "Africa/Lagos", WhatsAppPhoneNumberId = "pnid-1", CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
    }

    [Fact]
    public async Task GetSettings_ReturnsShopFields()
    {
        await SeedShopAsync();

        var result = await CreateService().GetSettingsAsync(_shopId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Lagos Kicks", result.Data!.Name);
        Assert.Equal("NGN", result.Data!.Currency);
    }

    [Fact]
    public async Task GetSettings_MissingShop_ReturnsNotFound()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().GetSettingsAsync(_shopId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Update_UpdatesEditableFieldsAndPreservesPhoneNumberId()
    {
        await SeedShopAsync();

        var result = await CreateService().UpdateAsync(_shopId, new ShopSettingsInput(
            "New Name", "Nigeria", "NGN", "Africa/Lagos",
            "Always open", "Be kind", "GTBank", "0123456789", "New Name Ventures"));

        Assert.True(result.IsSuccessful);
        Assert.Equal("New Name", result.Data!.Name);
        Assert.Equal("Always open", result.Data!.HoursText);
        Assert.Equal("0123456789", result.Data!.BankAccountNumber);

        var persisted = await Db.Shops.SingleAsync(s => s.Id == _shopId);
        Assert.Equal("New Name", persisted.Name);
        Assert.Equal("pnid-1", persisted.WhatsAppPhoneNumberId);
    }

    [Fact]
    public async Task Update_EmptyName_ReturnsBadRequest()
    {
        await SeedShopAsync();

        var result = await CreateService().UpdateAsync(_shopId, new ShopSettingsInput(
            "  ", "Nigeria", "NGN", "Africa/Lagos", null, null, null, null, null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }
}
