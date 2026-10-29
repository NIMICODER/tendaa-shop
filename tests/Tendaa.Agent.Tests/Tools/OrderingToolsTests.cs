using System.Text.Json;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tests.Fakes;
using Tendaa.Agent.Tools;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Agent.Tests.Tools;

public class OrderingToolsTests
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly Guid _conversationId = Guid.NewGuid();

    private readonly FakeProductService _products = new();
    private readonly FakeDeliveryService _delivery = new();
    private readonly FakeShopService _shop = new();
    private readonly FakeOrderService _orders = new();
    private readonly FakeHandoffService _handoffs = new();
    private readonly FakeCustomerService _customers = new();
    private readonly FakeWhatsAppSender _sender = new();
    private readonly PendingPaymentMessage _pendingPayment = new();
    private readonly AgentRequestContext _context = new();
    private readonly ToolTraceCollector _trace = new();

    private OrderingTools CreateTools()
    {
        _context.SetIdentity(_shopId, _customerId, "+2340000000", _conversationId);
        return new OrderingTools(
            _products, _delivery, _shop, _orders, _handoffs, _customers, _sender, _pendingPayment, _context, _trace);
    }

    [Fact]
    public async Task SearchProducts_ReadsIdentityFromContext_NotFromArguments()
    {
        var item = new ProductSearchItem(Guid.NewGuid(), "Classic Low Sneakers", "Sneakers", []);
        _products.SearchResult = TendaaResult<IReadOnlyList<ProductSearchItem>>.Success([item]);

        var result = await CreateTools().SearchProducts("sneakers");

        Assert.Equal(_shopId, _products.LastShopId);
        Assert.Equal("sneakers", _products.LastQuery);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<ProductSearchItem>>(result);
        Assert.Same(item, returned[0]);
    }

    [Fact]
    public async Task GetProductDetails_Found_ReturnsDetails()
    {
        var details = new ProductDetails(Guid.NewGuid(), "Classic Low Sneakers", "Sneakers", null, [], null);
        _products.DetailsResult = TendaaResult<ProductDetails>.Success(details);

        var result = await CreateTools().GetProductDetails(details.ProductId);

        Assert.Same(details, result);
    }

    [Fact]
    public async Task GetProductDetails_NotFound_ReturnsNotFoundLiteral()
    {
        _products.DetailsResult = TendaaResult<ProductDetails>.NotFound();

        var result = await CreateTools().GetProductDetails(Guid.NewGuid());

        Assert.Equal("NOT_FOUND", result);
    }

    [Fact]
    public async Task GetDeliveryFee_PassesShopIdAndArea()
    {
        var lookup = new DeliveryFeeLookup(new DeliveryZoneSummary(Guid.NewGuid(), "Lekki", 1500m, "Same day", "₦1,500"), []);
        _delivery.FeeResult = TendaaResult<DeliveryFeeLookup>.Success(lookup);

        var result = await CreateTools().GetDeliveryFee("Lekki");

        Assert.Equal(_shopId, _delivery.LastShopId);
        Assert.Equal("Lekki", _delivery.LastArea);
        Assert.Same(lookup, result);
    }

    [Fact]
    public async Task GetShopInfo_Found_ReturnsInfo()
    {
        var info = new ShopInfo("Mon-Sat 9-7", "No returns after 48h", "GTBank", "012345", "Lagos Kicks");
        _shop.InfoResult = TendaaResult<ShopInfo>.Success(info);

        var result = await CreateTools().GetShopInfo();

        Assert.Equal(_shopId, _shop.LastShopId);
        Assert.Same(info, result);
    }

    [Fact]
    public async Task GetShopInfo_NotFound_ReturnsNotFoundLiteral()
    {
        _shop.InfoResult = TendaaResult<ShopInfo>.NotFound();

        var result = await CreateTools().GetShopInfo();

        Assert.Equal("NOT_FOUND", result);
    }

    [Fact]
    public async Task UpsertDraftOrder_MapsItemsAndPassesIdentity()
    {
        var variantId = Guid.NewGuid();
        var summary = new OrderSummary(1042, [], 25000m, 1500m, 26500m, "Lekki", "₦25,000", "₦1,500", "₦26,500", "Order #1042");
        _orders.UpsertResult = TendaaResult<UpsertDraftOrderResult>.Success(
            new UpsertDraftOrderResult(Guid.NewGuid(), [], [], summary));

        var result = await CreateTools().UpsertDraftOrder(
            [new OrderItemArg(variantId, 2)], "Tolu", "Lekki", null);

        Assert.Equal(_shopId, _orders.LastShopId);
        Assert.Equal(_customerId, _orders.LastCustomerId);
        Assert.Equal("Tolu", _orders.LastCustomerName);
        Assert.Equal("Lekki", _orders.LastArea);
        var mappedItem = Assert.Single(_orders.LastItems!);
        Assert.Equal(variantId, mappedItem.ProductVariantId);
        Assert.Equal(2, mappedItem.Quantity);

        var json = JsonSerializer.Serialize(result);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(1042, parsed.RootElement.GetProperty("summary").GetProperty("Number").GetInt32());
    }

    [Fact]
    public async Task GetOrderSummary_Found_ReturnsSummary()
    {
        var summary = new OrderSummary(1042, [], 25000m, 1500m, 26500m, "Lekki", "₦25,000", "₦1,500", "₦26,500", "Order #1042");
        _orders.SummaryResult = TendaaResult<OrderSummary>.Success(summary);

        var result = await CreateTools().GetOrderSummary();

        Assert.Same(summary, result);
    }

    [Fact]
    public async Task GetOrderSummary_NotFound_ReturnsNotFoundLiteral()
    {
        _orders.SummaryResult = TendaaResult<OrderSummary>.NotFound();

        var result = await CreateTools().GetOrderSummary();

        Assert.Equal("NOT_FOUND", result);
    }

    [Fact]
    public async Task ConfirmOrderSummary_Success_ReturnsBankDetails()
    {
        var confirm = new ConfirmOrderResult(
            1042, 26500m, "GTBank", "012345", "Lagos Kicks", "₦26,500",
            "Order #1042 - ₦26,500\nPay to: GTBank, 012345 (Lagos Kicks)\nSend your receipt here once you've paid.");
        _orders.ConfirmResult = TendaaResult<ConfirmOrderResult>.Success(confirm);

        var result = await CreateTools().ConfirmOrderSummary();

        Assert.Same(confirm, result);
    }

    [Fact]
    public async Task ConfirmOrderSummary_IncompleteDraft_ReturnsLiteral()
    {
        _orders.ConfirmResult = TendaaResult<ConfirmOrderResult>.BadRequest();

        var result = await CreateTools().ConfirmOrderSummary();

        Assert.Equal("NO_COMPLETE_DRAFT_ORDER", result);
    }

    [Fact]
    public async Task GetMyOrders_PassesCustomerIdFromContext_NeverFromArguments()
    {
        var status = new OrderStatusSummary(1041, "Delivered", 18000m, "₦18,000", DateTimeOffset.UtcNow);
        _orders.MyOrdersResult = TendaaResult<IReadOnlyList<OrderStatusSummary>>.Success([status]);

        var result = await CreateTools().GetMyOrders();

        Assert.Equal(_customerId, _orders.LastCustomerId);
        var returned = Assert.IsAssignableFrom<IReadOnlyList<OrderStatusSummary>>(result);
        Assert.Same(status, returned[0]);
    }

    [Fact]
    public async Task RequestHandoff_PassesConversationIdFromContext_AndReturnsOk()
    {
        var result = await CreateTools().RequestHandoff("refund_request", "Customer wants a refund for a damaged item.");

        Assert.Equal(_shopId, _handoffs.LastShopId);
        Assert.Equal(_conversationId, _handoffs.LastConversationId);
        Assert.Equal("refund_request", _handoffs.LastReason);
        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task SendProductImage_ProductHasPhoto_SendsImageToCustomer()
    {
        var productId = Guid.NewGuid();
        var customer = new Customer { Id = _customerId, ShopId = _shopId, BsuId = "bsuid-1", PhoneNumber = "+2340000000" };
        _products.DetailsResult = TendaaResult<ProductDetails>.Success(
            new ProductDetails(productId, "Classic Low Sneakers", "Sneakers", null, [], "https://example.com/photo.jpg"));
        _customers.GetByIdResult = TendaaResult<Customer>.Success(customer);

        var result = await CreateTools().SendProductImage(productId);

        Assert.Equal("OK", result);
        var sent = Assert.Single(_sender.SentImages);
        Assert.Same(customer, sent.Customer);
        Assert.Equal("https://example.com/photo.jpg", sent.ImageUrl);
        Assert.Equal("Classic Low Sneakers", sent.Caption);
    }

    [Fact]
    public async Task SendProductImage_ProductHasNoPhoto_ReturnsLiteral_SendsNothing()
    {
        _products.DetailsResult = TendaaResult<ProductDetails>.Success(
            new ProductDetails(Guid.NewGuid(), "Canvas High-Tops", "Sneakers", null, [], null));

        var result = await CreateTools().SendProductImage(Guid.NewGuid());

        Assert.Equal("NO_PHOTO_AVAILABLE", result);
        Assert.Empty(_sender.SentImages);
    }

    [Fact]
    public async Task SendProductImage_ProductNotFound_ReturnsNotFoundLiteral()
    {
        _products.DetailsResult = TendaaResult<ProductDetails>.NotFound();

        var result = await CreateTools().SendProductImage(Guid.NewGuid());

        Assert.Equal("NOT_FOUND", result);
        Assert.Empty(_sender.SentImages);
    }

    [Fact]
    public async Task GetCustomerProfile_Found_ReturnsName()
    {
        var customer = new Customer { Id = _customerId, ShopId = _shopId, BsuId = "bsuid-1", Name = "David" };
        _customers.GetByIdResult = TendaaResult<Customer>.Success(customer);

        var result = await CreateTools().GetCustomerProfile();

        var profile = Assert.IsType<CustomerProfile>(result);
        Assert.Equal("David", profile.Name);
    }

    [Fact]
    public async Task GetCustomerProfile_NotFound_ReturnsNotFoundLiteral()
    {
        _customers.GetByIdResult = TendaaResult<Customer>.NotFound();

        var result = await CreateTools().GetCustomerProfile();

        Assert.Equal("NOT_FOUND", result);
    }

    [Fact]
    public async Task SetCustomerName_PassesShopIdCustomerIdAndName()
    {
        var result = await CreateTools().SetCustomerName("David");

        Assert.Equal(_shopId, _customers.LastShopId);
        Assert.Equal(_customerId, _customers.LastCustomerId);
        Assert.Equal("David", _customers.LastUpdatedName);
        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task EveryToolCall_RecordsOneTraceEntry()
    {
        var tools = CreateTools();

        await tools.GetShopInfo();
        await tools.GetMyOrders();

        Assert.Equal(2, _trace.Entries.Count);
        Assert.Equal(nameof(OrderingTools.GetShopInfo), _trace.Entries[0].ToolName);
        Assert.Equal(nameof(OrderingTools.GetMyOrders), _trace.Entries[1].ToolName);
        Assert.True(_trace.Entries[0].DurationMs >= 0);
    }
}
