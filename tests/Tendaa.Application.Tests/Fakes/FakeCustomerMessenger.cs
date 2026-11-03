using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Application.Tests.Fakes;

public sealed class FakeCustomerMessenger : ICustomerMessenger
{
    public List<(Guid ShopId, Guid CustomerId, string Text)> Sent { get; } = [];

    public bool Result { get; set; } = true;

    public Task<bool> TrySendAsync(
        Guid shopId, Guid customerId, string text, CancellationToken cancellationToken = default)
    {
        Sent.Add((shopId, customerId, text));
        return Task.FromResult(Result);
    }
}
