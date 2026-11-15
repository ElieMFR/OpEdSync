namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// An audio API resource represents the audio track of a video.
///
/// For example, the audio Bakemonogatari-OP1.ogg represents the audio track of the Bakemonogatari-OP1.webm video.
/// </summary>
/// <param name="Filename">The filename of the file without extension.</param>
/// <param name="Link">The URL to stream the file from storage.</param>
public record Audio(
    string Filename,
    string Link
);
