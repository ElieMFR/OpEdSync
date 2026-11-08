namespace Jellyfin.Plugin.OpEdSync.Models;

/// <summary>
/// Represents a resource. Subset of <a href="https://api-docs.animethemes.moe/resource">resource</a>.
///
/// For site types, see <see cref="Sites"/>.
/// </summary>
public record Resource(
    int Id,
    int ExternalId,
    string Site
);
