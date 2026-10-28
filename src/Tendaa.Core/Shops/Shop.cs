namespace Tendaa.Core.Shops;

public sealed class Shop
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Country { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "NGN".</summary>
    public required string Currency { get; set; }

    /// <summary>IANA time zone id, e.g. "Africa/Lagos".</summary>
    public required string TimeZone { get; set; }

    public string? HoursText { get; set; }
    public string? RulesText { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankAccountName { get; set; }

    /// <summary>Meta's phone_number_id for this shop's own WhatsApp Business line (sending), not
    /// a customer's phone number.</summary>
    public string? WhatsAppPhoneNumberId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
