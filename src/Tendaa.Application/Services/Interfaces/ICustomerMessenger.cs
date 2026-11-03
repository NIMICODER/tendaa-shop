namespace Tendaa.Application.Services.Interfaces;

/// <summary>Sends a seller-initiated WhatsApp message to a customer, honoring Meta's 24-hour
/// customer-service window: messages are only sent when the customer inbound-messaged within
/// the last 24 hours, otherwise the call is skipped and the caller tells the seller (ARCHITECTURE.md
/// "Outbound messages"). Shared by order confirm/reject and handoff replies.</summary>
public interface ICustomerMessenger
{
    /// <returns>True when the message was sent; false when skipped (unknown customer, no
    /// inbound history, or outside the 24-hour window).</returns>
    Task<bool> TrySendAsync(Guid shopId, Guid customerId, string text, CancellationToken cancellationToken = default);
}
