using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Orders;
using Tendaa.Core.Models.Enums.Payments;
using Tendaa.Core.Money;
using Tendaa.Core.Orders;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class OrderService(
    IRepository<Order> orders,
    IRepository<Product> products,
    IRepository<Shop> shops,
    IRepository<DeliveryZone> deliveryZones,
    IRepository<PaymentProof> paymentProofs,
    IDeliveryService deliveryService,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier,
    IRepository<Customer> customers,
    ICustomerMessenger customerMessenger) : IOrderService
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
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

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
                matchedZone = new DeliveryZoneSummary(
                    existingZone.Id, existingZone.Name, existingZone.Fee, existingZone.DeliveryTimeText,
                    MoneyFormatter.Format(existingZone.Fee, currency));
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
                    .Select(v => ToVariantStock(v, found.Product.Price, currency))
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

            summary = BuildSummary(order, resolvedLines, currency);
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
        await dashboardNotifier.NotifyOrdersChangedAsync(shopId, cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var formattedGrandTotal = MoneyFormatter.Format(order.GrandTotal, shop?.Currency ?? "NGN");
        var paymentMessage = BuildPaymentMessage(
            order.Number, formattedGrandTotal, shop?.BankName, shop?.BankAccountNumber, shop?.BankAccountName);

        var result = new ConfirmOrderResult(
            order.Number,
            order.GrandTotal,
            shop?.BankName,
            shop?.BankAccountNumber,
            shop?.BankAccountName,
            formattedGrandTotal,
            paymentMessage);

        return TendaaResult<ConfirmOrderResult>.Success(result);
    }

    /// <summary>The exact text the customer must receive for payment — built here in code, never
    /// left to the model to type or paraphrase. Never mentions "confirm"/"confirmed": that word is
    /// reserved for after the seller approves the payment (<see cref="OrderStatus.Confirmed"/>),
    /// which only a <see cref="OrderActor.Seller"/> can reach.</summary>
    private static string BuildPaymentMessage(
        int orderNumber, string formattedGrandTotal, string? bankName, string? bankAccountNumber, string? bankAccountName) =>
        string.Join('\n',
        [
            $"Order #{orderNumber} - {formattedGrandTotal}",
            $"Pay to: {bankName}, {bankAccountNumber} ({bankAccountName})",
            "Send your receipt here once you've paid.",
        ]);

    public async Task<TendaaResult<IReadOnlyList<OrderStatusSummary>>> GetMyOrdersAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

        // Order by Number, not CreatedAt: same "most recent" intent, but Number is a plain int,
        // so it sidesteps the Sqlite provider's refusal to translate ORDER BY on DateTimeOffset
        // (see CheckReceiptAsync's order lookup above for the same fix, same reason).
        var rows = await orders.Query()
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId)
            .OrderByDescending(o => o.Number)
            .Take(10)
            .Select(o => new { o.Number, Status = o.Status.ToString(), o.GrandTotal, o.CreatedAt })
            .ToListAsync(cancellationToken);

        IReadOnlyList<OrderStatusSummary> list = rows
            .Select(o => new OrderStatusSummary(
                o.Number, o.Status, o.GrandTotal, MoneyFormatter.Format(o.GrandTotal, currency), o.CreatedAt))
            .ToList();

        return TendaaResult<IReadOnlyList<OrderStatusSummary>>.Success(list);
    }

    public async Task<TendaaResult<ReceiptCheckResult>> CheckReceiptAsync(
        Guid shopId,
        Guid customerId,
        string mediaId,
        decimal? extractedAmount,
        DateOnly? transferDate,
        string? reference,
        double confidence,
        string extractedFieldsJson,
        CancellationToken cancellationToken = default)
    {
        // A customer CAN have more than one AwaitingPayment order at once: UpsertDraftOrderAsync
        // always starts a fresh Draft when none exists, even if an earlier order is still sitting
        // in AwaitingPayment unresolved — confirmed live (an abandoned old order's receipt check
        // was picked over the one the customer had just placed). Order by Number, not CreatedAt:
        // same "most recent" intent as CurrentOpenOrderAsync above, but Number is a plain int, so
        // it sidesteps the Sqlite provider's refusal to translate ORDER BY on DateTimeOffset.
        var order = await orders.Query()
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId && o.Status == OrderStatus.AwaitingPayment)
            .OrderByDescending(o => o.Number)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return TendaaResult<ReceiptCheckResult>.Success(new ReceiptCheckResult(false, null, null, null));
        }

        var previousReferences = await PreviouslyMatchedReferencesAsync(shopId, cancellationToken);
        var (result, reasons) = PaymentVerifier.Verify(
            order, extractedAmount, transferDate, reference, confidence, previousReferences);

        await paymentProofs.AddAsync(new PaymentProof
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            OrderId = order.Id,
            MediaId = mediaId,
            ExtractedFieldsJson = extractedFieldsJson,
            VerificationResult = result,
            Reasons = reasons,
            CreatedAt = DateTimeOffset.UtcNow,
        }, cancellationToken);

        if (result == PaymentVerificationResult.Matched)
        {
            OrderStateMachine.Transition(order, OrderStatus.PaymentReview, OrderActor.System);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dashboardNotifier.NotifyOrdersChangedAsync(shopId, cancellationToken);

        return TendaaResult<ReceiptCheckResult>.Success(
            new ReceiptCheckResult(true, order.Number, result, reasons.Length == 0 ? null : reasons));
    }

    /// <summary>References from already-matched receipts, read out of
    /// <see cref="PaymentProof.ExtractedFieldsJson"/> — there's no dedicated column for it (that
    /// JSON shape is owned by <c>IMediaAnalyzer</c>, not fixed here), so reuse-checking means
    /// parsing each row. A malformed row (e.g. from an earlier extraction-shape change) is
    /// skipped rather than failing the whole check.</summary>
    private async Task<IReadOnlyList<string>> PreviouslyMatchedReferencesAsync(
        Guid shopId, CancellationToken cancellationToken)
    {
        var rows = await paymentProofs.Query()
            .Where(p => p.ShopId == shopId && p.VerificationResult == PaymentVerificationResult.Matched)
            .Select(p => p.ExtractedFieldsJson)
            .ToListAsync(cancellationToken);

        var references = new List<string>();
        foreach (var json in rows)
        {
            if (ParseExtractedFields(json).Reference is { Length: > 0 } reference)
            {
                references.Add(reference);
            }
        }

        return references;
    }

    public async Task<TendaaResult<OrderListPage>> ListAsync(
        Guid shopId, OrderStatus? statusFilter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = orders.Query().Where(o => o.ShopId == shopId);
        if (statusFilter is { } status)
        {
            query = query.Where(o => o.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Order by Number, not CreatedAt: per-shop sequential, same recency intent, and it
        // sidesteps SQLite's refusal to translate ORDER BY on DateTimeOffset.
        var rows = await query
            .OrderByDescending(o => o.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.Status,
                o.CustomerId,
                o.CustomerName,
                o.GrandTotal,
                o.CreatedAt,
                o.PaymentReviewAt,
                ItemCount = o.Items.Count,
            })
            .ToListAsync(cancellationToken);

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

        var customerIds = rows.Select(r => r.CustomerId).Distinct().ToList();
        var customerNames = await customers.Query()
            .Where(c => c.ShopId == shopId && customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var items = rows.Select(r => new OrderListItem(
            r.Id,
            r.Number,
            r.Status,
            r.CustomerName ?? (customerNames.TryGetValue(r.CustomerId, out var name) ? name : null) ?? $"Order #{r.Number}",
            r.ItemCount,
            r.GrandTotal,
            MoneyFormatter.Format(r.GrandTotal, currency),
            r.CreatedAt,
            r.PaymentReviewAt)).ToList();

        return TendaaResult<OrderListPage>.Success(new OrderListPage(items, totalCount, page, pageSize));
    }

    public async Task<TendaaResult<IReadOnlyList<OrderStatusCount>>> GetStatusCountsAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var grouped = await orders.Query()
            .Where(o => o.ShopId == shopId)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var counts = grouped.ToDictionary(r => r.Status, r => r.Count);
        IReadOnlyList<OrderStatusCount> result = Enum.GetValues<OrderStatus>()
            .Select(status => new OrderStatusCount(status, counts.TryGetValue(status, out var count) ? count : 0))
            .ToList();

        return TendaaResult<IReadOnlyList<OrderStatusCount>>.Success(result);
    }

    public async Task<TendaaResult<OrderDetail>> GetDetailAsync(
        Guid shopId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var detail = await BuildDetailAsync(shopId, orderId, cancellationToken);
        return detail is null
            ? TendaaResult<OrderDetail>.NotFound()
            : TendaaResult<OrderDetail>.Success(detail);
    }

    public async Task<TendaaResult<OrderDetail>> ConfirmPaymentAsync(
        Guid shopId, Guid paymentProofId, string confirmedBy, CancellationToken cancellationToken = default)
    {
        var proof = await paymentProofs.Query()
            .FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == paymentProofId, cancellationToken);
        if (proof is null)
        {
            return TendaaResult<OrderDetail>.NotFound();
        }

        var order = await orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ShopId == shopId && o.Id == proof.OrderId, cancellationToken);
        if (order is null || order.Status != OrderStatus.PaymentReview)
        {
            return TendaaResult<OrderDetail>.BadRequest("Only orders awaiting review can be confirmed.");
        }

        OrderStateMachine.Transition(order, OrderStatus.Confirmed, OrderActor.Seller);
        order.ConfirmedBy = confirmedBy;
        proof.ReviewedBy = confirmedBy;

        // First place in the app stock is ever decremented: the sale is real only once the
        // seller confirms payment. Clamped at zero — concurrent confirms must not drive it negative.
        var variantIds = order.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await products.Query()
            .Where(p => p.ShopId == shopId)
            .SelectMany(p => p.Variants)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync(cancellationToken);
        foreach (var item in order.Items)
        {
            var variant = variants.FirstOrDefault(v => v.Id == item.ProductVariantId);
            if (variant is not null)
            {
                variant.Stock = Math.Max(0, variant.Stock - item.Quantity);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dashboardNotifier.NotifyOrdersChangedAsync(shopId, cancellationToken);

        var notified = await customerMessenger.TrySendAsync(
            shopId, order.CustomerId, $"Payment confirmed. Order #{order.Number} is being prepared.", cancellationToken);

        var detail = await BuildDetailAsync(shopId, order.Id, cancellationToken);
        if (detail is null)
        {
            return TendaaResult<OrderDetail>.InternalServerError("Order was confirmed but could not be reloaded.");
        }

        return TendaaResult<OrderDetail>.Success(detail, notified
            ? "Payment confirmed. The customer has been notified."
            : "Payment confirmed, but the customer is outside WhatsApp's 24-hour window, so no message was sent.");
    }

    public async Task<TendaaResult<OrderDetail>> RejectPaymentAsync(
        Guid shopId, Guid paymentProofId, string reason, string rejectedBy, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return TendaaResult<OrderDetail>.BadRequest("A reason is required so the customer knows what to fix.");
        }

        var proof = await paymentProofs.Query()
            .FirstOrDefaultAsync(p => p.ShopId == shopId && p.Id == paymentProofId, cancellationToken);
        if (proof is null)
        {
            return TendaaResult<OrderDetail>.NotFound();
        }

        var order = await orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ShopId == shopId && o.Id == proof.OrderId, cancellationToken);
        if (order is null || order.Status != OrderStatus.PaymentReview)
        {
            return TendaaResult<OrderDetail>.BadRequest("Only orders awaiting review can be rejected.");
        }

        OrderStateMachine.Transition(order, OrderStatus.AwaitingPayment, OrderActor.Seller);
        proof.ReviewedBy = rejectedBy;
        proof.Reasons = string.IsNullOrWhiteSpace(proof.Reasons)
            ? $"Seller: {reason.Trim()}"
            : $"{proof.Reasons}; Seller: {reason.Trim()}";

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dashboardNotifier.NotifyOrdersChangedAsync(shopId, cancellationToken);

        var notified = await customerMessenger.TrySendAsync(
            shopId,
            order.CustomerId,
            $"There's an issue with your payment for order #{order.Number}: {reason.Trim()} Please check and resend your receipt.",
            cancellationToken);

        var detail = await BuildDetailAsync(shopId, order.Id, cancellationToken);
        if (detail is null)
        {
            return TendaaResult<OrderDetail>.InternalServerError("Order was updated but could not be reloaded.");
        }

        return TendaaResult<OrderDetail>.Success(detail, notified
            ? "Payment rejected. The customer has been asked to resend proof."
            : "Payment rejected, but the customer is outside WhatsApp's 24-hour window, so no message was sent.");
    }

    private async Task<OrderDetail?> BuildDetailAsync(Guid shopId, Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.Query()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ShopId == shopId && o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";
        var summary = await BuildSummaryFromOrderAsync(order, shopId, cancellationToken);

        string? zoneName = null;
        if (order.DeliveryZoneId is { } zoneId)
        {
            zoneName = (await deliveryZones.GetByIdAsync(zoneId, cancellationToken))?.Name;
        }

        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == order.CustomerId, cancellationToken);

        // Sorted client-side: SQLite can't translate ORDER BY over DateTimeOffset.
        var proofs = (await paymentProofs.Query()
                .Where(p => p.ShopId == shopId && p.OrderId == order.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var proofDetails = proofs
            .Select(p =>
            {
                var extracted = ParseExtractedFields(p.ExtractedFieldsJson);
                return new PaymentProofDetail(
                    p.Id,
                    p.MediaId,
                    $"/api/media/{p.MediaId}",
                    extracted.Amount,
                    extracted.Amount is { } amount ? MoneyFormatter.Format(amount, currency) : null,
                    extracted.SenderName,
                    extracted.BankName,
                    extracted.TransferDate,
                    extracted.Reference,
                    extracted.Confidence,
                    p.VerificationResult,
                    p.Reasons,
                    p.ReviewedBy);
            })
            .ToList();

        return new OrderDetail(
            order.Id,
            order.Number,
            order.Status,
            order.CustomerName ?? customer?.Name ?? $"Order #{order.Number}",
            customer?.PhoneNumber,
            summary.Items,
            order.ItemTotal,
            order.DeliveryFee,
            order.GrandTotal,
            MoneyFormatter.Format(order.ItemTotal, currency),
            MoneyFormatter.Format(order.DeliveryFee, currency),
            MoneyFormatter.Format(order.GrandTotal, currency),
            zoneName,
            order.Address,
            order.ConfirmedBy,
            order.CreatedAt,
            order.AwaitingPaymentAt,
            order.PaymentReviewAt,
            order.ConfirmedAt,
            proofDetails);
    }

    private async Task<Order?> CurrentOpenOrderAsync(Guid shopId, Guid customerId, CancellationToken cancellationToken) =>
        await orders.Query()
            .Include(o => o.Items)
            .Where(o => o.ShopId == shopId && o.CustomerId == customerId
                && (o.Status == OrderStatus.Draft || o.Status == OrderStatus.AwaitingPayment))
            // Number, not CreatedAt: Sqlite can't translate ORDER BY over DateTimeOffset (same
            // fix as CheckReceiptAsync/GetMyOrdersAsync above).
            .OrderByDescending(o => o.Number)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<OrderSummary> BuildSummaryFromOrderAsync(Order order, Guid shopId, CancellationToken cancellationToken)
    {
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        var currency = shop?.Currency ?? "NGN";

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

        return BuildSummary(order, lines, currency);
    }

    private static OrderSummary BuildSummary(
        Order order,
        List<(ProductVariant Variant, Product Product, int Quantity)> lines,
        string currency)
    {
        var items = lines.Select(l =>
        {
            var unitPrice = l.Variant.PriceOverride ?? l.Product.Price;
            var lineTotal = unitPrice * l.Quantity;
            return new OrderSummaryItem(
                l.Variant.Id, l.Product.Name, l.Variant.Size, l.Variant.Colour, l.Quantity,
                unitPrice, lineTotal,
                MoneyFormatter.Format(unitPrice, currency), MoneyFormatter.Format(lineTotal, currency));
        }).ToList();

        var formattedItemTotal = MoneyFormatter.Format(order.ItemTotal, currency);
        var formattedDeliveryFee = MoneyFormatter.Format(order.DeliveryFee, currency);
        var formattedGrandTotal = MoneyFormatter.Format(order.GrandTotal, currency);

        var summaryText = BuildSummaryText(order.Number, items, formattedItemTotal, formattedDeliveryFee, formattedGrandTotal);

        return new OrderSummary(
            order.Number, items, order.ItemTotal, order.DeliveryFee, order.GrandTotal, order.Address,
            formattedItemTotal, formattedDeliveryFee, formattedGrandTotal, summaryText);
    }

    private static string BuildSummaryText(
        int orderNumber,
        IReadOnlyList<OrderSummaryItem> items,
        string formattedItemTotal,
        string formattedDeliveryFee,
        string formattedGrandTotal)
    {
        var lines = items.Select(i =>
        {
            var variant = string.Join(" ", new[] { i.Size, i.Colour }.Where(s => !string.IsNullOrWhiteSpace(s)));
            var variantSuffix = variant.Length == 0 ? "" : $" ({variant})";
            return $"{i.Quantity} x {i.ProductName}{variantSuffix} - {i.FormattedLineTotal}";
        });

        return string.Join('\n', new[]
        {
            $"Order #{orderNumber}",
            string.Join('\n', lines),
            $"Items: {formattedItemTotal}",
            $"Delivery: {formattedDeliveryFee}",
            $"Total: {formattedGrandTotal}",
        });
    }

    private sealed record ExtractedReceiptFields(
        decimal? Amount,
        string? BankName,
        string? SenderName,
        string? TransferDate,
        string? Reference,
        double? Confidence)
    {
        public static readonly ExtractedReceiptFields Empty = new(null, null, null, null, null, null);
    }

    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Parses a proof's stored extracted-fields JSON. Case-insensitive: rows were
    /// written by serializing the PascalCase <c>MediaAnalysisResult</c>, so a case-sensitive
    /// lookup silently finds nothing. Never throws — malformed rows yield <see
    /// cref="ExtractedReceiptFields.Empty"/>.</summary>
    private static ExtractedReceiptFields ParseExtractedFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ExtractedReceiptFields.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<ExtractedReceiptFields>(json, CaseInsensitiveJson)
                ?? ExtractedReceiptFields.Empty;
        }
        catch (JsonException)
        {
            return ExtractedReceiptFields.Empty;
        }
    }

    private async Task<int> NextOrderNumberAsync(Guid shopId, CancellationToken cancellationToken)
    {
        var max = await orders.Query()
            .Where(o => o.ShopId == shopId)
            .Select(o => (int?)o.Number)
            .MaxAsync(cancellationToken);

        return (max ?? 0) + 1;
    }

    private static VariantStock ToVariantStock(ProductVariant variant, decimal basePrice, string currency)
    {
        var price = variant.PriceOverride ?? basePrice;
        return new(variant.Id, variant.Size, variant.Colour, variant.Stock, price, MoneyFormatter.Format(price, currency));
    }

    private static string DescribeVariant(ProductVariant variant) =>
        string.Join(" ", new[] { variant.Size, variant.Colour }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
