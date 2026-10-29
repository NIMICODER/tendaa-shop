using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;

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
    IAgentRequestContext requestContext,
    IToolTraceCollector trace,
    AIAgent agent,
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

        requestContext.SetIdentity(shop.Id, customer.Id, customer.PhoneNumber, conversation.Id);

        var session = await conversationService.LoadSessionAsync(agent, conversation, cancellationToken);
        var response = await agent.RunAsync(request.Message, session, cancellationToken: cancellationToken);

        await conversationService.SaveSessionAsync(agent, conversation, session, cancellationToken);
        await conversationService.SaveTurnAsync(
            shop.Id, conversation.Id, request.Message, response.Text, trace.Entries, cancellationToken);

        return Ok(new ChatResponse(response.Text));
    }
}
