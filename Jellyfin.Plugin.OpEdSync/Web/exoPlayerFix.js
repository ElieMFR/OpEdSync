// Appli Jellyfin Android, lecteur intégré (ExoPlayer) : impossible de relancer une vidéo
// après en avoir lu une qui avait coupé la musique de thème.
// En coupant la musique, jellyfin-web garde son lecteur audio comme lecteur actif, mais vide
// ses informations. ExoPlayer, lui, n'est jamais déclaré actif. Au lancement suivant,
// jellyfin-web lit l'élément en cours de ce lecteur fantôme (null), plante, et l'erreur est
// avalée sans message. On retire donc ce lecteur fantôme quand ExoPlayer démarre.
// Ne fait rien hors de l'appli Android : window.ExoPlayer n'y existe pas.
(function () {
    'use strict';

    function patch(exoPlayer) {
        if (!exoPlayer || exoPlayer.opEdSyncPatched || typeof exoPlayer.play !== 'function') {
            return;
        }

        exoPlayer.opEdSyncPatched = true;
        const play = exoPlayer.play;

        exoPlayer.play = function (options) {
            try {
                const playbackManager = this.playbackManager;
                const stale = playbackManager && playbackManager._currentPlayer;

                if (stale && stale !== this && !playbackManager.currentItem(stale)) {
                    console.info('OpEdSync: lecteur inactif retiré avant ExoPlayer (' + stale.name + ')');
                    playbackManager._currentPlayer = null;
                }
            } catch (e) {
                console.warn('OpEdSync: correctif ExoPlayer ignoré', e);
            }

            return play.call(this, options);
        };
    }

    // L'appli crée window.ExoPlayer après le chargement de la page : on l'intercepte au passage.
    let current = window.ExoPlayer;
    patch(current);

    try {
        Object.defineProperty(window, 'ExoPlayer', {
            configurable: true,
            get: function () {
                return current;
            },
            set: function (value) {
                current = value;
                patch(value);
            }
        });
    } catch (e) {
        console.warn('OpEdSync: impossible de surveiller window.ExoPlayer', e);
    }
})();
