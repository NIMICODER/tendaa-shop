using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class CustomerService(IRepository<Customer> customers, IUnitOfWork unitOfWork) : ICustomerService
{
    public async Task<TendaaResult<Customer>> FindOrCreateByBsuIdAsync(
        Guid shopId,
        string bsuId,
        string? phone = null,
        string? username = null,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.BsuId == bsuId, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                BsuId = bsuId,
                PhoneNumber = phone,
                Username = username,
                Name = name,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await customers.AddAsync(customer, cancellationToken);
        }
        else
        {
            // Update-only-when-present: a webhook event that omits a field (e.g. phone, once a
            // username hides it) never clears what we already know.
            if (phone is not null)
            {
                customer.PhoneNumber = phone;
            }

            if (username is not null)
            {
                customer.Username = username;
            }

            if (name is not null)
            {
                customer.Name = name;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TendaaResult<Customer>.Success(customer);
    }

    public async Task<TendaaResult<Customer>> LinkBsuIdChangeAsync(
        Guid shopId,
        string previousBsuId,
        string newBsuId,
        string? newPhone = null,
        CancellationToken cancellationToken = default)
    {
        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.BsuId == previousBsuId, cancellationToken);

        if (customer is null)
        {
            return TendaaResult<Customer>.NotFound();
        }

        customer.BsuId = newBsuId;
        if (newPhone is not null)
        {
            customer.PhoneNumber = newPhone;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TendaaResult<Customer>.Success(customer);
    }

    public async Task<TendaaResult<Customer>> GetByIdAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == customerId, cancellationToken);

        return customer is null ? TendaaResult<Customer>.NotFound() : TendaaResult<Customer>.Success(customer);
    }

    public async Task<TendaaResult<Customer>> UpdateNameAsync(
        Guid shopId, Guid customerId, string name, CancellationToken cancellationToken = default)
    {
        var customer = await customers.Query()
            .FirstOrDefaultAsync(c => c.ShopId == shopId && c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return TendaaResult<Customer>.NotFound();
        }

        customer.Name = name.Trim();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TendaaResult<Customer>.Success(customer);
    }
}
