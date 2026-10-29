using System.Text.Json.Serialization;

namespace Tendaa.Application.WhatsApp;

/// <summary>Raw wire-format DTOs for a WhatsApp Cloud API webhook POST body — only the fields
/// Tendaa actually uses (CLAUDE.md's "WhatsApp Cloud API" section), not the full schema. Field
/// names confirmed against developers.facebook.com, Oct 2026 (see CLAUDE.md).</summary>
public sealed record WhatsAppWebhookPayload(
    [property: JsonPropertyName("entry")] IReadOnlyList<WhatsAppEntry>? Entry);

public sealed record WhatsAppEntry(
    [property: JsonPropertyName("changes")] IReadOnlyList<WhatsAppChange>? Changes);

public sealed record WhatsAppChange(
    [property: JsonPropertyName("value")] WhatsAppChangeValue? Value);

public sealed record WhatsAppChangeValue(
    [property: JsonPropertyName("metadata")] WhatsAppMetadata? Metadata,
    [property: JsonPropertyName("contacts")] IReadOnlyList<WhatsAppContact>? Contacts,
    [property: JsonPropertyName("messages")] IReadOnlyList<WhatsAppMessage>? Messages);

public sealed record WhatsAppMetadata(
    [property: JsonPropertyName("phone_number_id")] string? PhoneNumberId);

public sealed record WhatsAppContact(
    [property: JsonPropertyName("wa_id")] string? WaId,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("profile")] WhatsAppProfile? Profile);

public sealed record WhatsAppProfile(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("username")] string? Username);

public sealed record WhatsAppMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("from_user_id")] string? FromUserId,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WhatsAppTextBody? Text,
    [property: JsonPropertyName("system")] WhatsAppSystem? System);

public sealed record WhatsAppTextBody(
    [property: JsonPropertyName("body")] string? Body);

/// <summary>BSUID-change system message — see ARCHITECTURE.md "Customer identity" / CLAUDE.md's
/// WhatsApp Cloud API section. <see cref="Type"/> is <c>user_changed_number</c> or
/// <c>user_changed_user_id</c>.</summary>
public sealed record WhatsAppSystem(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("wa_id")] string? WaId,
    [property: JsonPropertyName("user_id")] string? UserId,
    [property: JsonPropertyName("previous_user_id")] string? PreviousUserId);
