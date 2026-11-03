using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SettingsController(IShopService shopService, IDeliveryService deliveryService)
    : DashboardControllerBase(shopService)
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await ShopService.GetSettingsAsync(shopId.Data, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] ShopSettingsInput input, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await ShopService.UpdateAsync(shopId.Data, input, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("delivery-zones")]
    public async Task<IActionResult> ListZones(CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await deliveryService.ListZonesAsync(shopId.Data, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("delivery-zones")]
    public async Task<IActionResult> CreateZone(
        [FromBody] DeliveryZoneInput input, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await deliveryService.CreateZoneAsync(shopId.Data, input, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("delivery-zones/{id:guid}")]
    public async Task<IActionResult> UpdateZone(
        Guid id, [FromBody] DeliveryZoneInput input, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await deliveryService.UpdateZoneAsync(shopId.Data, id, input, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("delivery-zones/{id:guid}")]
    public async Task<IActionResult> DeleteZone(Guid id, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await deliveryService.DeleteZoneAsync(shopId.Data, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
