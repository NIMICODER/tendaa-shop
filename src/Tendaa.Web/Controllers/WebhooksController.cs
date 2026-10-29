using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Application.WhatsApp;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Inbox;

namespace Tendaa.Web.Controllers;

/// <summary>WhatsApp Cloud API webhook. Confirmed against developers.facebook.com, see CLAUDE.md's
/// "WhatsApp Cloud API" section.</summary>
[ApiController]
[Route("webhooks")]
public sealed class WebhooksController(
    IOptions<WhatsAppOptions> whatsAppOptions,
    IShopService shopService,
    IInboundEventService inboundEventService,
    InboundEventChannel channel,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpGet("whatsapp")]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe" && challenge is not null
            && verifyToken == whatsAppOptions.Value.VerifyToken)
        {
            return Content(challenge, "text/plain");
        }

        return StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpPost("whatsapp")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        var rawBytes = buffer.ToArray();

        if (!IsValidSignature(rawBytes))
        {
            return Unauthorized();
        }

        var rawBody = Encoding.UTF8.GetString(rawBytes);
        var parsed = WhatsAppWebhookParser.Parse(rawBody);

        if (parsed.PhoneNumberId is null || parsed.Messages.Count == 0)
        {
            return Ok();
        }

        var shopResult = await shopService.GetByPhoneNumberIdAsync(parsed.PhoneNumberId, cancellationToken);
        if (!shopResult.IsSuccessful)
        {
            logger.LogWarning(
                "Webhook for unconfigured phone_number_id {PhoneNumberId} - dropped.", parsed.PhoneNumberId);
            return Ok();
        }

        var shopId = shopResult.Data!.Id;
        foreach (var message in parsed.Messages)
        {
            await InsertAndEnqueueAsync(shopId, message, rawBody, cancellationToken);
        }

        return Ok();
    }

    private async Task InsertAndEnqueueAsync(
        Guid shopId, ParsedInboundMessage message, string rawBody, CancellationToken cancellationToken)
    {
        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = message.WhatsAppMessageId,
            Bsuid = message.Bsuid,
            RawPayloadJson = rawBody,
            Status = InboundEventStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var inserted = await inboundEventService.TryInsertAsync(inboundEvent, cancellationToken);
        if (inserted)
        {
            channel.TryEnqueue(inboundEvent);
        }
    }

    private bool IsValidSignature(byte[] rawBytes)
    {
        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var headerValues))
        {
            return false;
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("sha256=", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(header["sha256=".Length..]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(whatsAppOptions.Value.AppSecret));
        var computed = hmac.ComputeHash(rawBytes);

        return CryptographicOperations.FixedTimeEquals(computed, provided);
    }
}
