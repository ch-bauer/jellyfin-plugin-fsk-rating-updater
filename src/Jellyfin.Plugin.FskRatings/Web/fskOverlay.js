(function () {
    'use strict';

    // Netflix-style FSK rating overlay, injected into the Jellyfin web client
    // by the FSK Rating Updater plugin. Shows the FSK badge for a few seconds
    // when playback of a rated item starts.

    var FSK = {
        '0': { color: '#ffffff', text: '#000000', border: '1px solid rgba(0, 0, 0, 0.6)', label: 'Ohne Altersbeschränkung' },
        '6': { color: '#ffd400', text: '#000000', border: 'none', label: 'Freigegeben ab 6 Jahren' },
        '12': { color: '#00a53d', text: '#ffffff', border: 'none', label: 'Freigegeben ab 12 Jahren' },
        '16': { color: '#00b6ed', text: '#ffffff', border: 'none', label: 'Freigegeben ab 16 Jahren' },
        '18': { color: '#e2001a', text: '#ffffff', border: 'none', label: 'Freigegeben ab 18 Jahren' }
    };

    var OVERLAY_ID = 'fskRatingOverlay';
    var STYLE_ID = 'fskRatingOverlayStyle';

    var configPromise = null;
    var lastShownItemId = null;
    var pollTimer = null;
    var hideTimer = null;

    function getConfig() {
        if (!configPromise) {
            configPromise = window.ApiClient
                .fetch({ url: window.ApiClient.getUrl('FskRatings/OverlayConfig'), type: 'GET', dataType: 'json' })
                .catch(function (err) {
                    console.debug('fskOverlay: could not load overlay config', err);
                    return { Enabled: false, DurationSeconds: 5 };
                });
        }
        return configPromise;
    }

    function ensureStyle() {
        if (document.getElementById(STYLE_ID)) {
            return;
        }
        var style = document.createElement('style');
        style.id = STYLE_ID;
        style.textContent =
            '#' + OVERLAY_ID + ' {' +
            '  position: absolute; top: 12%; left: 4%; z-index: 1000;' +
            '  display: flex; align-items: center; pointer-events: none;' +
            '  opacity: 0; transition: opacity 0.6s ease;' +
            '  font-family: inherit;' +
            // Scale the whole overlay with the viewport: all inner sizes are em-based.
            '  font-size: clamp(10px, 1.1vw, 16px);' +
            '}' +
            '#' + OVERLAY_ID + '.fskVisible { opacity: 1; }' +
            '#' + OVERLAY_ID + ' .fskSquare {' +
            '  width: 2.6em; height: 2.6em; border-radius: 0.25em;' +
            '  display: flex; align-items: center; justify-content: center;' +
            '  font-size: 1.25em; font-weight: 700;' +
            '  box-shadow: 0 0.1em 0.6em rgba(0, 0, 0, 0.5);' +
            '}' +
            '#' + OVERLAY_ID + ' .fskText { margin-left: 0.8em; color: #ffffff; text-shadow: 0 0.05em 0.4em rgba(0, 0, 0, 0.8); }' +
            '#' + OVERLAY_ID + ' .fskTitle { font-size: 1.25em; font-weight: 700; line-height: 1.2; }' +
            '#' + OVERLAY_ID + ' .fskSubtitle { font-size: 0.85em; opacity: 0.9; }';
        document.head.appendChild(style);
    }

    function removeOverlay() {
        if (hideTimer) {
            clearTimeout(hideTimer);
            hideTimer = null;
        }
        var el = document.getElementById(OVERLAY_ID);
        if (el && el.parentNode) {
            el.parentNode.removeChild(el);
        }
    }

    function showOverlay(level, durationSeconds) {
        removeOverlay();
        ensureStyle();

        var info = FSK[level];
        var container = document.querySelector('#videoOsdPage:not(.hide)') || document.querySelector('#videoOsdPage') || document.body;

        var overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;

        var square = document.createElement('div');
        square.className = 'fskSquare';
        square.style.background = info.color;
        square.style.color = info.text;
        square.style.border = info.border;
        square.textContent = level;

        var text = document.createElement('div');
        text.className = 'fskText';
        var title = document.createElement('div');
        title.className = 'fskTitle';
        title.textContent = 'FSK ' + level;
        var subtitle = document.createElement('div');
        subtitle.className = 'fskSubtitle';
        subtitle.textContent = info.label;
        text.appendChild(title);
        text.appendChild(subtitle);

        overlay.appendChild(square);
        overlay.appendChild(text);
        container.appendChild(overlay);

        // Force a layout pass so the transition from opacity 0 actually runs.
        void overlay.offsetWidth;
        overlay.classList.add('fskVisible');

        hideTimer = setTimeout(function () {
            overlay.classList.remove('fskVisible');
            setTimeout(removeOverlay, 700);
        }, durationSeconds * 1000);
    }

    function maybeShow(item, durationSeconds) {
        if (!item || !item.Id) {
            return false;
        }
        var match = /^FSK-(0|6|12|16|18)$/.exec(item.OfficialRating || '');
        if (!match) {
            // Rated but not FSK (or unrated): nothing to show, stop polling.
            lastShownItemId = item.Id;
            return true;
        }
        if (item.Id === lastShownItemId) {
            return true;
        }
        lastShownItemId = item.Id;
        showOverlay(match[1], durationSeconds);
        return true;
    }

    function stopPolling() {
        if (pollTimer) {
            clearInterval(pollTimer);
            pollTimer = null;
        }
    }

    function startPolling(durationSeconds) {
        stopPolling();
        var tries = 0;
        pollTimer = setInterval(function () {
            tries++;
            if (tries > 20) {
                stopPolling();
                return;
            }
            var apiClient = window.ApiClient;
            apiClient.getSessions({ deviceId: apiClient.deviceId() }).then(function (sessions) {
                var session = (sessions || []).filter(function (s) {
                    return s.NowPlayingItem;
                })[0];
                if (!session) {
                    return;
                }
                var item = session.NowPlayingItem;
                if (item.OfficialRating || !item.Id) {
                    if (maybeShow(item, durationSeconds)) {
                        stopPolling();
                    }
                    return;
                }
                // NowPlayingItem sometimes omits OfficialRating; fetch the full item once.
                stopPolling();
                apiClient.getItem(apiClient.getCurrentUserId(), item.Id).then(function (full) {
                    maybeShow(full || item, durationSeconds);
                }).catch(function () {
                    maybeShow(item, durationSeconds);
                });
            }).catch(function (err) {
                console.debug('fskOverlay: session lookup failed', err);
            });
        }, 500);
    }

    function isVideoOsd(e) {
        if (e && e.detail && typeof e.detail.type === 'string') {
            return e.detail.type === 'video-osd';
        }
        var page = e && e.target;
        return !!(page && page.id === 'videoOsdPage') || !!document.querySelector('#videoOsdPage:not(.hide)');
    }

    document.addEventListener('viewshow', function (e) {
        if (!window.ApiClient || !isVideoOsd(e)) {
            return;
        }
        getConfig().then(function (config) {
            if (!config || !config.Enabled) {
                return;
            }
            var duration = Math.min(Math.max(parseInt(config.DurationSeconds, 10) || 5, 1), 30);
            startPolling(duration);
        });
    });

    document.addEventListener('viewhide', function (e) {
        if (e && e.target && e.target.id === 'videoOsdPage') {
            stopPolling();
            removeOverlay();
        }
    });
})();
