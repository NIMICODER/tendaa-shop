using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ShopService(IRepository<Shop> shops) : IShopService
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
}
