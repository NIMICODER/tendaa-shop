using Tendaa.Core;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no real Graph API call. Returns fixed bytes
/// for whatever media id is asked for; tests that care which id was requested read
/// <see cref="LastRequestedMediaId"/>.</summary>
public sealed class FakeWhatsAppMediaDownloader(byte[] bytes, string mimeType) : IWhatsAppMediaDownloader
{
    public string? LastRequestedMediaId { get; private set; }

    public Task<(byte[] Bytes, string MimeType)> DownloadAsync(
        string mediaId, CancellationToken cancellationToken = default)
    {
        LastRequestedMediaId = mediaId;
        return Task.FromResult((bytes, mimeType));
    }
}
