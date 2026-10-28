using Tendaa.Core.Models.Enums.Orders;

namespace Tendaa.Core.Models.Entities;

public sealed class Order : IShopScoped
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>Human-friendly, sequential per shop (not globally unique).</summary>
    public required int Number { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public decimal ItemTotal { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal GrandTotal { get; set; }

    public Guid? DeliveryZoneId { get; set; }
    public string? Address { get; set; }

    /// <summary>Set by the dashboard when a seller moves the order to <see cref="OrderStatus.Confirmed"/>.
    /// No separate seller-user model yet in the MVP (one admin login per shop), so this is free text.</summary>
    public string? ConfirmedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AwaitingPaymentAt { get; set; }
    public DateTimeOffset? PaymentReviewAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}
