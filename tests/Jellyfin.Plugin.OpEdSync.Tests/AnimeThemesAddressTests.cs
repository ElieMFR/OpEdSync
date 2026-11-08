using System;
using System.IO;
using System.Xml.Serialization;
using Jellyfin.Plugin.OpEdSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// Users keep the public GraphQL API; a test points the plugin at a fake AnimeThemes.
// Versions 1.5.3.0 and 1.5.3.1 saved the address of the retired REST API as the default: it must not stick.
public class AnimeThemesAddressTests
{
    [Fact]
    public void Default_IsThePublicApi()
    {
        var configuration = new PluginConfiguration();

        Assert.Equal(new Uri("https://rs.animethemes.moe/graphql"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configuration.AnimeThemesBaseUrl));
    }

    [Fact]
    public void OlderSettings_WithoutTheField_KeepThePublicApi()
    {
        const string xml = "<?xml version=\"1.0\"?><PluginConfiguration><DegreeOfParallelism>2</DegreeOfParallelism></PluginConfiguration>";
        var configuration = (PluginConfiguration)new XmlSerializer(typeof(PluginConfiguration)).Deserialize(new StringReader(xml))!;

        Assert.Equal(2, configuration.DegreeOfParallelism);
        Assert.Equal(new Uri("https://rs.animethemes.moe/graphql"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configuration.AnimeThemesBaseUrl));
    }

    // The XML of a server that ran 1.5.3.0 or 1.5.3.1 holds the retired address as if the user had chosen it.
    [Theory]
    [InlineData("https://api.animethemes.moe")]
    [InlineData("https://api.animethemes.moe/")]
    [InlineData("HTTPS://API.ANIMETHEMES.MOE")]
    [InlineData("https://graphql.animethemes.moe")]
    public void RetiredAddress_IsReadAsTheDefault(string saved)
    {
        var xml = $"<?xml version=\"1.0\"?><PluginConfiguration><AnimeThemesBaseUrl>{saved}</AnimeThemesBaseUrl></PluginConfiguration>";
        var configuration = (PluginConfiguration)new XmlSerializer(typeof(PluginConfiguration)).Deserialize(new StringReader(xml))!;

        Assert.Equal(saved, configuration.AnimeThemesBaseUrl);
        Assert.Equal(new Uri("https://rs.animethemes.moe/graphql"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configuration.AnimeThemesBaseUrl));
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
        Assert.Equal(new Uri("https://rs.animethemes.moe/graphql"), PluginConfiguration.ResolveAnimeThemesBaseUrl(configured));
    }
}
