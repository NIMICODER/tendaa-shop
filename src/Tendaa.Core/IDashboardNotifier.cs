namespace Tendaa.Core;

/// <summary>Lets Application services tell the seller dashboard that seller-visible state
/// changed, without referencing Tendaa.Web (where the SignalR hub lives) — plain signatures,
/// no SignalR types, same pattern as <see cref="IWhatsAppSender"/>. Delivery is
/// invalidate-and-refetch: the SPA refetches its queries, the hub never ships DTOs.</summary>
public interface IDashboardNotifier
{
    Task NotifyOrdersChangedAsync(Guid shopId, CancellationToken cancellationToken = default);

    Task NotifyHandoffsChangedAsync(Guid shopId, CancellationToken cancellationToken = default);
}
