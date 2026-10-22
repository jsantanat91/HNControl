(() => {
    'use strict';
    const modalElement = document.getElementById('hnModulePanel');
    if (!modalElement || !window.bootstrap) return;
    const frame = document.getElementById('hnModulePanelFrame');
    const title = document.getElementById('hnModulePanelTitle');
    const open = document.getElementById('hnModulePanelOpen');
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    let changed = false;
    const panelUrl = value => {
        const url = new URL(value, window.location.href);
        if (url.origin !== window.location.origin || !/^https?:$/.test(url.protocol)) return null;
        url.searchParams.set('panel', '1');
        return url;
    };
    document.addEventListener('click', event => {
        const link = event.target instanceof Element ? event.target.closest('a[data-module-panel]') : null;
        if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || event.button !== 0) return;
        const url = panelUrl(link.href);
        if (!url) return;
        event.preventDefault();
        title.textContent = link.dataset.modulePanel || link.textContent.trim();
        frame.title = title.textContent;
        open.href = link.href;
        changed = false;
        frame.src = url.href;
        modal.show();
    });
    frame.addEventListener('load', () => {
        if (frame.getAttribute('src') === 'about:blank') return;
        try {
            const doc = frame.contentDocument;
            if (!doc?.body) return;
            doc.body.classList.add('hn-panel-view');
            const current = new URL(frame.contentWindow.location.href);
            current.searchParams.delete('panel');
            open.href = current.href;
            // Keep the existing links, POST forms, anti-forgery tokens and redirects working inside the panel.
            doc.addEventListener('click', event => {
                const anchor = event.target instanceof frame.contentWindow.Element ? event.target.closest('a[href]') : null;
                if (!anchor || anchor.target === '_blank' || anchor.hasAttribute('download') || anchor.getAttribute('href').startsWith('#')) return;
                const url = panelUrl(anchor.href);
                if (url) anchor.href = url.href;
            }, true);
            doc.addEventListener('submit', event => {
                const form = event.target;
                if (form.method.toLowerCase() === 'post') changed = true;
                const url = panelUrl(form.action || frame.contentWindow.location.href);
                if (url) form.action = url.href;
            }, true);
        } catch { /* External pages keep their own navigation; never inspect cross-origin content. */ }
    });
    modalElement.addEventListener('hidden.bs.modal', () => {
        frame.src = 'about:blank';
        if (changed) window.location.reload();
    });
})();
