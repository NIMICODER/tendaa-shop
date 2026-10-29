namespace Tendaa.Application.Models;

public sealed record DeliveryZoneSummary(Guid Id, string Name, decimal Fee, string DeliveryTimeText);

/// <summary><see cref="Matched"/> is null when the requested area doesn't match a known zone —
/// <see cref="AllZones"/> is always populated so the caller can offer the list instead of a bare
/// "not found".</summary>
public sealed record DeliveryFeeLookup(DeliveryZoneSummary? Matched, IReadOnlyList<DeliveryZoneSummary> AllZones);
