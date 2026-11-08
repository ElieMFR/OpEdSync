using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.OpEdSync.Models;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// The requests sent to the AnimeThemes GraphQL API and the reading of its answers. Written by hand over
/// <see cref="JsonElement"/> so that a field AnimeThemes adds or leaves out never fails a whole answer.
/// </summary>
public static class AnimeThemesGraphQl
{
    /// <summary>
    /// The lookup: anime by their ID on an external site, with the themes, videos and audio the plugin downloads.
    /// </summary>
    public const string LookupQuery =
        "query($site: ResourceSite!, $id: [Int!]) { filterAnimeByExternalSite(site: $site, id: $id) { "
        + "id slug year title { romaji } resources { pageInfo { hasNextPage } nodes { id externalId site } } "
        + "themes { id type sequence slug entries { id version episodes nsfw spoiler "
        + "videos { nodes { id basename filename link nc overlap source audio { id filename link mimetype path } } } } } } }";

    /// <summary>
    /// The smallest valid request, used to check that the API answers.
    /// </summary>
    public const string CheckRequest = "{\"query\":\"{ __typename }\"}";

    /// <summary>
    /// Builds the body of a lookup request.
    /// </summary>
    /// <param name="site">External site, see <see cref="Sites"/>.</param>
    /// <param name="ids">IDs on that site.</param>
    /// <returns>The JSON body.</returns>
    public static string BuildLookupRequest(string site, IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("query", LookupQuery);
            writer.WriteStartObject("variables");
            writer.WriteString("site", ToGraphQlSite(site));
            writer.WriteStartArray("id");
            foreach (var id in ids)
            {
                writer.WriteNumberValue(id);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Converts a site of <see cref="Sites"/> to the GraphQL enum.
    /// </summary>
    /// <param name="site">Site name.</param>
    /// <returns>The enum value.</returns>
    public static string ToGraphQlSite(string site) => site switch
    {
        Sites.ANIDB => "ANIDB",
        Sites.MYANIMELIST => "MAL",
        Sites.ANILIST => "ANILIST",
        _ => throw new ArgumentOutOfRangeException(nameof(site), site, "Unsupported site"),
    };

    /// <summary>
    /// Reads the answer to a lookup.
    /// </summary>
    /// <param name="document">The parsed answer.</param>
    /// <returns>The anime and the errors.</returns>
    /// <exception cref="JsonException">The answer is not what the API documents.</exception>
    public static GraphQlResult Parse(JsonDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("AnimeThemes answered something that is not a JSON object.");
        }

        var errors = new Collection<string>();
        if (root.TryGetProperty("errors", out var errorList) && errorList.ValueKind == JsonValueKind.Array)
        {
            foreach (var error in errorList.EnumerateArray())
            {
                errors.Add(Text(error, "message") ?? error.ToString());
            }
        }

        var found = new Collection<Anime>();
        int truncated = 0;
        if (root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("filterAnimeByExternalSite", out var list)
            && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var anime in list.EnumerateArray())
            {
                found.Add(ReadAnime(anime, ref truncated));
            }
        }
        else if (errors.Count == 0)
        {
            throw new JsonException("AnimeThemes answered without data.");
        }

        return new GraphQlResult(found, errors, truncated);
    }

    private static Anime ReadAnime(JsonElement anime, ref int truncated)
    {
        var resources = new Collection<Resource>();
        if (anime.TryGetProperty("resources", out var connection) && connection.ValueKind == JsonValueKind.Object)
        {
            if (connection.TryGetProperty("pageInfo", out var page) && Flag(page, "hasNextPage"))
            {
                truncated++;
            }

            foreach (var node in Items(connection, "nodes"))
            {
                var site = ToSite(Text(node, "site"));
                var externalId = IntOrNull(node, "externalId");
                if (site is not null && externalId is not null)
                {
                    resources.Add(new Resource(IntOrNull(node, "id") ?? 0, externalId.Value, site));
                }
            }
        }

        var themes = new Collection<AnimeTheme>();
        foreach (var theme in Items(anime, "themes"))
        {
            themes.Add(ReadTheme(theme));
        }

        var name = anime.TryGetProperty("title", out var title) ? Text(title, "romaji") : null;
        return new Anime(Int(anime, "id"), name ?? string.Empty, Text(anime, "slug") ?? string.Empty, IntOrNull(anime, "year"), resources, themes);
    }

    private static AnimeTheme ReadTheme(JsonElement theme)
    {
        var entries = new Collection<AnimeThemeEntry>();
        foreach (var entry in Items(theme, "entries"))
        {
            var videos = new Collection<Video>();
            if (entry.TryGetProperty("videos", out var connection) && connection.ValueKind == JsonValueKind.Object)
            {
                foreach (var video in Items(connection, "nodes"))
                {
                    videos.Add(ReadVideo(video));
                }
            }

            entries.Add(new AnimeThemeEntry(Int(entry, "id"), IntOrNull(entry, "version"), Text(entry, "episodes"), Flag(entry, "nsfw"), Flag(entry, "spoiler"), videos));
        }

        return new AnimeTheme(Int(theme, "id"), ToThemeType(Text(theme, "type")), IntOrNull(theme, "sequence"), null, Text(theme, "slug"), entries);
    }

    private static Video ReadVideo(JsonElement video)
    {
        Audio audio = null!;
        if (video.TryGetProperty("audio", out var audioElement) && audioElement.ValueKind == JsonValueKind.Object)
        {
            audio = new Audio(
                IntOrNull(audioElement, "id") ?? 0,
                Text(audioElement, "path") ?? string.Empty,
                Text(audioElement, "filename") ?? string.Empty,
                Text(audioElement, "mimetype") ?? string.Empty,
                Text(audioElement, "link") ?? string.Empty);
        }

        return new Video(
            Int(video, "id"),
            Text(video, "basename") ?? string.Empty,
            Text(video, "filename") ?? string.Empty,
            ToSource(Text(video, "source")),
            ToOverlap(Text(video, "overlap")),
            Text(video, "link") ?? string.Empty,
            Flag(video, "nc"),
            audio);
    }

    /// <summary>
    /// A site the plugin understands, or <c>null</c> for any other link of the anime (Netflix, Wikipedia...).
    /// </summary>
    private static string? ToSite(string? graphQlSite) => graphQlSite switch
    {
        "ANIDB" => Sites.ANIDB,
        "MAL" => Sites.MYANIMELIST,
        "ANILIST" => Sites.ANILIST,
        _ => null,
    };

    // "IN" (insert song) and anything AnimeThemes adds later are never downloaded.
    private static ThemeType ToThemeType(string? value) => value switch
    {
        "OP" => ThemeType.OP,
        "ED" => ThemeType.ED,
        _ => ThemeType.Other,
    };

    // The API says TRANS where the plugin says Transition. An unknown value is read as the most cautious one.
    private static OverlapType ToOverlap(string? value) => value switch
    {
        "NONE" => OverlapType.None,
        "TRANS" => OverlapType.Transition,
        _ => OverlapType.Over,
    };

    private static VideoSource? ToSource(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return Enum.TryParse<VideoSource>(value, true, out var source) && Enum.IsDefined(source) && !char.IsDigit(value[0])
            ? source
            : VideoSource.WEB;
    }

    private static IEnumerable<JsonElement> Items(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : [];

    private static string? Text(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? IntOrNull(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static int Int(JsonElement element, string name)
        => IntOrNull(element, name) ?? throw new JsonException($"AnimeThemes answered without \"{name}\".");

    private static bool Flag(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
