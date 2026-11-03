using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;
using Tendaa.Agent.Media;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Agent.Tests.Media;

/// <summary>Hand-written <see cref="HttpMessageHandler"/> stub — no mocking library, matching the
/// repo convention (<c>WhatsAppGraphApiClientTests</c>) — standing in for Muse Voice Transcribe so
/// the multipart request shape can be asserted without a real network call. Reads each part's
/// bytes inside <see cref="SendAsync"/> itself, not after — <see cref="Transcriber"/>'s own
/// <c>using var content = new MultipartFormDataContent()</c> disposes (and empties) it the moment
/// the call returns, so capturing just the object reference for inspection afterward sees 0 parts.</summary>
public sealed class CapturingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
{
    public string? RequestPartJson { get; private set; }
    public string? AudioPartFileName { get; private set; }
    public byte[]? AudioPartBytes { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (var part in multipart)
            {
                var name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (name == "request")
                {
                    RequestPartJson = await part.ReadAsStringAsync(cancellationToken);
                }
                else if (name == "audio")
                {
                    AudioPartFileName = part.Headers.ContentDisposition?.FileName?.Trim('"');
                    AudioPartBytes = await part.ReadAsByteArrayAsync(cancellationToken);
                }
            }
        }

        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json"),
        };
        return response;
    }
}

/// <summary>Exercises the real ffmpeg conversion step too — no fake for that boundary, matching
/// this repo's philosophy of testing external-tool integrations directly rather than mocking them
/// (see <c>WhatsAppGraphApiClientTests</c>). Needs `ffmpeg` on the machine running `dotnet test`,
/// including CI — a dependency this phase introduces, not previously required.</summary>
public sealed class TranscriberTests
{
    [Fact]
    public async Task TranscribeAsync_SendsMultipartRequestWithRequestAndAudioParts_ParsesTranscript()
    {
        var oggBytes = await GenerateTinySilentOggAsync();

        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, """{"transcript":"I want ten socks"}""");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.meta.ai/v1/") };
        var options = Options.Create(new MetaModelOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://api.meta.ai/v1",
            ChatModel = "test-chat-model",
            TranscribeModel = "muse-voice-transcribe-1.0",
        });
        var transcriber = new Transcriber(httpClient, options);

        var (text, language) = await transcriber.TranscribeAsync(oggBytes);

        Assert.Equal("I want ten socks", text);
        // The API doesn't return a detected-language field in this response shape (CLAUDE.md) —
        // must stay null, never invented.
        Assert.Null(language);

        Assert.NotNull(handler.RequestPartJson);
        Assert.Contains("\"mode\":\"ENDPOINTING\"", handler.RequestPartJson);
        Assert.Contains("\"model\":\"muse-voice-transcribe-1.0\"", handler.RequestPartJson);
        Assert.Contains("\"audioEncoding\":\"WAV\"", handler.RequestPartJson);

        Assert.Equal("audio.wav", handler.AudioPartFileName);
        var wavBytes = handler.AudioPartBytes!;
        Assert.True(wavBytes.Length > 44); // more than just a WAV header
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wavBytes, 0, 4));
    }

    private static async Task<byte[]> GenerateTinySilentOggAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-f lavfi -i anullsrc=r=16000:cl=mono -t 0.1 -c:a libopus -f ogg -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        using var buffer = new MemoryStream();
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(buffer);
        await process.WaitForExitAsync();
        await copyTask;

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"ffmpeg fixture generation failed: {error}");
        }

        return buffer.ToArray();
    }
}
