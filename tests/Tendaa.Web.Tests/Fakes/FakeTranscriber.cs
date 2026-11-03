using Tendaa.Agent.Media;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no ffmpeg, no network call. Returns a fixed
/// transcript so the processor's voice-note routing can be tested without a real Muse Voice
/// Transcribe call.</summary>
public sealed class FakeTranscriber(string text, string? language = null) : ITranscriber
{
    public byte[]? LastBytes { get; private set; }

    public Task<(string Text, string? Language)> TranscribeAsync(
        byte[] oggBytes, CancellationToken cancellationToken = default)
    {
        LastBytes = oggBytes;
        return Task.FromResult((text, language));
    }
}
