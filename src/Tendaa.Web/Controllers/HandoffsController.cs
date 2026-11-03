using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Web.Controllers;

public sealed record ResolveHandoffRequest(string? ReplyText);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class HandoffsController(IHandoffService handoffService, IShopService shopService)
    : DashboardControllerBase(shopService)
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] HandoffStatus? status, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await handoffService.ListAsync(shopId.Data, status, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await handoffService.GetDetailAsync(shopId.Data, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(
        Guid id, [FromBody] ResolveHandoffRequest request, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await handoffService.ResolveHandoffAsync(
            shopId.Data, id, SellerName, request.ReplyText, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
