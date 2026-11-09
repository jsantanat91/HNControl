document.addEventListener('click', async e => {
 const button = e.target.closest('[data-copy-quote-link]'); if (!button) return;
 const url = new URL('/cotizar?public=true', location.origin).href;
 try { await navigator.clipboard.writeText(url); button.textContent = 'Enlace copiado'; }
 catch { window.prompt('Copia el enlace público:', url); }
});
