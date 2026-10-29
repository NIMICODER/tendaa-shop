using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class InboundEventConfiguration : IEntityTypeConfiguration<InboundEvent>
{
    public void Configure(EntityTypeBuilder<InboundEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.WhatsAppMessageId).HasMaxLength(200);
        builder.Property(e => e.Bsuid).HasMaxLength(160);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Error).HasMaxLength(2000);

        // The dedupe guard — checked by catching the unique-constraint violation on insert, not
        // by a check-then-insert query, to stay race-safe under Meta's own webhook retries.
        builder.HasIndex(e => e.WhatsAppMessageId).IsUnique();
    }
}
