using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Money;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ProductService(IRepository<Product> products, IRepository<Shop> shops) : IProductService
{
    public async Task<TendaaResult<IReadOnlyList<ProductSearchItem>>> SearchAsync(
        Guid shopId, string query, CancellationToken cancellationToken = default)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return TendaaResult<IReadOnlyList<ProductSearchItem>>.Success([]);
        }

        var currency = await CurrencyAsync(shopId, cancellationToken);
        var shopProducts = await products.Query()
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        // Word-by-word, OR across fields: a natural query like "black sneakers" must match
        // "sneakers" against Name and "black" against a variant's Colour — neither word alone
        // matches a single field end to end, so a single ILIKE-the-whole-phrase check (the
        // original approach) never found this, the exact phrasing SPEC.md's flow D uses.
        IReadOnlyList<ProductSearchItem> items = shopProducts
            .Where(p => words.All(word => ProductMatchesWord(p, word)))
            .Select(p => ToSearchItem(p, currency))
            .ToList();

        return TendaaResult<IReadOnlyList<ProductSearchItem>>.Success(items);
    }

    private static bool ProductMatchesWord(Product product, string word) =>
        Contains(product.Name, word)
        || Contains(product.Category, word)
        || product.Variants.Any(v => Contains(v.Colour, word) || Contains(v.Size, word));

    private static bool Contains(string? haystack, string word) =>
        haystack is not null && haystack.Contains(word, StringComparison.OrdinalIgnoreCase);

    public async Task<TendaaResult<ProductDetails>> GetDetailsAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await products.Query()
            .Where(p => p.ShopId == shopId && p.Id == productId)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);

        if (product is null)
        {
            return TendaaResult<ProductDetails>.NotFound();
        }

        var currency = await CurrencyAsync(shopId, cancellationToken);
        var details = new ProductDetails(
            product.Id,
            product.Name,
            product.Category,
            product.Description,
            product.Variants.Select(v => ToVariantStock(v, product.Price, currency)).ToList(),
            product.PhotoUrl);

        return TendaaResult<ProductDetails>.Success(details);
    }

    private async Task<string> CurrencyAsync(Guid shopId, CancellationToken cancellationToken)
    {
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        return shop?.Currency ?? "NGN";
    }

    private static ProductSearchItem ToSearchItem(Product product, string currency) =>
        new(product.Id, product.Name, product.Category,
            product.Variants.Select(v => ToVariantStock(v, product.Price, currency)).ToList());

    private static VariantStock ToVariantStock(ProductVariant variant, decimal basePrice, string currency)
    {
        var price = variant.PriceOverride ?? basePrice;
        return new(variant.Id, variant.Size, variant.Colour, variant.Stock, price, MoneyFormatter.Format(price, currency));
    }
}
