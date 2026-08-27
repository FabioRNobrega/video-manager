(function () {
    "use strict";

    const storageKey = "video-manager-theme";
    const darkTheme = "dark";
    const lightTheme = "light";

    function isTheme(value) {
        return value === darkTheme || value === lightTheme;
    }

    function readStoredTheme() {
        try {
            const value = window.localStorage.getItem(storageKey);
            return isTheme(value) ? value : null;
        } catch {
            return null;
        }
    }

    function applyTheme(theme) {
        const value = isTheme(theme) ? theme : darkTheme;
        document.documentElement.setAttribute("data-bs-theme", value);
        document.documentElement.style.colorScheme = value;
        return value;
    }

    function getTheme() {
        const current = document.documentElement.getAttribute("data-bs-theme");
        return isTheme(current) ? current : applyTheme(darkTheme);
    }

    function setTheme(theme) {
        const value = applyTheme(theme);

        try {
            window.localStorage.setItem(storageKey, value);
        } catch {
            // The in-page theme still works when storage is blocked or unavailable.
        }

        return value;
    }

    function toggleTheme() {
        return setTheme(getTheme() === darkTheme ? lightTheme : darkTheme);
    }

    applyTheme(readStoredTheme() ?? darkTheme);
    window.videoManagerTheme = Object.freeze({ getTheme, toggleTheme });
})();
