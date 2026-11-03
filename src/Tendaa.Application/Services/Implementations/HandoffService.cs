using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class HandoffService(
    IRepository<Handoff> handoffs,
    IRepository<Conversation> conversations,
    IRepository<Customer> customers,
    IRepository<Message> messages,
    IUnitOfWork unitOfWork,
    IDashboardNotifier dashboardNotifier,
    ICustomerMessenger customerMessenger) : IHandoffService
{
    public async Task<TendaaResult> RequestHandoffAsync(
        Guid shopId,
        Guid conversationId,
        string reason,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var alreadyOpen = await handoffs.Query()
            .AnyAsync(h => h.ShopId == shopId && h.ConversationId == conversationId && h.Status == HandoffStatus.Open, cancellationToken);

        if (alreadyOpen)
        {
            return TendaaResult.Success("A handoff is already open for this conversation.");
        }

        var handoff = new Handoff
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            ConversationId = conversationId,
            Reason = reason,
            Summary = summary,
            Status = HandoffStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await handoffs.AddAsync(handoff, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dashboardNotifier.NotifyHandoffsChangedAsync(shopId, cancellationToken);

        return TendaaResult.Success();
    }

    public async Task<bool> HasOpenHandoffAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default) =>
        await handoffs.Query()
            .AnyAsync(h => h.ShopId == shopId && h.ConversationId == conversationId && h.Status == HandoffStatus.Open, cancellationToken);

    public async Task<TendaaResult<IReadOnlyList<HandoffListItem>>> ListAsync(
        Guid shopId, HandoffStatus? statusFilter, CancellationToken cancellationToken = default)
    {
        var query = handoffs.Query().Where(h => h.ShopId == shopId);
        if (statusFilter is { } status)
        {
            query = query.Where(h => h.Status == status);
        }

        // Sorted client-side: SQLite can't translate ORDER BY over DateTimeOffset.
        var rows = (await query.ToListAsync(cancellationToken)).OrderByDescending(h => h.CreatedAt).ToList();

        var conversationIds = rows.Select(h => h.ConversationId).Distinct().ToList();
        var customerByConversation = await conversations.Query()
            .Where(c => c.ShopId == shopId && conversationIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.CustomerId, cancellationToken);

        var customerIds = customerByConversation.Values.Distinct().ToList();
        var names = await customers.Query()
            .Where(c => c.ShopId == shopId && customerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        IReadOnlyList<HandoffListItem> items = rows.Select(h =>
        {
            names.TryGetValue(
                customerByConversation.TryGetValue(h.ConversationId, out var customerId) ? customerId : Guid.Empty,
                out var name);
            return new HandoffListItem(
                h.Id, h.ConversationId, name ?? "Customer", h.Reason, h.Summary,
                h.Status, h.CreatedAt, h.ResolvedAt, h.ResolvedBy);
        }).ToList();

        return TendaaResult<IReadOnlyList<HandoffListItem>>.Success(items);
    }

    public async Task<TendaaResult<HandoffDetail>> GetDetailAsync(
        Guid shopId, Guid handoffId, CancellationToken cancellationToken = default)
    {
        var detail = await BuildDetailAsync(shopId, handoffId, cancellationToken);
        return detail is null
            ? TendaaResult<HandoffDetail>.NotFound()
            : TendaaResult<HandoffDetail>.Success(detail);
    }

    public async Task<TendaaResult<HandoffDetail>> ResolveHandoffAsync(
        Guid shopId, Guid handoffId, string resolvedBy, string? replyText, CancellationToken cancellationToken = default)
    {
        var handoff = await handoffs.Query()
            .FirstOrDefaultAsync(h => h.ShopId == shopId && h.Id == handoffId, cancellationToken);
        if (handoff is null)
        {
            return TendaaResult<HandoffDetail>.NotFound();
        }

        if (handoff.Status != HandoffStatus.Open)
        {
            return TendaaResult<HandoffDetail>.BadRequest("This handoff is already resolved.");
        }

        var conversation = await conversations.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == handoff.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return TendaaResult<HandoffDetail>.NotFound("Conversation not found.");
        }

        var reply = replyText?.Trim();
        bool? sent = null;
        if (!string.IsNullOrWhiteSpace(reply))
        {
            sent = await customerMessenger.TrySendAsync(shopId, conversation.CustomerId, reply, cancellationToken);
            if (sent == true)
            {
                await messages.AddAsync(new Message
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopId,
                    ConversationId = conversation.Id,
                    Direction = MessageDirection.Outbound,
                    Type = MessageType.Text,
                    Text = reply,
                    CreatedAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
            }
        }

        handoff.Status = HandoffStatus.Resolved;
        handoff.ResolvedAt = DateTimeOffset.UtcNow;
        handoff.ResolvedBy = resolvedBy;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await dashboardNotifier.NotifyHandoffsChangedAsync(shopId, cancellationToken);

        var detail = await BuildDetailAsync(shopId, handoff.Id, cancellationToken);
        if (detail is null)
        {
            return TendaaResult<HandoffDetail>.InternalServerError("Handoff was resolved but could not be reloaded.");
        }

        var message = sent switch
        {
            true => "Handoff resolved. Your reply was sent.",
            false => "Handoff resolved, but the customer is outside WhatsApp's 24-hour window, so the reply was not sent.",
            null => "Handoff resolved.",
        };
        return TendaaResult<HandoffDetail>.Success(detail, message);
    }

    private async Task<HandoffDetail?> BuildDetailAsync(
        Guid shopId, Guid handoffId, CancellationToken cancellationToken)
    {
        var handoff = await handoffs.Query()
            .FirstOrDefaultAsync(h => h.ShopId == shopId && h.Id == handoffId, cancellationToken);
        if (handoff is null)
        {
            return null;
        }

        var conversation = await conversations.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == handoff.ConversationId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == conversation.CustomerId, cancellationToken);

        var recent = (await messages.Query()
                .Where(m => m.ShopId == shopId && m.ConversationId == conversation.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new HandoffMessage(m.Direction, m.Text, m.CreatedAt))
            .ToList();

        return new HandoffDetail(
            handoff.Id,
            handoff.ConversationId,
            customer?.Name ?? "Customer",
            customer?.PhoneNumber,
            handoff.Reason,
            handoff.Summary,
            handoff.Status,
            handoff.CreatedAt,
            handoff.ResolvedAt,
            handoff.ResolvedBy,
            recent);
    }
}
