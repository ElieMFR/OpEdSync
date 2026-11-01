namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Reads <see cref="ThemeType"/>; an unknown type becomes <see cref="ThemeType.Other"/>.
/// </summary>
public sealed class ThemeTypeConverter() : LenientEnumConverter<ThemeType>(ThemeType.Other);
