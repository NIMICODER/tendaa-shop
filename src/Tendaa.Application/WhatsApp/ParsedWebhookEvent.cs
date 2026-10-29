namespace Tendaa.Application.WhatsApp;

/// <summary>Everything the webhook controller and processor need from one webhook POST, after
/// stripping away the wire-format nesting. A single POST can carry more than one message.</summary>
public sealed record ParsedWebhookEvent(string? PhoneNumberId, IReadOnlyList<ParsedInboundMessage> Messages);

public abstract record ParsedInboundMessage(string WhatsAppMessageId, string Bsuid);

public sealed record ParsedTextMessage(
    string WhatsAppMessageId, string Bsuid, string Text, string? Phone, string? Name, string? Username)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);

/// <summary>A BSUID-change system message — <see cref="ParsedInboundMessage.Bsuid"/> is the
/// customer's *new* BSUID; <see cref="PreviousBsuid"/> is the one to look the existing
/// <c>Customer</c> row up by. See ARCHITECTURE.md "Customer identity".</summary>
public sealed record ParsedBsuidChangeMessage(
    string WhatsAppMessageId, string Bsuid, string PreviousBsuid, string? NewPhone)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);

/// <summary>Anything not yet handled — non-text message types (voice, image: Phase 4) or an
/// unrecognized system message type. Parsing never throws on these; the processor decides what,
/// if anything, to do with them.</summary>
public sealed record ParsedUnsupportedMessage(string WhatsAppMessageId, string Bsuid, string? MessageType)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);
