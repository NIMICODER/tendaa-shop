using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Services.Interfaces;

public interface IInboundEventService
{
    /// <summary>Inserts a new event; returns <c>false</c> (never throws) if this WhatsApp message
    /// id was already seen. The dedupe guard is the unique index on
    /// <see cref="InboundEvent.WhatsAppMessageId"/>, checked by catching the constraint violation
    /// rather than a check-then-insert query, to stay race-safe under Meta's own webhook
    /// retries.</summary>
    Task<bool> TryInsertAsync(InboundEvent inboundEvent, CancellationToken cancellationToken = default);

    /// <summary>Events whose webhook POST returned 200 but never finished processing — read once
    /// at startup to requeue them.</summary>
    Task<IReadOnlyList<InboundEvent>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(Guid inboundEventId, CancellationToken cancellationToken = default);

    /// <summary>Records a failed attempt, marking the event <c>Failed</c> once
    /// <paramref name="maxAttempts"/> is reached. Returns whether the caller should retry.</summary>
    Task<bool> RecordFailedAttemptAsync(
        Guid inboundEventId, string error, int maxAttempts, CancellationToken cancellationToken = default);
}
