namespace Tendaa.Core.Models.Entities;

public sealed class Product : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>Base price in the shop's currency. A variant's <see cref="ProductVariant.PriceOverride"/>
    /// takes precedence when set.</summary>
    public required decimal Price { get; set; }

    public List<ProductVariant> Variants { get; set; } = [];
}
