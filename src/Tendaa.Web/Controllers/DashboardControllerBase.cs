using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Common;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

/// <summary>Base for dashboard API controllers: single-shop resolution plus the seller name.</summary>
public abstract class DashboardControllerBase(IShopService shopService) : ControllerBase
{
    protected IShopService ShopService { get; } = shopService;

    protected string SellerName => User.Identity?.Name ?? "Seller";

    /// <summary>Single-shop MVP: the one seeded shop is the dashboard's shop (same pattern as
    /// DevController's chat endpoint).</summary>
    protected async Task<TendaaResult<Guid>> ResolveShopIdAsync(CancellationToken cancellationToken)
    {
        var shop = await ShopService.GetOnlyShopAsync(cancellationToken);
        return shop.IsSuccessful
            ? TendaaResult<Guid>.Success(shop.Data!.Id)
            : TendaaResult<Guid>.NotFound(shop.Message);
    }
}
