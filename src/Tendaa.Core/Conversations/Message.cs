namespace Tendaa.Core.Conversations;

public sealed class Message : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid ConversationId { get; set; }
    public required MessageDirection Direction { get; set; }
    public required MessageType Type { get; set; }

    /// <summary>The message text, or a voice note's transcript — transcripts are treated as the
    /// customer's text (see ARCHITECTURE.md's media pipeline), not stored separately.</summary>
    public string? Text { get; set; }

    /// <summary>Detected language of a voice-note transcript, when known.</summary>
    public string? Language { get; set; }

    public string? MediaId { get; set; }
    public string? WhatsAppMessageId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<ToolTrace> ToolTraces { get; set; } = [];
}
