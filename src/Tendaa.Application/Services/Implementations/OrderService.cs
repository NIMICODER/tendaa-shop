using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Orders;
using Tendaa.Core.Orders;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class OrderService(
    IRepository<Order> orders,
    IRepository<Product> products,
    IRepository<Shop> shops,
    IRepository<DeliveryZone> deliveryZones,
    IDeliveryService deliveryService,
    IUnitOfWork unitOfWork) : IOrderService
{
    public async Task<TendaaResult<UpsertDraftOrderResult>> UpsertDraftOrderAsync(
        Guid shopId,
        Guid customerId,
        IReadOnlyList<OrderItemInput> items,
        string? customerName,
        string? area,
        string? address,
        CancellationToken cancellationToken = default)
    {
        var order = await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId && o.Status == OrderStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            order = new Order
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                CustomerId = customerId,
                Number = await NextOrderNumberAsync(shopId, cancellationToken),
                Status = OrderStatus.Draft,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await orders.AddAsync(order, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            order.CustomerName = customerName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            order.Address = address.Trim();
        }

        DeliveryZoneSummary? matchedZone = null;
        if (!string.IsNullOrWhiteSpace(area))
        {
            var lookup = (await deliveryService.GetFeeAsync(shopId, area, cancellationToken)).Data;
            matchedZone = lookup?.Matched;
            order.DeliveryZoneId = matchedZone?.Id;
        }
        else if (order.DeliveryZoneId is { } existingZoneId)
        {
            // area wasn't resent this call — keep the zone already resolved on an earlier turn.
            var existingZone = await deliveryZones.GetByIdAsync(existingZoneId, cancellationToken);
            if (existingZone is not null)
            {
                matchedZone = new DeliveryZoneSummary(existingZone.Id, existingZone.Name, existingZone.Fee, existingZone.DeliveryTimeText);
            }
        }

        var shopProducts = await products.Query()
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);
        var variantsById = shopProducts
            .SelectMany(p => p.Variants.Select(v => (Product: p, Variant: v)))
            .ToDictionary(x => x.Variant.Id);

        var stockIssues = new List<StockIssue>();
        var resolvedLines = new List<(ProductVariant Variant, Product Product, int Quantity)>();

        foreach (var item in items)
        {
            if (!variantsById.TryGetValue(item.ProductVariantId, out var found))
            {
                continue; // not this shop's variant — model hallucinated an id; caller sees it missing from the summary
            }

            if (found.Variant.Stock < item.Quantity)
            {
                var alternatives = found.Product.Variants
                    .Where(v => v.Id != found.Variant.Id && v.Stock > 0)
                    .Select(v => ToVariantStock(v, found.Product.Price))
                    .ToList();
                stockIssues.Add(new StockIssue(
                    found.Variant.Id,
                    $"{found.Product.Name} ({DescribeVariant(found.Variant)})",
                    alternatives));
                continue;
            }

            resolvedLines.Add((found.Variant, found.Product, item.Quantity));
        }

        var missingFields = new List<string>();
        if (items.Count == 0)
        {
            missingFields.Add("items");
        }

        if (string.IsNullOrWhiteSpace(order.CustomerName))
        {
            missingFields.Add("customerName");
        }

        if (matchedZone is null)
        {
            missingFields.Add("area");
        }

        order.Items.Clear();
        OrderSummary? summary = null;

        if (missingFields.Count == 0 && stockIssues.Count == 0)
        {
            foreach (var (variant, product, quantity) in resolvedLines)
            {
                order.Items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopId,
                    OrderId = order.Id,
                    ProductVariantId = variant.Id,
                    Quantity = quantity,
                    UnitPrice = variant.PriceOverride ?? product.Price,
                });
            }

            var totals = OrderTotals.Calculate(order.Items, matchedZone!.Fee);
            order.ItemTotal = totals.ItemTotal;
            order.DeliveryFee = totals.DeliveryFee;
            order.GrandTotal = totals.GrandTotal;

            summary = BuildSummary(order, resolvedLines);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TendaaResult<UpsertDraftOrderResult>.Success(
            new UpsertDraftOrderResult(order.Id, missingFields, stockIssues, summary));
    }

    public async Task<TendaaResult<OrderSummary>> GetOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var order = await CurrentOpenOrderAsync(shopId, customerId, cancellationToken);
        if (order is null || order.Items.Count == 0)
        {
            return TendaaResult<OrderSummary>.NotFound();
        }

        var summary = await BuildSummaryFromOrderAsync(order, shopId, cancellationToken);
        return TendaaResult<OrderSummary>.Success(summary);
    }

    public async Task<TendaaResult<ConfirmOrderResult>> ConfirmOrderSummaryAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var order = await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId && o.Status == OrderStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null || order.Items.Count == 0 || order.DeliveryZoneId is null)
        {
            return TendaaResult<ConfirmOrderResult>.BadRequest(
                "No complete draft order to confirm — call UpsertDraftOrder first.");
        }

        OrderStateMachine.Transition(order, OrderStatus.AwaitingPayment, OrderActor.Agent);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);

        var result = new ConfirmOrderResult(
            order.Number,
            order.GrandTotal,
            shop?.BankName,
            shop?.BankAccountNumber,
            shop?.BankAccountName);

        return TendaaResult<ConfirmOrderResult>.Success(result);
    }

    public async Task<TendaaResult<IReadOnlyList<OrderStatusSummary>>> GetMyOrdersAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var list = await orders.Query()
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new OrderStatusSummary(o.Number, o.Status.ToString(), o.GrandTotal, o.CreatedAt))
            .ToListAsync(cancellationToken);

        return TendaaResult<IReadOnlyList<OrderStatusSummary>>.Success(list);
    }

    private async Task<Order?> CurrentOpenOrderAsync(Guid shopId, Guid customerId, CancellationToken cancellationToken) =>
        await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId
                && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.AwaitingPayment))
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<OrderSummary> BuildSummaryFromOrderAsync(Order order, Guid shopId, CancellationToken cancellationToken)
    {
        var variantIds = order.Items.Select(i => i.ProductVariantId).ToList();
        var shopProducts = await products.Query()
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);
        var variantsById = shopProducts
            .SelectMany(p => p.Variants.Select(v => (Product: p, Variant: v)))
            .Where(x => variantIds.Contains(x.Variant.Id))
            .ToDictionary(x => x.Variant.Id);

        var lines = order.Items
            .Where(i => variantsById.ContainsKey(i.ProductVariantId))
            .Select(i =>
            {
                var (product, variant) = variantsById[i.ProductVariantId];
                return (Variant: variant, Product: product, Quantity: i.Quantity);
            })
            .ToList();

        return new OrderSummary(
            order.Number,
            lines.Select(l => new OrderSummaryItem(
                l.Variant.Id, l.Product.Name, l.Variant.Size, l.Variant.Colour, l.Quantity,
                l.Variant.PriceOverride ?? l.Product.Price,
                (l.Variant.PriceOverride ?? l.Product.Price) * l.Quantity)).ToList(),
            order.ItemTotal,
            order.DeliveryFee,
            order.GrandTotal,
            order.Address);
    }

    private static OrderSummary BuildSummary(
        Order order,
        List<(ProductVariant Variant, Product Product, int Quantity)> lines) =>
        new(
            order.Number,
            lines.Select(l => new OrderSummaryItem(
                l.Variant.Id, l.Product.Name, l.Variant.Size, l.Variant.Colour, l.Quantity,
                l.Variant.PriceOverride ?? l.Product.Price,
                (l.Variant.PriceOverride ?? l.Product.Price) * l.Quantity)).ToList(),
            order.ItemTotal,
            order.DeliveryFee,
            order.GrandTotal,
            order.Address);

    private async Task<int> NextOrderNumberAsync(Guid shopId, CancellationToken cancellationToken)
    {
        var max = await orders.Query()
            .Where(o => o.ShopId == shopId)
            .Select(o => (int?)o.Number)
            .MaxAsync(cancellationToken);

        return (max ?? 0) + 1;
    }

    private static VariantStock ToVariantStock(ProductVariant variant, decimal basePrice) =>
        new(variant.Id, variant.Size, variant.Colour, variant.Stock, variant.PriceOverride ?? basePrice);

    private static string DescribeVariant(ProductVariant variant) =>
        string.Join(" ", new[] { variant.Size, variant.Colour }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
