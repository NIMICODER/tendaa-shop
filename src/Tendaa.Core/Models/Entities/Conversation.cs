namespace Tendaa.Core.Models.Entities;

public sealed class Conversation : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Serialized `AgentSession` (Phase 2), saved/restored via
    /// `SerializeSessionAsync`/`DeserializeSessionAsync`. Null until the agent has run once.</summary>
    public string? SessionJson { get; set; }

    /// <summary>Set on every turn (see `ConversationService.SaveTurnAsync`), including
    /// handed-off ones. `AgentTurnRunner` uses this to decide whether to start a fresh session
    /// instead of loading a stale one — see `AgentOptions.StaleConversationHours`.</summary>
    public DateTimeOffset? LastMessageAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<Message> Messages { get; set; } = [];
}
