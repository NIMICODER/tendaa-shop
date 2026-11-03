using Microsoft.Extensions.DependencyInjection;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Enums.Payments;

namespace Tendaa.Evals.Cases;

/// <summary>Honest scope note (see BUILDLOG.md / README eval section): these cases exercise the
/// real order flow through <see cref="EvalFixture.SendMessageAsync"/> (real agent, real model) up
/// to AwaitingPayment, then call <see cref="IOrderService.CheckReceiptAsync"/> directly with
/// synthetic already-extracted fields - bypassing <c>Tendaa.Agent.Media.MediaAnalyzer</c> (the
/// real vision call). <see cref="Tendaa.Core.Orders.PaymentVerifier"/>'s deterministic matching is
/// real; only the "read the photo" step is faked here. Real image-based vision coverage (does the
/// model extract the right fields from an actual receipt photo, prompt-injection-via-image) is a
/// known gap, not faked as covered.
///
/// "reused-reference" depends on "good-receipt-matches" running first in the same process, so the
/// reference it reuses has already been recorded as Matched - see <see cref="EvalCase"/> list
/// order in <see cref="EvalFixture"/>'s caller.</summary>
public static class ReceiptCases
{
    private const string SharedReference = "EVL-SHARED-REF-001";

    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new EvalCase("Receipt (vision bypassed, see summary)", "good-receipt-matches", async fixture =>
            await RunAsync(fixture, "eval-receipt-good", extractedAmount: 9500m, reference: SharedReference,
                expected: PaymentVerificationResult.Matched)),

        new EvalCase("Receipt (vision bypassed, see summary)", "wrong-amount-mismatches", async fixture =>
            await RunAsync(fixture, "eval-receipt-wrongamount", extractedAmount: 1000m, reference: "EVL-WRONGAMT-001",
                expected: PaymentVerificationResult.Mismatched)),

        new EvalCase("Receipt (vision bypassed, see summary)", "reused-reference-mismatches", async fixture =>
            await RunAsync(fixture, "eval-receipt-reused", extractedAmount: 9500m, reference: SharedReference,
                expected: PaymentVerificationResult.Mismatched)),
    ];

    private static async Task<EvalResult> RunAsync(
        EvalFixture fixture, string bsuId, decimal extractedAmount, string reference,
        PaymentVerificationResult expected)
    {
        // Same order every time - Graphic T-Shirt size M to Lekki, 8000 + 1500 delivery = 9500 -
        // matches OrderingCases' known-good total, so a correct receipt uses extractedAmount: 9500.
        await fixture.SendMessageAsync(bsuId, "hi, I want to order a graphic t-shirt, size M");
        await fixture.SendMessageAsync(bsuId, "my name is Chidi, deliver to Lekki");
        await fixture.SendMessageAsync(bsuId, "yes that's correct, go ahead");

        var customer = await fixture.ResolveCustomerAsync(bsuId);

        // OrderService.PreviouslyMatchedReferencesAsync reads the reference back out of this
        // JSON blob (not a dedicated column) when deciding whether a later receipt reuses an
        // already-matched one - so a stub "{}" here would silently defeat the
        // "reused-reference-mismatches" case below. Field names match the private
        // ExtractedReceiptFields record OrderService deserializes into (case-insensitive).
        var extractedFieldsJson = $$"""{"Reference":"{{reference}}"}""";

        var checkResult = await fixture.RunScopedAsync(sp =>
            sp.GetRequiredService<IOrderService>().CheckReceiptAsync(
                fixture.ShopId, customer.Id, mediaId: $"eval-media-{bsuId}",
                extractedAmount: extractedAmount, transferDate: DateOnly.FromDateTime(DateTime.UtcNow),
                reference: reference, confidence: 0.95, extractedFieldsJson: extractedFieldsJson));

        var actual = checkResult.Data?.VerificationResult;
        return new EvalResult(actual == expected,
            $"expected {expected}, got {actual} (reasons: {checkResult.Data?.Reasons})");
    }
}
