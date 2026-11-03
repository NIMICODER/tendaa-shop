namespace Tendaa.Core;

/// <summary>Downloads media (a voice note or a customer photo) referenced by a WhatsApp media id.
/// Confirmed Oct 2026 (CLAUDE.md's "WhatsApp Cloud API" section): a two-step flow — resolve a
/// short-lived download URL from the media id, then download from that URL with the same bearer
/// token. Resolve promptly; the URL expires in 5 minutes and the id itself in 7 days.</summary>
public interface IWhatsAppMediaDownloader
{
    Task<(byte[] Bytes, string MimeType)> DownloadAsync(string mediaId, CancellationToken cancellationToken = default);
}
