using Tendaa.Core;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, matching the repo convention. Lets the
/// processor tests resolve the real Application services through DI without a real SignalR hub.</summary>
public sealed class FakeDashboardNotifier : IDashboardNotifier
{
    public List<Guid> OrdersChangedShopIds { get; } = [];

    public List<Guid> HandoffsChangedShopIds { get; } = [];

    public Task NotifyOrdersChangedAsync(Guid shopId, CancellationToken cancellationToken = default)
    {
        OrdersChangedShopIds.Add(shopId);
        return Task.CompletedTask;
    }

    public Task NotifyHandoffsChangedAsync(Guid shopId, CancellationToken cancellationToken = default)
    {
        HandoffsChangedShopIds.Add(shopId);
        return Task.CompletedTask;
    }
}
