using Microsoft.Agents.AI;
using Tendaa.Application.Common;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class ModelPingService(AIAgent agent) : IModelPingService
{
    public async Task<TendaaResult<string>> PingAsync(CancellationToken cancellationToken = default)
    {
        var response = await agent.RunAsync(
            "Reply with a short greeting so I know you're working.",
            cancellationToken: cancellationToken);

        return TendaaResult<string>.Success(response.Text);
    }
}
