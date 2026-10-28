using Tendaa.Application.Common;
using Tendaa.Application.Models;

namespace Tendaa.Application.Services.Interfaces;

public interface IHealthService
{
    TendaaResult<HealthStatus> GetHealth();
}
