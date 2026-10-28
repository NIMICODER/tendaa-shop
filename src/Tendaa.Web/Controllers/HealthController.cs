using Microsoft.AspNetCore.Mvc;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(IHealthService healthService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var result = healthService.GetHealth();
        return StatusCode(result.StatusCode, result);
    }
}
