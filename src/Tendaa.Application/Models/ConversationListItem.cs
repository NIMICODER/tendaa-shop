namespace Tendaa.Application.Models;

public sealed record ConversationListItem(
    Guid ConversationId,
    string CustomerName,
    int MessageCount,
    bool HasOpenHandoff,
    DateTimeOffset? LastMessageAt);
