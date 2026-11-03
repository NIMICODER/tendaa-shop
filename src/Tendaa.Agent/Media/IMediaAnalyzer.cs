using Tendaa.Application.Models;

namespace Tendaa.Agent.Media;

/// <summary><paramref name="Category"/> is <c>"receipt"</c>, <c>"product_photo"</c>, or
/// <c>"other"</c>. The receipt fields are null when <paramref name="Category"/> isn't
/// <c>"receipt"</c>, or when the model couldn't read them off the photo.
/// <paramref name="TransferDate"/> is the raw ISO <c>yyyy-MM-dd</c> string the model returned —
/// parsing it is the caller's job, keeping this record a plain data holder.</summary>
public sealed record MediaAnalysisResult(
    string Category,
    decimal? Amount,
    string? BankName,
    string? SenderName,
    string? TransferDate,
    string? Reference,
    double Confidence);

/// <summary><paramref name="ProductId"/> is only ever one of the ids given in the catalog list
/// passed to <see cref="IMediaAnalyzer.MatchProductPhotoAsync"/> — the schema can't stop the model
/// inventing an id outright, so the caller must still validate it against the real catalog before
/// trusting it (same "never trust an id from the model" rule as elsewhere in this codebase).</summary>
public sealed record ProductPhotoMatchCandidate(Guid ProductId, double Confidence);

public interface IMediaAnalyzer
{
    Task<MediaAnalysisResult> AnalyzeAsync(
        byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default);

    /// <summary>Up to 3 candidate products from <paramref name="catalog"/> that the photo might
    /// show, most confident first. Empty when nothing looks like a plausible match.</summary>
    Task<IReadOnlyList<ProductPhotoMatchCandidate>> MatchProductPhotoAsync(
        byte[] imageBytes,
        string mimeType,
        IReadOnlyList<ProductCatalogEntry> catalog,
        CancellationToken cancellationToken = default);
}
