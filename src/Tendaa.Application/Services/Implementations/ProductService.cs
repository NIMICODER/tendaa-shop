using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Money;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ProductService(
    IRepository<Product> products, IRepository<Shop> shops, IUnitOfWork unitOfWork) : IProductService
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

    public async Task<TendaaResult<IReadOnlyList<ProductCatalogEntry>>> GetCatalogAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var shopProducts = await products.Query()
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ProductCatalogEntry> catalog = shopProducts
            .Select(p => new ProductCatalogEntry(
                p.Id,
                p.Name,
                p.Category,
                p.Variants
                    .Select(v => v.Colour)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()!))
            .ToList();

        return TendaaResult<IReadOnlyList<ProductCatalogEntry>>.Success(catalog);
    }

    public async Task<TendaaResult<IReadOnlyList<ProductDashboardItem>>> ListForDashboardAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var shopProducts = await products.Query()
            .Where(p => p.ShopId == shopId)
            .Include(p => p.Variants)
            .ToListAsync(cancellationToken);

        var currency = await CurrencyAsync(shopId, cancellationToken);
        IReadOnlyList<ProductDashboardItem> items = shopProducts
            .OrderBy(p => p.Name)
            .Select(p => ToDashboardItem(p, currency))
            .ToList();

        return TendaaResult<IReadOnlyList<ProductDashboardItem>>.Success(items);
    }

    public async Task<TendaaResult<ProductDashboardItem>> CreateAsync(
        Guid shopId, ProductInput input, CancellationToken cancellationToken = default)
    {
        if (Validate(input) is { } error)
        {
            return TendaaResult<ProductDashboardItem>.BadRequest(error);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Name = input.Name.Trim(),
            Category = Clean(input.Category),
            Description = Clean(input.Description),
            PhotoUrl = Clean(input.PhotoUrl),
            Price = input.Price,
            Variants = input.Variants.Select(v => new ProductVariant
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                Size = Clean(v.Size),
                Colour = Clean(v.Colour),
                Stock = v.Stock,
                PriceOverride = v.PriceOverride,
            }).ToList(),
        };

        await products.AddAsync(product, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var currency = await CurrencyAsync(shopId, cancellationToken);
        return TendaaResult<ProductDashboardItem>.Success(ToDashboardItem(product, currency));
    }

    public async Task<TendaaResult<ProductDashboardItem>> UpdateAsync(
        Guid shopId, Guid productId, ProductInput input, CancellationToken cancellationToken = default)
    {
        if (Validate(input) is { } error)
        {
            return TendaaResult<ProductDashboardItem>.BadRequest(error);
        }

        var product = await products.Query()
            .Where(p => p.ShopId == shopId && p.Id == productId)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return TendaaResult<ProductDashboardItem>.NotFound();
        }

        product.Name = input.Name.Trim();
        product.Category = Clean(input.Category);
        product.Description = Clean(input.Description);
        product.PhotoUrl = Clean(input.PhotoUrl);
        product.Price = input.Price;

        var incomingIds = input.Variants
            .Where(v => v.VariantId.HasValue)
            .Select(v => v.VariantId!.Value)
            .ToHashSet();
        foreach (var removed in product.Variants.Where(v => !incomingIds.Contains(v.Id)).ToList())
        {
            product.Variants.Remove(removed);
        }

        foreach (var variant in input.Variants)
        {
            if (variant.VariantId is { } variantId
                && product.Variants.FirstOrDefault(v => v.Id == variantId) is { } existing)
            {
                existing.Size = Clean(variant.Size);
                existing.Colour = Clean(variant.Colour);
                existing.Stock = variant.Stock;
                existing.PriceOverride = variant.PriceOverride;
            }
            else
            {
                product.Variants.Add(new ProductVariant
                {
                    Id = Guid.NewGuid(),
                    ShopId = shopId,
                    Size = Clean(variant.Size),
                    Colour = Clean(variant.Colour),
                    Stock = variant.Stock,
                    PriceOverride = variant.PriceOverride,
                });
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var currency = await CurrencyAsync(shopId, cancellationToken);
        return TendaaResult<ProductDashboardItem>.Success(ToDashboardItem(product, currency));
    }

    public async Task<TendaaResult> DeleteAsync(
        Guid shopId, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await products.Query()
            .Where(p => p.ShopId == shopId && p.Id == productId)
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(cancellationToken);
        if (product is null)
        {
            return TendaaResult.NotFound();
        }

        products.Remove(product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TendaaResult.Success();
    }

    private static string? Validate(ProductInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return "Product name is required.";
        }

        if (input.Price < 0)
        {
            return "Price cannot be negative.";
        }

        if (input.Variants.Any(v => v.Stock < 0))
        {
            return "Stock cannot be negative.";
        }

        if (input.Variants.Any(v => v.PriceOverride is < 0))
        {
            return "Variant price cannot be negative.";
        }

        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ProductDashboardItem ToDashboardItem(Product product, string currency) =>
        new(product.Id, product.Name, product.Category, product.Description, product.Price,
            MoneyFormatter.Format(product.Price, currency), product.PhotoUrl,
            product.Variants.Select(v =>
            {
                var effective = v.PriceOverride ?? product.Price;
                return new VariantDashboardItem(
                    v.Id, v.Size, v.Colour, v.Stock, effective, MoneyFormatter.Format(effective, currency));
            }).ToList());

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
