import {useLayoutEffect,useRef} from 'react';
import type {ReactNode,KeyboardEvent} from 'react';

export function Dialog({label,close,busy=false,children}:{label:string;close:()=>void;busy?:boolean;children:ReactNode}) {
  const root=useRef<HTMLDivElement>(null);
  const previous=useRef(document.activeElement instanceof HTMLElement?document.activeElement:null);
  useLayoutEffect(()=>{
    if(root.current&&!root.current.contains(document.activeElement))root.current.querySelector<HTMLElement>('input,select,textarea,button')?.focus();
    return ()=>{if(previous.current?.isConnected)previous.current.focus();};
  },[]);
  function keyDown(event:KeyboardEvent<HTMLDivElement>){
    if(event.key==='Escape'){
      event.preventDefault();event.stopPropagation();if(!busy)close();return;
    }
    if(event.key!=='Tab')return;
    const controls=[...(root.current?.querySelectorAll<HTMLElement>('button:not(:disabled),input:not(:disabled),select:not(:disabled),textarea:not(:disabled),[href],[tabindex="0"]')??[])].filter(el=>el.getClientRects().length>0);
    if(!controls.length){event.preventDefault();root.current?.focus();return;}
    const first=controls[0],last=controls.at(-1)!;
    if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
    else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
  }
  return <div className="modal-backdrop" role="dialog" aria-modal="true" aria-label={label} tabIndex={-1} ref={root} onKeyDown={keyDown}>{children}</div>;
}
