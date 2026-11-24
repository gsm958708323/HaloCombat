import {useEffect,useRef,useState} from 'react';
import type {Field} from './model';
import {displayNumber,numericText,rangeBounds,rangeValue,steppedValue} from './authoring/numericParameters';

export function NumericParameter({field,value,change,beginEdit,endEdit}:{field:Field;value:number;change:(value:number)=>void;beginEdit:()=>void;endEdit:()=>void}) {
  const displayed=displayNumber(value,field),[text,setText]=useState(String(displayed)),[error,setError]=useState('');
  const editing=useRef(false),scale=field.displayScale??1;
  useEffect(()=>{
    // Preserve an unfinished edit, but always adopt external undo/redo/import updates.
    if(!editing.current||numericText(text)!==displayed){setText(String(displayed));setError('');}
  },[displayed]);
  const bounds=rangeBounds(displayed,field);
  function accept(raw:string,badInput=false){
    setText(raw);const next=numericText(raw,field.integer);
    if(next===null){setError(!raw&&!badInput?'':field.integer?'请输入整数；当前输入尚未应用':'请输入有限数字；当前输入尚未应用');return;}
    setError('');change(next/scale);
  }
  function restore(){setText(String(displayed));setError('');}
  return <div className="number-pair">
    <input aria-label={`${field.label}滑条`} aria-valuetext={String(displayed)+(field.unit??'')} type="range"
      min={bounds.min} max={bounds.max} step="any" value={displayed} onPointerDown={beginEdit} onPointerUp={endEdit} onPointerCancel={endEdit}
      onKeyDown={e=>{
        if(!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key))return;
        if(!e.repeat)beginEdit();e.preventDefault();
        const next=steppedValue(value,field,e.key==='ArrowRight'||e.key==='ArrowUp'?1:-1);
        const display=Math.max(bounds.min,Math.min(bounds.max,displayNumber(next,field)));
        setText(String(display));setError('');change(display/scale);
      }} onKeyUp={e=>{if(e.key.startsWith('Arrow'))endEdit();}} onBlur={endEdit} onChange={e=>{
        const next=rangeValue(Number(e.target.value),field);setText(String(displayNumber(next,field)));setError('');change(next);
      }}/>
    <input aria-label={field.label} aria-invalid={!!error} title={error||field.label+(field.unit?' ('+field.unit+')':'')}
      type="number" inputMode={field.integer?'numeric':'decimal'} step="any" value={text}
      onFocus={()=>{editing.current=true;}} onChange={e=>accept(e.target.value,e.target.validity.badInput)}
      onBlur={()=>{editing.current=false;if(numericText(text,field.integer)===null)restore();else setText(String(displayed));}}
      onKeyDown={e=>{
        if(e.key==='Escape'){e.preventDefault();restore();}
        if(e.key==='ArrowUp'||e.key==='ArrowDown'){
          if(!e.repeat)beginEdit();
          e.preventDefault();const next=steppedValue(value,field,e.key==='ArrowUp'?1:-1);setText(String(displayNumber(next,field)));setError('');change(next);
        }
      }} onKeyUp={e=>{if(e.key==='ArrowUp'||e.key==='ArrowDown')endEdit();}}/>
    {error&&<small className="number-error" role="alert">{error}</small>}
  </div>;
}
