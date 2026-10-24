using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OpEdSync.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        // set default options here
        DegreeOfParallelism = 1;
        ForceSync = false;
        ThemeSource = ThemeSource.LatestSeason;
        CheckYear = true;
        // New installs only look at libraries whose name contains "anim" (Animés, Anime, Animation...).
        IncludeLibraries = "anim";
        ExcludeLibraries = string.Empty;

        AudioSettings = new MediaTypeConfiguration()
        {
            FetchType = FetchType.Single,
            IgnoreOverlapping = true,
            IgnoreThemesWithCredits = false,
            IgnoreOPs = false,
            IgnoreEDs = false,
            Volume = 0.5,
        };

        VideoSettings = new MediaTypeConfiguration()
        {
            FetchType = FetchType.None,
            IgnoreOverlapping = true,
            IgnoreThemesWithCredits = true,
            IgnoreOPs = false,
            IgnoreEDs = false,
            Volume = 0.0,
        };

        MovieSettings = new CollectionTypeConfiguration();
        LibraryIds = [];
    }

    /// <summary>
    /// Gets or sets how many items are processed at the same time. Clamped between 1 and 8 when used.
    /// </summary>
    public int DegreeOfParallelism { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="LibraryIds"/> is used instead of the regular expressions.
    /// Set by the configuration page; older settings keep using <see cref="IncludeLibraries"/>.
    /// </summary>
    public bool UseLibraryList { get; set; }

    /// <summary>
    /// Gets or sets the IDs of the libraries to work on, when <see cref="UseLibraryList"/> is set.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Serialized to XML.")]
    public string[] LibraryIds { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether movies use <see cref="MovieSettings"/> instead of the series settings.
    /// </summary>
    public bool SeparateMovieSettings { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an anime is only accepted when its year is close to the item's.
    /// </summary>
    public bool CheckYear { get; set; }

    /// <summary>
    /// Gets or sets which season of a series is used to look up themes.
    /// </summary>
    public ThemeSource ThemeSource { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sync should enforce conformity.
    /// </summary>
    public bool ForceSync { get; set; }

    /// <summary>
    /// Gets or sets a regex for libraries to consider.
    /// </summary>
    public string IncludeLibraries { get; set; }

    /// <summary>
    /// Gets or sets a regex for libraries to exclude from consideration.
    /// </summary>
    public string ExcludeLibraries { get; set; }

    /// <summary>
    /// Gets or sets the audio settings.
    /// </summary>
    public MediaTypeConfiguration AudioSettings { get; set; }

    /// <summary>
    /// Gets or sets the video settings.
    /// </summary>
    public MediaTypeConfiguration VideoSettings { get; set; }

    /// <summary>
    /// Gets or sets the download settings for the movie type.
    /// </summary>
    public CollectionTypeConfiguration MovieSettings { get; set; }
}
