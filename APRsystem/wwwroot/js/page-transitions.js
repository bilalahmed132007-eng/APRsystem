/* ===================================================================
   PAGE TRANSITIONS — lightweight client-side navigation
   Intercepts clicks on internal links, fetches the destination page,
   swaps out #page-content with a fade, and updates the URL/title.
   Falls back to a normal page load for anything it can't safely handle.
   =================================================================== */
(function () {
    const CONTENT_SELECTOR = '#page-content';
    const LEAVE_ANIMATION_MS = 220; // must roughly match the CSS .is-leaving duration

    const bar = document.createElement('div');
    bar.id = 'page-transition-bar';
    document.body.appendChild(bar);

    function showBar() {
        bar.style.width = '0%';
        bar.classList.add('is-active');
        requestAnimationFrame(() => { bar.style.width = '70%'; });
    }

    function finishBar() {
        bar.style.width = '100%';
        setTimeout(() => {
            bar.classList.remove('is-active');
            bar.style.width = '0%';
        }, 250);
    }

    function isSameOriginNavigable(link) {
        if (!link || !link.href) return false;
        if (link.target && link.target !== '' && link.target !== '_self') return false;
        if (link.hasAttribute('download')) return false;
        if (link.dataset.noTransition !== undefined) return false;

        let url;
        try {
            url = new URL(link.href, window.location.href);
        } catch (e) {
            return false;
        }

        if (url.origin !== window.location.origin) return false;
        if (url.pathname === window.location.pathname && url.hash) return false; // in-page anchor
        return true;
    }

    async function navigateTo(url, addToHistory) {
        const content = document.querySelector(CONTENT_SELECTOR);
        if (!content) {
            window.location.href = url; // safety net if the container isn't present
            return;
        }

        showBar();
        content.classList.add('is-leaving');

        try {
            const response = await fetch(url, { headers: { 'X-Requested-With': 'PageTransition' } });
            if (!response.ok) throw new Error('Navigation fetch failed: ' + response.status);

            const html = await response.text();

            setTimeout(() => {
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newContent = doc.querySelector(CONTENT_SELECTOR);
                const newTitle = doc.querySelector('title');

                if (!newContent) {
                    // Target page didn't render the expected container — do a real navigation instead
                    window.location.href = url;
                    return;
                }

                content.innerHTML = newContent.innerHTML;
                if (newTitle) document.title = newTitle.textContent;

                // Sync the visible topbar title too — document.title above only updates
                // the browser tab, not the on-screen <span class="topbar-title"> in
                // _Layout.cshtml, which otherwise stays frozen at whatever it was on
                // the very first full page load until a real reload happens.
                const newTopbarTitle = doc.querySelector('.topbar-title');
                const currentTopbarTitle = document.querySelector('.topbar-title');
                if (newTopbarTitle && currentTopbarTitle) {
                    currentTopbarTitle.textContent = newTopbarTitle.textContent;
                }

                content.classList.remove('is-leaving');
                // restart the enter animation
                content.style.animation = 'none';
                void content.offsetWidth; // reflow to reset animation
                content.style.animation = '';

                if (addToHistory) {
                    window.history.pushState({ pageTransition: true }, '', url);
                }

                window.scrollTo({ top: 0, behavior: 'instant' in window ? 'instant' : 'auto' });
                finishBar();

                // Re-run any per-page init scripts that listen for this event
                document.dispatchEvent(new CustomEvent('page-transition:loaded'));
            }, LEAVE_ANIMATION_MS);

        } catch (err) {
            console.error('Page transition failed, falling back to normal navigation:', err);
            window.location.href = url;
        }
    }

    document.addEventListener('click', function (e) {
        const link = e.target.closest('a');
        if (!isSameOriginNavigable(link)) return;

        e.preventDefault();
        navigateTo(link.href, true);
    });

    window.addEventListener('popstate', function () {
        navigateTo(window.location.href, false);
    });

    /* -----------------------------------------------------------------
       Keep the sidebar's "active" highlight in sync with the current URL.
       Needed because the sidebar lives outside #page-content, so it never
       re-renders during a JS-driven transition — without this, whichever
       link was active on the very first full page load stays highlighted
       forever, no matter where you navigate afterward.
       ----------------------------------------------------------------- */
    function updateActiveNavLink() {
        const currentPath = window.location.pathname.toLowerCase().replace(/\/$/, '');
        const navLinks = document.querySelectorAll('.app-sidebar .sidebar-nav-item');

        navLinks.forEach((link) => {
            const rawHref = (link.getAttribute('href') || '').trim();

            // Skip dropdown toggles and placeholder links entirely —
            // resolving "#" through new URL() would otherwise inherit
            // the CURRENT page's path and falsely light up as active.
            if (
                rawHref === '' ||
                rawHref === '#' ||
                rawHref.startsWith('javascript:') ||
                link.hasAttribute('data-bs-toggle')
            ) {
                link.classList.remove('active');
                return;
            }

            let linkPath;
            try {
                linkPath = new URL(link.href, window.location.href).pathname.toLowerCase().replace(/\/$/, '');
            } catch (e) {
                return;
            }

            const isActive = linkPath !== '' && (currentPath === linkPath || currentPath.startsWith(linkPath + '/'));
            link.classList.toggle('active', isActive);
        });
    }

    document.addEventListener('page-transition:loaded', updateActiveNavLink);
    document.addEventListener('DOMContentLoaded', updateActiveNavLink);
    window.addEventListener('popstate', updateActiveNavLink);
})();