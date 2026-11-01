namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Reads <see cref="OverlapType"/>; an unknown overlap counts as <see cref="OverlapType.Over"/>, the most cautious value.
/// </summary>
public sealed class OverlapTypeConverter() : LenientEnumConverter<OverlapType>(OverlapType.Over);
