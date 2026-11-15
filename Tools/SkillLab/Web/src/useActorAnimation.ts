import {useCallback,useEffect,useLayoutEffect,useRef} from 'react';
import type {ActorView} from './model';

interface Pose{x:number;z:number}
interface Tween{from:Pose;to:Pose;start:number;duration:number}
const sample=(value:Tween,now:number):Pose=>{
  const alpha=value.duration>0?Math.max(0,Math.min(1,(now-value.start)/value.duration)):1;
  return {x:value.from.x+(value.to.x-value.from.x)*alpha,z:value.from.z+(value.to.z-value.from.z)*alpha};
};

// Rendering only: blend received poses; never predict simulation, collisions or damage.
export function useActorAnimation(actors:ActorView[],playing:boolean,sessionId:string){
  const elements=useRef(new Map<string,SVGGElement>()),tweens=useRef(new Map<string,Tween>());
  const arrival=useRef(0),session=useRef(sessionId);
  const bind=useCallback((id:string,element:SVGGElement|null)=>{if(element)elements.current.set(id,element);else elements.current.delete(id);},[]);
  function paint(now:number){for(const [id,tween] of tweens.current){const pose=sample(tween,now);elements.current.get(id)?.setAttribute('transform',`translate(${pose.x},${-pose.z})`);}}
  useLayoutEffect(()=>{
    const now=performance.now();if(session.current!==sessionId){tweens.current.clear();arrival.current=0;session.current=sessionId;}
    const duration=playing?Math.max(16,Math.min(100,now-arrival.current)):0;arrival.current=now;
    const ids=new Set(actors.map(a=>a.id));for(const id of tweens.current.keys())if(!ids.has(id))tweens.current.delete(id);
    for(const actor of actors){
      const old=tweens.current.get(actor.id),from=old?sample(old,now):actor;
      const smooth=playing&&old&&!actor.stopped&&!actor.dead&&Math.hypot(actor.x-from.x,actor.z-from.z)<4;
      tweens.current.set(actor.id,{from:smooth?from:actor,to:{x:actor.x,z:actor.z},start:now,duration:smooth?duration:0});
    }
    paint(now);
  },[actors,playing,sessionId]);
  useEffect(()=>{
    if(!playing)return;let frame=0;
    const draw=(now:number)=>{paint(now);frame=requestAnimationFrame(draw);};frame=requestAnimationFrame(draw);
    return()=>cancelAnimationFrame(frame);
  },[playing]);
  return bind;
}
