# AnimeThemes for Jellyfin

Plugin for [Jellyfin](https://jellyfin.org/) that fetches anime themes (OPs and EDs)
from [animethemes.moe](https://animethemes.moe). Both audio files and video files are supported.

**Themes are matched through the AniDB, MyAnimeList or AniList ID of the item (in that order of preference), so install at least one of the corresponding metadata plugins. Items without any of these IDs are ignored.**

Compatible with Jellyfin 10.11 (plugin v6) and Jellyfin 12.x (plugin v7+).

## How to use

The plugin creates a new scheduled task that you can configure. By default, it will run every 6 hours.
Also make sure to enable theme music / videos in your display settings.

## How to install

### Plugin Manager

1. Add the following repository to your plugin repositories:
   https://raw.githubusercontent.com/EusthEnoptEron/jellyfin-plugin-animethemes/main/manifest.json
2. Find the plugin in the catalog and install it.

### Manually

1. [Grab the latest build.](https://github.com/EusthEnoptEron/jellyfin-plugin-animethemes/actions/workflows/build.yaml)
2. Download it into your plugins folder [as described here](https://jellyfin.org/docs/general/server/plugins/).

## Configuration

| Configuration                           | Description                                                                                               |
|-----------------------------------------|-----------------------------------------------------------------------------------------------------------|
| Degree of parallelism                   | How many items should be downloaded in parallel. Default is set to 1 to go easy on the servers.           |
| Ignore themes that overlap with episode | Some shows transition into their themes. Use this setting to filter those themes out.                     |
| Ignore themes with credits              | Whether to skip themes with credits in them. Useful for video downloads.                                  |
| Ignore OP themes                        | Use this to control whether OPs are considered.                                                           |
| Ignore ED themes                        | Use this to control whether EDs are considered.                                                           |
| Fetch type                              | Determines if you want all distinct themes, only the best match, or none at all.                          |
| Volume                                  | The desired volume, which will be baked in. For videos, 0 will result in the audio channel being removed. |

## Other notes

If you want the videos to cover the whole screen, try the following CSS:

```css
body:not(.hide-scroll) > .videoPlayerContainer > video {
    object-fit: cover;
}
```
