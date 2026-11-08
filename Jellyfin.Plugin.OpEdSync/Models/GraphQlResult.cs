using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// What an AnimeThemes lookup answered: the anime found, the errors reported in the body (GraphQL answers errors with
/// HTTP 200) and how many anime came with a truncated list of external links.
/// </summary>
/// <param name="Anime">Anime found.</param>
/// <param name="Errors">Error messages of the answer, empty when it went well.</param>
/// <param name="TruncatedResources">Number of anime whose list of external links has a next page.</param>
public sealed record GraphQlResult(Collection<Anime> Anime, Collection<string> Errors, int TruncatedResources);
