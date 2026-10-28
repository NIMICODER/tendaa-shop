using System.ComponentModel.DataAnnotations;

namespace Tendaa.Core.Configurations.ConfigModels;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    [Required]
    public string AdminPassword { get; set; } = string.Empty;
}
