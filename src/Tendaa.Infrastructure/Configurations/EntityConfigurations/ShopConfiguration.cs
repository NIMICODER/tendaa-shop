using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class ShopConfiguration : IEntityTypeConfiguration<Shop>
{
    public void Configure(EntityTypeBuilder<Shop> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Country).HasMaxLength(100);
        builder.Property(s => s.Currency).HasMaxLength(3);
        builder.Property(s => s.TimeZone).HasMaxLength(100);
    }
}
