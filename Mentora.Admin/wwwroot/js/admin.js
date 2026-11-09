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

    window.mentoraAdmin = {
        getThemeMode: function () { return getMode(); },
        setThemeMode: function (mode) {
            if (mode !== "light" && mode !== "dark" && mode !== "auto") mode = "auto";
            writeStorage(THEME_KEY, mode);
            applyTheme(mode);
        },
        syncTheme: function () { applyTheme(getMode()); },

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
        }
    };
})();
