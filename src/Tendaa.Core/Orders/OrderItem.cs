namespace Tendaa.Core.Orders;

public sealed class OrderItem : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductVariantId { get; set; }
    public required int Quantity { get; set; }

    /// <summary>Snapshot of the variant's effective price at the time it was added to the order,
    /// so a later catalog price change never changes an existing order's total.</summary>
    public required decimal UnitPrice { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
