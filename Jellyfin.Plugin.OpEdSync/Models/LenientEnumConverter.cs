using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Reads an enum by name like <see cref="JsonStringEnumConverter"/>, but turns a value added later by AnimeThemes
/// into a fallback instead of failing the whole response.
/// </summary>
/// <typeparam name="T">Enum type.</typeparam>
public abstract class LenientEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    private readonly T _fallback;

    /// <summary>
    /// Initializes a new instance of the <see cref="LenientEnumConverter{T}"/> class.
    /// </summary>
    /// <param name="fallback">Value used for an unknown name.</param>
    protected LenientEnumConverter(T fallback)
    {
        _fallback = fallback;
    }

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && Enum.TryParse<T>(reader.GetString(), true, out var value)
            && Enum.IsDefined(value)
            && !char.IsDigit(reader.GetString()![0]))
        {
            return value;
        }

        reader.Skip();
        return _fallback;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToString());
    }
}
