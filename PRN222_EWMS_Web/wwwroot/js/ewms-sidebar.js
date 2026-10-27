(() => {
    const body = document.body;
    const toggle = document.getElementById('ewmsToggleSidebar');
    const close = document.getElementById('ewmsCloseSidebar');
    const overlay = document.getElementById('ewmsSidebarOverlay');
    const accountMenu = document.getElementById('ewmsAccountMenu');
    document.addEventListener('click', event => {
        if (accountMenu && !accountMenu.contains(event.target)) accountMenu.open = false;
    });
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && accountMenu?.open) {
            accountMenu.open = false;
            accountMenu.querySelector('summary').focus();
        }
    });
    if (!toggle) return;

    const mobile = () => window.matchMedia('(max-width: 800px)').matches;
    const updateAria = () => toggle.setAttribute('aria-expanded', String(mobile()
        ? body.classList.contains('sidebar-mobile-open')
        : !body.classList.contains('sidebar-collapsed')));

    try {
        if (localStorage.getItem('ewms-sidebar-collapsed') === 'true')
            body.classList.add('sidebar-collapsed');
    } catch (_) { /* localStorage may be unavailable */ }

    const closeMobile = () => { body.classList.remove('sidebar-mobile-open'); updateAria(); };
    toggle.addEventListener('click', () => {
        if (accountMenu) accountMenu.open = false;
        if (mobile()) body.classList.toggle('sidebar-mobile-open');
        else {
            body.classList.toggle('sidebar-collapsed');
            try { localStorage.setItem('ewms-sidebar-collapsed', String(body.classList.contains('sidebar-collapsed'))); } catch (_) { }
        }
        updateAria();
    });
    close?.addEventListener('click', closeMobile);
    overlay?.addEventListener('click', closeMobile);
    document.addEventListener('keydown', e => { if (e.key === 'Escape') closeMobile(); });
    window.addEventListener('resize', () => { if (!mobile()) closeMobile(); updateAria(); });
    updateAria();
})();
