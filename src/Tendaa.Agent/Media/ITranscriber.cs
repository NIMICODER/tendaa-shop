namespace Tendaa.Agent.Media;

/// <summary>Transcribes a WhatsApp voice note. See ARCHITECTURE.md's "Media pipeline" — the
/// transcript is treated as the customer's own text from then on, never shown to the agent as raw
/// audio.</summary>
public interface ITranscriber
{
    /// <param name="oggBytes">Raw bytes as downloaded from WhatsApp — <c>audio/ogg; codecs=opus</c>.
    /// Muse Voice Transcribe only accepts WAV/PCM (CLAUDE.md's "Meta Model API" section), so this
    /// converts via ffmpeg before calling the API.</param>
    Task<(string Text, string? Language)> TranscribeAsync(byte[] oggBytes, CancellationToken cancellationToken = default);
}
