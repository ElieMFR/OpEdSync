using Jellyfin.Plugin.OpEdSync.Configuration;
using Xunit;

namespace Jellyfin.Plugin.OpEdSync.Tests;

// The decision "what to delete, and when" taken at the end of an item (EXG-026, EXG-028, EXG-029).
public class ObsoleteFilesTests
{
    private static readonly string[] OldFiles = ["Show-OP1__50.mp3", "Show-ED1__50.mp3", "theme.mp3"];

    [Fact]
    public void IncompleteSelection_DeletesNothing_EvenWithForceSyncAndAnotherVolume()
    {
        // A download failed: the selection is not all on disk, the old themes stay.
        var obsolete = ThemeFileRules.ObsoleteFiles(OldFiles, ["Show-OP2__80.mp3"], selectionOnDisk: false, forceSync: true, FetchType.Single, 0.8);

        Assert.Empty(obsolete);
    }

    [Fact]
    public void CompleteSelection_WithForceSync_DeletesPluginFilesOutsideIt()
    {
        string[] existing = ["Show-OP1__50.mp3", "Show-ED1__50.mp3", "Show-OP2__50.mp3"];

        var obsolete = ThemeFileRules.ObsoleteFiles(existing, ["Show-OP2__50.mp3"], selectionOnDisk: true, forceSync: true, FetchType.Single, 0.5);

        Assert.Equal(["Show-OP1__50.mp3", "Show-ED1__50.mp3"], obsolete);
    }

    [Fact]
    public void CompleteSelection_WithoutForceSync_KeepsOtherThemesAtTheSameVolume()
    {
        string[] existing = ["Show-OP1__50.mp3", "Show-OP2__50.mp3"];

        var obsolete = ThemeFileRules.ObsoleteFiles(existing, ["Show-OP2__50.mp3"], selectionOnDisk: true, forceSync: false, FetchType.Single, 0.5);

        Assert.Empty(obsolete);
    }

    [Theory]
    [InlineData(true, FetchType.Single)]
    [InlineData(false, FetchType.Single)]
    [InlineData(true, FetchType.All)]
    [InlineData(true, FetchType.None)]
    public void AUserFile_IsNeverDeleted(bool forceSync, FetchType fetchType)
    {
        string[] existing = ["theme.mp3", "my song.mp3", "Show-OP1__50.mp3"];
        string[] selected = fetchType == FetchType.None ? [] : ["Show-OP2__80.mp3"];

        var obsolete = ThemeFileRules.ObsoleteFiles(existing, selected, selectionOnDisk: selected.Length > 0, forceSync, fetchType, 0.8);

        Assert.DoesNotContain("theme.mp3", obsolete);
        Assert.DoesNotContain("my song.mp3", obsolete);
    }

    [Fact]
    public void ChangedVolume_DeletesTheOtherVolumeOnlyOnceTheWholeSelectionIsOnDisk()
    {
        string[] existing = ["Show-OP1__50.mp3", "Show-ED1__50.mp3"];
        string[] selected = ["Show-OP1__80.mp3", "Show-ED1__80.mp3"];

        Assert.Empty(ThemeFileRules.ObsoleteFiles(existing, selected, selectionOnDisk: false, forceSync: false, FetchType.All, 0.8));

        var done = ThemeFileRules.ObsoleteFiles([.. existing, .. selected], selected, selectionOnDisk: true, forceSync: false, FetchType.All, 0.8);
        Assert.Equal(["Show-OP1__50.mp3", "Show-ED1__50.mp3"], done);
    }

    [Fact]
    public void ChangedVolume_WithForceSync_ListsEachFileOnce()
    {
        string[] existing = ["Show-OP1__50.mp3", "Show-OP2__80.mp3"];

        var obsolete = ThemeFileRules.ObsoleteFiles(existing, ["Show-OP2__80.mp3"], selectionOnDisk: true, forceSync: true, FetchType.Single, 0.8);

        Assert.Equal(["Show-OP1__50.mp3"], obsolete);
    }

    [Fact]
    public void None_WithForceSync_ClearsThePluginFilesButNotTheUserOnes()
    {
        var obsolete = ThemeFileRules.ObsoleteFiles(OldFiles, [], selectionOnDisk: false, forceSync: true, FetchType.None, 0.5);

        Assert.Equal(["Show-OP1__50.mp3", "Show-ED1__50.mp3"], obsolete);
    }

    [Fact]
    public void None_WithoutForceSync_DeletesNothing()
    {
        Assert.Empty(ThemeFileRules.ObsoleteFiles(OldFiles, [], selectionOnDisk: false, forceSync: false, FetchType.None, 0.5));
    }

    [Fact]
    public void NoThemeFound_WithForceSync_KeepsWhatIsThere()
    {
        // The show has no matching theme: the selection is empty but the choice is not "None".
        Assert.Empty(ThemeFileRules.ObsoleteFiles(OldFiles, [], selectionOnDisk: false, forceSync: true, FetchType.Single, 0.5));
    }

    [Theory]
    [InlineData(FetchType.None, 0, false, true)]
    [InlineData(FetchType.None, 1, true, false)]
    [InlineData(FetchType.Single, 0, false, false)]
    [InlineData(FetchType.Single, 1, true, true)]
    [InlineData(FetchType.All, 2, false, false)]
    public void ReplacementsReady_FollowsTheSelection(FetchType fetchType, int count, bool onDisk, bool expected)
    {
        Assert.Equal(expected, ThemeFileRules.ReplacementsReady(fetchType, count, onDisk));
    }
}
