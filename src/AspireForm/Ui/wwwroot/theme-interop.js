// theme-interop.js — Dark mode toggle + theme CSS reload for AspireForm UI.

/**
 * Apply or remove the 'dark' class on <html> and reload the /theme.css link.
 * @param {boolean} isDark
 */
export function setDarkMode(isDark) {
    const root = document.documentElement;
    if (isDark) {
        root.classList.add('dark');
    } else {
        root.classList.remove('dark');
    }
    reloadThemeCss();
}

/**
 * Force the browser to re-fetch /theme.css by toggling a cache-busting query param.
 */
export function reloadThemeCss() {
    const link = document.querySelector('link[href*="/theme.css"]');
    if (link) {
        const url = new URL(link.href, window.location.href);
        url.searchParams.set('v', Date.now().toString());
        link.href = url.toString();
    }
}

/**
 * Switch the active theme via POST, then reload the theme CSS.
 * @param {string} themeName
 */
export async function switchTheme(themeName) {
    try {
        await fetch('/themes/set-active', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name: themeName }),
        });
    } catch { /* best-effort */ }
    reloadThemeCss();
}
