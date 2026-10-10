## 1.0.0.0 — Première version OpEdSync (Jellyfin 12)

### Nouveautés
- Compatible avec **Jellyfin 12.x** (.NET 10).
- Nouveaux identifiants pris en charge : si une série ou un film n'a pas d'ID AniDB, le plugin essaie **MyAnimeList** puis **AniList**.

### Corrections
- Plus de fichier tronqué laissé sur le disque si le téléchargement ou ffmpeg échoue.
- L'annulation de la tâche est respectée et ffmpeg est arrêté proprement.
- ffmpeg ne peut plus se bloquer faute de lecture de sa sortie.
- Un identifiant mal formé ou une erreur réseau n'interrompt plus toute la tâche.
- La barre de progression atteint bien 100 %.
- Une même série présente dans plusieurs bibliothèques est désormais traitée partout.
- Les réglages « Films » sont maintenant respectés.
- Une expression régulière invalide dans la configuration est ignorée avec un message dans les logs.

### Installation
Voir le README. OpEdSync est un plugin distinct de l'ancien AnimeThemes : désactive ou désinstalle ce dernier pour éviter les doublons. Les réglages sont à refaire.
