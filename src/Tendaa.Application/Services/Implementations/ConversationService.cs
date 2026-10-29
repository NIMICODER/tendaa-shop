using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ConversationService(
    IRepository<Conversation> conversations,
    IRepository<Message> messages,
    IUnitOfWork unitOfWork) : IConversationService
{
    public async Task<TendaaResult<Conversation>> GetOrCreateAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.CustomerId == customerId, cancellationToken);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                CustomerId = customerId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await conversations.AddAsync(conversation, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return TendaaResult<Conversation>.Success(conversation);
    }

    public async Task<AgentSession> LoadSessionAsync(
        AIAgent agent, Conversation conversation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversation.SessionJson))
        {
            return await agent.CreateSessionAsync(cancellationToken);
        }

        using var document = JsonDocument.Parse(conversation.SessionJson);
        return await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
    }

    public async Task SaveSessionAsync(
        AIAgent agent, Conversation conversation, AgentSession session, CancellationToken cancellationToken = default)
    {
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
        conversation.SessionJson = serialized.GetRawText();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveTurnAsync(
        Guid shopId,
        Guid conversationId,
        string userText,
        string? assistantText,
        IReadOnlyList<ToolTraceEntry> toolTraces,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Runs on every turn, including the handoff-silent path (assistantText null) — this is
        // the one place guaranteed to fire per inbound message, so it's where conversation
        // staleness gets "touched" rather than in SaveSessionAsync, which only runs when the
        // agent actually replies.
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.LastMessageAt = now;
        }

        var inbound = new Message
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            ConversationId = conversationId,
            Direction = MessageDirection.Inbound,
            Type = MessageType.Text,
            Text = userText,
            CreatedAt = now,
        };
        await messages.AddAsync(inbound, cancellationToken);

        if (assistantText is not null)
        {
            var outboundId = Guid.NewGuid();
            var outbound = new Message
            {
                Id = outboundId,
                ShopId = shopId,
                ConversationId = conversationId,
                Direction = MessageDirection.Outbound,
                Type = MessageType.Text,
                Text = assistantText,
                CreatedAt = now,
                ToolTraces = toolTraces
                    .Select(t => new ToolTrace
                    {
                        Id = Guid.NewGuid(),
                        ShopId = shopId,
                        MessageId = outboundId,
                        ToolName = t.ToolName,
                        ArgumentsJson = t.ArgumentsJson,
                        ResultJson = t.ResultJson,
                        DurationMs = t.DurationMs,
                        CreatedAt = now,
                    })
                    .ToList(),
            };
            await messages.AddAsync(outbound, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
