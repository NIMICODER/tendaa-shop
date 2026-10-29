using System.Net;

namespace Tendaa.Core;

/// <summary>A non-success response from the WhatsApp Graph API, with Meta's own error code and
/// message when the body parsed as their documented <c>{ "error": { "code", "message" } }</c>
/// shape. Thrown instead of a plain <see cref="HttpRequestException"/> so callers (and
/// <c>InboundEvent.Error</c>) can show what actually went wrong, e.g. code 131030 "Recipient
/// phone number not in allowed list" for an unverified test-number recipient.</summary>
public sealed class WhatsAppApiException(HttpStatusCode statusCode, int? errorCode, string? errorMessage, string rawBody)
    : Exception(BuildMessage(statusCode, errorCode, errorMessage, rawBody))
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public int? ErrorCode { get; } = errorCode;

    public string? ErrorMessage { get; } = errorMessage;

    private static string BuildMessage(HttpStatusCode statusCode, int? errorCode, string? errorMessage, string rawBody) =>
        errorCode is not null
            ? $"WhatsApp API error {(int)statusCode}: code {errorCode} - {errorMessage}"
            : $"WhatsApp API error {(int)statusCode}: {rawBody}";
}
