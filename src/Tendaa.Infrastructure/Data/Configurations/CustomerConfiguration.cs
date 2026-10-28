using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Customers;

namespace Tendaa.Infrastructure.Data.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.BsuId).HasMaxLength(160);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.Username).HasMaxLength(100);
        builder.Property(c => c.Name).HasMaxLength(200);

        // BSUID is unique per shop, not globally — see ARCHITECTURE.md "Customer identity".
        builder.HasIndex(c => new { c.ShopId, c.BsuId }).IsUnique();
    }
}
