using System.ComponentModel.DataAnnotations;

namespace Tendaa.Web.Configuration;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    [Required]
    public string AdminPassword { get; set; } = string.Empty;
}
