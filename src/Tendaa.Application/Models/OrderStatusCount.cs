using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Application.Models;

public sealed record OrderStatusCount(OrderStatus Status, int Count);
