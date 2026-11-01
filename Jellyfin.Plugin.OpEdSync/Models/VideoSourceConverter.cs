namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Reads <see cref="VideoSource"/>; an unknown source gets the small penalty of <see cref="VideoSource.WEB"/>.
/// </summary>
public sealed class VideoSourceConverter() : LenientEnumConverter<VideoSource>(VideoSource.WEB);
