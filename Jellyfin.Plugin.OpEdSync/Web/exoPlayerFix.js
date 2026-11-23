// Appli Jellyfin Android, lecteur intégré (ExoPlayer) : impossible de relancer une vidéo
// après en avoir lu une qui avait coupé la musique de thème.
// En coupant la musique, jellyfin-web garde son lecteur audio comme lecteur actif, mais vide
// ses informations. ExoPlayer, lui, n'est jamais déclaré actif. Au lancement suivant,
// jellyfin-web lit l'élément en cours de ce lecteur fantôme (null), plante, et l'erreur est
// avalée sans message. On retire donc ce lecteur fantôme quand ExoPlayer démarre.
// Ne fait rien hors de l'appli Android : window.ExoPlayer n'y existe pas.
(function () {
    'use strict';

    // Retour de la vidéo : jellyfin-web ne relance le thème d'une fiche que si le propriétaire du thème a changé,
    // et ExoPlayer (natif) ne renvoie aucun événement à la page. Avant de passer au lecteur natif, un « viewshow »
    // sans paramètres fait oublier à jellyfin-web le propriétaire du thème. Au retour de l'appli, la page rejoue
    // le « viewshow » de la fiche, ce qui relance le thème.
    // Le signal de retour (blur/focus, visibilitychange) est une hypothèse : pas encore vérifié sur appareil.
    let lastParams = null;
    let returnArmed = false;

    document.addEventListener('viewshow', function (e) {
        const params = e.detail && e.detail.params;
        if (e.opEdSyncSynthetic) {
            return;
        }

        lastParams = params && params.serverId && params.id ? { serverId: params.serverId, id: params.id } : null;
    }, true);

    function dispatchViewShow(params) {
        const event = new CustomEvent('viewshow', { bubbles: true, detail: { params: params, options: {} } });
        event.opEdSyncSynthetic = true;
        document.dispatchEvent(event);
    }

    function armReturn() {
        if (returnArmed || !lastParams) {
            return;
        }

        returnArmed = true;
        const params = lastParams;
        let left = false;

        const leave = function () {
            if (document.visibilityState === 'hidden' || !document.hasFocus()) {
                left = true;
            }
        };

        const back = function () {
            if (!left || document.visibilityState === 'hidden') {
                return;
            }

            window.removeEventListener('blur', leave);
            window.removeEventListener('focus', back);
            document.removeEventListener('visibilitychange', leave);
            document.removeEventListener('visibilitychange', back);
            returnArmed = false;
            // La page est déjà revenue au premier plan ; laisse jellyfin-web finir de la réafficher.
            setTimeout(function () {
                console.info('OpEdSync: back from ExoPlayer, theme restarted');
                dispatchViewShow(lastParams || params);
            }, 300);
        };

        window.addEventListener('blur', leave);
        window.addEventListener('focus', back);
        document.addEventListener('visibilitychange', leave);
        document.addEventListener('visibilitychange', back);
    }

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
                    console.info('OpEdSync: stale player removed before ExoPlayer (' + stale.name + ')');
                    playbackManager._currentPlayer = null;
                }
            } catch (e) {
                console.warn('OpEdSync: ExoPlayer fix skipped', e);
            }

            try {
                armReturn();
                dispatchViewShow({});
            } catch (e) {
                console.warn('OpEdSync: theme restart after ExoPlayer skipped', e);
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
        console.warn('OpEdSync: cannot watch window.ExoPlayer', e);
    }
})();
