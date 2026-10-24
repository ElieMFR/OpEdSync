# Release notes

Every GitHub release has the same shape as NotifySync's: English, then French. The text lives in `notes/<version>.md`
and becomes the release body.

## Rules

- **One short bold line per change**, saying what the user sees, never how it was done. No file, class or setting names, no code.
- **No introduction.** The notes open on the banner, then the sections.
- **Each version has its own wording.** Never copy the previous release's lines.
- Sections, in this order, only those that apply: `🛡️ Security`, `✨ New` or `✨ Improvement`, `🐛 Fixes`, `🔧 Internal: nothing changes on screen`.
- In French: `🛡️ Sécurité`, `✨ Nouveautés` or `✨ Amélioration`, `🐛 Corrections`, `🔧 Interne : rien ne change à l'écran`.
- No long dash (U+2014) in public texts.
- No semicolons in public texts: split the sentence or use a colon.
- No `v` in front of version numbers: `1.5.0.0`, not `v1.5.0.0`.

## Banner

Every release opens with:

```markdown
> 🚀 **For Jellyfin 12.0 and newer.**
> *Pour Jellyfin 12.0 et plus récent.*
```

## Title and tag

- Title: `🎵 OpEdSync X.Y.Z.W · Jellyfin 12`
- Tag: `X.Y.Z.W`, without `v`: the download links of the catalog depend on it.
- Asset: `opedsync_X.Y.Z.W.zip`, built by `tools/package.ps1` (DLL at the root of the zip). The MD5 it prints is the manifest `checksum`, and the download link is the release asset.

## Manifest `changelog`

A link to the GitHub release, like NotifySync, so Jellyfin's version history opens the full notes:

```html
<a style='color:white;text-decoration:none' href="https://github.com/ElieMFR/OpEdSync/releases/tag/X.Y.Z.W">📋 <span style='text-decoration:underline'>Release Notes X.Y.Z.W</span></a>
```

## Shape

```markdown
## 🇬🇧 English

### 🐛 Fixes

- **A series gets the theme of its latest season again.**

---

## 🇫🇷 Français

### 🐛 Corrections

- **Une série reçoit de nouveau le thème de sa dernière saison.**
```
