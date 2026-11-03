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

/// <summary>A voice note. <see cref="MediaId"/> resolves to bytes via
/// <c>IWhatsAppMediaDownloader</c>, then <c>ITranscriber</c> — see Phase 4a. Carries the same
/// contact fields as <see cref="ParsedTextMessage"/> so a first-time voice note still resolves a
/// full <c>Customer</c>, not a bare BSUID.</summary>
public sealed record ParsedVoiceMessage(
    string WhatsAppMessageId, string Bsuid, string MediaId, string? Phone, string? Name, string? Username)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);

/// <summary>A customer-sent photo — a receipt, a product photo, or something else;
/// <c>IMediaAnalyzer</c> decides which. See Phase 4b.</summary>
public sealed record ParsedImageMessage(
    string WhatsAppMessageId, string Bsuid, string MediaId, string? Caption,
    string? Phone, string? Name, string? Username)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);

/// <summary>Anything not yet handled — an unrecognized message or system message type. Parsing
/// never throws on these; the processor decides what, if anything, to do with them.</summary>
public sealed record ParsedUnsupportedMessage(string WhatsAppMessageId, string Bsuid, string? MessageType)
    : ParsedInboundMessage(WhatsAppMessageId, Bsuid);
