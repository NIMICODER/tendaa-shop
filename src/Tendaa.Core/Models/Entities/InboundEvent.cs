using Tendaa.Core.Models.Enums.Inbox;

namespace Tendaa.Core.Models.Entities;

/// <summary>A durably-stored raw webhook event, inserted before processing starts — the "inbox"
/// half of the inbox pattern (ARCHITECTURE.md "Crash safety"). Separate from <see cref="Message"/>:
/// this is the raw payload for dedupe/retry/replay, not the agent's conversation history.</summary>
public sealed class InboundEvent : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }

    /// <summary>Unique across the whole platform — the dedupe guard. A duplicate webhook delivery
    /// hits this constraint on insert rather than being checked for up front, to stay race-safe
    /// under Meta's own webhook retries.</summary>
    public required string WhatsAppMessageId { get; set; }

    /// <summary>The sender's BSUID, denormalized from the payload so a startup requeue can pick
    /// the right partition without re-parsing <see cref="RawPayloadJson"/>.</summary>
    public required string Bsuid { get; set; }

    public required string RawPayloadJson { get; set; }

    public InboundEventStatus Status { get; set; } = InboundEventStatus.Pending;
    public int Attempts { get; set; }
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
