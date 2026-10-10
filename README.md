# OpEdSync

Plugin [Jellyfin](https://jellyfin.org/) 12 qui télécharge automatiquement les génériques d'animes (openings et endings) depuis [animethemes.moe](https://animethemes.moe), en audio et en vidéo.

Les thèmes sont retrouvés grâce à l'identifiant **AniDB**, **MyAnimeList** ou **AniList** de tes séries et films : installe au moins un plugin de métadonnées qui le fournit.

Développé par ElieM, basé sur [jellyfin-plugin-animethemes](https://github.com/EusthEnoptEron/jellyfin-plugin-animethemes) (GPL-3.0).

## Installation

1. Dans Jellyfin : **Tableau de bord → Plugins → Dépôts → Nouveau dépôt**, puis ajoute :

   ```
   https://gitlab.bmeouchi.fr/Elie/OpEdSync/raw/branch/main/manifest.json
   ```

2. Ouvre le **Catalogue**, installe **OpEdSync**, puis redémarre Jellyfin.
3. Lance la tâche planifiée **Search For Anime Themes** et active la musique de thème dans les réglages de ton profil.

Si l'ancien plugin AnimeThemes est installé, désinstalle-le : les deux téléchargeraient les mêmes fichiers.
