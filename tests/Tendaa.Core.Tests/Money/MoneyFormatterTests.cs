using Tendaa.Core.Money;

namespace Tendaa.Core.Tests.Money;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(50000, "NGN", "₦50,000")]
    [InlineData(1250.5, "NGN", "₦1,250.50")]
    [InlineData(1500, "GHS", "₵1,500")]
    [InlineData(2500, "KES", "KSh 2,500")]
    [InlineData(100, "ZAR", "R100")]
    [InlineData(99.99, "USD", "$99.99")]
    public void Format_KnownCurrency_UsesSymbolAndGrouping(double amount, string currency, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format((decimal)amount, currency));
    }

    [Fact]
    public void Format_UnknownCurrency_FallsBackToCodePrefix()
    {
        Assert.Equal("XOF 5,000", MoneyFormatter.Format(5000m, "XOF"));
    }
}
