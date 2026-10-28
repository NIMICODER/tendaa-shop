namespace Tendaa.Core.Delivery;

public sealed class DeliveryZone : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public required string Name { get; set; }
    public required decimal Fee { get; set; }

    /// <summary>Free text, e.g. "Same day" or "1 to 2 business days".</summary>
    public required string DeliveryTimeText { get; set; }
}
