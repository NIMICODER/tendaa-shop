using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Configurations.EntityConfigurations;

public sealed class ToolTraceConfiguration : IEntityTypeConfiguration<ToolTrace>
{
    public void Configure(EntityTypeBuilder<ToolTrace> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.ToolName).HasMaxLength(100);
    }
}
