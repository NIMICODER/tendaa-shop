namespace Tendaa.Agent.Context;

/// <summary>Mutable default implementation of <see cref="IAgentRequestContext"/>. Registered
/// scoped; the caller sets every property once per turn before running the agent.</summary>
public sealed class AgentRequestContext : IAgentRequestContext
{
    public Guid ShopId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string? Phone { get; private set; }
    public Guid ConversationId { get; private set; }

    public void SetIdentity(Guid shopId, Guid customerId, string? phone, Guid conversationId)
    {
        ShopId = shopId;
        CustomerId = customerId;
        Phone = phone;
        ConversationId = conversationId;
    }
}
