using Tendaa.Core.Models.Enums.Conversations;

namespace Tendaa.Application.Models;

public sealed record ThreadToolTrace(
    string ToolName,
    string ArgumentsJson,
    string ResultJson,
    int DurationMs);

public sealed record ThreadMessage(
    MessageDirection Direction,
    MessageType Type,
    string? Text,
    string? Language,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ThreadToolTrace> ToolTraces);

public sealed record ConversationThread(
    Guid ConversationId,
    string CustomerName,
    IReadOnlyList<ThreadMessage> Messages);
