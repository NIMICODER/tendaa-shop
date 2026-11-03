using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class CatalogController(IProductService productService, IShopService shopService)
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

        var result = await productService.ListForDashboardAsync(shopId.Data, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] ProductInput input, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await productService.CreateAsync(shopId.Data, input, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] ProductInput input, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await productService.UpdateAsync(shopId.Data, id, input, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var shopId = await ResolveShopIdAsync(cancellationToken);
        if (!shopId.IsSuccessful)
        {
            return StatusCode(shopId.StatusCode, shopId);
        }

        var result = await productService.DeleteAsync(shopId.Data, id, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
