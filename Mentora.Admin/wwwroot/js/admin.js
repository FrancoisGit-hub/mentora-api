// Mentora Admin : interop JS (thème, stockage, verrou de rafraîchissement).
// Aucune valeur sensible n'est écrite dans la console.
(function () {
    var THEME_KEY = "mentora-admin-theme";
    var REFRESH_KEY = "mentora-admin-refresh";
    var LOCK_NAME = "mentora-admin-refresh";

    function readStorage(key) {
        try { return window.localStorage.getItem(key); } catch (e) { return null; }
    }
    function writeStorage(key, value) {
        try { window.localStorage.setItem(key, value); return true; } catch (e) { return false; }
    }
    function removeStorage(key) {
        try { window.localStorage.removeItem(key); } catch (e) { }
    }

    function getMode() {
        var stored = readStorage(THEME_KEY);
        return stored === "light" || stored === "dark" || stored === "auto" ? stored : "auto";
    }

    function systemPrefersDark() {
        try { return window.matchMedia("(prefers-color-scheme: dark)").matches; } catch (e) { return false; }
    }

    function applyTheme(mode) {
        var dark = mode === "dark" || (mode === "auto" && systemPrefersDark());
        var root = document.documentElement;
        root.classList.remove("theme-light", "theme-dark");
        root.classList.add(dark ? "theme-dark" : "theme-light");
    }

    // Auto suit le changement de préférence système.
    try {
        var mq = window.matchMedia("(prefers-color-scheme: dark)");
        var onChange = function () { if (getMode() === "auto") applyTheme("auto"); };
        if (mq.addEventListener) mq.addEventListener("change", onChange);
        else if (mq.addListener) mq.addListener(onChange);
    } catch (e) { }

    // Un changement de thème dans un autre onglet est repris ici.
    window.addEventListener("storage", function (ev) {
        if (ev.key === THEME_KEY) applyTheme(getMode());
    });

    // Envoi par morceaux vers l'adresse signée MUX (PUT avec Content-Range).
    var videoPicker = (function () {
        var CHUNK = 8 * 1024 * 1024;            // multiple de 256 Kio, exigé par MUX
        var RETRIES = 3;                        // nouvelles tentatives par morceau
        var PROGRESS_EVERY_MS = 500;
        var file = null;
        var current = null;                     // { aborted, xhr }

        function onBeforeUnload(ev) {
            ev.preventDefault();
            ev.returnValue = "";
            return "";
        }

        function getSelected(inputId) {
            var input = document.getElementById(inputId);
            var f = input && input.files && input.files.length > 0 ? input.files[0] : null;
            file = f;
            return f ? { name: f.name, size: f.size, type: f.type || "" } : null;
        }

        function clear(inputId) {
            file = null;
            var input = document.getElementById(inputId);
            if (input) { try { input.value = ""; } catch (e) { } }
        }

        // Un morceau : 308 = continuer, 200 ou 201 = fini. Retourne { status } ou { network: true }.
        function sendChunk(url, start, end, total, state, report) {
            return new Promise(function (resolve) {
                var xhr = new XMLHttpRequest();
                state.xhr = xhr;
                xhr.open("PUT", url, true);
                xhr.withCredentials = false;    // équivalent de credentials "omit" : ni cookie ni Authorization
                xhr.setRequestHeader("Content-Range", "bytes " + start + "-" + (end - 1) + "/" + total);
                xhr.upload.onprogress = function (ev) {
                    if (ev.lengthComputable) report(start + ev.loaded);
                };
                xhr.onload = function () { resolve({ status: xhr.status }); };
                xhr.onerror = function () { resolve({ network: true }); };
                xhr.ontimeout = function () { resolve({ network: true }); };
                xhr.onabort = function () { resolve({ aborted: true }); };
                xhr.send(file.slice(start, end));
            });
        }

        function wait(ms) { return new Promise(function (r) { setTimeout(r, ms); }); }

        // Retourne { ok: true } | { ok: false, aborted: true } | { ok: false, status, network }.
        async function upload(url, dotNetRef) {
            if (!file || !url) return { ok: false, status: 0, network: false };
            var total = file.size;
            var state = { aborted: false, xhr: null };
            current = state;
            var lastSent = 0;
            var lastReport = 0;

            function report(sent, force) {
                var now = Date.now();
                if (!force && now - lastReport < PROGRESS_EVERY_MS) return;
                lastReport = now;
                lastSent = sent;
                try { dotNetRef.invokeMethodAsync("OnVideoProgress", sent, total); } catch (e) { }
            }

            window.addEventListener("beforeunload", onBeforeUnload);
            try {
                var offset = 0;
                while (offset < total) {
                    var end = Math.min(offset + CHUNK, total);
                    var attempt = 0;
                    var result;
                    while (true) {
                        result = await sendChunk(url, offset, end, total, state, function (s) { report(s, false); });
                        if (state.aborted || result.aborted) return { ok: false, aborted: true };
                        var retryable = result.network || result.status >= 500 || result.status === 408 || result.status === 429;
                        if (!retryable || attempt >= RETRIES) break;
                        attempt++;
                        await wait(1000 * attempt);
                        if (state.aborted) return { ok: false, aborted: true };
                    }

                    if (result.network) return { ok: false, status: 0, network: true };
                    var last = end >= total;
                    var good = last
                        ? (result.status === 200 || result.status === 201)
                        : (result.status === 308);
                    if (!good) return { ok: false, status: result.status, network: false };
                    offset = end;
                    report(offset, last);
                }
                return { ok: true };
            } finally {
                window.removeEventListener("beforeunload", onBeforeUnload);
                if (current === state) current = null;
                state.xhr = null;
            }
        }

        function abort() {
            if (current) {
                current.aborted = true;
                try { if (current.xhr) current.xhr.abort(); } catch (e) { }
            }
        }

        return { getSelected: getSelected, clear: clear, upload: upload, abort: abort };
    })();

    window.mentoraAdmin = {
        getThemeMode: function () { return getMode(); },
        setThemeMode: function (mode) {
            if (mode !== "light" && mode !== "dark" && mode !== "auto") mode = "auto";
            writeStorage(THEME_KEY, mode);
            applyTheme(mode);
        },
        syncTheme: function () { applyTheme(getMode()); },

        // Boîte de dialogue native (focus piégé, Échap, fond inerte).
        showDialog: function (el) {
            try { if (el && !el.open && el.showModal) el.showModal(); } catch (e) { }
        },
        scrollTop: function () {
            try { window.scrollTo(0, 0); } catch (e) { }
        },
        focusById: function (id) {
            var el = document.getElementById(id);
            if (el && el.focus) el.focus();
        },

        getRefreshToken: function () { return readStorage(REFRESH_KEY); },
        setRefreshToken: function (value) { return writeStorage(REFRESH_KEY, value); },
        clearRefreshToken: function () { removeStorage(REFRESH_KEY); },

        // Exécute le rappel .NET sous verrou exclusif (entre onglets et appels parallèles).
        runExclusiveRefresh: function (dotNetRef, methodName) {
            if (navigator.locks && navigator.locks.request) {
                return navigator.locks.request(LOCK_NAME, { mode: "exclusive" }, function () {
                    return dotNetRef.invokeMethodAsync(methodName);
                });
            }
            return dotNetRef.invokeMethodAsync(methodName);
        },

        // Vidéo de démonstration (lot 12.3b). Le fichier va directement chez MUX par l'adresse signée
        // reçue de l'API : aucun en-tête Authorization, aucun cookie. L'adresse n'est jamais écrite
        // dans la console ni renvoyée à .NET.
        openFilePicker: function (id) {
            var el = document.getElementById(id);
            if (el && el.click) el.click();
        },
        getSelectedVideo: videoPicker.getSelected,
        clearSelectedVideo: videoPicker.clear,
        uploadVideo: videoPicker.upload,
        abortVideoUpload: videoPicker.abort
    };
})();
