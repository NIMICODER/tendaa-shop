using Microsoft.Agents.AI;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Entities;

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

    /// <summary>Persists the inbound customer message and the outbound assistant reply, with
    /// <paramref name="toolTraces"/> linked to the assistant <c>Message</c>, in one call.</summary>
    Task SaveTurnAsync(
        Guid shopId,
        Guid conversationId,
        string userText,
        string assistantText,
        IReadOnlyList<ToolTraceEntry> toolTraces,
        CancellationToken cancellationToken = default);
}
