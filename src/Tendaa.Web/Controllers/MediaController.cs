using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tendaa.Core;

namespace Tendaa.Web.Controllers;

/// <summary>Streams previously saved media (receipt photos for the order detail page) back to
/// the dashboard. Authorized and dashboard-only — never a public file host.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class MediaController(IMediaStore mediaStore) : ControllerBase
{
    [HttpGet("{mediaId}")]
    public async Task<IActionResult> Get(string mediaId, CancellationToken cancellationToken)
    {
        var file = await mediaStore.GetAsync(mediaId, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        return File(file.Value.Stream, file.Value.MimeType);
    }
}
