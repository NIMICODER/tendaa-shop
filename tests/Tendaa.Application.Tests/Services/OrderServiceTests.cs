using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Application.Tests.Services;

public class OrderServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private Guid _productId;
    private Guid _variantId;

    private OrderService CreateService() => new(
        CreateRepository<Order>(),
        CreateRepository<Product>(),
        CreateRepository<Shop>(),
        CreateRepository<DeliveryZone>(),
        new DeliveryService(CreateRepository<DeliveryZone>()),
        CreateUnitOfWork());

    private async Task SeedAsync()
    {
        Db.Shops.Add(new Shop
        {
            Id = _shopId,
            Name = "Lagos Kicks",
            Country = "Nigeria",
            Currency = "NGN",
            TimeZone = "Africa/Lagos",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        Db.Customers.Add(new Customer { Id = _customerId, ShopId = _shopId, BsuId = "bsuid-1", CreatedAt = DateTimeOffset.UtcNow });

        _productId = Guid.NewGuid();
        _variantId = Guid.NewGuid();
        Db.Products.Add(new Product
        {
            Id = _productId,
            ShopId = _shopId,
            Name = "Classic Low Sneakers",
            Price = 25000m,
            Variants = [new ProductVariant { Id = _variantId, ShopId = _shopId, Size = "42", Colour = "Black", Stock = 2 }],
        });
        Db.DeliveryZones.Add(new DeliveryZone { Id = Guid.NewGuid(), ShopId = _shopId, Name = "Lekki", Fee = 1500m, DeliveryTimeText = "Same day" });

        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
    }

    [Fact]
    public async Task UpsertDraftOrder_MissingCustomerNameAndArea_ReportsMissingFields_NoSummary()
    {
        await SeedAsync();
        var items = new List<OrderItemInput> { new(_variantId, 1) };

        var result = await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, null, null, null);

        Assert.True(result.IsSuccessful);
        Assert.Contains("customerName", result.Data!.MissingFields);
        Assert.Contains("area", result.Data!.MissingFields);
        Assert.Null(result.Data!.Summary);
    }

    [Fact]
    public async Task UpsertDraftOrder_RequestedVariantOutOfStock_ReportsStockIssueWithAlternative()
    {
        await SeedAsync();
        var otherVariantId = Guid.NewGuid();
        var product = await Db.Products.FindAsync(_productId);
        product!.Variants.Add(new ProductVariant { Id = otherVariantId, ShopId = _shopId, Size = "43", Colour = "Black", Stock = 5 });
        await Db.SaveChangesAsync();

        var items = new List<OrderItemInput> { new(_variantId, 10) }; // only 2 in stock

        var result = await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);

        Assert.True(result.IsSuccessful);
        var issue = Assert.Single(result.Data!.StockIssues);
        Assert.Equal(_variantId, issue.RequestedProductVariantId);
        var alternative = Assert.Single(issue.Alternatives);
        Assert.Equal(otherVariantId, alternative.ProductVariantId);
        Assert.Null(result.Data!.Summary);
    }

    [Fact]
    public async Task UpsertDraftOrder_AllKnownAndInStock_ComputesSummaryAndPersistsItems()
    {
        await SeedAsync();
        var items = new List<OrderItemInput> { new(_variantId, 2) };

        var result = await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);

        Assert.True(result.IsSuccessful);
        Assert.Empty(result.Data!.MissingFields);
        Assert.Empty(result.Data!.StockIssues);
        Assert.NotNull(result.Data!.Summary);
        Assert.Equal(50000m, result.Data!.Summary!.ItemTotal);
        Assert.Equal(1500m, result.Data!.Summary!.DeliveryFee);
        Assert.Equal(51500m, result.Data!.Summary!.GrandTotal);

        var persisted = await Db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == result.Data!.OrderId);
        Assert.Single(persisted.Items);
    }

    [Fact]
    public async Task UpsertDraftOrder_AreaNotResentOnSecondCall_KeepsPreviouslyMatchedZone()
    {
        await SeedAsync();
        var service = CreateService();
        var items = new List<OrderItemInput> { new(_variantId, 1) };

        await service.UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);
        var second = await service.UpsertDraftOrderAsync(_shopId, _customerId, items, null, null, null);

        Assert.DoesNotContain("area", second.Data!.MissingFields);
        Assert.NotNull(second.Data!.Summary);
        Assert.Equal(1500m, second.Data!.Summary!.DeliveryFee);
    }

    [Fact]
    public async Task ConfirmOrderSummary_NoDeliveryZoneYet_ReturnsBadRequest()
    {
        await SeedAsync();
        var items = new List<OrderItemInput> { new(_variantId, 1) };
        await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", null, null);

        var result = await CreateService().ConfirmOrderSummaryAsync(_shopId, _customerId);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ConfirmOrderSummary_CompleteDraft_MovesToAwaitingPaymentAndReturnsBankDetails()
    {
        await SeedAsync();
        var shop = await Db.Shops.FindAsync(_shopId);
        shop!.BankName = "GTBank";
        shop.BankAccountNumber = "0123456789";
        shop.BankAccountName = "Lagos Kicks Ventures";
        await Db.SaveChangesAsync();

        var items = new List<OrderItemInput> { new(_variantId, 1) };
        await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);

        var result = await CreateService().ConfirmOrderSummaryAsync(_shopId, _customerId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("GTBank", result.Data!.BankName);

        var persisted = await Db.Orders.FirstAsync(o => o.CustomerId == _customerId);
        Assert.Equal(OrderStatus.AwaitingPayment, persisted.Status);
        Assert.NotNull(persisted.AwaitingPaymentAt);
    }
}
