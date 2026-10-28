using System.ComponentModel.DataAnnotations;

namespace Tendaa.Core.Configurations.ConfigModels;

public sealed class MetaModelOptions
{
    public const string SectionName = "MetaModel";

    [Required]
    public string ApiKey { get; set; } = string.Empty;

    [Required, Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string ChatModel { get; set; } = string.Empty;

    [Required]
    public string TranscribeModel { get; set; } = string.Empty;
}
