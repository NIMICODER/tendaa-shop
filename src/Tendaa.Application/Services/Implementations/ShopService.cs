using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ShopService(IRepository<Shop> shops, IUnitOfWork unitOfWork) : IShopService
{
    public async Task<TendaaResult<ShopInfo>> GetInfoAsync(Guid shopId, CancellationToken cancellationToken = default)
    {
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        if (shop is null)
        {
            return TendaaResult<ShopInfo>.NotFound();
        }

        var info = new ShopInfo(shop.HoursText, shop.RulesText, shop.BankName, shop.BankAccountNumber, shop.BankAccountName);
        return TendaaResult<ShopInfo>.Success(info);
    }

    public async Task<TendaaResult<Shop>> GetOnlyShopAsync(CancellationToken cancellationToken = default)
    {
        var shop = await shops.Query().FirstOrDefaultAsync(cancellationToken);
        return shop is null ? TendaaResult<Shop>.NotFound() : TendaaResult<Shop>.Success(shop);
    }

    public async Task<TendaaResult<Shop>> GetByPhoneNumberIdAsync(
        string phoneNumberId, CancellationToken cancellationToken = default)
    {
        var shop = await shops.Query()
            .FirstOrDefaultAsync(s => s.WhatsAppPhoneNumberId == phoneNumberId, cancellationToken);
        return shop is null ? TendaaResult<Shop>.NotFound() : TendaaResult<Shop>.Success(shop);
    }

    public async Task<TendaaResult<ShopSettings>> GetSettingsAsync(
        Guid shopId, CancellationToken cancellationToken = default)
    {
        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        return shop is null
            ? TendaaResult<ShopSettings>.NotFound()
            : TendaaResult<ShopSettings>.Success(ToSettings(shop));
    }

    public async Task<TendaaResult<ShopSettings>> UpdateAsync(
        Guid shopId, ShopSettingsInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return TendaaResult<ShopSettings>.BadRequest("Shop name is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Country))
        {
            return TendaaResult<ShopSettings>.BadRequest("Country is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Currency))
        {
            return TendaaResult<ShopSettings>.BadRequest("Currency is required.");
        }

        if (string.IsNullOrWhiteSpace(input.TimeZone))
        {
            return TendaaResult<ShopSettings>.BadRequest("Time zone is required.");
        }

        var shop = await shops.GetByIdAsync(shopId, cancellationToken);
        if (shop is null)
        {
            return TendaaResult<ShopSettings>.NotFound();
        }

        shop.Name = input.Name.Trim();
        shop.Country = input.Country.Trim();
        shop.Currency = input.Currency.Trim();
        shop.TimeZone = input.TimeZone.Trim();
        shop.HoursText = Clean(input.HoursText);
        shop.RulesText = Clean(input.RulesText);
        shop.BankName = Clean(input.BankName);
        shop.BankAccountNumber = Clean(input.BankAccountNumber);
        shop.BankAccountName = Clean(input.BankAccountName);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return TendaaResult<ShopSettings>.Success(ToSettings(shop));
    }

    private static ShopSettings ToSettings(Shop shop) => new(
        shop.Id, shop.Name, shop.Country, shop.Currency, shop.TimeZone,
        shop.HoursText, shop.RulesText, shop.BankName, shop.BankAccountNumber, shop.BankAccountName);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
