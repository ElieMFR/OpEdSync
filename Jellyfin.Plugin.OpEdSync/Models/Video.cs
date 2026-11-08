using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// A video API resource represents a WebM of an anime theme.
///
/// For example, the video Bakemonogatari-OP1.webm represents the WebM of the Bakemonogatari OP1 theme.
/// </summary>
/// <param name="Id">The primary key of the resource.</param>
/// <param name="Basename">The basename of the file in storage.</param>
/// <param name="Filename">The filename of the file in storage.</param>
/// <param name="Source">Where did this video come from? [WEB, RAW, BD, DVD, VHS, LD].</param>
/// <param name="Overlap">The degree to which the sequence and episode content overlap [None, Transition, Over].</param>
/// <param name="Link">The URL to stream the file from storage.</param>
/// <param name="Creditless">Whether the video is creditless.</param>
/// <param name="Audio">Audio for the video.</param>
public record Video(
    int Id,
    string Basename,
    string Filename,
    VideoSource? Source,
    OverlapType Overlap,
    string Link,
    bool Creditless,
    Audio Audio
);
