using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// The settings file of a server that already runs the plugin must be read back identically after internal clean-ups.
public class SavedSettingsTests
{
    private const string Saved = """
        <?xml version="1.0"?>
        <PluginConfiguration>
          <DegreeOfParallelism>3</DegreeOfParallelism>
          <UseLibraryList>true</UseLibraryList>
          <LibraryIds><string>aaa</string><string>bbb</string></LibraryIds>
          <CheckYear>false</CheckYear>
          <ThemeSource>LatestSeason</ThemeSource>
          <ForceSync>true</ForceSync>
          <IncludeLibraries>Anime</IncludeLibraries>
          <ExcludeLibraries>Kids</ExcludeLibraries>
          <AnimeThemesBaseUrl>https://example.test/graphql</AnimeThemesBaseUrl>
          <AudioSettings>
            <FetchType>All</FetchType>
            <IgnoreOverlapping>false</IgnoreOverlapping>
            <AllowTransitions>false</AllowTransitions>
            <IgnoreEDs>true</IgnoreEDs>
            <IgnoreOPs>false</IgnoreOPs>
            <IgnoreThemesWithCredits>true</IgnoreThemesWithCredits>
            <Choice>First</Choice>
            <Volume>0.8</Volume>
          </AudioSettings>
          <VideoSettings>
            <FetchType>Single</FetchType>
            <IgnoreOverlapping>true</IgnoreOverlapping>
            <AllowTransitions>true</AllowTransitions>
            <IgnoreEDs>false</IgnoreEDs>
            <IgnoreOPs>true</IgnoreOPs>
            <IgnoreThemesWithCredits>false</IgnoreThemesWithCredits>
            <Choice>Latest</Choice>
            <Volume>0.25</Volume>
          </VideoSettings>
          <MovieSettings><AudioSettings><FetchType>None</FetchType></AudioSettings></MovieSettings>
        </PluginConfiguration>
        """;

    [Fact]
    public void SavedSettings_AreReadBackIdentically()
    {
        var configuration = (PluginConfiguration)new XmlSerializer(typeof(PluginConfiguration)).Deserialize(new StringReader(Saved))!;

        Assert.Equal(3, configuration.DegreeOfParallelism);
        Assert.True(configuration.UseLibraryList);
        Assert.Equal(["aaa", "bbb"], configuration.LibraryIds);
        Assert.False(configuration.CheckYear);
        Assert.Equal(ThemeSource.LatestSeason, configuration.ThemeSource);
        Assert.True(configuration.ForceSync);
        Assert.Equal("Anime", configuration.IncludeLibraries);
        Assert.Equal("Kids", configuration.ExcludeLibraries);
        Assert.Equal("https://example.test/graphql", configuration.AnimeThemesBaseUrl);

        var audio = configuration.AudioSettings;
        Assert.Equal(FetchType.All, audio.FetchType);
        Assert.False(audio.IgnoreOverlapping);
        Assert.False(audio.AllowTransitions);
        Assert.True(audio.IgnoreEDs);
        Assert.False(audio.IgnoreOPs);
        Assert.True(audio.IgnoreThemesWithCredits);
        Assert.Equal(ThemeChoice.First, audio.Choice);
        Assert.Equal(0.8, audio.Volume);

        var video = configuration.VideoSettings;
        Assert.Equal(FetchType.Single, video.FetchType);
        Assert.True(video.IgnoreOverlapping);
        Assert.True(video.AllowTransitions);
        Assert.False(video.IgnoreEDs);
        Assert.True(video.IgnoreOPs);
        Assert.False(video.IgnoreThemesWithCredits);
        Assert.Equal(ThemeChoice.Latest, video.Choice);
        Assert.Equal(0.25, video.Volume);
    }

    [Fact]
    public void SavedSettings_WrittenBack_KeepTheSameElements()
    {
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        var configuration = (PluginConfiguration)serializer.Deserialize(new StringReader(Saved))!;

        using var writer = new StringWriter();
        serializer.Serialize(writer, configuration);
        var xml = writer.ToString();

        Assert.Contains("<AudioSettings>", xml, System.StringComparison.Ordinal);
        Assert.Contains("<VideoSettings>", xml, System.StringComparison.Ordinal);
        Assert.DoesNotContain("CollectionType", xml, System.StringComparison.Ordinal);

        var again = (PluginConfiguration)serializer.Deserialize(new StringReader(xml))!;
        Assert.Equal(configuration.AudioSettings.Volume, again.AudioSettings.Volume);
        Assert.Equal(configuration.VideoSettings.FetchType, again.VideoSettings.FetchType);
    }
}
