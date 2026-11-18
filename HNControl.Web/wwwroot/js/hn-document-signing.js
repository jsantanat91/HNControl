(() => {
    'use strict';
    const canvas = document.getElementById('sigCanvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const placeholder = document.getElementById('signaturePlaceholder');
    const state = document.getElementById('signatureState');
    const error = document.getElementById('signatureError');
    const form = canvas.closest('form');
    let strokes = [], stroke = null, pointerId = null;
    let basis = null, scale = 1, offsetX = 0, offsetY = 0;
    const hasSignature = () => strokes.some(points => points.length > 1);
    function updateState() {
        const ready = hasSignature();
        placeholder.hidden = ready;
        state.textContent = ready ? 'Firma lista' : 'Sin firma';
        state.classList.toggle('is-ready', ready);
        if (ready) error.hidden = true;
    }
    function redraw() {
        const rect = canvas.getBoundingClientRect();
        if (!rect.width || !rect.height) return;
        basis ??= { width: rect.width, height: rect.height };
        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(rect.width * dpr);
        canvas.height = Math.round(rect.height * dpr);
        scale = Math.min(rect.width / basis.width, rect.height / basis.height);
        offsetX = (rect.width - basis.width * scale) / 2;
        offsetY = (rect.height - basis.height * scale) / 2;
        ctx.setTransform(dpr * scale, 0, 0, dpr * scale, dpr * offsetX, dpr * offsetY);
        ctx.lineWidth = 2.5;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.strokeStyle = '#172c45';
        for (const points of strokes) {
            ctx.beginPath();
            points.forEach((p, index) => index ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y));
            ctx.stroke();
        }
    }
    function position(event) {
        const rect = canvas.getBoundingClientRect();
        return { x: (event.clientX - rect.left - offsetX) / scale, y: (event.clientY - rect.top - offsetY) / scale };
    }
    canvas.addEventListener('pointerdown', event => {
        if (!event.isPrimary || (event.pointerType === 'mouse' && event.button !== 0)) return;
        event.preventDefault();
        pointerId = event.pointerId;
        canvas.setPointerCapture(pointerId);
        stroke = [position(event)]; strokes.push(stroke);
    });
    canvas.addEventListener('pointermove', event => {
        if (!stroke || event.pointerId !== pointerId) return;
        event.preventDefault();
        const p = position(event), previous = stroke[stroke.length - 1];
        if (Math.hypot(p.x - previous.x, p.y - previous.y) < 0.5) return;
        ctx.beginPath(); ctx.moveTo(previous.x, previous.y); ctx.lineTo(p.x, p.y); ctx.stroke();
        stroke.push(p); updateState();
    });
    function end(event) {
        if (event.pointerId !== pointerId) return;
        stroke = null; pointerId = null;
    }
    canvas.addEventListener('pointerup', end);
    canvas.addEventListener('pointercancel', end);
    canvas.addEventListener('lostpointercapture', end);
    window.clearSig = () => {
        strokes = []; stroke = null; pointerId = null; basis = null;
        document.getElementById('sigDataUrl').value = '';
        error.hidden = true;
        redraw(); updateState();
    };
    window.captureSig = () => {
        if (!hasSignature()) { error.hidden = false; canvas.scrollIntoView({ block: 'center', behavior: 'smooth' }); return false; }
        document.getElementById('sigDataUrl').value = canvas.toDataURL('image/png');
        const button = form.querySelector('button[type="submit"]');
        button.disabled = true;
        button.querySelector('span').textContent = 'Registrando firma…';
        return true;
    };
    redraw(); updateState();
    if (typeof ResizeObserver !== 'undefined') new ResizeObserver(redraw).observe(canvas);
    else window.addEventListener('resize', redraw);
    window.addEventListener('pageshow', () => {
        const button = form.querySelector('button[type="submit"]');
        button.disabled = false;
        button.querySelector('span').textContent = 'Firmar documento';
    });
})();
