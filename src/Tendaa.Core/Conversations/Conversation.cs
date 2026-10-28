namespace Tendaa.Core.Conversations;

public sealed class Conversation : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Serialized `AgentSession` (Phase 2), saved/restored via
    /// `SerializeSessionAsync`/`DeserializeSessionAsync`. Null until the agent has run once.</summary>
    public string? SessionJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<Message> Messages { get; set; } = [];
}
