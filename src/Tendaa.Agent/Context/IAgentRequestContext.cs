namespace Tendaa.Agent.Context;

/// <summary>Identity for the current agent turn, set once by the caller (the dev chat endpoint for
/// now, Phase 3's webhook processor later) before <c>agent.RunAsync</c> runs. Tools read these
/// values directly and never accept them as model arguments — SPEC.md's identity rule means the
/// model can never supply its own ShopId/CustomerId.</summary>
public interface IAgentRequestContext
{
    Guid ShopId { get; }
    Guid CustomerId { get; }
    string? Phone { get; }
    Guid ConversationId { get; }

    /// <summary>Sets identity for this turn, once, before <c>agent.RunAsync</c> runs.</summary>
    void SetIdentity(Guid shopId, Guid customerId, string? phone, Guid conversationId);
}
