using Microsoft.AspNetCore.SignalR;
using Tendaa.Core;

namespace Tendaa.Web.Hubs;

/// <summary>Web-layer <see cref="IDashboardNotifier"/>: wraps <see cref="IHubContext{T}"/>
/// so Application services can notify without referencing SignalR. Registered in Program.cs,
/// not ServiceCollections.cs — that's Tendaa.Application's registration file and can't see
/// this Web-layer type.</summary>
public sealed class DashboardNotifier(IHubContext<DashboardHub> hub) : IDashboardNotifier
{
    public static string GroupFor(Guid shopId) => $"shop-{shopId}";

    public Task NotifyOrdersChangedAsync(Guid shopId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(GroupFor(shopId)).SendAsync("OrdersChanged", cancellationToken);

    public Task NotifyHandoffsChangedAsync(Guid shopId, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(GroupFor(shopId)).SendAsync("HandoffsChanged", cancellationToken);
}
