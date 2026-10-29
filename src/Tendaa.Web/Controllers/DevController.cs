using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Tendaa.Agent;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Configurations.ConfigModels;

namespace Tendaa.Web.Controllers;

public sealed record ChatRequest(string Bsuid, string Message);

public sealed record ChatResponse(string Reply);

/// <summary>Development-only diagnostics. Not gated via conditional route registration (MVC
/// controllers don't support that inline the way minimal APIs do) — each action checks
/// <see cref="IWebHostEnvironment.IsDevelopment"/> itself and returns 404 outside Development.</summary>
[ApiController]
[Route("dev")]
public sealed class DevController(
    IModelPingService modelPingService,
    IShopService shopService,
    ICustomerService customerService,
    IConversationService conversationService,
    ICurrentShopAccessor currentShopAccessor,
    IAgentTurnRunner agentTurnRunner,
    IHttpClientFactory httpClientFactory,
    IOptions<WhatsAppOptions> whatsAppOptions,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("ping-model")]
    public async Task<IActionResult> PingModel(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await modelPingService.PingAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    /// <summary>Stands in for Phase 3's real webhook processor: resolves identity, runs the agent
    /// for one turn, and persists it — against the real Muse Spark, no mocking.</summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var shopResult = await shopService.GetOnlyShopAsync(cancellationToken);
        if (!shopResult.IsSuccessful)
        {
            return StatusCode(shopResult.StatusCode, shopResult);
        }

        var shop = shopResult.Data!;
        currentShopAccessor.SetShopId(shop.Id);

        var customer = (await customerService.FindOrCreateByBsuIdAsync(
            shop.Id, request.Bsuid, cancellationToken: cancellationToken)).Data!;

        var conversation = (await conversationService.GetOrCreateAsync(
            shop.Id, customer.Id, cancellationToken)).Data!;

        var reply = await agentTurnRunner.RunTurnAsync(
            shop.Id, customer.Id, customer.PhoneNumber, conversation, request.Message, cancellationToken);

        return Ok(new ChatResponse(reply));
    }

    /// <summary>Intentional head-start for multi-seller onboarding, not leftover debugging: every
    /// seller's WABA needs this same check (webhook URL verification and WABA-to-app subscription
    /// are separate Graph API steps — a configured webhook alone doesn't mean events arrive).
    /// `GET /{WabaId}/subscribed_apps`. Never logs or returns the access token itself.</summary>
    [HttpGet("whatsapp/subscribed-apps")]
    public async Task<IActionResult> GetSubscribedApps(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        return await CallGraphApiAsync(HttpMethod.Get, cancellationToken);
    }

    /// <summary>Intentional head-start for multi-seller onboarding, not leftover debugging:
    /// subscribes this app to the configured WABA's webhooks — Meta's test numbers come
    /// pre-subscribed to Meta's own demo app, so this is required for every seller's WABA, even
    /// after their webhook URL is verified. Additive: doesn't remove any other app's subscription.
    /// A real onboarding flow will call this per-seller instead of against one fixed WABA.</summary>
    [HttpPost("whatsapp/subscribed-apps")]
    public async Task<IActionResult> SubscribeApp(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        return await CallGraphApiAsync(HttpMethod.Post, cancellationToken);
    }

    private async Task<IActionResult> CallGraphApiAsync(HttpMethod method, CancellationToken cancellationToken)
    {
        var options = whatsAppOptions.Value;
        using var request = new HttpRequestMessage(
            method, $"{options.GraphApiVersion}/{options.WabaId}/subscribed_apps");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);

        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri("https://graph.facebook.com/");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ContentResult
        {
            Content = body,
            ContentType = "application/json",
            StatusCode = (int)response.StatusCode,
        };
    }
}
