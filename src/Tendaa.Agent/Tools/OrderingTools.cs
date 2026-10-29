using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Agent.Tools;

public sealed record OrderItemArg(
    [property: Description("Product variant id, from a prior SearchProducts or GetProductDetails call")]
    Guid ProductVariantId,
    [property: Description("How many of this variant")]
    int Quantity);

/// <summary>Thin tool wrappers over <c>Tendaa.Application</c> services, matching
/// ARCHITECTURE.md's tool table exactly. Each method reads identity from
/// <see cref="IAgentRequestContext"/> — never a model argument, per SPEC.md rule 4 — and records
/// its call in <see cref="IToolTraceCollector"/> by hand, since Microsoft.Agents.AI 1.26 has no
/// public tool-call middleware hook.</summary>
public sealed class OrderingTools(
    IProductService productService,
    IDeliveryService deliveryService,
    IShopService shopService,
    IOrderService orderService,
    IHandoffService handoffService,
    IAgentRequestContext requestContext,
    IToolTraceCollector trace)
{
    [Description("Find products by name, category or colour.")]
    public Task<object> SearchProducts(
        [Description("Free-text search, e.g. 'black sneakers'")] string query) =>
        TraceAsync(nameof(SearchProducts), new { query }, async () =>
        {
            var result = await productService.SearchAsync(requestContext.ShopId, query);
            return (object)result.Data!;
        });

    [Description("Full details for one product, including all variants and stock.")]
    public Task<object> GetProductDetails(
        [Description("Product id, from a prior SearchProducts result")] Guid productId) =>
        TraceAsync(nameof(GetProductDetails), new { productId }, async () =>
        {
            var result = await productService.GetDetailsAsync(requestContext.ShopId, productId);
            return result.IsSuccessful ? (object)result.Data! : "NOT_FOUND";
        });

    [Description("Delivery fee and time for an area. An area that doesn't match a known zone returns the full zone list instead.")]
    public Task<object> GetDeliveryFee(
        [Description("Customer's delivery area or neighbourhood, e.g. 'Lekki'")] string area) =>
        TraceAsync(nameof(GetDeliveryFee), new { area }, async () =>
        {
            var result = await deliveryService.GetFeeAsync(requestContext.ShopId, area);
            return (object)result.Data!;
        });

    [Description("Shop hours, rules and payment methods.")]
    public Task<object> GetShopInfo() =>
        TraceAsync(nameof(GetShopInfo), new { }, async () =>
        {
            var result = await shopService.GetInfoAsync(requestContext.ShopId);
            return result.IsSuccessful ? (object)result.Data! : "NOT_FOUND";
        });

    [Description("Create or update the customer's draft order. Pass every item and field you currently know each time — this replaces the draft, it does not append to it.")]
    public Task<object> UpsertDraftOrder(
        [Description("All items currently wanted, each with a product variant id and quantity")] IReadOnlyList<OrderItemArg> items,
        [Description("Customer's name, once known")] string? customerName = null,
        [Description("Delivery area or neighbourhood, once known")] string? area = null,
        [Description("Delivery address detail, if the customer gave one")] string? address = null) =>
        TraceAsync(nameof(UpsertDraftOrder), new { items, customerName, area, address }, async () =>
        {
            var inputs = items.Select(i => new OrderItemInput(i.ProductVariantId, i.Quantity)).ToList();
            var result = await orderService.UpsertDraftOrderAsync(
                requestContext.ShopId, requestContext.CustomerId, inputs, customerName, area, address);

            return (object)new
            {
                missingFields = result.Data!.MissingFields,
                stockIssues = result.Data!.StockIssues,
                summary = result.Data!.Summary,
            };
        });

    [Description("The customer's current draft order: items, item total, delivery fee, grand total.")]
    public Task<object> GetOrderSummary() =>
        TraceAsync(nameof(GetOrderSummary), new { }, async () =>
        {
            var result = await orderService.GetOrderSummaryAsync(requestContext.ShopId, requestContext.CustomerId);
            return result.IsSuccessful ? (object)result.Data! : "NOT_FOUND";
        });

    [Description("Confirm the draft order after the customer has clearly agreed to the summary. Moves it to awaiting payment and returns bank details. Only call this after the customer has said yes to that exact summary.")]
    public Task<object> ConfirmOrderSummary() =>
        TraceAsync(nameof(ConfirmOrderSummary), new { }, async () =>
        {
            var result = await orderService.ConfirmOrderSummaryAsync(requestContext.ShopId, requestContext.CustomerId);
            return result.IsSuccessful ? (object)result.Data! : "NO_COMPLETE_DRAFT_ORDER";
        });

    [Description("This customer's recent orders and statuses. Never another customer's.")]
    public Task<object> GetMyOrders() =>
        TraceAsync(nameof(GetMyOrders), new { }, async () =>
        {
            var result = await orderService.GetMyOrdersAsync(requestContext.ShopId, requestContext.CustomerId);
            return (object)result.Data!;
        });

    [Description("Hand this conversation off to the seller — for complaints, refund requests, requests for a human, or anything you're genuinely unsure about. Stay silent on this chat afterwards until the customer raises something new.")]
    public Task<object> RequestHandoff(
        [Description("Short machine-facing reason, e.g. 'refund_request'")] string reason,
        [Description("Two-line summary for the seller")] string summary) =>
        TraceAsync(nameof(RequestHandoff), new { reason, summary }, async () =>
        {
            await handoffService.RequestHandoffAsync(requestContext.ShopId, requestContext.ConversationId, reason, summary);
            return (object)"OK";
        });

    private async Task<object> TraceAsync(string toolName, object args, Func<Task<object>> call)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await call();
        stopwatch.Stop();

        trace.Record(toolName, JsonSerializer.Serialize(args), JsonSerializer.Serialize(result), (int)stopwatch.ElapsedMilliseconds);
        return result;
    }
}
