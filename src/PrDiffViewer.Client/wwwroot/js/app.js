// Minimal JS interop helpers for the Blazor client.
window.prDiff = {
    // Smoothly scroll a diff card into view and nudge it below the sticky toolbar.
    scrollToId: function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }
};
