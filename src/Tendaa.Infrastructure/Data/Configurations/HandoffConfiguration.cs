using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Handoffs;

namespace Tendaa.Infrastructure.Data.Configurations;

public sealed class HandoffConfiguration : IEntityTypeConfiguration<Handoff>
{
    public void Configure(EntityTypeBuilder<Handoff> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.ResolvedBy).HasMaxLength(200);
    }
}
