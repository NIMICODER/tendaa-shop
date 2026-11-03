namespace Tendaa.Core.Configurations.ConfigModels;

public sealed class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    /// <summary>Not secret, has a working default. Relative to the process's working directory;
    /// object storage replaces this entirely once this moves past local development.</summary>
    public string RootPath { get; set; } = "media-storage";
}
