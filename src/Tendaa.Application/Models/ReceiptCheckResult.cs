using Tendaa.Core.Models.Enums.Payments;

namespace Tendaa.Application.Models;

/// <summary><see cref="OrderFound"/> is false when the customer has no order currently
/// <c>AwaitingPayment</c> — the processor turns that into a context note for the agent rather
/// than a crash. <see cref="VerificationResult"/>/<see cref="Reasons"/> are null exactly when
/// <see cref="OrderFound"/> is false.</summary>
public sealed record ReceiptCheckResult(
    bool OrderFound,
    int? OrderNumber,
    PaymentVerificationResult? VerificationResult,
    string? Reasons);
