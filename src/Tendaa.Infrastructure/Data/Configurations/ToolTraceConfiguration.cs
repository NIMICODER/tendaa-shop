using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Conversations;

namespace Tendaa.Infrastructure.Data.Configurations;

public sealed class ToolTraceConfiguration : IEntityTypeConfiguration<ToolTrace>
{
    public void Configure(EntityTypeBuilder<ToolTrace> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.ToolName).HasMaxLength(100);
    }
}
