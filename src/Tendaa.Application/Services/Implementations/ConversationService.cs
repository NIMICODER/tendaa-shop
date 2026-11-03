using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ConversationService(
    IRepository<Conversation> conversations,
    IRepository<Message> messages,
    IRepository<Customer> customers,
    IRepository<Handoff> handoffs,
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
        MessageType inboundType = MessageType.Text,
        string? language = null,
        string? mediaId = null,
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
            Type = inboundType,
            Text = userText,
            Language = language,
            MediaId = mediaId,
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

    public async Task<TendaaResult<IReadOnlyList<ConversationListItem>>> ListForDashboardAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var convos = await conversations.Query()
            .Where(c => c.ShopId == shopId)
            .ToListAsync(cancellationToken);

        var customerIds = convos.Select(c => c.CustomerId).Distinct().ToList();
        var names = await customers.Query()
            .Where(c => c.ShopId == shopId && customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var openHandoffConversations = await handoffs.Query()
            .Where(h => h.ShopId == shopId && h.Status == HandoffStatus.Open)
            .Select(h => h.ConversationId)
            .ToListAsync(cancellationToken);

        var counts = await messages.Query()
            .Where(m => m.ShopId == shopId)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countByConversation = counts.ToDictionary(r => r.ConversationId, r => r.Count);

        IReadOnlyList<ConversationListItem> items = convos
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Select(c => new ConversationListItem(
                c.Id,
                names.TryGetValue(c.CustomerId, out var name) ? name ?? "Customer" : "Customer",
                countByConversation.TryGetValue(c.Id, out var messageCount) ? messageCount : 0,
                openHandoffConversations.Contains(c.Id),
                c.LastMessageAt))
            .ToList();

        return TendaaResult<IReadOnlyList<ConversationListItem>>.Success(items);
    }

    public async Task<TendaaResult<ConversationThread>> GetThreadAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await conversations.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == conversationId, cancellationToken);
        if (conversation is null)
        {
            return TendaaResult<ConversationThread>.NotFound();
        }

        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == conversation.CustomerId, cancellationToken);

        // Sorted client-side: SQLite can't translate ORDER BY over DateTimeOffset.
        var threadMessages = (await messages.Query()
                .Where(m => m.ShopId == shopId && m.ConversationId == conversationId)
                .Include(m => m.ToolTraces)
                .ToListAsync(cancellationToken))
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ThreadMessage(
                m.Direction,
                m.Type,
                m.Text,
                m.Language,
                m.CreatedAt,
                m.ToolTraces
                    .OrderBy(t => t.CreatedAt)
                    .Select(t => new ThreadToolTrace(t.ToolName, t.ArgumentsJson, t.ResultJson, t.DurationMs))
                    .ToList()))
            .ToList();

        return TendaaResult<ConversationThread>.Success(
            new ConversationThread(conversationId, customer?.Name ?? "Customer", threadMessages));
    }
}
