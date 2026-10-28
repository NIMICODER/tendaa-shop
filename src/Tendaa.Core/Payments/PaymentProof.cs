namespace Tendaa.Core.Payments;

public sealed class PaymentProof : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public required string MediaId { get; set; }

    /// <summary>Extracted fields (amount, sender, bank, date, reference, confidence) as JSON —
    /// shape owned by the Phase 4b receipt extractor, not fixed columns here.</summary>
    public string? ExtractedFieldsJson { get; set; }

    public PaymentVerificationResult? VerificationResult { get; set; }
    public string? Reasons { get; set; }
    public string? ReviewedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
