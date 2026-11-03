using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Agent.Media;

/// <summary>Confirmed Oct 2026 against dev.meta.ai/docs/speech-to-text (CLAUDE.md's "Meta Model
/// API" section) — not OpenAI-shaped, so a plain <see cref="HttpClient"/>, not the OpenAI SDK.
/// Multipart POST with a <c>request</c> JSON part and an <c>audio</c> WAV part; WhatsApp's native
/// <c>audio/ogg; codecs=opus</c> isn't accepted, so this shells out to ffmpeg first.</summary>
public sealed class Transcriber(HttpClient httpClient, IOptions<MetaModelOptions> options) : ITranscriber
{
    public async Task<(string Text, string? Language)> TranscribeAsync(
        byte[] oggBytes, CancellationToken cancellationToken = default)
    {
        var wavBytes = await ConvertOggToWavAsync(oggBytes, cancellationToken);

        using var content = new MultipartFormDataContent();
        var requestJson = JsonSerializer.Serialize(new TranscribeRequest("ENDPOINTING", options.Value.TranscribeModel, "WAV"));
        content.Add(new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json"), "request");
        content.Add(new ByteArrayContent(wavBytes), "audio", "audio.wav");

        using var response = await httpClient.PostAsync("asr/transcribe", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<TranscribeResponse>(cancellationToken: cancellationToken);
        // The API doesn't return a detected-language field in this response shape (confirmed
        // against the docs) — never invent one; language stays null until there's a real signal.
        return (result?.Transcript ?? "", null);
    }

    private static async Task<byte[]> ConvertOggToWavAsync(byte[] oggBytes, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                // 16-bit PCM mono at 16 kHz — the lower of Muse Voice Transcribe's two accepted
                // sample rates (CLAUDE.md), reading OGG from stdin and writing WAV to stdout so no
                // temp files are needed.
                Arguments = "-i - -f wav -ar 16000 -ac 1 -acodec pcm_s16le -",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        var writeTask = process.StandardInput.BaseStream.WriteAsync(oggBytes, cancellationToken)
            .AsTask()
            .ContinueWith(_ => process.StandardInput.BaseStream.Close(), cancellationToken);
        var readTask = ReadAllBytesAsync(process.StandardOutput.BaseStream, cancellationToken);

        await Task.WhenAll(writeTask, readTask);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {error}");
        }

        return readTask.Result;
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return buffer.ToArray();
    }

    private sealed record TranscribeRequest(
        [property: JsonPropertyName("mode")] string Mode,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("audioEncoding")] string AudioEncoding);

    private sealed record TranscribeResponse(
        [property: JsonPropertyName("transcript")] string? Transcript);
}
