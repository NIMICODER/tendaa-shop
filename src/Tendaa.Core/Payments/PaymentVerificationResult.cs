namespace Tendaa.Core.Payments;

/// <summary>Set by <c>PaymentVerifier</c> (Phase 4b) after checking a receipt against the order.
/// Null until checked.</summary>
public enum PaymentVerificationResult
{
    Matched,
    Mismatched,
    Suspicious,
}
