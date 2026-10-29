using Tendaa.Core.Models.Entities;

namespace Tendaa.Core;

/// <summary>Sends outbound WhatsApp messages. Addressing is the sender's job, not the caller's —
/// see ARCHITECTURE.md's "Customer identity": <c>to</c> = phone when
/// <see cref="Customer.PhoneNumber"/> is known, else <c>recipient</c> = <see cref="Customer.BsuId"/>,
/// never both.</summary>
public interface IWhatsAppSender
{
    Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default);

    /// <summary>Sends an image by public link (confirmed against developers.facebook.com: JPEG
    /// or PNG, 5 MB max, caption optional up to 1024 characters). Meta recommends uploaded media
    /// ids for "better performance", but link-based needs no upload pipeline — the right trade-off
    /// for this product's timeline.</summary>
    Task SendImageAsync(Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default);

    /// <summary>Marks the message read and shows a typing indicator (dismisses after 25s or on
    /// the next send) — both are the same Graph API call, so there's no separate method for it.</summary>
    Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default);
}
