using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.WhatsApp;

/// <summary>WhatsApp Cloud API client. Confirmed against developers.facebook.com (see CLAUDE.md's
/// "WhatsApp Cloud API" section) — not the OpenAI-compatible Meta Model API, so a plain
/// <see cref="HttpClient"/>, not the OpenAI SDK.</summary>
public sealed class WhatsAppGraphApiClient(HttpClient httpClient, IOptions<WhatsAppOptions> options) : IWhatsAppSender
{
    private const int MaxAttempts = 2;

    public Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["type"] = "text",
            ["text"] = new { body = text },
        };

        // `to` wins if both are present (Meta's own guidance), so send exactly one — never both.
        if (customer.PhoneNumber is { } phone)
        {
            body["to"] = phone;
        }
        else
        {
            body["recipient"] = customer.BsuId;
        }

        return PostWithRetryAsync(body, cancellationToken);
    }

    public Task SendImageAsync(
        Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["type"] = "image",
            ["image"] = new { link = imageUrl, caption },
        };

        if (customer.PhoneNumber is { } phone)
        {
            body["to"] = phone;
        }
        else
        {
            body["recipient"] = customer.BsuId;
        }

        return PostWithRetryAsync(body, cancellationToken);
    }

    public Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default)
    {
        var body = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = whatsAppMessageId,
            typing_indicator = new { type = "text" },
        };

        return PostWithRetryAsync(body, cancellationToken);
    }

    private async Task PostWithRetryAsync(object body, CancellationToken cancellationToken)
    {
        var relativeUrl = $"{options.Value.GraphApiVersion}/{options.Value.PhoneNumberId}/messages";

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await httpClient.PostAsJsonAsync(relativeUrl, body, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                // Network-level failure (no response at all) — worth one retry for a transient blip.
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                // Meta answered — that's not a transient blip, so don't waste another call
                // retrying it. Read the body now so the caller (and InboundEvent.Error) can show
                // what actually went wrong instead of a bare status code.
                var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var (errorCode, errorMessage) = TryParseMetaError(rawBody);
                throw new WhatsAppApiException(response.StatusCode, errorCode, errorMessage, rawBody);
            }
        }
    }

    private static (int? Code, string? Message) TryParseMetaError(string rawBody)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<MetaErrorEnvelope>(rawBody);
            return (envelope?.Error?.Code, envelope?.Error?.Message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private sealed class MetaErrorEnvelope
    {
        [JsonPropertyName("error")]
        public MetaErrorDetail? Error { get; set; }
    }

    private sealed class MetaErrorDetail
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
