using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// Figures shown at the top of the configuration page.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("OpEdSync")]
[Produces("application/json")]
public class StatsController : ControllerBase
{
    private readonly StatsService _statsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsController"/> class.
    /// </summary>
    /// <param name="statsService">Statistics service.</param>
    public StatsController(StatsService statsService)
    {
        _statsService = statsService;
    }

    /// <summary>
    /// Gets the coverage of the selected libraries and the summary of the last run.
    /// </summary>
    /// <param name="refresh">Recompute instead of using the figures of the last few minutes.</param>
    /// <returns>The statistics.</returns>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatsResponse> GetStats([FromQuery] bool refresh = false) => _statsService.Get(refresh);
}
