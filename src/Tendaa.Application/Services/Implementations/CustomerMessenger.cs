using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class CustomerMessenger(
    IRepository<Customer> customers,
    IRepository<Conversation> conversations,
    IRepository<Message> messages,
    IWhatsAppSender whatsAppSender) : ICustomerMessenger
{
    public async Task<bool> TrySendAsync(
        Guid shopId, Guid customerId, string text, CancellationToken cancellationToken = default)
    {
        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return false;
        }

        var conversationIds = await conversations.Query()
            .Where(c => c.ShopId == shopId && c.CustomerId == customerId)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        // Max taken client-side: SQLite's provider can't translate ORDER BY over DateTimeOffset
        // (same reason InboundEventService sorts Pending events in .NET).
        var lastInbound = await messages.Query()
            .Where(m => m.ShopId == shopId
                && conversationIds.Contains(m.ConversationId)
                && m.Direction == MessageDirection.Inbound)
            .Select(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var mostRecent = lastInbound.Count == 0 ? (DateTimeOffset?)null : lastInbound.Max();
        if (mostRecent is null || DateTimeOffset.UtcNow - mostRecent > TimeSpan.FromHours(24))
        {
            return false;
        }

        await whatsAppSender.SendTextAsync(customer, text, cancellationToken);
        return true;
    }
}
