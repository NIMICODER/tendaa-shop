using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Inbox;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class InboundEventService(IRepository<InboundEvent> inboundEvents, IUnitOfWork unitOfWork) : IInboundEventService
{
    public async Task<bool> TryInsertAsync(InboundEvent inboundEvent, CancellationToken cancellationToken = default)
    {
        await inboundEvents.AddAsync(inboundEvent, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // The only unique constraint on InboundEvent is WhatsAppMessageId, so a failure here
            // is the dedupe guard tripping on a duplicate/retried webhook delivery, not a real error.
            return false;
        }
    }

    public async Task<IReadOnlyList<InboundEvent>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        // Ordered client-side, not via SQL ORDER BY: this is a small, one-time startup query (a
        // handful of rows at most), and SQLite's provider — used in tests — can't translate
        // ORDER BY over DateTimeOffset. Sorting in .NET sidesteps that for a negligible cost here.
        var pending = await inboundEvents.Query()
            .Where(e => e.Status == InboundEventStatus.Pending)
            .ToListAsync(cancellationToken);

        return pending.OrderBy(e => e.CreatedAt).ToList();
    }

    public async Task MarkProcessedAsync(Guid inboundEventId, CancellationToken cancellationToken = default)
    {
        var inboundEvent = await inboundEvents.GetByIdAsync(inboundEventId, cancellationToken);
        if (inboundEvent is null)
        {
            return;
        }

        inboundEvent.Status = InboundEventStatus.Processed;
        inboundEvent.ProcessedAt = DateTimeOffset.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RecordFailedAttemptAsync(
        Guid inboundEventId, string error, int maxAttempts, CancellationToken cancellationToken = default)
    {
        var inboundEvent = await inboundEvents.GetByIdAsync(inboundEventId, cancellationToken);
        if (inboundEvent is null)
        {
            return false;
        }

        inboundEvent.Attempts++;
        inboundEvent.Error = error;

        var shouldRetry = inboundEvent.Attempts < maxAttempts;
        if (!shouldRetry)
        {
            inboundEvent.Status = InboundEventStatus.Failed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return shouldRetry;
    }
}
