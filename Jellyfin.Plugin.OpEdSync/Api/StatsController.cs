using System.Threading;
using System.Threading.Tasks;
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
    private readonly StatusService _statusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatsController"/> class.
    /// </summary>
    /// <param name="statsService">Statistics service.</param>
    /// <param name="statusService">Status service.</param>
    public StatsController(StatsService statsService, StatusService statusService)
    {
        _statsService = statsService;
        _statusService = statusService;
    }

    /// <summary>
    /// Gets the plugin version, the plugins it relies on and whether AnimeThemes answers.
    /// </summary>
    /// <param name="recheck">Check AnimeThemes again instead of using the check of the last minutes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<StatusResponse>> GetStatus([FromQuery] bool recheck = false, CancellationToken cancellationToken = default)
        => await _statusService.GetAsync(recheck, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gets the coverage of the selected libraries and the summary of the last run.
    /// </summary>
    /// <param name="refresh">Recompute instead of using the figures of the last few minutes.</param>
    /// <returns>The statistics.</returns>
    [HttpGet("Stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatsResponse> GetStats([FromQuery] bool refresh = false) => _statsService.Get(refresh);
}
