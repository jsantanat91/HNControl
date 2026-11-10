(() => {
    'use strict';
    const form=document.getElementById('kpiRatingForm'), context=document.getElementById('kpiContextForm');
    if(!form || !context) return;
    const save=document.getElementById('kpiSave'), dirtyLabel=document.getElementById('kpiDirty');
    const initialContext = new FormData(context), original = new URLSearchParams(new FormData(form)).toString();
    const money=new Intl.NumberFormat('es-MX',{style:'currency',currency:'MXN'});
    let leaving=false;
    function dirty(){return new URLSearchParams(new FormData(form)).toString()!==original;}
    function pendingContext(){return ['employeeId','ym','half'].some(name=>new FormData(context).get(name)!==initialContext.get(name));}
    const notice=document.createElement('p');notice.className='alert alert-info py-2 small';notice.hidden=true;notice.textContent='Pulsa Cargar evaluación para consultar el empleado y periodo seleccionados.';context.after(notice);
    context.addEventListener('change',()=>{save.disabled=pendingContext();notice.hidden=!save.disabled;});
    function render(){const values=[...form.querySelectorAll('input[type="radio"]:checked')].map(input=>Number(input.value));const sum=values.reduce((a,b)=>a+b,0), avg=values.length?sum/values.length:0, fraction=Math.round(sum/35*10000)/10000;const base=Number(form.dataset.salary)/2, fixed=base*.8, variable=base*.2*fraction;
        document.getElementById('kpiLiveScore').textContent=avg.toFixed(1);document.getElementById('kpiLivePercent').textContent=(fraction*100).toFixed(2).replace(/\.?0+$/,'')+'%';document.getElementById('kpiLiveProgress').value=fraction*100;
        [['Base',base],['Fixed',fixed],['Variable',variable],['Total',fixed+variable]].forEach(([id,value])=>document.getElementById('kpiLive'+id).textContent=money.format(value));dirtyLabel.hidden=!dirty();
    }
    form.addEventListener('input',render);render();
    context.addEventListener('submit',event=>{if(dirty()&&!confirm('Hay cambios sin guardar. ¿Cargar otra evaluación y descartarlos?'))event.preventDefault();else leaving=true;});
    document.querySelectorAll('a[data-module-panel-close]').forEach(link=>link.addEventListener('click',event=>{if(dirty()&&!leaving&&!confirm('Hay cambios sin guardar. ¿Salir sin guardarlos?')){event.preventDefault();event.stopImmediatePropagation();}},true));
    document.addEventListener('hn:panel-closing',event=>{if(dirty()&&!leaving){if(!confirm('Hay cambios sin guardar. ¿Cerrar sin guardarlos?'))event.preventDefault();else leaving=true;}});
    form.addEventListener('submit',event=>{if(pendingContext()){event.preventDefault();notice.hidden=false;return;}if(form.checkValidity()){leaving=true;save.disabled=true;save.textContent='Guardando evaluación…';}});
})();
