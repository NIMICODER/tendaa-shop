namespace Tendaa.Application.Models;

public sealed record StockIssue(Guid RequestedProductVariantId, string RequestedDescription, IReadOnlyList<VariantStock> Alternatives);

/// <summary><see cref="Summary"/> is only populated once <see cref="MissingFields"/> and
/// <see cref="StockIssues"/> are both empty — otherwise the agent asks about the missing field or
/// offers the alternatives instead of showing a total.</summary>
public sealed record UpsertDraftOrderResult(
    Guid OrderId,
    IReadOnlyList<string> MissingFields,
    IReadOnlyList<StockIssue> StockIssues,
    OrderSummary? Summary);
