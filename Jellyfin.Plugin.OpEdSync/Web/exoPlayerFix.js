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
    // sans paramètres fait oublier à jellyfin-web le propriétaire du thème. Quand le lecteur se ferme, la page rejoue
    // le « viewshow » de la fiche, ce qui relance le thème.
    // Le lecteur natif est un fragment de la même activité : la page ne reçoit ni blur, ni focus, ni visibilitychange
    // (relevé sur émulateur Android 14 et sur Pixel 10 Pro, appli 2.7.3). Deux signaux :
    // - le serveur : l'appli signale sa lecture sous le même appareil ; elle cesse à la fermeture du lecteur ;
    // - « resize » : les barres système se masquent à l'ouverture du lecteur et reviennent à sa fermeture, mais pas
    //   toujours (selon la vidéo). Quand le serveur a vu la lecture, il confirme ou infirme ce signal.
    // Blur/focus et visibilitychange sont gardés au cas où un autre appareil les enverrait.
    // Entre deux épisodes, l'appli signale l'arrêt du premier avant d'avoir chargé le suivant : un arrêt vu par le
    // serveur ne prouve pas que le lecteur est fermé. Il est cru tout de suite si la fenêtre, redimensionnée par le
    // lecteur, a repris sa taille. Sinon il doit durer, plus longtemps tant que la fenêtre garde la taille du lecteur.
    const POLL_MS = 3000;
    const QUIET_SAME_SIZE_MS = 20000;
    const QUIET_RESIZED_MS = 60000;
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

    // Le nom d'appareil de la page est encodé (« Pixel%2B10%2BPro »), celui de l'appli native ne l'est pas.
    function deviceKey(name) {
        let text = String(name || '');
        try {
            text = decodeURIComponent(text);
        } catch (e) {
            // Déjà lisible.
        }

        return text.replace(/\+/g, ' ').trim().toLowerCase();
    }

    // Une vidéo est-elle en lecture sur cet appareil d'après le serveur ? null si on ne peut pas le savoir.
    async function videoPlaying() {
        const client = window.ApiClient;
        if (!client || typeof client.getJSON !== 'function') {
            return null;
        }

        try {
            const sessions = await client.getJSON(client.getUrl('Sessions'));
            const device = deviceKey(client.deviceName());
            const user = String(client.getCurrentUserId() || '').replace(/-/g, '').toLowerCase();
            return sessions.some(function (session) {
                return String(session.UserId || '').replace(/-/g, '').toLowerCase() === user
                    && session.NowPlayingItem
                    && session.NowPlayingItem.MediaType === 'Video'
                    && deviceKey(session.DeviceName) === device;
            });
        } catch (e) {
            return null;
        }
    }

    function armReturn() {
        if (returnArmed || !lastParams) {
            return;
        }

        returnArmed = true;
        const params = lastParams;
        let left = false;
        let seen = false;
        let timer = 0;
        const width = window.innerWidth;
        const height = window.innerHeight;
        let moved = false;
        let quietSince = 0;
        const sizeChanged = function () {
            return window.innerWidth !== width || window.innerHeight !== height;
        };

        const finish = function () {
            if (!returnArmed) {
                return;
            }

            returnArmed = false;
            clearInterval(timer);
            window.removeEventListener('resize', resized);
            window.removeEventListener('blur', leave);
            window.removeEventListener('focus', back);
            document.removeEventListener('visibilitychange', leave);
            document.removeEventListener('visibilitychange', back);
            // La page est déjà revenue au premier plan ; laisse jellyfin-web finir de la réafficher.
            setTimeout(function () {
                console.info('OpEdSync: back from ExoPlayer, theme restarted');
                dispatchViewShow(lastParams || params);
            }, 300);
        };

        const check = async function () {
            const playing = await videoPlaying();
            if (playing) {
                seen = true;
                quietSince = 0;
            } else if (playing === false && seen) {
                quietSince = quietSince || Date.now();
                let needed = QUIET_SAME_SIZE_MS;
                if (sizeChanged()) {
                    needed = QUIET_RESIZED_MS;
                } else if (moved) {
                    needed = 0;
                }

                if (Date.now() - quietSince >= needed) {
                    finish();
                }
            }
        };

        // Un signal de la page : si le serveur a vu la lecture, il dit si elle continue (rotation, barres système).
        const candidate = function () {
            if (!seen) {
                finish();
                return;
            }

            check();
        };

        const resized = function () {
            if (sizeChanged()) {
                left = true;
                moved = true;
            } else if (left) {
                candidate();
            }
        };

        const leave = function () {
            if (document.visibilityState === 'hidden' || !document.hasFocus()) {
                left = true;
            }
        };

        const back = function () {
            if (left && document.visibilityState !== 'hidden') {
                candidate();
            }
        };

        window.addEventListener('resize', resized);
        window.addEventListener('blur', leave);
        window.addEventListener('focus', back);
        document.addEventListener('visibilitychange', leave);
        document.addEventListener('visibilitychange', back);

        timer = setInterval(check, POLL_MS);
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
