using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class ProductServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

    private ProductService CreateService() => new(
        CreateRepository<Product>(), CreateRepository<Shop>(), CreateUnitOfWork());

    private async Task SeedShopAsync()
    {
        Db.Shops.Add(new Shop
        {
            Id = _shopId, Name = "Lagos Kicks", Country = "Nigeria", Currency = "NGN",
            TimeZone = "Africa/Lagos", CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
    }

    [Fact]
    public async Task Create_ValidInput_PersistsAndFormats()
    {
        await SeedShopAsync();

        var result = await CreateService().CreateAsync(_shopId, new ProductInput(
            "Sneakers", "Shoes", null, 25000m, null,
            [new VariantInput(null, "42", "Black", 4, null)]));

        Assert.True(result.IsSuccessful);
        Assert.Equal("Sneakers", result.Data!.Name);
        Assert.Equal("₦25,000", result.Data!.FormattedPrice);
        var variant = Assert.Single(result.Data!.Variants);
        Assert.Equal(4, variant.Stock);
        Assert.Equal("₦25,000", variant.FormattedEffectivePrice);

        Assert.Equal(1, await Db.Products.CountAsync());
    }

    [Fact]
    public async Task Create_EmptyName_ReturnsBadRequest()
    {
        await SeedShopAsync();

        var result = await CreateService().CreateAsync(
            _shopId, new ProductInput("  ", null, null, 100m, null, []));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Create_NegativePrice_ReturnsBadRequest()
    {
        await SeedShopAsync();

        var result = await CreateService().CreateAsync(
            _shopId, new ProductInput("Sneakers", null, null, -5m, null, []));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ListForDashboard_ReturnsProductsWithVariants()
    {
        await SeedShopAsync();
        var service = CreateService();
        await service.CreateAsync(_shopId, new ProductInput(
            "Sneakers", null, null, 25000m, null, [new VariantInput(null, "42", "Black", 4, null)]));
        await service.CreateAsync(_shopId, new ProductInput(
            "Bag", null, null, 18000m, null, []));

        var result = await service.ListForDashboardAsync(_shopId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal("Bag", result.Data![0].Name); // alphabetical
        Assert.Single(result.Data!.Single(p => p.Name == "Sneakers").Variants);
    }

    [Fact]
    public async Task Update_MergesVariants()
    {
        await SeedShopAsync();
        var service = CreateService();
        var created = await service.CreateAsync(_shopId, new ProductInput(
            "Sneakers", null, null, 25000m, null,
            [
                new VariantInput(null, "42", "Black", 4, null),
                new VariantInput(null, "43", "Black", 2, null),
            ]));
        var keepId = created.Data!.Variants.Single(v => v.Size == "42").VariantId;

        var result = await service.UpdateAsync(_shopId, created.Data!.ProductId, new ProductInput(
            "Sneakers", "Shoes", null, 26000m, null,
            [
                new VariantInput(keepId, "42", "Black", 10, null), // stock updated
                new VariantInput(null, "44", "White", 3, 27000m), // added with override
            ])); // 43 removed

        Assert.True(result.IsSuccessful);
        Assert.Equal("Shoes", result.Data!.Category);
        Assert.Equal(26000m, result.Data!.Price);
        Assert.Equal(2, result.Data!.Variants.Count);

        var kept = result.Data!.Variants.Single(v => v.VariantId == keepId);
        Assert.Equal(10, kept.Stock);

        var added = result.Data!.Variants.Single(v => v.Size == "44");
        Assert.Equal(27000m, added.EffectivePrice);
        Assert.Equal("₦27,000", added.FormattedEffectivePrice);

        Assert.Equal(2, await Db.ProductVariants.CountAsync());
    }

    [Fact]
    public async Task Update_MissingProduct_ReturnsNotFound()
    {
        await SeedShopAsync();

        var result = await CreateService().UpdateAsync(
            _shopId, Guid.NewGuid(), new ProductInput("Sneakers", null, null, 1m, null, []));

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesProductAndVariants()
    {
        await SeedShopAsync();
        var created = await CreateService().CreateAsync(_shopId, new ProductInput(
            "Sneakers", null, null, 25000m, null, [new VariantInput(null, "42", "Black", 4, null)]));

        var result = await CreateService().DeleteAsync(_shopId, created.Data!.ProductId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, await Db.Products.CountAsync());
        Assert.Equal(0, await Db.ProductVariants.CountAsync());
    }

    [Fact]
    public async Task Delete_MissingProduct_ReturnsNotFound()
    {
        await SeedShopAsync();

        var result = await CreateService().DeleteAsync(_shopId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }
}
