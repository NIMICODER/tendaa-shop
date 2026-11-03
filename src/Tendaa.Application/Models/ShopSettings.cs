namespace Tendaa.Application.Models;

public sealed record ShopSettings(
    Guid ShopId,
    string Name,
    string Country,
    string Currency,
    string TimeZone,
    string? HoursText,
    string? RulesText,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);

public sealed record ShopSettingsInput(
    string Name,
    string Country,
    string Currency,
    string TimeZone,
    string? HoursText,
    string? RulesText,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);
