using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;

namespace Tendaa.Agent;

/// <summary>Runs one agent turn end to end: set identity, load the session, run the agent, save
/// the session, persist the turn. Extracted so both the dev chat endpoint and the real webhook
/// processor (Phase 3) share this instead of duplicating it — see PLAN.md's Phase 2 note that
/// <c>POST /dev/chat</c> is "a stand-in for what Phase 3's real webhook processor will do."</summary>
public interface IAgentTurnRunner
{
    /// <summary><paramref name="messageText"/> is always the resolved text the agent reasons over
    /// — a voice note's transcript, not raw audio. <paramref name="inboundType"/>/
    /// <paramref name="language"/>/<paramref name="mediaId"/> describe the original inbound
    /// message for <see cref="Tendaa.Application.Services.Interfaces.IConversationService.SaveTurnAsync"/>;
    /// default to today's text-message behavior.</summary>
    Task<string> RunTurnAsync(
        Guid shopId,
        Guid customerId,
        string? phone,
        Conversation conversation,
        string messageText,
        MessageType inboundType = MessageType.Text,
        string? language = null,
        string? mediaId = null,
        CancellationToken cancellationToken = default);
}
