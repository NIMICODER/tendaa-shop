namespace Tendaa.Core.Orders;

public readonly record struct OrderTotalsResult(decimal ItemTotal, decimal DeliveryFee, decimal GrandTotal);

public static class OrderTotals
{
    public static OrderTotalsResult Calculate(IReadOnlyCollection<OrderItem> items, decimal deliveryFee)
    {
        if (deliveryFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveryFee), "Delivery fee cannot be negative.");
        }

        var itemTotal = items.Sum(item => item.LineTotal);
        return new OrderTotalsResult(itemTotal, deliveryFee, itemTotal + deliveryFee);
    }
}
