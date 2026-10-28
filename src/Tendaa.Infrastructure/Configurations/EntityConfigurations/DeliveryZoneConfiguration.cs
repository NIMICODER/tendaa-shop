using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> builder)
    {
        builder.HasKey(z => z.Id);
        builder.Property(z => z.Name).HasMaxLength(200);
        builder.Property(z => z.Fee).HasPrecision(18, 2);
        builder.Property(z => z.DeliveryTimeText).HasMaxLength(100);
    }
}
