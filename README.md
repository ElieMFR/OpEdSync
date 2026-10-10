# OpEdSync

Plugin [Jellyfin](https://jellyfin.org/) qui télécharge automatiquement les génériques d'animes (openings et endings) depuis [animethemes.moe](https://animethemes.moe). Les fichiers audio et vidéo sont pris en charge.

Compatible **Jellyfin 12.x**. Développé par ElieM, basé sur [jellyfin-plugin-animethemes](https://github.com/EusthEnoptEron/jellyfin-plugin-animethemes) d'EusthEnoptEron.

## Fonctionnement

Le plugin cherche, pour chaque série ou film de ta bibliothèque, l'identifiant **AniDB**, **MyAnimeList** ou **AniList** (dans cet ordre de préférence). Il interroge ensuite AnimeThemes et enregistre les thèmes à côté du média. Les éléments sans aucun de ces identifiants sont ignorés.

Il te faut donc au moins un plugin de métadonnées qui fournit l'un de ces identifiants (AniDB, MyAnimeList ou AniList).

## Installation

### Depuis le catalogue Jellyfin

Ajoute ce dépôt de plugins (Tableau de bord → Plugins → Dépôts) puis installe **OpEdSync** :

```
https://gitlab.bmeouchi.fr/Elie/OpEdSync/raw/branch/main/manifest.json
```

### À la main

1. Télécharge `dist/opedsync_1.0.0.0.zip` depuis le dépôt.
2. Arrête Jellyfin.
3. Crée un dossier `OpEdSync_1.0.0.0` dans le dossier `plugins` du serveur :
   - Windows : `C:\ProgramData\Jellyfin\Server\plugins\`
   - Docker : `/config/plugins/`
   - Linux : `/var/lib/jellyfin/plugins/`
4. Place-y `Jellyfin.Plugin.OpEdSync.dll`, puis relance Jellyfin.

OpEdSync est un plugin à part entière : il s'installe à côté de l'ancien plugin AnimeThemes, mais les deux téléchargeraient les mêmes fichiers. Désactive ou désinstalle l'ancien.

## Utilisation

1. Ouvre **Tableau de bord → Tâches planifiées** et lance « Search For Anime Themes ». Elle s'exécute ensuite toute seule toutes les 12 heures.
2. Active la musique de thème (et les vidéos de thème si besoin) dans les réglages d'affichage de ton profil.

## Configuration

| Réglage | Description |
|---|---|
| Degré de parallélisme | Nombre de téléchargements simultanés. 1 par défaut, pour ménager le serveur d'AnimeThemes. |
| Ignorer les thèmes qui chevauchent l'épisode | Certaines séries enchaînent directement avec leur thème. |
| Ignorer les thèmes avec crédits | Utile surtout pour les vidéos. |
| Ignorer les OP / les ED | Choisis quels types de thèmes garder. |
| Type de récupération | Tous les thèmes, seulement le meilleur, ou aucun. |
| Volume | Volume intégré au fichier. À 0, la piste audio des vidéos est supprimée. |
| Bibliothèques incluses / exclues | Expressions régulières sur le nom des bibliothèques. |

## Astuce d'affichage

Pour que les vidéos de thème remplissent tout l'écran, ajoute ce CSS personnalisé :

```css
body:not(.hide-scroll) > .videoPlayerContainer > video {
    object-fit: cover;
}
```

## Compilation

Nécessite le SDK .NET 10.

```bash
dotnet build Jellyfin.Plugin.OpEdSync -c Release
```

Le fichier `Jellyfin.Plugin.OpEdSync.dll` se trouve ensuite dans `bin/Release/net10.0/`.

## Licence

GPL-3.0, comme le projet d'origine. Voir [LICENSE](LICENSE).
