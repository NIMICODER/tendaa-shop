using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ConversationsController(
    IConversationService conversationService, IShopService shopService)
    : DashboardControllerBase(shopService)
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await conversationService.ListForDashboardAsync(shopId.Data, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Thread(Guid id, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await conversationService.GetThreadAsync(shopId.Data, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
