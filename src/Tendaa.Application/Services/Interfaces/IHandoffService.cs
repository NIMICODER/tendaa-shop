using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Application.Services.Interfaces;

public interface IHandoffService
{
    /// <summary>Opens a handoff for the conversation, unless one is already open — calling this
    /// twice in the same conversation (e.g. the model re-asking) never creates duplicate rows.</summary>
    Task<TendaaResult> RequestHandoffAsync(
        Guid shopId,
        Guid conversationId,
        string reason,
        string summary,
        CancellationToken cancellationToken = default);

    /// <summary>Whether this conversation has an open handoff — the webhook processor checks
    /// this before running the agent, per SPEC.md rule 7 ("stays silent on handed-off chats until
    /// resolved") and ARCHITECTURE.md's sequence diagram <c>alt conversation handed off</c>
    /// branch.</summary>
    Task<bool> HasOpenHandoffAsync(Guid shopId, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: handoffs, newest first, optionally filtered by status.</summary>
    Task<TendaaResult<IReadOnlyList<HandoffListItem>>> ListAsync(
        Guid shopId, HandoffStatus? statusFilter, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: handoff detail with customer contact and recent conversation context.</summary>
    Task<TendaaResult<HandoffDetail>> GetDetailAsync(
        Guid shopId, Guid handoffId, CancellationToken cancellationToken = default);

    /// <summary>Dashboard: resolves the handoff, optionally sending <paramref name="replyText"/>
    /// to the customer first (same 24-hour rule as <see cref="ICustomerMessenger"/> — a blank
    /// reply resolves silently). The reply is persisted as an outbound message only when
    /// actually sent.</summary>
    Task<TendaaResult<HandoffDetail>> ResolveHandoffAsync(
        Guid shopId, Guid handoffId, string resolvedBy, string? replyText, CancellationToken cancellationToken = default);
}
