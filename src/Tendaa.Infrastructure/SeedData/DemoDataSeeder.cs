using Microsoft.EntityFrameworkCore;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.Context;

namespace Tendaa.Infrastructure.SeedData;

/// <summary>Demo data for local development and the hackathon demo: one shop, a sneakers/bags/
/// clothing catalog, Lagos delivery zones (matching SPEC.md's demo story — Lekki, Ajah). Safe to
/// call on every startup: idempotent, does nothing once a shop already exists.</summary>
public static class DemoDataSeeder
{
    // Shared between the fresh-seed Product(...) calls below and the existing-shop backfill
    // above, so the two can never drift apart.
    private static readonly Dictionary<string, string> SeedPhotoUrls = new()
    {
        ["Classic Low Sneakers"] = "https://images.unsplash.com/photo-1549298916-b41d501d3772?w=800",
        ["Crossbody Bag"] = "https://images.unsplash.com/photo-1591561954557-26941169b49e?w=800",
        ["Backpack"] = "https://images.unsplash.com/photo-1553062407-98eeb64c6a62?w=800",
        ["Graphic T-Shirt"] = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?w=800",
    };

    public static async Task SeedAsync(
        TendaaDbContext db, string? whatsAppPhoneNumberId, CancellationToken cancellationToken = default)
    {
        var existingShop = await db.Shops.FirstOrDefaultAsync(cancellationToken);
        if (existingShop is not null)
        {
            var changed = false;

            // Backfill for a shop seeded before WhatsAppPhoneNumberId was added to this seeder
            // (Phase 3) — self-healing on the next startup rather than needing a manual UPDATE.
            if (existingShop.WhatsAppPhoneNumberId is null && whatsAppPhoneNumberId is not null)
            {
                existingShop.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
                changed = true;
            }

            // Same self-healing pattern for products seeded before PhotoUrl was added below —
            // only touches the rows this seeder itself would have given a photo, never a product
            // the seller added or edited since (PhotoUrl already set, even to something else).
            var existingProducts = await db.Products
                .Where(p => p.ShopId == existingShop.Id && p.PhotoUrl == null)
                .ToListAsync(cancellationToken);
            foreach (var product in existingProducts)
            {
                if (SeedPhotoUrls.TryGetValue(product.Name, out var photoUrl))
                {
                    product.PhotoUrl = photoUrl;
                    changed = true;
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var shop = new Shop
        {
            Id = Guid.NewGuid(),
            Name = "Lagos Kicks",
            Country = "Nigeria",
            Currency = "NGN",
            TimeZone = "Africa/Lagos",
            HoursText = "Mon to Sat, 9am to 7pm. Sun, 12pm to 5pm.",
            RulesText =
                "No returns after 48 hours. Payment by bank transfer; pay on delivery available within "
                + "Lagos Island for orders above NGN 20,000. No exchanges once an item has been worn. "
                + "Confirm your size before ordering - sales are final once dispatched.",
            BankName = "GTBank",
            BankAccountNumber = "0123456789",
            BankAccountName = "Lagos Kicks Ventures",
            // The actual configured WhatsApp test number's phone_number_id, so inbound webhooks
            // can resolve which shop they belong to — never a hardcoded value.
            WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Shops.Add(shop);

        db.DeliveryZones.AddRange(
            Zone(shop.Id, "Lekki", 1500m, "Same day"),
            Zone(shop.Id, "Ikoyi", 1500m, "Same day"),
            Zone(shop.Id, "Victoria Island", 1000m, "Same day"),
            Zone(shop.Id, "Ajah", 2500m, "1 to 2 business days"),
            Zone(shop.Id, "Surulere", 2000m, "1 to 2 business days"),
            Zone(shop.Id, "Yaba", 2000m, "1 to 2 business days"),
            Zone(shop.Id, "Ikeja", 2500m, "1 to 2 business days"));

        db.Products.AddRange(
            Product(shop.Id, "Classic Low Sneakers", "Sneakers", 25000m,
                SizeVariants(shop.Id, "Black", 40, 44, stockPerSize: 4),
                SeedPhotoUrls["Classic Low Sneakers"]),
            Product(shop.Id, "Canvas High-Tops", "Sneakers", 22000m,
                SizeVariants(shop.Id, "White", 40, 44, stockPerSize: 3)),
            Product(shop.Id, "Running Trainers", "Sneakers", 32000m,
                SizeVariants(shop.Id, "Grey", 40, 45, stockPerSize: 2)),
            Product(shop.Id, "Leather Loafers", "Shoes", 28000m,
                SizeVariants(shop.Id, "Brown", 40, 44, stockPerSize: 3)),
            Product(shop.Id, "Suede Desert Boots", "Boots", 35000m,
                SizeVariants(shop.Id, "Tan", 41, 45, stockPerSize: 2)),
            Product(shop.Id, "Crossbody Bag", "Bags", 18000m,
                ColourVariants(shop.Id, ["Black", "Brown", "Tan"], stockPerColour: 5),
                SeedPhotoUrls["Crossbody Bag"]),
            Product(shop.Id, "Tote Bag", "Bags", 21000m,
                ColourVariants(shop.Id, ["Black", "Beige"], stockPerColour: 5)),
            Product(shop.Id, "Backpack", "Bags", 24000m,
                ColourVariants(shop.Id, ["Black", "Navy"], stockPerColour: 4),
                SeedPhotoUrls["Backpack"]),
            Product(shop.Id, "Graphic T-Shirt", "Clothing", 8000m,
                SizeLabelVariants(shop.Id, ["S", "M", "L", "XL"], stockPerSize: 10),
                SeedPhotoUrls["Graphic T-Shirt"]),
            Product(shop.Id, "Denim Jacket", "Clothing", 30000m,
                SizeLabelVariants(shop.Id, ["S", "M", "L", "XL"], stockPerSize: 5)),
            Product(shop.Id, "Ankle Socks (3-pack)", "Accessories", 3500m,
                [new ProductVariant { Id = Guid.NewGuid(), ShopId = shop.Id, Stock = 50 }]),
            Product(shop.Id, "Baseball Cap", "Accessories", 6000m,
                ColourVariants(shop.Id, ["Black", "Navy", "Red"], stockPerColour: 8)));

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DeliveryZone Zone(Guid shopId, string name, decimal fee, string deliveryTimeText) =>
        new() { Id = Guid.NewGuid(), ShopId = shopId, Name = name, Fee = fee, DeliveryTimeText = deliveryTimeText };

    private static Product Product(
        Guid shopId, string name, string category, decimal price, List<ProductVariant> variants, string? photoUrl = null) =>
        new()
        {
            Id = Guid.NewGuid(), ShopId = shopId, Name = name, Category = category, Price = price,
            Variants = variants, PhotoUrl = photoUrl,
        };

    private static List<ProductVariant> SizeVariants(Guid shopId, string colour, int fromSize, int toSize, int stockPerSize) =>
        Enumerable.Range(fromSize, toSize - fromSize + 1)
            .Select(size => new ProductVariant
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                Size = size.ToString(),
                Colour = colour,
                Stock = stockPerSize,
            })
            .ToList();

    private static List<ProductVariant> SizeLabelVariants(Guid shopId, string[] sizes, int stockPerSize) =>
        sizes.Select(size => new ProductVariant
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Size = size,
            Stock = stockPerSize,
        }).ToList();

    private static List<ProductVariant> ColourVariants(Guid shopId, string[] colours, int stockPerColour) =>
        colours.Select(colour => new ProductVariant
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Colour = colour,
            Stock = stockPerColour,
        }).ToList();
}
