using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Hubs;

/// <summary>Receive-only hub: clients never call methods, they just get told something changed
/// and refetch over REST. Polling stays the fallback if the connection drops (ARCHITECTURE.md),
/// so coarse-grained events are all this needs.</summary>
[Authorize]
public sealed class DashboardHub(IShopService shopService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Single-shop MVP: exactly one shop exists, so its group is the only one to join.
        var shop = (await shopService.GetOnlyShopAsync(Context.ConnectionAborted)).Data;
        if (shop is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DashboardNotifier.GroupFor(shop.Id));
        }

        await base.OnConnectedAsync();
    }
}
