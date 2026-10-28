using Tendaa.Application.Common;

namespace Tendaa.Application.Services.Interfaces;

public interface IModelPingService
{
    Task<TendaaResult<string>> PingAsync(CancellationToken cancellationToken = default);
}
