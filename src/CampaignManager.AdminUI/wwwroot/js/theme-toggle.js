// Theme toggle: flips [data-theme] on <html>, persists to localStorage, and dispatches a
// `themechange` CustomEvent so chart-bearing pages can rebuild with the new palette.
// The initial theme (before this file even loads) is applied by a blocking inline script in
// _Layout.cshtml's <head> to avoid a flash of the wrong theme on refresh.
(function () {
    var STORAGE_KEY = 'theme';
    var root = document.documentElement;

    function currentTheme() {
        return root.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function setTheme(theme) {
        root.setAttribute('data-theme', theme);
        localStorage.setItem(STORAGE_KEY, theme);
        window.dispatchEvent(new CustomEvent('themechange', { detail: { theme: theme } }));
    }

    document.addEventListener('DOMContentLoaded', function () {
        var toggle = document.getElementById('themeToggleBtn');
        if (!toggle) return;
        toggle.addEventListener('click', function () {
            setTheme(currentTheme() === 'dark' ? 'light' : 'dark');
        });
    });
})();
