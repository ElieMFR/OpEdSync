using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Web;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OpEdSync.Api;

/// <summary>
/// What OpEdSync depends on: the plugins that give IDs or inject the Android fix, and AnimeThemes itself.
/// </summary>
public sealed class StatusService : IDisposable
{
    /// <summary>
    /// Name of the HTTP client used to check AnimeThemes, without retries so a check answers fast.
    /// </summary>
    public const string CheckClientName = "AnimeThemesCheck";

    private static readonly TimeSpan CheckValidity = TimeSpan.FromMinutes(10);

    // Plugin name in Jellyfin -> role shown on the page.
    private static readonly (string Name, string Role)[] KnownDependencies =
    [
        ("AniDB", "Ids"),
        ("MyAnimeList", "Ids"),
        ("AniList", "Ids"),
        ("File Transformation", "AndroidFix"),
    ];

    private readonly IPluginManager _pluginManager;
    private readonly IHttpClientFactory _clientFactory;
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private AnimeThemesCheck? _lastCheck;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusService"/> class.
    /// </summary>
    /// <param name="pluginManager">Plugin manager.</param>
    /// <param name="clientFactory">HTTP client factory.</param>
    public StatusService(IPluginManager pluginManager, IHttpClientFactory clientFactory)
    {
        _pluginManager = pluginManager;
        _clientFactory = clientFactory;
    }

    /// <summary>
    /// Gets the status, checking AnimeThemes when the last check is old or when asked to.
    /// </summary>
    /// <param name="recheck">Whether to check AnimeThemes again now.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status.</returns>
    public async Task<StatusResponse> GetAsync(bool recheck, CancellationToken cancellationToken)
    {
        var plugins = _pluginManager.Plugins.ToList();
        var dependencies = KnownDependencies.Select(known =>
        {
            var plugin = plugins.FirstOrDefault(it => string.Equals(it.Name, known.Name, StringComparison.OrdinalIgnoreCase));
            return new DependencyInfo
            {
                Name = known.Name,
                Role = known.Role,
                Installed = plugin is not null,
                Active = plugin?.Manifest.Status == PluginStatus.Active,
                Version = plugin?.Version.ToString() ?? string.Empty,
            };
        }).ToList();

        return new StatusResponse
        {
            Version = Plugin.Instance?.Version.ToString() ?? string.Empty,
            Dependencies = dependencies,
            AndroidFixActive = ExoPlayerFixRegistration.IsActive,
            AnimeThemes = await CheckAnimeThemesAsync(recheck, cancellationToken).ConfigureAwait(false),
        };
    }

    private async Task<AnimeThemesCheck> CheckAnimeThemesAsync(bool force, CancellationToken cancellationToken)
    {
        await _checkLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && _lastCheck is not null && DateTime.UtcNow - _lastCheck.CheckedUtc < CheckValidity)
            {
                return _lastCheck;
            }

            var watch = Stopwatch.StartNew();
            var check = new AnimeThemesCheck { CheckedUtc = DateTime.UtcNow };
            try
            {
                using var client = _clientFactory.CreateClient(CheckClientName);
                using var response = await client.GetAsync(new Uri("/anime?page%5Bsize%5D=1&fields%5Banime%5D=id", UriKind.Relative), cancellationToken).ConfigureAwait(false);
                check.StatusCode = (int)response.StatusCode;
                check.Reachable = response.IsSuccessStatusCode;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                check.Reachable = false;
                check.Error = e is TaskCanceledException ? "timeout" : e.Message;
            }

            check.LatencyMs = (int)watch.ElapsedMilliseconds;
            _lastCheck = check;
            return check;
        }
        finally
        {
            _checkLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _checkLock.Dispose();
}
