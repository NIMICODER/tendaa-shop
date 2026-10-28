using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.ItemTotal).HasPrecision(18, 2);
        builder.Property(o => o.DeliveryFee).HasPrecision(18, 2);
        builder.Property(o => o.GrandTotal).HasPrecision(18, 2);
        builder.Property(o => o.Address).HasMaxLength(500);
        builder.Property(o => o.ConfirmedBy).HasMaxLength(200);

        // Human-friendly order numbers are sequential per shop, not globally unique.
        builder.HasIndex(o => new { o.ShopId, o.Number }).IsUnique();

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
