using System.Globalization;

namespace Tendaa.Core.Money;

/// <summary>Formats money for customer-facing text so the model never does its own arithmetic or
/// formatting (SPEC.md rule: prices and totals come only from the database). Covers the demo
/// shop's currency plus other common African currencies for future multi-tenant sellers; an
/// unrecognised ISO 4217 code falls back to a plain code prefix rather than guessing a symbol.</summary>
public static class MoneyFormatter
{
    private static readonly Dictionary<string, string> Symbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NGN"] = "₦",
        ["GHS"] = "₵",
        ["KES"] = "KSh ",
        ["ZAR"] = "R",
        ["USD"] = "$",
    };

    public static string Format(decimal amount, string currencyCode)
    {
        var symbol = Symbols.TryGetValue(currencyCode, out var s) ? s : $"{currencyCode} ";
        var format = amount == Math.Truncate(amount) ? "#,0" : "#,0.00";
        return symbol + amount.ToString(format, CultureInfo.InvariantCulture);
    }
}
