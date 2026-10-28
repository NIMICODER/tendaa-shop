namespace Tendaa.Core.Orders;

/// <summary>See SPEC.md "Order statuses" for the allowed transition diagram.</summary>
public enum OrderStatus
{
    Draft,
    AwaitingPayment,
    PaymentReview,
    Confirmed,
    Dispatched,
    Delivered,
    Cancelled,
}
