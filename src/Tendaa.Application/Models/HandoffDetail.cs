using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Application.Models;

public sealed record HandoffMessage(
    MessageDirection Direction,
    string? Text,
    DateTimeOffset CreatedAt);

public sealed record HandoffDetail(
    Guid HandoffId,
    Guid ConversationId,
    string CustomerName,
    string? CustomerPhone,
    string Reason,
    string Summary,
    HandoffStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy,
    IReadOnlyList<HandoffMessage> RecentMessages);
