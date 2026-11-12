using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.OpEdSync.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Represents the AnimeThemes API (GraphQL).
/// </summary>
public sealed class AnimeThemesApi
{
    // Most IDs of one request; the API handles 100 in about a second.
    private const int MaxIds = 100;

    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<AnimeThemesDownloader> _logger;
    private readonly RateLimitGate _gate;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesApi"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="clientFactory">Client factory.</param>
    public AnimeThemesApi(IHttpClientFactory clientFactory, ILogger<AnimeThemesDownloader> logger)
        : this(clientFactory, logger, RateLimitGate.Shared)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesApi"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="clientFactory">Client factory.</param>
    /// <param name="gate">Gate that keeps the lookups under the rate limit of the API.</param>
    public AnimeThemesApi(IHttpClientFactory clientFactory, ILogger<AnimeThemesDownloader> logger, RateLimitGate gate)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _gate = gate;
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

        if (idList.Length > MaxIds)
        {
            throw new ArgumentOutOfRangeException(nameof(ids));
        }

        // A client per lookup: the factory reads the configured address each time, so a changed setting applies
        // without restarting the server.
        using var client = _clientFactory.CreateClient("AnimeThemes");
        var outcome = new LookupOutcome();
        var found = await FindBatch(client, site, idList, outcome, cancellationToken).ConfigureAwait(false);

        // The IDs that failed alone stay unanswered while the others are kept; none answered means the error is not theirs.
        if (outcome.Failed.Count > 0 && !outcome.Succeeded)
        {
            throw new HttpRequestException("AnimeThemes answered with an error: " + outcome.LastError);
        }

        if (outcome.Failed.Count > 0)
        {
            _logger.LogWarning("AnimeThemes refused {Count} IDs ({Ids}) one by one: {Error}", outcome.Failed.Count, string.Join(", ", outcome.Failed), outcome.LastError);
        }

        // One ID can match several anime: the links of each one tell which ID it answers.
        var animeByAniId = found
            .Select(a => (Id: a.GetExternalId(site), Anime: a))
            .Where(a => a.Id.HasValue)
            .GroupBy(a => a.Id!.Value, a => a.Anime)
            .ToDictionary(a => a.Key, a => a.ToArray());

        _logger.LogInformation("Of a total of {TotalCount} shows, we were able to find entries for {FinalCount} shows", idList.Length, animeByAniId.Count);

        return idList.ToDictionary(id => id, id => animeByAniId.GetValueOrDefault(id) ?? []);
    }

    private static bool IsTooComplex(string error)
        => error.Contains("complex", StringComparison.OrdinalIgnoreCase) || error.Contains("nested too deep", StringComparison.OrdinalIgnoreCase);

    private static int? Header(HttpResponseMessage response, string name)
        => response.Headers.TryGetValues(name, out var values) && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;

    private async Task<List<Anime>> FindBatch(HttpClient client, string site, int[] ids, LookupOutcome outcome, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        using var content = new StringContent(AnimeThemesGraphQl.BuildLookupRequest(site, ids), Encoding.UTF8, "application/json");
        _logger.LogInformation("Posting a lookup of {Count} IDs to the AnimeThemes GraphQL API", ids.Length);
        using var response = await client.PostAsync(new Uri(string.Empty, UriKind.Relative), content, cancellationToken).ConfigureAwait(false);

        long? reset = Header(response, "x-ratelimit-reset");
        _gate.Update(Header(response, "x-ratelimit-remaining"), reset);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var bodyDisposal = body.ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = AnimeThemesGraphQl.Parse(document);

        if (result.Errors.Count > 0)
        {
            var error = string.Join(" | ", result.Errors);

            // An error in HTTP 200 is either the complexity limit or one ID the API chokes on: half the IDs cost half
            // as much, and halving again ends on the faulty one alone, so the others are not lost with it. A 5xx
            // (thrown above) is the server's, not the IDs': splitting would only multiply the failing requests.
            if (ids.Length > 1 && outcome.CanSplit)
            {
                _logger.LogWarning("AnimeThemes answered an error for {Count} IDs, splitting the request: {Error}", ids.Length, error);
                var half = ids.Length / 2;
                var first = await FindBatch(client, site, ids[..half], outcome, cancellationToken).ConfigureAwait(false);
                var second = await FindBatch(client, site, ids[half..], outcome, cancellationToken).ConfigureAwait(false);
                first.AddRange(second);
                return first;
            }

            if (ids.Length == 1 && !IsTooComplex(error))
            {
                outcome.Failed.AddRange(ids);
                outcome.LastError = error;
                return [];
            }

            throw new HttpRequestException("AnimeThemes answered with an error: " + error);
        }

        outcome.Succeeded = true;

        if (result.TruncatedResources > 0)
        {
            _logger.LogWarning("{Count} anime came with a truncated list of external links; some may not be matched to their ID", result.TruncatedResources);
        }

        return [.. result.Anime];
    }

    // What the splitting of one lookup has seen. Without any success after a few lone failures, the error is the
    // API's and not the IDs': splitting further would burn the rate limit for nothing.
    private sealed class LookupOutcome
    {
        private const int GiveUpAfter = 3;

        public List<int> Failed { get; } = [];

        public string LastError { get; set; } = string.Empty;

        public bool Succeeded { get; set; }

        public bool CanSplit => Succeeded || Failed.Count < GiveUpAfter;
    }
}
