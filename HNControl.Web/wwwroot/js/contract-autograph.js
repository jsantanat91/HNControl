document.querySelectorAll('[data-autograph-form]').forEach(form => {
  const canvas = form.querySelector('[data-autograph]'), ctx = canvas.getContext('2d');
  let drawing = false, strokes = 0, last;
  ctx.lineWidth = 3; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; ctx.strokeStyle = '#10284a';
  const point = e => { const r = canvas.getBoundingClientRect(); return { x: (e.clientX-r.left)*canvas.width/r.width, y: (e.clientY-r.top)*canvas.height/r.height }; };
  canvas.addEventListener('pointerdown', e => { drawing = true; last = point(e); canvas.setPointerCapture(e.pointerId); });
  canvas.addEventListener('pointermove', e => {
    if (!drawing) return;
    const p = point(e); if (Math.hypot(p.x-last.x,p.y-last.y)<1) return;
    ctx.beginPath(); ctx.moveTo(last.x,last.y); ctx.lineTo(p.x,p.y); ctx.stroke(); last=p; strokes++;
  });
  const end = () => { drawing=false; };
  canvas.addEventListener('pointerup', end); canvas.addEventListener('pointercancel', end);
  form.querySelector('[data-clear-autograph]').addEventListener('click', () => { ctx.clearRect(0,0,canvas.width,canvas.height); strokes=0; });
  form.addEventListener('submit', e => {
    if (strokes<2) { e.preventDefault(); form.querySelector('[data-autograph-error]').hidden=false; return; }
    form.querySelector('[name=sigDataUrl]').value=canvas.toDataURL('image/png');
    form.querySelector('[type=submit]').disabled=true;
  });
});
