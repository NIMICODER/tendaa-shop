using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class CustomerServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

    private CustomerService CreateService() => new(CreateRepository<Customer>(), CreateUnitOfWork());

    [Fact]
    public async Task LinkBsuIdChangeAsync_ExistingCustomer_UpdatesBsuIdInPlace()
    {
        var customerId = Guid.NewGuid();
        Db.Customers.Add(new Customer
        {
            Id = customerId,
            ShopId = _shopId,
            BsuId = "US.old",
            PhoneNumber = "16505551234",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        var result = await CreateService().LinkBsuIdChangeAsync(_shopId, previousBsuId: "US.old", newBsuId: "US.new");

        Assert.True(result.IsSuccessful);
        Assert.Equal(customerId, result.Data!.Id);

        var customerCount = await Db.Customers.CountAsync();
        Assert.Equal(1, customerCount);

        var persisted = await Db.Customers.SingleAsync();
        Assert.Equal("US.new", persisted.BsuId);
        Assert.Equal("16505551234", persisted.PhoneNumber); // unchanged — no newPhone given
    }

    [Fact]
    public async Task LinkBsuIdChangeAsync_WithNewPhone_UpdatesPhoneToo()
    {
        Db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            ShopId = _shopId,
            BsuId = "US.old",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();

        await CreateService().LinkBsuIdChangeAsync(_shopId, previousBsuId: "US.old", newBsuId: "US.new", newPhone: "16505559999");

        var persisted = await Db.Customers.SingleAsync();
        Assert.Equal("US.new", persisted.BsuId);
        Assert.Equal("16505559999", persisted.PhoneNumber);
    }

    [Fact]
    public async Task LinkBsuIdChangeAsync_NoMatchingCustomer_ReturnsNotFound()
    {
        var result = await CreateService().LinkBsuIdChangeAsync(_shopId, previousBsuId: "US.missing", newBsuId: "US.new");

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task UpdateNameAsync_ExistingCustomer_SetsNameTrimmed()
    {
        var customerId = Guid.NewGuid();
        Db.Customers.Add(new Customer { Id = customerId, ShopId = _shopId, BsuId = "US.1", CreatedAt = DateTimeOffset.UtcNow });
        await Db.SaveChangesAsync();

        var result = await CreateService().UpdateNameAsync(_shopId, customerId, "  David  ");

        Assert.True(result.IsSuccessful);
        var persisted = await Db.Customers.SingleAsync();
        Assert.Equal("David", persisted.Name);
    }

    [Fact]
    public async Task UpdateNameAsync_NoMatchingCustomer_ReturnsNotFound()
    {
        var result = await CreateService().UpdateNameAsync(_shopId, Guid.NewGuid(), "David");

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }
}
