using System.IO;
using Jellyfin.Plugin.OpEdSync.Models;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// ffmpeg reads a file given by a remote server: it must only be allowed to open the local input file.
public class FfmpegArgumentsTests
{
    private static readonly string Input = Path.Combine(Path.GetTempPath(), "tmpABC.tmp");
    private static readonly string Output = Path.Combine(Path.GetTempPath(), "Show-OP1__50.mp3.part");

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(true, 0.5)]
    [InlineData(true, 0.0)]
    public void ProtocolWhitelist_IsFileOnly_AndComesBeforeTheInput(bool video, double volume)
    {
        var arguments = AnimeThemesDownloader.BuildFfmpegArguments(video ? MediaType.Video : MediaType.Audio, Input, volume, Output);

        var whitelist = arguments.IndexOf("-protocol_whitelist");
        var input = arguments.IndexOf("-i");
        Assert.True(whitelist >= 0, "-protocol_whitelist is missing");
        Assert.Equal("file", arguments[whitelist + 1]);
        Assert.True(whitelist < input, "-protocol_whitelist must come before -i");
        Assert.Equal(whitelist + 2, input);
    }

    [Theory]
    [InlineData(false, 0.5)]
    [InlineData(true, 0.5)]
    [InlineData(true, 0.0)]
    public void TheInput_IsTheLocalFile_AndOnlyOneInputIsGiven(bool video, double volume)
    {
        var arguments = AnimeThemesDownloader.BuildFfmpegArguments(video ? MediaType.Video : MediaType.Audio, Input, volume, Output);

        Assert.Single(arguments, "-i");
        var value = arguments[arguments.IndexOf("-i") + 1];
        Assert.Equal(Input, value);
        Assert.True(Path.IsPathFullyQualified(value));
        Assert.False(value.Contains("://", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Audio_ConvertsToMp3_WithTheVolume()
    {
        var arguments = AnimeThemesDownloader.BuildFfmpegArguments(MediaType.Audio, Input, 0.5, Output);

        Assert.Equal(["-y", "-nostdin", "-loglevel", "error", "-protocol_whitelist", "file", "-i", Input, "-filter:a", "volume=0.50", "-f", "mp3", Output], arguments);
    }

    [Fact]
    public void Video_CopiesTheVideoStream_AndAppliesTheVolume()
    {
        var arguments = AnimeThemesDownloader.BuildFfmpegArguments(MediaType.Video, Input, 0.8, Output);

        Assert.Equal(["-y", "-nostdin", "-loglevel", "error", "-protocol_whitelist", "file", "-i", Input, "-c:v", "copy", "-filter:a", "volume=0.80", "-f", "webm", Output], arguments);
    }

    [Fact]
    public void MutedVideo_DropsTheAudio()
    {
        var arguments = AnimeThemesDownloader.BuildFfmpegArguments(MediaType.Video, Input, 0.0, Output);

        Assert.Equal(["-y", "-nostdin", "-loglevel", "error", "-protocol_whitelist", "file", "-i", Input, "-c:v", "copy", "-an", "-f", "webm", Output], arguments);
    }
}
