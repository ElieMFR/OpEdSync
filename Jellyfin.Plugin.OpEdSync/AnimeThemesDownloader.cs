using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Jellyfin.Plugin.OpEdSync.Exceptions;
using Jellyfin.Plugin.OpEdSync.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using MediaType = Jellyfin.Plugin.OpEdSync.Models.MediaType;
using Video = Jellyfin.Plugin.OpEdSync.Models.Video;

namespace Jellyfin.Plugin.OpEdSync;

/// <summary>
/// Class that is responsible for downloading themes.
/// </summary>
public class AnimeThemesDownloader : IDisposable
{
    private const string ThemeMusicFileName = "theme.mp3";
    private const string ThemeMusicDirectory = "theme-music";
    private const string ThemeVideoDirectory = "backdrops";

    // Providers in order of preference: Jellyfin provider key -> AnimeThemes site name.
    private static readonly (string ProviderKey, string Site)[] SupportedProviders =
    [
        ("AniDB", Sites.ANIDB),
        ("MyAnimeList", Sites.MYANIMELIST),
        ("AniList", Sites.ANILIST),
    ];

    private readonly HttpClient _client;
    private readonly AnimeThemesApi _api;
    private readonly ILogger<AnimeThemesDownloader> _logger;
    private readonly IMediaEncoder _mediaEncoder;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnimeThemesDownloader"/> class.
    /// </summary>
    /// <param name="mediaEncoder">Media Encoder to convert OGG files.</param>
    /// <param name="libraryManager">Library manager, used to find the seasons of a series.</param>
    /// <param name="clientFactory">Client factory.</param>
    /// <param name="logger">Logger.</param>
    public AnimeThemesDownloader(IMediaEncoder mediaEncoder, ILibraryManager libraryManager, IHttpClientFactory clientFactory, ILogger<AnimeThemesDownloader> logger)
    {
        _mediaEncoder = mediaEncoder;
        _libraryManager = libraryManager;
        _logger = logger;
        _client = clientFactory.CreateClient("AnimeThemes");
        _api = new AnimeThemesApi(clientFactory, logger);
    }

    /// <summary>
    /// Checks if this item should be processed.
    /// </summary>
    /// <param name="item">Item to check.</param>
    /// <param name="configuration">Plugin configuration.</param>
    /// <returns>Whether item should be processed.</returns>
    public bool ShouldUpdate(BaseItem item, PluginConfiguration configuration)
    {
        return IsSupportedKind(item) && (configuration.ForceSync || !IsSatisfied(item, configuration));
    }

    /// <summary>
    /// Resolves a list of BaseItems to a list of BaseItems with their corresponding anime object.
    /// </summary>
    /// <param name="items">Chunk of items.</param>
    /// <param name="configuration">Plugin configuration to do some pre-filtering.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A filtered list of items to be processed with their corresponding anime.</returns>
    public async IAsyncEnumerable<ItemWithAnime> ResolveItems(BaseItem[] items, PluginConfiguration configuration, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var pending = items
            .Where(IsSupportedKind)
            .Select(it => (Item: it, Candidates: GetCandidates(it, configuration)))
            .Where(it => it.Candidates.Count > 0)
            .ToDictionary(it => it.Item, it => it.Candidates);

        // Round N tries the N-th candidate of every item that is still unresolved.
        for (int round = 0; pending.Count > 0; round++)
        {
            var active = pending.Where(it => it.Value.Count > round).ToList();
            if (active.Count == 0)
            {
                yield break;
            }

            foreach (var siteGroup in active.GroupBy(it => it.Value[round].Site))
            {
                var itemsById = siteGroup
                    .GroupBy(it => it.Value[round].Id, it => it.Key)
                    .ToDictionary(it => it.Key, it => it.ToList());

                Dictionary<int, Anime[]> animeById;
                try
                {
                    animeById = await _api.FindByExternalId(siteGroup.Key, itemsById.Keys, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException e)
                {
                    // Don't let a failing lookup abort the whole run; the next run will retry.
                    _logger.LogError(e, "Lookup on {Site} failed", siteGroup.Key);
                    continue;
                }

                foreach (var entry in animeById)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // A season can be listed before any video is uploaded; treat it as not found so the
                    // previous season (or the series) is tried instead.
                    var animeWithVideos = entry.Value.Where(HasVideos).ToArray();
                    if (animeWithVideos.Length == 0)
                    {
                        continue;
                    }

                    // The same ID may be shared by several items (e.g. the same show in two libraries).
                    foreach (var item in itemsById[entry.Key])
                    {
                        pending.Remove(item);
                        yield return new ItemWithAnime(item, new ReadOnlyCollection<Anime>(animeWithVideos));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Processes an item, downloading its theme if applicable.
    /// </summary>
    /// <param name="item">The DB item to process.</param>
    /// <param name="anime">The anime object belonging to the item.</param>
    /// <param name="configuration">Configuration of the plugin.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that runts until item is done processing.</returns>
    public async ValueTask HandleAsync(BaseItem item, Anime anime, PluginConfiguration configuration, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{Id}] Attempting to filter theme songs for: {Name} (AniDB={AniId})", item.Id, item.Name, anime.Id);

        bool isMovie = item.GetBaseItemKind() == BaseItemKind.Movie;
        var collectionTypeConfig = isMovie ? configuration.MovieSettings : new CollectionTypeConfiguration { AudioSettings = configuration.AudioSettings, VideoSettings = configuration.VideoSettings };

        // Process videos
        bool videoChanged = await ProcessMediaType(MediaType.Video, anime, item, configuration.ForceSync, collectionTypeConfig, cancellationToken).ConfigureAwait(false);

        // Process audios
        bool audioChanged = await ProcessMediaType(MediaType.Audio, anime, item, configuration.ForceSync, collectionTypeConfig, cancellationToken).ConfigureAwait(false);

        if (videoChanged || audioChanged)
        {
            _logger.LogInformation("[{Id}] Saving metadata", item.Id);
            await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("[{Id}] Finished without changes", item.Id);
        }
    }

    private async ValueTask<bool> ProcessMediaType(MediaType type, Anime anime, BaseItem item, bool forceSync, CollectionTypeConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var settings = type == MediaType.Audio ? configuration.AudioSettings : configuration.VideoSettings;

        var distinctThemes = GetBestThemes(anime, settings).DistinctBy(it => it.Theme.Id);

        // Pick themes according to fetch type
        var requiredThemes = PickThemes(settings.FetchType, distinctThemes);

        // Turn them into downloadable links
        var links = ExtractLinks(type, requiredThemes, settings).ToArray();

        bool changesMade = false;
        foreach (var (url, relativePath) in links)
        {
            // Download if needed
            changesMade |= await Download(type, url, item, relativePath, settings.Volume, cancellationToken).ConfigureAwait(false);
        }

        if (forceSync)
        {
            // Only clean up once the replacements are on disk, so a failed download or a show without
            // matching themes never loses the theme it already had. "None" is an explicit request to clear them.
            bool replacementsReady = settings.FetchType == FetchType.None
                ? links.Length == 0
                : links.Length > 0 && links.All(it => File.Exists(Path.Combine(item.ContainingFolderPath, it.Filepath)));

            if (replacementsReady)
            {
                if (type == MediaType.Audio)
                {
                    // We don't need this file because we're using the directory variant.
                    changesMade |= RemoveFile(item, ThemeMusicFileName);
                }

                changesMade |= CleanDirectory(item, type, links.Select(it => Path.GetFileName(it.Filepath)));
            }
            else
            {
                _logger.LogInformation("[{Id}] Keeping existing {Type} themes: no complete replacement available", item.Id, type);
            }
        }

        return changesMade;
    }

    private IEnumerable<FlattenedTheme> PickThemes(FetchType fetchType, IEnumerable<FlattenedTheme> themes)
    {
        switch (fetchType)
        {
            case FetchType.None:
                return [];
            case FetchType.Single:
                return themes.Take(1);
            case FetchType.All:
                return themes;
            default:
                throw new ArgumentOutOfRangeException($"Unknown fetch type: {fetchType}");
        }
    }

    private IEnumerable<(string Url, string Filepath)> ExtractLinks(MediaType type, IEnumerable<FlattenedTheme> themes, MediaTypeConfiguration settings)
    {
        bool isAudio = type == MediaType.Audio;

        foreach (var theme in themes)
        {
            var link = isAudio ? theme.Audio.Link : theme.Video.Link;
            var path = isAudio
                ? Path.Combine(ThemeMusicDirectory, $"{theme.Audio.Filename}__{settings.Volume * 100:0}.mp3")
                : Path.Combine(ThemeVideoDirectory, $"{theme.Video.Filename}__{settings.Volume * 100:0}.webm");

            yield return (link, path);
        }
    }

    private static bool RemoveFile(BaseItem series, string filename)
    {
        var path = Path.Combine(series.ContainingFolderPath, filename);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private bool CleanDirectory(BaseItem series, MediaType mediaType, IEnumerable<string> allowedNames)
    {
        var directory = mediaType == MediaType.Audio ? ThemeMusicDirectory : ThemeVideoDirectory;
        var searchPattern = mediaType == MediaType.Audio ? "*.mp3" : "*.webm";

        var path = Path.Combine(series.ContainingFolderPath, directory);
        if (!Directory.Exists(path))
        {
            return false;
        }

        var allowedNamesSet = allowedNames.ToHashSet();
        bool removed = false;

        foreach (var filepath in Directory.GetFiles(path, searchPattern))
        {
            var name = Path.GetFileName(filepath);
            if (!allowedNamesSet.Contains(name))
            {
                _logger.LogInformation("[{Id}] Removing obsolete theme: {Theme}", series.Id, filepath);
                File.Delete(filepath);
                removed = true;
            }
        }

        return removed;
    }

    private async ValueTask<bool> Download(MediaType type, string url, BaseItem item, string relativePath, double volume = 1.0, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(item.ContainingFolderPath, relativePath);
        if (File.Exists(path))
        {
            // Nothing to do
            return false;
        }

        var tempFile = Path.GetTempFileName();

        // ffmpeg writes to a temporary name first, so an interrupted run never leaves a truncated theme behind
        // (which the next run would treat as "already downloaded").
        var partialPath = path + ".part";
        try
        {
            // Make sure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            _logger.LogInformation("[{Id}] Downloading {Url} to {Path}", item.Id, url, path);
            var downloadStream = await _client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
            await using (downloadStream.ConfigureAwait(false))
            {
                var fileStream = File.Create(tempFile);
                await using (fileStream.ConfigureAwait(false))
                {
                    await downloadStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }
            }

            // Convert OGG
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,

                    // Must consume both or ffmpeg may hang due to deadlocks.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = _mediaEncoder.EncoderPath,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false,
                    ArgumentList = { "-y", "-nostdin", "-loglevel", "error", "-i", tempFile }
                },
                EnableRaisingEvents = true
            };

            // Update arguments
            var arguments = process.StartInfo.ArgumentList;
            if (type == MediaType.Video)
            {
                // Copy video stream
                arguments.Add("-c:v");
                arguments.Add("copy");
            }

            if (volume < 0.01 && type == MediaType.Video)
            {
                // Mute videos when volume is low enough
                arguments.Add("-an");
            }
            else
            {
                arguments.Add("-filter:a");
                arguments.Add(string.Create(CultureInfo.InvariantCulture, $"volume={volume:0.00}"));
            }

            // The container can't be guessed from the ".part" extension.
            arguments.Add("-f");
            arguments.Add(type == MediaType.Audio ? "mp3" : "webm");
            arguments.Add(partialPath);

            process.Start();

            try
            {
                var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);
                await outputTask.ConfigureAwait(false);

                if (process.ExitCode != 0)
                {
                    var commandInfo = $"Command line: {process.StartInfo.FileName} {string.Join(" ", arguments)}";
                    throw new ConversionException(process.ExitCode, commandInfo + "\n" + error);
                }
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }

                throw;
            }

            File.Move(partialPath, path, true);
            _logger.LogInformation("[{Id}] Successfully downloaded theme song!", item.Id);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Download failed");
            return false;
        }
        finally
        {
            File.Delete(tempFile);
            File.Delete(partialPath);
        }

        return true;
    }

    private static bool IsSatisfied(BaseItem item, PluginConfiguration configuration)
    {
        var settings = item.GetBaseItemKind() == BaseItemKind.Movie
            ? configuration.MovieSettings
            : new CollectionTypeConfiguration { AudioSettings = configuration.AudioSettings, VideoSettings = configuration.VideoSettings };

        var audioSatisfied = item.GetThemeSongs().Any() || settings.AudioSettings.FetchType == FetchType.None;
        var videoSatisfied = item.GetThemeVideos().Any() || settings.VideoSettings.FetchType == FetchType.None;

        return audioSatisfied && videoSatisfied;
    }

    private static bool HasVideos(Anime anime)
    {
        return anime.Themes.Any(theme => theme.Entries.Any(entry => entry.Videos.Count > 0));
    }

    private static bool IsSupportedKind(BaseItem item)
    {
        var kind = item.GetBaseItemKind();
        return kind == BaseItemKind.Series || kind == BaseItemKind.Movie;
    }

    private static bool TryGetProviderId(BaseItem item, string providerKey, out int id)
    {
        id = -1;
        return item.TryGetProviderId(providerKey, out var idAsString)
               && int.TryParse(idAsString, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
    }

    private static IEnumerable<(string Site, int Id)> GetSiteIds(BaseItem item)
    {
        foreach (var (providerKey, site) in SupportedProviders)
        {
            if (TryGetProviderId(item, providerKey, out var id))
            {
                yield return (site, id);
            }
        }
    }

    /// <summary>
    /// Gets the lookups to try for an item, most preferred first.
    /// </summary>
    private List<(string Site, int Id)> GetCandidates(BaseItem item, PluginConfiguration configuration)
    {
        var candidates = new List<(string Site, int Id)>();

        if (item.GetBaseItemKind() == BaseItemKind.Series && configuration.ThemeSource == ThemeSource.LatestSeason)
        {
            var seasons = _libraryManager.GetItemList(new InternalItemsQuery
            {
                ParentId = item.Id,
                IncludeItemTypes = [BaseItemKind.Season],
                IsVirtualItem = false,
            });

            // Newest season first. Specials (index 0) are not real seasons.
            foreach (var season in seasons.Where(it => it.IndexNumber > 0).OrderByDescending(it => it.IndexNumber))
            {
                candidates.AddRange(GetSiteIds(season));
            }
        }

        candidates.AddRange(GetSiteIds(item));
        return candidates.Distinct().ToList();
    }

    /// <summary>
    /// Gets themes roughly sorted by relevance and filtered as needed.
    /// </summary>
    /// <param name="anime">The anime in question.</param>
    /// <param name="settings">The configuration that sets the rules.</param>
    /// <returns>A filtered and sorted and flattened enumerable of themes.</returns>
    private IEnumerable<FlattenedTheme> GetBestThemes(Anime anime, MediaTypeConfiguration settings)
    {
        return anime.Themes.SelectMany(theme => theme.Entries.SelectMany(entry => entry.Videos.Select(video => Wrap(theme, entry, video))))
            .OrderBy(Rate)
            .Where(it => !settings.IgnoreOverlapping || it.Video.Overlap == OverlapType.None)
            .Where(it => !settings.IgnoreThemesWithCredits || it.Video.Creditless)
            .Where(it => !settings.IgnoreEDs || it.Theme.Type != ThemeType.ED)
            .Where(it => !settings.IgnoreOPs || it.Theme.Type != ThemeType.OP);
    }

    private FlattenedTheme Wrap(AnimeTheme theme, AnimeThemeEntry entry, Video video)
    {
        return new FlattenedTheme(theme, entry, video, video.Audio);
    }

    private double Rate(FlattenedTheme theme)
    {
        double score = 0;

        if (theme.Entry.Nsfw)
        {
            score += 10;
        }

        if (theme.Entry.Spoiler)
        {
            // Huge penalty for spoilers
            score += 50;
        }

        // Big penalties for overlap
        switch (theme.Video.Overlap)
        {
            case OverlapType.Over:
                score += 20;
                break;
            case OverlapType.Transition:
                score += 15;
                break;
        }

        // Small penalties for source
        switch (theme.Video.Source)
        {
            case VideoSource.LD:
            case VideoSource.VHS:
                score += 10;
                break;
            case VideoSource.WEB:
            case VideoSource.RAW:
                score += 5;
                break;
        }

        // Medium penalty for credits
        if (!theme.Video.Creditless)
        {
            score += 10;
        }

        return score;
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    /// <param name="disposing">Whether we're disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _api.Dispose();
            _client.Dispose();
        }
    }

    /// <summary>
    /// Dispose.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
