using Tendaa.Application.Common;
using Tendaa.Application.Models;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class HealthService : IHealthService
{
    public TendaaResult<HealthStatus> GetHealth() =>
        TendaaResult<HealthStatus>.Success(new HealthStatus("ok", DateTimeOffset.UtcNow));
}
