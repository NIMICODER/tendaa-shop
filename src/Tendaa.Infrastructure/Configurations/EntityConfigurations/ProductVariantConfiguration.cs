using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Size).HasMaxLength(50);
        builder.Property(v => v.Colour).HasMaxLength(50);
        builder.Property(v => v.PriceOverride).HasPrecision(18, 2);
    }
}
