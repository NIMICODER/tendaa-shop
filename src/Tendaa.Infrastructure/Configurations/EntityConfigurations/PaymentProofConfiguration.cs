using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class PaymentProofConfiguration : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.MediaId).HasMaxLength(200);
        builder.Property(p => p.VerificationResult).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ReviewedBy).HasMaxLength(200);
    }
}
