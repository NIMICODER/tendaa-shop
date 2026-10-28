namespace Tendaa.Core.Handoffs;

public sealed class Handoff : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid ConversationId { get; set; }
    public required string Reason { get; set; }
    public required string Summary { get; set; }
    public HandoffStatus Status { get; set; } = HandoffStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
}
