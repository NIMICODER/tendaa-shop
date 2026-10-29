using System.ComponentModel.DataAnnotations;

namespace Tendaa.Core.Configurations.ConfigModels;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    [Required]
    public string PhoneNumberId { get; set; } = string.Empty;

    [Required]
    public string WabaId { get; set; } = string.Empty;

    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string AppSecret { get; set; } = string.Empty;

    [Required]
    public string VerifyToken { get; set; } = string.Empty;

    /// <summary>Graph API version, e.g. "v23.0". Not a secret — defaults to a working value so
    /// nothing new needs configuring, but override here if Meta deprecates it.</summary>
    public string GraphApiVersion { get; set; } = "v23.0";
}
