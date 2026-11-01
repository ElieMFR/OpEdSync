using System;
using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// The test bench points the plugin at a fake AnimeThemes; users keep the public API.
public class AnimeThemesAddressTests
{
    [Fact]
    public void Default_IsThePublicApi()
    {
        var configuration = new PluginConfiguration();

        Assert.Equal(new Uri("https://api.animethemes.moe"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configuration.AnimeThemesBaseUrl));
    }

    [Fact]
    public void OlderSettings_WithoutTheField_KeepThePublicApi()
    {
        const string xml = "<?xml version=\"1.0\"?><PluginConfiguration><DegreeOfParallelism>2</DegreeOfParallelism></PluginConfiguration>";
        var configuration = (PluginConfiguration)new XmlSerializer(typeof(PluginConfiguration)).Deserialize(new StringReader(xml))!;

        Assert.Equal(2, configuration.DegreeOfParallelism);
        Assert.Equal(new Uri("https://api.animethemes.moe"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configuration.AnimeThemesBaseUrl));
    }

    [Fact]
    public void ConfiguredAddress_IsUsed()
    {
        Assert.Equal(new Uri("http://127.0.0.1:8197"), PluginConfiguration.ResolveAnimeThemesBaseUrl(" http://127.0.0.1:8197 "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("api.animethemes.moe")]
    [InlineData("/anime")]
    [InlineData("file:///C:/x")]
    [InlineData("ftp://example.test")]
    public void UnusableAddress_FallsBackToThePublicApi(string? configured)
    {
        Assert.Equal(new Uri("https://api.animethemes.moe"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configured));
    }
}
