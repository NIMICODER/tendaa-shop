namespace Tendaa.Application.Models;

public sealed record ConfirmOrderResult(
    int Number,
    decimal GrandTotal,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName);
