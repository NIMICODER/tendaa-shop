using Tendaa.Application.Common;

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
}
