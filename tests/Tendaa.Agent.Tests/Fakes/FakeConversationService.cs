using Microsoft.Agents.AI;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;

namespace Tendaa.Agent.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no database. Mirrors the real
/// <c>ConversationService</c>'s session create/restore behaviour (needed so <c>AIAgent.RunAsync</c>
/// gets a valid session) but records calls instead of persisting anything.</summary>
public sealed class FakeConversationService : IConversationService
{
    public string? LastUserText { get; private set; }
    public string? LastAssistantText { get; private set; }
    public MessageType? LastInboundType { get; private set; }
    public string? LastLanguage { get; private set; }
    public string? LastMediaId { get; private set; }
    public int LoadSessionCallCount { get; private set; }

    public Task<TendaaResult<Conversation>> GetOrCreateAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by AgentTurnRunner tests.");

    public async Task<AgentSession> LoadSessionAsync(
        AIAgent agent, Conversation conversation, CancellationToken cancellationToken = default)
    {
        LoadSessionCallCount++;
        return await agent.CreateSessionAsync(cancellationToken);
    }

    public Task SaveSessionAsync(
        AIAgent agent, Conversation conversation, AgentSession session, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SaveTurnAsync(
        Guid shopId,
        Guid conversationId,
        string userText,
        string? assistantText,
        IReadOnlyList<ToolTraceEntry> toolTraces,
        MessageType inboundType = MessageType.Text,
        string? language = null,
        string? mediaId = null,
        CancellationToken cancellationToken = default)
    {
        LastUserText = userText;
        LastAssistantText = assistantText;
        LastInboundType = inboundType;
        LastLanguage = language;
        LastMediaId = mediaId;
        return Task.CompletedTask;
    }

    public Task<TendaaResult<IReadOnlyList<ConversationListItem>>> ListForDashboardAsync(
        Guid shopId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by AgentTurnRunner tests.");

    public Task<TendaaResult<ConversationThread>> GetThreadAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by AgentTurnRunner tests.");
}
