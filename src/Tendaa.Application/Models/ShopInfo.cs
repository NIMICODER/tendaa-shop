namespace Tendaa.Application.Models;

public sealed record ShopInfo(
    string? HoursText,
    string? RulesText,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);
