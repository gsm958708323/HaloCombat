import type {PreviewState} from './model';

export interface PreviewRuntime{state:PreviewState|null;ready:boolean;send:(input:string)=>void;playing:boolean;play:()=>void}
const idle:PreviewRuntime={state:null,ready:false,send:()=>{},playing:false,play:()=>{}};
// Only subscribers to runtime display receive frames; graph authoring stays independent.
export function createPreviewRuntime(){
  let value=idle;const listeners=new Set<()=>void>();
  return {get:()=>value,subscribe:(listener:()=>void)=>{listeners.add(listener);return()=>{listeners.delete(listener);};},
    publish:(next:PreviewRuntime)=>{value=next;listeners.forEach(listener=>listener());}};
}
export type PreviewRuntimeStore=ReturnType<typeof createPreviewRuntime>;
