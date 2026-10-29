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
}
