namespace Tendaa.Core.Models.Entities;

/// <summary>One recorded tool call (name, arguments, result, duration) behind an assistant
/// message — what the dashboard's conversation trace view shows as "why the agent said this".</summary>
public sealed class ToolTrace : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid MessageId { get; set; }
    public required string ToolName { get; set; }
    public required string ArgumentsJson { get; set; }
    public required string ResultJson { get; set; }
    public required int DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
