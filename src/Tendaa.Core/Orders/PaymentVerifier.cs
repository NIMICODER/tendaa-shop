using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Payments;

namespace Tendaa.Core.Orders;

/// <summary>Pure C#, no Meta API call here — the receipt photo is already turned into plain
/// fields by <c>IMediaAnalyzer</c> (Phase 4b) before this runs. SPEC.md: never auto-confirm a
/// payment from a model's say-so; compare the extracted fields against the order in code.</summary>
public static class PaymentVerifier
{
    /// <summary>Below this, even a fields-match reads as risky rather than confirmed — it's the
    /// model itself saying it isn't sure what it read off the photo.</summary>
    public const double MinimumConfidence = 0.6;

    public static (PaymentVerificationResult Result, string Reasons) Verify(
        Order order,
        decimal? extractedAmount,
        DateOnly? transferDate,
        string? reference,
        double confidence,
        IReadOnlyList<string> previousReferencesForShop)
    {
        var reasons = new List<string>();

        if (extractedAmount is null)
        {
            reasons.Add("no amount found on receipt");
        }
        else if (extractedAmount != order.GrandTotal)
        {
            reasons.Add($"amount {extractedAmount} does not match order total {order.GrandTotal}");
        }

        var orderPlacedOn = DateOnly.FromDateTime((order.AwaitingPaymentAt ?? order.CreatedAt).UtcDateTime);
        if (transferDate is null)
        {
            reasons.Add("no transfer date found on receipt");
        }
        else if (transferDate < orderPlacedOn)
        {
            reasons.Add("transfer date is before the order was placed");
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            reasons.Add("no payment reference found on receipt");
        }
        else if (previousReferencesForShop.Contains(reference, StringComparer.OrdinalIgnoreCase))
        {
            reasons.Add($"payment reference '{reference}' was already used on a previous order");
        }

        if (reasons.Count > 0)
        {
            return (PaymentVerificationResult.Mismatched, string.Join("; ", reasons));
        }

        return confidence < MinimumConfidence
            ? (PaymentVerificationResult.Suspicious, $"low extraction confidence ({confidence:P0}) despite matching fields")
            : (PaymentVerificationResult.Matched, "");
    }
}
