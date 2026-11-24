import {useEffect,useRef,useState} from 'react';
import type {PointerEvent as ReactPointerEvent,WheelEvent as ReactWheelEvent,KeyboardEvent as ReactKeyboardEvent} from 'react';
import type {ActorView,Project} from './model';
import {fitScene,sceneBounds,zoomScene,scenePlacementFree} from './authoring/scenePlacement';
import type {WorldPoint,WorldViewport} from './authoring/scenePlacement';

interface Drag {pointer:number;entity:ActorView|null;start:WorldPoint;point:WorldPoint;from:WorldPoint;moved:boolean}
export function useSceneViewport({project,sessionId,ready,actors,pause,aim,place,select,cast,release,cancelCast}:{project:Project;sessionId:string;ready:boolean;actors:ActorView[];
  pause:()=>void;aim:(point:WorldPoint)=>void;place:(actor:ActorView,point:WorldPoint)=>Promise<boolean>;select:(id:string)=>void;
  cast?:()=>void;release:()=>void;cancelCast:()=>void}) {
  const svg=useRef<SVGSVGElement>(null),[view,setView]=useState<WorldViewport>({x:-10,y:-9,width:24,height:18}),[ghost,setGhost]=useState<{actor:ActorView;point:WorldPoint;valid:boolean}|null>(null),[message,setMessage]=useState('');
  const current=useRef(project);current.current=project;
  const aspect=useRef(4/3),touched=useRef(false),drag=useRef<Drag|null>(null),firing=useRef<number|null>(null),pending=useRef(false),version=useRef(sessionId);version.current=sessionId;
  function releaseCapture(pointer:number){if(svg.current?.hasPointerCapture(pointer))svg.current.releasePointerCapture(pointer);}
  function finishFire(cancelled:boolean){
    const pointer=firing.current;if(pointer===null)return;
    firing.current=null;
    if(cancelled)cancelCast();else release();
    releaseCapture(pointer);
  }
  function reset(){touched.current=false;setView(fitScene(sceneBounds(current.current),aspect.current));setMessage('');}
  useEffect(()=>{
    const element=svg.current;if(!element)return;
    const resize=new ResizeObserver(entries=>{
      const r=entries[0]?.contentRect;if(!r||r.width<1||r.height<1)return;
      const ratio=r.width/r.height;aspect.current=ratio;
      setView(v=>touched.current?{...v,y:v.y+v.height/2-v.width/ratio/2,height:v.width/ratio}:fitScene(sceneBounds(current.current),ratio));
    });resize.observe(element);return()=>resize.disconnect();
  },[]);
  useEffect(()=>{cancel();pending.current=false;reset();},[sessionId]);
  useEffect(()=>{if(!ready)cancel();},[ready]);
  useEffect(()=>{
    window.addEventListener('blur',cancel);return()=>window.removeEventListener('blur',cancel);
  },[cancelCast]);
  function world(clientX:number,clientY:number):WorldPoint|null {
    const element=svg.current,matrix=element?.getScreenCTM();if(!element||!matrix)return null;
    const point=element.createSVGPoint();point.x=clientX;point.y=clientY;const result=point.matrixTransform(matrix.inverse());return {x:result.x,z:-result.y};
  }
  function down(e:ReactPointerEvent<SVGSVGElement>){
    if(pending.current||drag.current||firing.current!==null)return;const point=world(e.clientX,e.clientY);if(!point)return;
    const hit=(e.target as Element).closest('[data-scene-id]'),entity=hit?actors.find(a=>a.id===hit.getAttribute('data-entity-id'))??null:null;
    const panning=e.button===1||e.button===2||e.button===0&&e.shiftKey&&!entity;
    if(!panning&&(!entity||!ready||entity.dead)){
      if(e.button!==0)return;
      svg.current?.focus();aim(point);
      if(cast&&ready&&!entity&&!e.shiftKey&&!e.ctrlKey&&!e.metaKey&&!e.altKey){
        e.preventDefault();firing.current=e.pointerId;e.currentTarget.setPointerCapture(e.pointerId);cast();
      }
      return;
    }
    e.preventDefault();e.currentTarget.focus();e.currentTarget.setPointerCapture(e.pointerId);setMessage('');
    if(entity&&!panning){pause();select(entity.sceneId!);}
    if(panning)touched.current=true;
    drag.current={pointer:e.pointerId,entity:panning?null:entity,start:point,point,from:entity?{x:entity.x,z:entity.z}:point,moved:false};
  }
  function move(e:ReactPointerEvent<SVGSVGElement>){
    const point=world(e.clientX,e.clientY);if(!point)return;
    if(firing.current!==null){
      if(firing.current===e.pointerId){aim(point);if(!(e.buttons&1))finishFire(false);}
      return;
    }
    const active=drag.current;if(!active){if(!pending.current)aim(point);return;}
    if(active.pointer!==e.pointerId)return;
    if(!active.entity){
      const dx=point.x-active.start.x,dz=point.z-active.start.z;
      setView(v=>({...v,x:v.x-dx,y:v.y+dz}));return;
    }
    const next={x:active.from.x+point.x-active.start.x,z:active.from.z+point.z-active.start.z};
    const step=e.shiftKey?1:.01;next.x=Number((Math.round(next.x/step)*step).toFixed(2));next.z=Number((Math.round(next.z/step)*step).toFixed(2));
    active.moved||=Math.abs(next.x-active.from.x)+Math.abs(next.z-active.from.z)>.02;active.point=next;
    if(active.moved)setGhost({actor:active.entity,point:next,valid:scenePlacementFree(next,active.entity.radius,project.preview.obstacles??[])});
  }
  async function up(e:ReactPointerEvent<SVGSVGElement>){
    if(firing.current===e.pointerId){const point=world(e.clientX,e.clientY);if(point)aim(point);finishFire(false);return;}
    const active=drag.current;if(!active||active.pointer!==e.pointerId)return;
    drag.current=null;releaseCapture(e.pointerId);
    if(!active.entity||!active.moved){setGhost(null);return;}
    if(!scenePlacementFree(active.point,active.entity.radius,project.preview.obstacles??[])){setGhost(null);setMessage('位置与障碍重叠，保留原位置');return;}
    pending.current=true;const generation=version.current;
    try{const accepted=await place(active.entity,active.point);if(generation===version.current)setMessage(accepted?'位置已应用':'位置更新失败，保留原状态');}
    finally{if(generation===version.current){pending.current=false;setGhost(null);}}
  }
  function cancel(){const pointer=drag.current?.pointer;drag.current=null;finishFire(true);if(pointer!==undefined)releaseCapture(pointer);setGhost(null);}
  function wheel(e:ReactWheelEvent<SVGSVGElement>){
    if(drag.current||firing.current!==null)return;const point=world(e.clientX,e.clientY);if(point){touched.current=true;setView(v=>zoomScene(v,point,Math.exp(Math.max(-200,Math.min(200,e.deltaY))*.002)));}
  }
  function keyDown(e:ReactKeyboardEvent<SVGSVGElement>){
    if(!e.shiftKey||!['ArrowLeft','ArrowRight','ArrowUp','ArrowDown'].includes(e.key))return;
    e.preventDefault();e.stopPropagation();touched.current=true;
    const dx=e.key==='ArrowLeft'?-1:e.key==='ArrowRight'?1:0,dy=e.key==='ArrowUp'?-1:e.key==='ArrowDown'?1:0;
    setView(v=>({...v,x:v.x+dx*v.width*.1,y:v.y+dy*v.height*.1}));
  }
  return {svg,view,ghost,message,reset,down,move,up,cancel,wheel,keyDown};
}
