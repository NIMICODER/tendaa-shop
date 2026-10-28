using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

/// <summary>Development-only diagnostics. Not gated via conditional route registration (MVC
/// controllers don't support that inline the way minimal APIs do) — each action checks
/// <see cref="IWebHostEnvironment.IsDevelopment"/> itself and returns 404 outside Development.</summary>
[ApiController]
[Route("dev")]
public sealed class DevController(IModelPingService modelPingService, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("ping-model")]
    public async Task<IActionResult> PingModel(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var result = await modelPingService.PingAsync(cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
