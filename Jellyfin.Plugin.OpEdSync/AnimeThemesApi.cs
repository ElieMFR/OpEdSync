using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Represents the AnimeThemes API.
/// </summary>
public sealed class AnimeThemesApi : IDisposable
{
    // Largest page accepted by the API.
    private const int MaxPageSize = 100;

    // Safety net against a pagination loop.
    private const int MaxPages = 10;

    private readonly HttpClient _client;
    private readonly ILogger<AnimeThemesDownloader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesApi"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="clientFactory">Client factory.</param>
    public AnimeThemesApi(IHttpClientFactory clientFactory, ILogger<AnimeThemesDownloader> logger)
    {
        _logger = logger;
        _client = clientFactory.CreateClient("AnimeThemes");
    }

    /// <summary>
    /// Finds anime with all their themes by their IDs on an external site.
    /// </summary>
    /// <param name="site">The external site, see <see cref="Sites"/>.</param>
    /// <param name="ids">IDs on that site.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary with an entry for each passed <paramref name="ids"/>.</returns>
    public async ValueTask<Dictionary<int, Anime[]>> FindByExternalId(string site, IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.Distinct().ToArray();

        _logger.LogInformation("Looking up {Count} shows ({Site}) in the AnimeThemes db...", idList.Length, site);

        if (idList.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(ids));
        }

        // @formatter:off
        var arguments = new Dictionary<string, string?>
        {
            { "filter[resource][external_id]", string.Join(",", idList.Select(it => it.ToString(CultureInfo.InvariantCulture))) },
            { "filter[resource][site]", site },
            { "filter[has]", "resources" },
            { "page[size]", MaxPageSize.ToString(CultureInfo.InvariantCulture) },
            { "include", "animethemes.animethemeentries.videos.audio,resources" },
        };
        // @formatter:on

        string? uri = QueryHelpers.AddQueryString("/anime/", arguments);
        var found = new List<Anime>();

        // One ID can match several anime, so a page may not hold everything: follow the next links.
        for (int page = 0; uri is not null && page < MaxPages; page++)
        {
            _logger.LogInformation("Fetching from API: {Uri} (length={Length})", uri, uri.Length);
            using var result = await _client.GetAsync(new Uri(uri, UriKind.RelativeOrAbsolute), cancellationToken).ConfigureAwait(false);
            result.EnsureSuccessStatusCode();

            var content = await result.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var contentDisposal = content.ConfigureAwait(false);

            var response = await JsonSerializer.DeserializeAsync<AnimeResponse>(content, cancellationToken: cancellationToken).ConfigureAwait(false);
            found.AddRange(response?.Anime ?? []);
            uri = response?.Links?.Next;
        }

        var animeByAniId = found
            .Select(a => (Id: a.GetExternalId(site), Anime: a))
            .Where(a => a.Id.HasValue)
            .GroupBy(a => a.Id!.Value, a => a.Anime)
            .ToDictionary(a => a.Key, a => a.ToArray());

        _logger.LogInformation("Of a total of {TotalCount} shows, we were able to find entries for {FinalCount} shows", idList.Length, animeByAniId.Count);

        return idList.ToDictionary(id => id, id => animeByAniId.GetValueOrDefault(id) ?? []);
    }

    /// <summary>
    /// Disposes of the client.
    /// </summary>
    /// <param name="disposing">Whether the object is currently being disposed.</param>
    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _client.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
    }
}
