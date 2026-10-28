using Tendaa.Core.Orders;

namespace Tendaa.Core.Tests.Orders;

public class OrderTotalsTests
{
    private static OrderItem Item(decimal unitPrice, int quantity) =>
        new() { UnitPrice = unitPrice, Quantity = quantity };

    [Fact]
    public void Calculate_SumsItemsAndAddsDeliveryFee()
    {
        var items = new[] { Item(5000m, 2), Item(1500m, 1) };

        var result = OrderTotals.Calculate(items, deliveryFee: 1200m);

        Assert.Equal(11500m, result.ItemTotal);
        Assert.Equal(1200m, result.DeliveryFee);
        Assert.Equal(12700m, result.GrandTotal);
    }

    [Fact]
    public void Calculate_WithNoItems_ReturnsJustDeliveryFee()
    {
        var result = OrderTotals.Calculate([], deliveryFee: 1200m);

        Assert.Equal(0m, result.ItemTotal);
        Assert.Equal(1200m, result.GrandTotal);
    }

    [Fact]
    public void Calculate_NegativeDeliveryFee_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrderTotals.Calculate([], deliveryFee: -1m));
    }

    [Fact]
    public void LineTotal_IsUnitPriceTimesQuantity()
    {
        var item = Item(2500m, 3);

        Assert.Equal(7500m, item.LineTotal);
    }
}
