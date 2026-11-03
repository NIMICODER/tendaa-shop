using Tendaa.Core.Models.Enums.Orders;
using Tendaa.Core.Models.Enums.Payments;

namespace Tendaa.Application.Models;

public sealed record PaymentProofDetail(
    Guid PaymentProofId,
    string MediaId,
    string MediaUrl,
    decimal? ExtractedAmount,
    string? FormattedExtractedAmount,
    string? SenderName,
    string? BankName,
    string? TransferDate,
    string? Reference,
    double? Confidence,
    PaymentVerificationResult? VerificationResult,
    string? Reasons,
    string? ReviewedBy);

public sealed record OrderDetail(
    Guid OrderId,
    int Number,
    OrderStatus Status,
    string CustomerName,
    string? CustomerPhone,
    IReadOnlyList<OrderSummaryItem> Items,
    decimal ItemTotal,
    decimal DeliveryFee,
    decimal GrandTotal,
    string FormattedItemTotal,
    string FormattedDeliveryFee,
    string FormattedGrandTotal,
    string? DeliveryZoneName,
    string? Address,
    string? ConfirmedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AwaitingPaymentAt,
    DateTimeOffset? PaymentReviewAt,
    DateTimeOffset? ConfirmedAt,
    IReadOnlyList<PaymentProofDetail> PaymentProofs);
