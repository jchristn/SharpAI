import { useId, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { Icon } from '../../app/Icon';

/** Focusable information icon with a viewport-bounded hover/focus panel. */
export function InfoPopover({label,children}: {label:string;children:ReactNode}):JSX.Element {
  const id=useId();
  const anchor=useRef<HTMLButtonElement>(null);
  const [position,setPosition]=useState<{left:number;top:number}|null>(null);
  const show=():void=>{
    const rect=anchor.current?.getBoundingClientRect();
    if(rect)setPosition({left:Math.max(8,Math.min(rect.left,window.innerWidth-296)),top:Math.max(8,Math.min(rect.bottom+6,window.innerHeight-240))});
  };
  return <span className="sa-telemetry" onMouseEnter={show} onMouseLeave={()=>setPosition(null)}>
    <button ref={anchor} type="button" className="sa-btn sa-btn--ghost sa-btn--icon" aria-label={label} aria-describedby={position?id:undefined} onFocus={show} onBlur={()=>setPosition(null)} onClick={show} onKeyDown={event=>{if(event.key==='Escape')setPosition(null);}}><Icon name="info"/></button>
    {position && createPortal(<div id={id} className="sa-info-popover" role="tooltip" style={position}>{children}</div>,document.body)}
  </span>;
}
