using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Tests.Fakes;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Orders;
using Tendaa.Core.Models.Enums.Payments;

namespace Tendaa.Application.Tests.Services;

public class OrderServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly FakeDashboardNotifier _notifier = new();
    private readonly FakeCustomerMessenger _messenger = new();
    private Guid _productId;
    private Guid _variantId;

    private OrderService CreateService() => new(
        CreateRepository<Order>(),
        CreateRepository<Product>(),
        CreateRepository<Shop>(),
        CreateRepository<DeliveryZone>(),
        CreateRepository<PaymentProof>(),
        new DeliveryService(CreateRepository<DeliveryZone>(), CreateRepository<Shop>(), CreateUnitOfWork()),
        CreateUnitOfWork(),
        _notifier,
        CreateRepository<Customer>(),
        _messenger);

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

    private async Task<(Guid OrderId, Guid ProofId)> SeedPaymentReviewOrderAsync()
    {
        await SeedAsync();
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();
        var proofId = Guid.NewGuid();
        Db.Orders.Add(new Order
        {
            Id = orderId,
            ShopId = _shopId,
            CustomerId = _customerId,
            Number = 7,
            Status = OrderStatus.PaymentReview,
            ItemTotal = 25000m,
            DeliveryFee = 1500m,
            GrandTotal = 26500m,
            CustomerName = "Tolu",
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(), ShopId = _shopId, OrderId = orderId,
                    ProductVariantId = _variantId, Quantity = 1, UnitPrice = 25000m,
                },
            ],
            CreatedAt = now.AddHours(-2),
            AwaitingPaymentAt = now.AddHours(-1),
            PaymentReviewAt = now,
        });
        Db.PaymentProofs.Add(new PaymentProof
        {
            Id = proofId,
            ShopId = _shopId,
            OrderId = orderId,
            MediaId = "media-1",
            ExtractedFieldsJson = """{"Category":"receipt","Amount":26500,"BankName":"GTBank","SenderName":"Tolu","TransferDate":"2026-11-01","Reference":"ref-1","Confidence":0.95}""",
            VerificationResult = PaymentVerificationResult.Matched,
            Reasons = "",
            CreatedAt = now,
        });
        await Db.SaveChangesAsync();
        return (orderId, proofId);
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

    [Fact]
    public async Task ConfirmOrderSummary_Success_PaymentMessageHasExactAccountNumber_NoConfirmedWord()
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
        Assert.Contains("0123456789", result.Data!.PaymentMessage);
        Assert.Contains("GTBank", result.Data!.PaymentMessage);
        Assert.DoesNotContain("confirm", result.Data!.PaymentMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("₦26,500", result.Data!.FormattedGrandTotal);
    }

    [Fact]
    public async Task UpsertDraftOrder_AllKnownAndInStock_SummaryTextIsFormattedInCode()
    {
        await SeedAsync();
        var items = new List<OrderItemInput> { new(_variantId, 2) };

        var result = await CreateService().UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);

        var summaryText = result.Data!.Summary!.SummaryText;
        Assert.Contains("₦50,000", summaryText);
        Assert.Contains("₦1,500", summaryText);
        Assert.Contains("₦51,500", summaryText);
        Assert.Equal("₦50,000", result.Data!.Summary!.FormattedItemTotal);
    }

    [Fact]
    public async Task ConfirmOrderSummary_CompleteDraft_NotifiesDashboardOrdersChanged()
    {
        await SeedAsync();
        var service = CreateService();
        var items = new List<OrderItemInput> { new(_variantId, 1) };
        await service.UpsertDraftOrderAsync(_shopId, _customerId, items, "Tolu", "Lekki", null);

        var result = await service.ConfirmOrderSummaryAsync(_shopId, _customerId);

        Assert.True(result.IsSuccessful);
        var notified = Assert.Single(_notifier.OrdersChangedShopIds);
        Assert.Equal(_shopId, notified);
    }

    [Fact]
    public async Task CheckReceipt_MatchingReceipt_NotifiesDashboardOrdersChanged()
    {
        await SeedAsync();
        Db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            CustomerId = _customerId,
            Number = 7,
            Status = OrderStatus.AwaitingPayment,
            GrandTotal = 37500m,
            AwaitingPaymentAt = DateTimeOffset.UtcNow.AddHours(-1),
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
        });
        await Db.SaveChangesAsync();

        var result = await CreateService().CheckReceiptAsync(
            _shopId, _customerId, "media-1", 37500m,
            DateOnly.FromDateTime(DateTime.UtcNow), "ref-1", 0.95, "{}");

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.OrderFound);

        var persisted = await Db.Orders.FirstAsync(o => o.CustomerId == _customerId);
        Assert.Equal(OrderStatus.PaymentReview, persisted.Status);

        var notified = Assert.Single(_notifier.OrdersChangedShopIds);
        Assert.Equal(_shopId, notified);
    }

    [Fact]
    public async Task CheckReceipt_ReusedReferenceStoredAsPascalCaseJson_ReturnsMismatched()
    {
        // Regression: stored JSON uses PascalCase keys (serialized MediaAnalysisResult) — the
        // reuse check must still find the reference. A case-sensitive lookup silently returned
        // no references, so reused receipts verified as Matched.
        var now = DateTimeOffset.UtcNow;
        await SeedAsync();
        var matchedOrderId = Guid.NewGuid();
        Db.Orders.Add(new Order
        {
            Id = matchedOrderId, ShopId = _shopId, CustomerId = _customerId, Number = 7,
            Status = OrderStatus.PaymentReview, GrandTotal = 26500m,
            CreatedAt = now.AddHours(-3), AwaitingPaymentAt = now.AddHours(-2),
        });
        Db.PaymentProofs.Add(new PaymentProof
        {
            Id = Guid.NewGuid(), ShopId = _shopId, OrderId = matchedOrderId, MediaId = "media-old",
            ExtractedFieldsJson = """{"Category":"receipt","Amount":26500,"Reference":"ref-9","Confidence":0.9}""",
            VerificationResult = PaymentVerificationResult.Matched,
            CreatedAt = now.AddHours(-2),
        });
        Db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), ShopId = _shopId, CustomerId = _customerId, Number = 8,
            Status = OrderStatus.AwaitingPayment, GrandTotal = 26500m,
            CreatedAt = now.AddHours(-1), AwaitingPaymentAt = now.AddHours(-1),
        });
        await Db.SaveChangesAsync();

        var result = await CreateService().CheckReceiptAsync(
            _shopId, _customerId, "media-new", 26500m,
            DateOnly.FromDateTime(DateTime.UtcNow), "ref-9", 0.95, "{}");

        Assert.True(result.IsSuccessful);
        Assert.True(result.Data!.OrderFound);
        Assert.Equal(8, result.Data!.OrderNumber);
        Assert.Equal(PaymentVerificationResult.Mismatched, result.Data!.VerificationResult);
        Assert.Contains("already used", result.Data!.Reasons ?? string.Empty);

        var stillWaiting = await Db.Orders.FirstAsync(o => o.Number == 8);
        Assert.Equal(OrderStatus.AwaitingPayment, stillWaiting.Status);
    }

    [Fact]
    public async Task ConfirmPayment_MatchedProof_ConfirmsDecrementsStockNotifiesAndMessages()
    {
        var (orderId, proofId) = await SeedPaymentReviewOrderAsync();

        var result = await CreateService().ConfirmPaymentAsync(_shopId, proofId, "Seller");

        Assert.True(result.IsSuccessful);
        Assert.Equal(OrderStatus.Confirmed, result.Data!.Status);
        Assert.Equal("Seller", result.Data!.ConfirmedBy);

        var persisted = await Db.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Confirmed, persisted.Status);
        Assert.Equal("Seller", persisted.ConfirmedBy);

        var proof = await Db.PaymentProofs.FirstAsync(p => p.Id == proofId);
        Assert.Equal("Seller", proof.ReviewedBy);

        var variant = await Db.Products.SelectMany(p => p.Variants).FirstAsync(v => v.Id == _variantId);
        Assert.Equal(1, variant.Stock);

        var notified = Assert.Single(_notifier.OrdersChangedShopIds);
        Assert.Equal(_shopId, notified);

        var sent = Assert.Single(_messenger.Sent);
        Assert.Contains("Payment confirmed", sent.Text);
        Assert.Contains("#7", sent.Text);
    }

    [Fact]
    public async Task ConfirmPayment_WrongStatus_ReturnsBadRequest()
    {
        await SeedAsync();
        var orderId = Guid.NewGuid();
        var proofId = Guid.NewGuid();
        Db.Orders.Add(new Order
        {
            Id = orderId, ShopId = _shopId, CustomerId = _customerId, Number = 7,
            Status = OrderStatus.AwaitingPayment, GrandTotal = 26500m, CreatedAt = DateTimeOffset.UtcNow,
        });
        Db.PaymentProofs.Add(new PaymentProof
        {
            Id = proofId, ShopId = _shopId, OrderId = orderId, MediaId = "media-1",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        var result = await CreateService().ConfirmPaymentAsync(_shopId, proofId, "Seller");

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ConfirmPayment_MissingProof_ReturnsNotFound()
    {
        await SeedAsync();

        var result = await CreateService().ConfirmPaymentAsync(_shopId, Guid.NewGuid(), "Seller");

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task RejectPayment_MatchedProof_ReturnsToAwaitingPaymentWithReason()
    {
        var (orderId, proofId) = await SeedPaymentReviewOrderAsync();

        var result = await CreateService().RejectPaymentAsync(_shopId, proofId, "amount too low", "Seller");

        Assert.True(result.IsSuccessful);
        Assert.Equal(OrderStatus.AwaitingPayment, result.Data!.Status);

        var persisted = await Db.Orders.FirstAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.AwaitingPayment, persisted.Status);

        var proof = await Db.PaymentProofs.FirstAsync(p => p.Id == proofId);
        Assert.Equal("Seller", proof.ReviewedBy);
        Assert.Contains("amount too low", proof.Reasons ?? string.Empty);

        var sent = Assert.Single(_messenger.Sent);
        Assert.Contains("amount too low", sent.Text);
    }

    [Fact]
    public async Task RejectPayment_EmptyReason_ReturnsBadRequest()
    {
        var (_, proofId) = await SeedPaymentReviewOrderAsync();

        var result = await CreateService().RejectPaymentAsync(_shopId, proofId, "  ", "Seller");

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatusAndPages()
    {
        await SeedAsync();
        var now = DateTimeOffset.UtcNow;
        Db.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), ShopId = _shopId, CustomerId = _customerId, Number = 1, Status = OrderStatus.Draft, GrandTotal = 1000m, CustomerName = "A", CreatedAt = now },
            new Order { Id = Guid.NewGuid(), ShopId = _shopId, CustomerId = _customerId, Number = 2, Status = OrderStatus.PaymentReview, GrandTotal = 2000m, CustomerName = "B", CreatedAt = now },
            new Order { Id = Guid.NewGuid(), ShopId = _shopId, CustomerId = _customerId, Number = 3, Status = OrderStatus.PaymentReview, GrandTotal = 3000m, CustomerName = "C", CreatedAt = now });
        await Db.SaveChangesAsync();

        var page1 = await CreateService().ListAsync(_shopId, OrderStatus.PaymentReview, page: 1, pageSize: 1);

        Assert.True(page1.IsSuccessful);
        Assert.Equal(2, page1.Data!.TotalCount);
        Assert.Equal(3, Assert.Single(page1.Data!.Items).Number); // newest (highest number) first

        var page2 = await CreateService().ListAsync(_shopId, OrderStatus.PaymentReview, page: 2, pageSize: 1);
        Assert.Equal(2, Assert.Single(page2.Data!.Items).Number);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsItemsAndParsedProofs()
    {
        var (orderId, proofId) = await SeedPaymentReviewOrderAsync();

        var result = await CreateService().GetDetailAsync(_shopId, orderId);

        Assert.True(result.IsSuccessful);
        var detail = result.Data!;
        Assert.Equal(7, detail.Number);
        Assert.Equal("Tolu", detail.CustomerName);
        Assert.Null(detail.CustomerPhone);

        var item = Assert.Single(detail.Items);
        Assert.Equal("Classic Low Sneakers", item.ProductName);
        Assert.Equal("₦25,000", item.FormattedLineTotal);

        var proof = Assert.Single(detail.PaymentProofs);
        Assert.Equal(proofId, proof.PaymentProofId);
        Assert.Equal("/api/media/media-1", proof.MediaUrl);
        Assert.Equal(26500m, proof.ExtractedAmount);
        Assert.Equal("₦26,500", proof.FormattedExtractedAmount);
        Assert.Equal("GTBank", proof.BankName);
        Assert.Equal("ref-1", proof.Reference);
        Assert.Equal(PaymentVerificationResult.Matched, proof.VerificationResult);
    }

    [Fact]
    public async Task GetStatusCountsAsync_ReturnsEveryStatus()
    {
        await SeedPaymentReviewOrderAsync();

        var result = await CreateService().GetStatusCountsAsync(_shopId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(Enum.GetValues<OrderStatus>().Length, result.Data!.Count);
        Assert.Equal(1, result.Data!.Single(c => c.Status == OrderStatus.PaymentReview).Count);
        Assert.Equal(1, result.Data!.Sum(c => c.Count));
    }
}
