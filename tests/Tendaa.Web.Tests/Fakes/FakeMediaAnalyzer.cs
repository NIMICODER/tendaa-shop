using Tendaa.Agent.Media;
using Tendaa.Application.Models;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no real Muse Spark call. Returns whatever
/// <see cref="Result"/>/<see cref="MatchCandidates"/> is set to, so the processor's receipt- and
/// product-photo-routing branches can be tested without a network call.</summary>
public sealed class FakeMediaAnalyzer : IMediaAnalyzer
{
    public MediaAnalysisResult Result { get; set; } =
        new("other", null, null, null, null, null, 1.0);

    public IReadOnlyList<ProductPhotoMatchCandidate> MatchCandidates { get; set; } = [];

    public Task<MediaAnalysisResult> AnalyzeAsync(
        byte[] imageBytes, string mimeType, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result);

    public Task<IReadOnlyList<ProductPhotoMatchCandidate>> MatchProductPhotoAsync(
        byte[] imageBytes,
        string mimeType,
        IReadOnlyList<ProductCatalogEntry> catalog,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(MatchCandidates);
}
