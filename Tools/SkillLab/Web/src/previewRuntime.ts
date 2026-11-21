import type {PreviewState} from './model';

export type PreviewAim = {x:number;z:number};

export interface PreviewRuntime{state:PreviewState|null;ready:boolean;send:(input:string,aim?:PreviewAim)=>void;inputAndStep:(input:string,aim?:PreviewAim)=>void;step:()=>void;reset:()=>void;playing:boolean;setPlaying:(playing:boolean)=>void}
const idle:PreviewRuntime={state:null,ready:false,send:()=>{},inputAndStep:()=>{},step:()=>{},reset:()=>{},playing:false,setPlaying:()=>{}};
// Only subscribers to runtime display receive frames; graph authoring stays independent.
export function createPreviewRuntime(){
  let value=idle;const listeners=new Set<()=>void>();
  return {get:()=>value,subscribe:(listener:()=>void)=>{listeners.add(listener);return()=>{listeners.delete(listener);};},
    publish:(next:PreviewRuntime)=>{value=next;listeners.forEach(listener=>listener());}};
}
export type PreviewRuntimeStore=ReturnType<typeof createPreviewRuntime>;
