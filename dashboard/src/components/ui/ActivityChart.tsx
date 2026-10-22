import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { formatDateTime } from '../../i18n/format';
import './ui.css';

export interface ActivityBucket { start:string;end:string;success:number;failure:number }
export interface ActivityChartProps { buckets:ActivityBucket[];height?:number;onBucketClick?:(bucket:ActivityBucket)=>void }

/** Stacked counts with time/count axes, hover details, and keyboard-accessible time filtering. */
export function ActivityChart({buckets,height=288,onBucketClick}:ActivityChartProps):JSX.Element {
  const {t,i18n}=useTranslation();
  const [active,setActive]=useState<ActivityBucket|null>(null);
  const chartRef=useRef<HTMLDivElement>(null);
  const [width,setWidth]=useState(800);
  const [position,setPosition]=useState({x:60,y:40});
  useEffect(()=>{
    const element=chartRef.current;
    if(!element || typeof ResizeObserver==='undefined') return;
    const observer=new ResizeObserver(entries=>setWidth(Math.max(240,entries[0].contentRect.width)));
    observer.observe(element);return ()=>observer.disconnect();
  },[buckets.length]);
  if(!buckets.length) return <div className="sa-chart sa-chart--empty">{t('text.noActivityInRange')}</div>;
  const max=Math.max(1,...buckets.map(b=>b.success+b.failure));
  const left=55,right=12,top=12,bottom=height-38,plot=bottom-top;
  const unit=(width-left-right)/buckets.length;
  const label=(bucket:ActivityBucket)=>`${formatDateTime(bucket.start,i18n.language)} – ${formatDateTime(bucket.end,i18n.language)} · ${t('chart.counts',{success:bucket.success,failure:bucket.failure})}`;
  return <div ref={chartRef} className="sa-chart sa-history-chart">
    <div className="sa-chart-axis-label">{t('chart.requests')}</div>
    <svg width="100%" height={height} viewBox={`0 0 ${width} ${height}`} role="img" aria-label={t('text.requestActivityOverTime')}>
      {[0,.5,1].map(f=><line key={f} x1={left} x2={width-right} y1={bottom-f*plot} y2={bottom-f*plot} stroke="var(--sa-border)"/>)}
      {[0,.5,1].map(f=><text key={f} x={left-8} y={bottom-f*plot+4} textAnchor="end" fill="currentColor" fontSize="11">{new Intl.NumberFormat(i18n.language,{maximumFractionDigits:0}).format(Math.ceil(max*f))}</text>)}
      {buckets.map((bucket,index)=>{
        const x=left+index*unit,success=bucket.success/max*plot,failure=bucket.failure/max*plot;
        return <g key={bucket.start} tabIndex={0} role={onBucketClick?'button':undefined} aria-label={label(bucket)}
          onMouseMove={event=>{const box=chartRef.current?.getBoundingClientRect();if(box)setPosition({x:Math.min(Math.max(8,event.clientX-box.left+12),Math.max(8,box.width-260)),y:event.clientY-box.top+12});}} onMouseEnter={()=>setActive(bucket)} onMouseLeave={()=>setActive(null)} onFocus={()=>{setActive(bucket);setPosition({x:Math.min(x+20,Math.max(8,width-260)),y:40});}} onBlur={()=>setActive(null)}
          onClick={()=>onBucketClick?.(bucket)} onKeyDown={e=>{if(onBucketClick && (e.key==='Enter'||e.key===' ')){e.preventDefault();onBucketClick(bucket);}}} style={{cursor:onBucketClick?'pointer':'default'}}>
          <rect x={x} y={top} width={unit} height={plot} fill="transparent"/>
          <rect x={x+1} y={bottom-success} width={Math.max(1,unit-2)} height={success} fill="var(--sa-success)"/>
          <rect x={x+1} y={bottom-success-failure} width={Math.max(1,unit-2)} height={failure} fill="var(--sa-danger)"/>
        </g>;
      })}
      {(width<500?[0,buckets.length-1]:[0,Math.floor((buckets.length-1)/2),buckets.length-1]).filter((v,i,a)=>a.indexOf(v)===i).map((index)=><text key={index} x={left+index*unit+unit/2} y={bottom+20} textAnchor={index===0?'start':index===buckets.length-1?'end':'middle'} fill="currentColor" fontSize="11">{new Intl.DateTimeFormat(i18n.language,{month:'short',day:'numeric',hour:'2-digit',minute:'2-digit'}).format(new Date(buckets[index].start))}</text>)}
    </svg>
    <div className="sa-chart-axis-label sa-chart-axis-label--x">{t('chart.time')}</div>
    {active && <div className="sa-chart-tooltip" role="tooltip" style={{left:position.x,top:position.y}}>{label(active)}</div>}
  </div>;
}
