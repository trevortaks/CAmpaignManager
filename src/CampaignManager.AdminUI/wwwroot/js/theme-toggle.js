// Theme toggle: flips Bootstrap's own [data-bs-theme] attribute on <html> (so Bootstrap's
// native dark-mode CSS applies, not a custom scheme), persists to localStorage, and
// dispatches a `themechange` CustomEvent so chart-bearing pages can rebuild with the new
// palette. The initial theme (before this file even loads) is applied by a blocking inline
// script in _Layout.cshtml's <head> to avoid a flash of the wrong theme on refresh.
(function () {
    var STORAGE_KEY = 'theme';
    var root = document.documentElement;

    function currentTheme() {
        return root.getAttribute('data-bs-theme') === 'dark' ? 'dark' : 'light';
    }

    function setTheme(theme) {
        root.setAttribute('data-bs-theme', theme);
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
