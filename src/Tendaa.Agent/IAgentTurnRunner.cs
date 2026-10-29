using Tendaa.Core.Models.Entities;

namespace Tendaa.Agent;

/// <summary>Runs one agent turn end to end: set identity, load the session, run the agent, save
/// the session, persist the turn. Extracted so both the dev chat endpoint and the real webhook
/// processor (Phase 3) share this instead of duplicating it — see PLAN.md's Phase 2 note that
/// <c>POST /dev/chat</c> is "a stand-in for what Phase 3's real webhook processor will do."</summary>
public interface IAgentTurnRunner
{
    Task<string> RunTurnAsync(
        Guid shopId,
        Guid customerId,
        string? phone,
        Conversation conversation,
        string messageText,
        CancellationToken cancellationToken = default);
}
