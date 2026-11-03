using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Web.Controllers;

public sealed record RejectPaymentRequest(string Reason);

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class OrdersController(IOrderService orderService, IShopService shopService)
    : DashboardControllerBase(shopService)
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await orderService.ListAsync(shopId.Data, status, page, pageSize, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("counts")]
    public async Task<IActionResult> Counts(CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await orderService.GetStatusCountsAsync(shopId.Data, cancellationToken);
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

        var result = await orderService.GetDetailAsync(shopId.Data, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("payment-proofs/{proofId:guid}/confirm")]
    public async Task<IActionResult> Confirm(Guid proofId, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await orderService.ConfirmPaymentAsync(shopId.Data, proofId, SellerName, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("payment-proofs/{proofId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid proofId, [FromBody] RejectPaymentRequest request, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await orderService.RejectPaymentAsync(shopId.Data, proofId, request.Reason, SellerName, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
