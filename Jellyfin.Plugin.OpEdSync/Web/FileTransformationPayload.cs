namespace Jellyfin.Plugin.OpEdSync.Web;

/// <summary>
/// Payload passed by the File Transformation plugin to the callback.
/// </summary>
public sealed class FileTransformationPayload
{
    /// <summary>
    /// Gets or sets the contents of the intercepted file.
    /// </summary>
    public string Contents { get; set; } = string.Empty;
}
