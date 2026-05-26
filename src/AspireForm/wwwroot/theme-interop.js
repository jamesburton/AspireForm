// AspireForm theme live-reload interop.
// Called from Blazor components via IJSRuntime after a token is saved.
window.afTheme = {
    reload: function () {
        var link = document.getElementById('af-theme');
        if (link) {
            link.href = '/theme.css?v=' + Date.now();
        }
    }
};
