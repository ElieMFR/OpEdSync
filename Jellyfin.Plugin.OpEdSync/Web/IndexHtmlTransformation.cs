using System;
using System.IO;

namespace Jellyfin.Plugin.OpEdSync.Web;

/// <summary>
/// Callback invoked by the File Transformation plugin to inline the ExoPlayer fix into index.html.
/// </summary>
public static class IndexHtmlTransformation
{
    private const string Marker = "data-opedsync=\"exoplayer-fix\"";

    private static readonly Lazy<string> ScriptTag = new(() =>
    {
        var type = typeof(IndexHtmlTransformation);
        using var stream = type.Assembly.GetManifestResourceStream(type.Namespace + ".exoPlayerFix.js")
            ?? throw new InvalidOperationException("exoPlayerFix.js resource is missing.");
        using var reader = new StreamReader(stream);
        return "<script " + Marker + ">" + reader.ReadToEnd() + "</script>";
    });

    /// <summary>
    /// Inserts the fix script before &lt;/body&gt;.
    /// </summary>
    /// <param name="payload">The file contents.</param>
    /// <returns>The modified HTML.</returns>
    public static string Transform(FileTransformationPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var contents = payload.Contents;
        if (string.IsNullOrEmpty(contents) || contents.Contains(Marker, StringComparison.Ordinal))
        {
            return contents;
        }

        var bodyIndex = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return bodyIndex < 0 ? contents : contents.Insert(bodyIndex, ScriptTag.Value);
    }
}
