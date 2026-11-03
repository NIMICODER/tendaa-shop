using Microsoft.Extensions.Options;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Infrastructure.WhatsApp;

/// <summary>Dev-only <see cref="IMediaStore"/>: writes to a local folder, named by WhatsApp media
/// id so a file can be found again later without a separate stored-path column. Replace with
/// object storage before this goes further than the hackathon demo.</summary>
public sealed class LocalDiskMediaStore(IOptions<MediaStorageOptions> options) : IMediaStore
{
    public async Task SaveAsync(string mediaId, byte[] bytes, string mimeType, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(options.Value.RootPath);

        var extension = mimeType switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "audio/ogg; codecs=opus" or "audio/ogg" => ".ogg",
            _ => "",
        };

        var path = Path.Combine(options.Value.RootPath, mediaId + extension);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }

    public async Task<(Stream Stream, string MimeType)?> GetAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        // The media id arrives from a URL route, so allowlist it — never let separators,
        // parent segments, or glob characters reach the filesystem.
        if (string.IsNullOrWhiteSpace(mediaId)
            || mediaId.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
        {
            return null;
        }

        if (!Directory.Exists(options.Value.RootPath))
        {
            return null;
        }

        var matches = Directory.GetFiles(options.Value.RootPath, $"{mediaId}.*");
        if (matches.Length != 1)
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(matches[0], cancellationToken);
        return (new MemoryStream(bytes), MimeTypeFromExtension(Path.GetExtension(matches[0])));
    }

    private static string MimeTypeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".ogg" => "audio/ogg",
        _ => "application/octet-stream",
    };
}
