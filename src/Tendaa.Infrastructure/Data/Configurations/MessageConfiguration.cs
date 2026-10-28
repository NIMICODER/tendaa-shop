using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Conversations;

namespace Tendaa.Infrastructure.Data.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(m => m.Language).HasMaxLength(20);
        builder.Property(m => m.MediaId).HasMaxLength(200);
        builder.Property(m => m.WhatsAppMessageId).HasMaxLength(200);

        builder.HasMany(m => m.ToolTraces)
            .WithOne()
            .HasForeignKey(t => t.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
