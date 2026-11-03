namespace Tendaa.Application.Models;

public sealed record DeliveryZoneInput(
    string Name,
    decimal Fee,
    string? DeliveryTimeText);
