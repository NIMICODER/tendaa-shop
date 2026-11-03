namespace Tendaa.Core;

/// <summary>Persists a downloaded media file so it can be shown later (e.g. a receipt beside its
/// extracted fields on the seller dashboard, Phase 5) without re-hitting the Graph API — WhatsApp's
/// own media URLs expire in 5 minutes and the media id itself in 7 days. Local disk in
/// development, object storage later (ARCHITECTURE.md's "Media pipeline"). Stored by WhatsApp
/// media id, matching <c>PaymentProof.MediaId</c>, so callers never need a separate stored path.</summary>
public interface IMediaStore
{
    Task SaveAsync(string mediaId, byte[] bytes, string mimeType, CancellationToken cancellationToken = default);

    /// <summary>Reads a previously saved file back (e.g. a receipt for the dashboard's order
    /// detail page). Returns null when no file exists for <paramref name="mediaId"/>.</summary>
    Task<(Stream Stream, string MimeType)?> GetAsync(string mediaId, CancellationToken cancellationToken = default);
}
