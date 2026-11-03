using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Payments;
using Tendaa.Core.Orders;

namespace Tendaa.Core.Tests.Orders;

/// <summary>Pure C#, no fakes needed — this is the actual judged logic of Phase 4b's receipt
/// check (SPEC.md: "never auto-confirm, compare in code").</summary>
public class PaymentVerifierTests
{
    private static Order NewOrder(decimal grandTotal, DateTimeOffset awaitingPaymentAt) => new()
    {
        Number = 7,
        GrandTotal = grandTotal,
        AwaitingPaymentAt = awaitingPaymentAt,
        CreatedAt = awaitingPaymentAt,
    };

    private static readonly DateTimeOffset OrderPlacedAt = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly OrderPlacedDate = DateOnly.FromDateTime(OrderPlacedAt.UtcDateTime);

    [Fact]
    public void Verify_MatchingAmountDateReference_ReturnsMatched()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Matched, result);
        Assert.Equal("", reasons);
    }

    [Fact]
    public void Verify_WrongAmount_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 10000m, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("amount", reasons);
    }

    [Fact]
    public void Verify_NoAmountFound_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, null, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("no amount found", reasons);
    }

    [Fact]
    public void Verify_TransferDateBeforeOrderWasPlaced_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(-1), "ref-123", confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("before the order was placed", reasons);
    }

    [Fact]
    public void Verify_NoTransferDateFound_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, null, "ref-123", confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("no transfer date found", reasons);
    }

    [Fact]
    public void Verify_NoReferenceFound_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(1), null, confidence: 0.95, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("no payment reference found", reasons);
    }

    [Fact]
    public void Verify_ReusedReference_ReturnsMismatchedWithReason()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.95,
            previousReferencesForShop: ["ref-123"]);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("already used", reasons);
    }

    [Fact]
    public void Verify_ReusedReference_IsCaseInsensitive()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, _) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(1), "REF-123", confidence: 0.95,
            previousReferencesForShop: ["ref-123"]);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
    }

    [Fact]
    public void Verify_LowConfidenceDespiteMatchingFields_ReturnsSuspicious()
    {
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 37500m, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.3, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Suspicious, result);
        Assert.Contains("low extraction confidence", reasons);
    }

    [Fact]
    public void Verify_LowConfidenceWithMismatchedFields_StillReturnsMismatched()
    {
        // A low-confidence extraction of a wrong amount is still a mismatch, not merely
        // "suspicious" — Suspicious is reserved for "the fields look right but I'm not sure."
        var order = NewOrder(37500m, OrderPlacedAt);

        var (result, reasons) = PaymentVerifier.Verify(
            order, 10000m, OrderPlacedDate.AddDays(1), "ref-123", confidence: 0.2, previousReferencesForShop: []);

        Assert.Equal(PaymentVerificationResult.Mismatched, result);
        Assert.Contains("amount", reasons);
    }
}
