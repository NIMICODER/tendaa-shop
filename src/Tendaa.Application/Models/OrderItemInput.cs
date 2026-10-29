namespace Tendaa.Application.Models;

public sealed record OrderItemInput(Guid ProductVariantId, int Quantity);
