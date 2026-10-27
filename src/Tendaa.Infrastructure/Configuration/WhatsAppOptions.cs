using System.ComponentModel.DataAnnotations;

namespace Tendaa.Infrastructure.Configuration;

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
}
