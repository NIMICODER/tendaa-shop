using Tendaa.Core;

namespace Tendaa.Application.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, matching the repo convention. Records which
/// shops were flagged so tests can assert services notify without needing a real SignalR hub.</summary>
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
