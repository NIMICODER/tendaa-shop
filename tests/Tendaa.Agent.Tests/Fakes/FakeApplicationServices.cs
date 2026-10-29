using Tendaa.Agent.Tools;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Agent.Tests.Fakes;

/// <summary>Hand-written fakes of the Application service interfaces <see cref="OrderingTools"/>
/// wraps — no mocking library, matching ARCHITECTURE.md's testing convention. Each records the
/// arguments it was called with, so a test can assert identity flowed from
/// <see cref="Tendaa.Agent.Context.IAgentRequestContext"/> rather than a model argument.</summary>
public sealed class FakeProductService : IProductService
{
    public Guid? LastShopId { get; private set; }
    public string? LastQuery { get; private set; }
    public Guid? LastProductId { get; private set; }

    public TendaaResult<IReadOnlyList<ProductSearchItem>> SearchResult { get; set; } =
        TendaaResult<IReadOnlyList<ProductSearchItem>>.Success([]);

    public TendaaResult<ProductDetails> DetailsResult { get; set; } = TendaaResult<ProductDetails>.NotFound();

    public Task<TendaaResult<IReadOnlyList<ProductSearchItem>>> SearchAsync(
        Guid shopId, string query, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastQuery = query;
        return Task.FromResult(SearchResult);
    }

    public Task<TendaaResult<ProductDetails>> GetDetailsAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastProductId = productId;
        return Task.FromResult(DetailsResult);
    }
}

public sealed class FakeDeliveryService : IDeliveryService
{
    public Guid? LastShopId { get; private set; }
    public string? LastArea { get; private set; }

    public TendaaResult<DeliveryFeeLookup> FeeResult { get; set; } =
        TendaaResult<DeliveryFeeLookup>.Success(new DeliveryFeeLookup(null, []));

    public Task<TendaaResult<DeliveryFeeLookup>> GetFeeAsync(
        Guid shopId, string area, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastArea = area;
        return Task.FromResult(FeeResult);
    }
}

public sealed class FakeShopService : IShopService
{
    public Guid? LastShopId { get; private set; }

    public TendaaResult<ShopInfo> InfoResult { get; set; } = TendaaResult<ShopInfo>.NotFound();
    public TendaaResult<Shop> OnlyShopResult { get; set; } = TendaaResult<Shop>.NotFound();
    public TendaaResult<Shop> ByPhoneNumberIdResult { get; set; } = TendaaResult<Shop>.NotFound();

    public Task<TendaaResult<ShopInfo>> GetInfoAsync(Guid shopId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        return Task.FromResult(InfoResult);
    }

    public Task<TendaaResult<Shop>> GetOnlyShopAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(OnlyShopResult);

    public Task<TendaaResult<Shop>> GetByPhoneNumberIdAsync(
        string phoneNumberId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ByPhoneNumberIdResult);
}

public sealed class FakeOrderService : IOrderService
{
    public Guid? LastShopId { get; private set; }
    public Guid? LastCustomerId { get; private set; }
    public IReadOnlyList<OrderItemInput>? LastItems { get; private set; }
    public string? LastCustomerName { get; private set; }
    public string? LastArea { get; private set; }
    public string? LastAddress { get; private set; }

    public TendaaResult<UpsertDraftOrderResult> UpsertResult { get; set; } =
        TendaaResult<UpsertDraftOrderResult>.Success(new UpsertDraftOrderResult(Guid.NewGuid(), [], [], null));

    public TendaaResult<OrderSummary> SummaryResult { get; set; } = TendaaResult<OrderSummary>.NotFound();

    public TendaaResult<ConfirmOrderResult> ConfirmResult { get; set; } =
        TendaaResult<ConfirmOrderResult>.BadRequest();

    public TendaaResult<IReadOnlyList<OrderStatusSummary>> MyOrdersResult { get; set; } =
        TendaaResult<IReadOnlyList<OrderStatusSummary>>.Success([]);

    public Task<TendaaResult<UpsertDraftOrderResult>> UpsertDraftOrderAsync(
        Guid shopId,
        Guid customerId,
        IReadOnlyList<OrderItemInput> items,
        string? customerName,
        string? area,
        string? address,
        CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        LastItems = items;
        LastCustomerName = customerName;
        LastArea = area;
        LastAddress = address;
        return Task.FromResult(UpsertResult);
    }

    public Task<TendaaResult<OrderSummary>> GetOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        return Task.FromResult(SummaryResult);
    }

    public Task<TendaaResult<ConfirmOrderResult>> ConfirmOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        return Task.FromResult(ConfirmResult);
    }

    public Task<TendaaResult<IReadOnlyList<OrderStatusSummary>>> GetMyOrdersAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        return Task.FromResult(MyOrdersResult);
    }
}

public sealed class FakeHandoffService : IHandoffService
{
    public Guid? LastShopId { get; private set; }
    public Guid? LastConversationId { get; private set; }
    public string? LastReason { get; private set; }
    public string? LastSummary { get; private set; }

    public TendaaResult Result { get; set; } = TendaaResult.Success();
    public bool HasOpenHandoffResult { get; set; }

    public Task<TendaaResult> RequestHandoffAsync(
        Guid shopId,
        Guid conversationId,
        string reason,
        string summary,
        CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastConversationId = conversationId;
        LastReason = reason;
        LastSummary = summary;
        return Task.FromResult(Result);
    }

    public Task<bool> HasOpenHandoffAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(HasOpenHandoffResult);
}

public sealed class FakeCustomerService : ICustomerService
{
    public Guid? LastShopId { get; private set; }
    public Guid? LastCustomerId { get; private set; }

    public TendaaResult<Customer> GetByIdResult { get; set; } = TendaaResult<Customer>.NotFound();
    public TendaaResult<Customer> UpdateNameResult { get; set; } = TendaaResult<Customer>.NotFound();
    public string? LastUpdatedName { get; private set; }

    public Task<TendaaResult<Customer>> FindOrCreateByBsuIdAsync(
        Guid shopId, string bsuId, string? phone = null, string? username = null, string? name = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by OrderingTools tests.");

    public Task<TendaaResult<Customer>> LinkBsuIdChangeAsync(
        Guid shopId, string previousBsuId, string newBsuId, string? newPhone = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by OrderingTools tests.");

    public Task<TendaaResult<Customer>> GetByIdAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        return Task.FromResult(GetByIdResult);
    }

    public Task<TendaaResult<Customer>> UpdateNameAsync(
        Guid shopId, Guid customerId, string name, CancellationToken cancellationToken = default)
    {
        LastShopId = shopId;
        LastCustomerId = customerId;
        LastUpdatedName = name;
        return Task.FromResult(UpdateNameResult);
    }
}

public sealed class FakeWhatsAppSender : IWhatsAppSender
{
    public List<(Customer Customer, string Text)> SentTexts { get; } = [];
    public List<(Customer Customer, string ImageUrl, string? Caption)> SentImages { get; } = [];
    public List<string> MarkedAsRead { get; } = [];

    public Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default)
    {
        SentTexts.Add((customer, text));
        return Task.CompletedTask;
    }

    public Task SendImageAsync(
        Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default)
    {
        SentImages.Add((customer, imageUrl, caption));
        return Task.CompletedTask;
    }

    public Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default)
    {
        MarkedAsRead.Add(whatsAppMessageId);
        return Task.CompletedTask;
    }
}
