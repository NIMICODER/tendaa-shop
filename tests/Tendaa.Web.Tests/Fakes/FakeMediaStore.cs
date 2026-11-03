using Tendaa.Core;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no disk I/O. Records what was saved so a test
/// can assert the downloaded bytes reached the store before analysis ran.</summary>
public sealed class FakeMediaStore : IMediaStore
{
    public List<(string MediaId, byte[] Bytes, string MimeType)> Saved { get; } = [];

    public Task SaveAsync(string mediaId, byte[] bytes, string mimeType, CancellationToken cancellationToken = default)
    {
        Saved.Add((mediaId, bytes, mimeType));
        return Task.CompletedTask;
    }

    public Task<(Stream Stream, string MimeType)?> GetAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        var saved = Saved.FirstOrDefault(s => s.MediaId == mediaId);
        if (saved == default)
        {
            return Task.FromResult<(Stream Stream, string MimeType)?>(null);
        }

        return Task.FromResult<(Stream Stream, string MimeType)?>((new MemoryStream(saved.Bytes), saved.MimeType));
    }
}
