using Microsoft.Agents.AI;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;

namespace Tendaa.Application.Services.Interfaces;

public interface IConversationService
{
    Task<TendaaResult<Conversation>> GetOrCreateAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Restores <paramref name="conversation"/>'s saved session, or creates a fresh one
    /// if this is its first turn.</summary>
    Task<AgentSession> LoadSessionAsync(
        AIAgent agent, Conversation conversation, CancellationToken cancellationToken = default);

    Task SaveSessionAsync(
        AIAgent agent, Conversation conversation, AgentSession session, CancellationToken cancellationToken = default);

    /// <summary>Persists the inbound customer message and, when <paramref name="assistantText"/>
    /// is non-null, the outbound assistant reply with <paramref name="toolTraces"/> linked to it
    /// — in one call. Pass null for <paramref name="assistantText"/> when the agent never ran
    /// (e.g. the conversation is handed off, SPEC.md rule 7): only the inbound message is saved.
    /// <paramref name="inboundType"/>/<paramref name="language"/>/<paramref name="mediaId"/>
    /// describe the inbound message itself (a voice note's transcript is stored as
    /// <paramref name="userText"/>, with <see cref="MessageType.Audio"/> and the WhatsApp media
    /// id recorded alongside it — see ARCHITECTURE.md's "Media pipeline").</summary>
    Task SaveTurnAsync(
        Guid shopId,
        Guid conversationId,
        string userText,
        string? assistantText,
        IReadOnlyList<ToolTraceEntry> toolTraces,
        MessageType inboundType = MessageType.Text,
        string? language = null,
        string? mediaId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Dashboard: all conversations with customer names, message counts, and handoff flags.</summary>
    Task<TendaaResult<IReadOnlyList<ConversationListItem>>> ListForDashboardAsync(
        Guid shopId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: full message thread with each message's tool-call trace.</summary>
    Task<TendaaResult<ConversationThread>> GetThreadAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default);
}
