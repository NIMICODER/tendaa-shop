using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Application.Models;

public sealed record HandoffListItem(
    Guid HandoffId,
    Guid ConversationId,
    string CustomerName,
    string Reason,
    string Summary,
    HandoffStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolvedBy);
