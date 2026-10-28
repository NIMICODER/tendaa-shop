namespace Tendaa.Core.Models.Entities;

public sealed class ProductVariant : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid ProductId { get; set; }
    public string? Size { get; set; }
    public string? Colour { get; set; }
    public required int Stock { get; set; }

    /// <summary>Overrides <see cref="Product.Price"/> when set.</summary>
    public decimal? PriceOverride { get; set; }
}
