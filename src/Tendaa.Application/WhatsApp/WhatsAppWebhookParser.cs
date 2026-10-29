using System.Text.Json;

namespace Tendaa.Application.WhatsApp;

/// <summary>Turns a raw WhatsApp webhook POST body into <see cref="ParsedWebhookEvent"/>. Pure,
/// stateless, never throws on a malformed or partial payload — a webhook endpoint must never 500
/// because of something the other side sent (callers should still 400 on fully unparseable JSON,
/// which <see cref="JsonException"/> signals).</summary>
public static class WhatsAppWebhookParser
{
    public static ParsedWebhookEvent Parse(string rawJson)
    {
        var payload = JsonSerializer.Deserialize<WhatsAppWebhookPayload>(rawJson)
            ?? new WhatsAppWebhookPayload(Entry: null);

        var value = payload.Entry?.FirstOrDefault()?.Changes?.FirstOrDefault()?.Value;
        if (value is null)
        {
            return new ParsedWebhookEvent(PhoneNumberId: null, Messages: []);
        }

        var contacts = value.Contacts ?? [];
        var messages = new List<ParsedInboundMessage>();

        foreach (var message in value.Messages ?? [])
        {
            var parsed = ParseMessage(message, contacts);
            // A message with no BSUID can't be represented — ARCHITECTURE.md's BSUID is "always
            // present" by design, but a parser must stay defensive against a payload that
            // somehow violates that rather than throwing and dropping the whole webhook.
            if (parsed is not null)
            {
                messages.Add(parsed);
            }
        }

        return new ParsedWebhookEvent(value.Metadata?.PhoneNumberId, messages);
    }

    private static ParsedInboundMessage? ParseMessage(WhatsAppMessage message, IReadOnlyList<WhatsAppContact> contacts)
    {
        if (string.IsNullOrWhiteSpace(message.Id))
        {
            return null;
        }

        if (message.Type == "system" && message.System is { } system)
        {
            return ParseSystemMessage(message.Id, system);
        }

        if (message.Type == "text" && message.Text is { Body: { } text })
        {
            if (string.IsNullOrWhiteSpace(message.FromUserId))
            {
                return null;
            }

            var contact = contacts.FirstOrDefault(c => c.UserId == message.FromUserId);
            return new ParsedTextMessage(
                message.Id, message.FromUserId, text,
                contact?.WaId, contact?.Profile?.Name, contact?.Profile?.Username);
        }

        var bsuid = message.FromUserId ?? message.System?.UserId;
        return string.IsNullOrWhiteSpace(bsuid)
            ? null
            : new ParsedUnsupportedMessage(message.Id, bsuid, message.Type);
    }

    private static ParsedInboundMessage? ParseSystemMessage(string messageId, WhatsAppSystem system)
    {
        if (system.Type is "user_changed_number" or "user_changed_user_id"
            && !string.IsNullOrWhiteSpace(system.UserId)
            && !string.IsNullOrWhiteSpace(system.PreviousUserId))
        {
            return new ParsedBsuidChangeMessage(messageId, system.UserId, system.PreviousUserId, system.WaId);
        }

        return string.IsNullOrWhiteSpace(system.UserId)
            ? null
            : new ParsedUnsupportedMessage(messageId, system.UserId, system.Type);
    }
}
