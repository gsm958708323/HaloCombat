import {useCallback, useLayoutEffect, useRef} from 'react';
import type {ActorView} from './model';

interface Pose {x:number; z:number}
interface Tween {from:Pose; to:Pose; start:number; duration:number}
interface Sprite {element:SVGGElement; pose?:Pose}

// Only this hook writes actor transforms. A snapshot also arrives for commands
// that do not advance time; those must not restart an in-flight movement.
export function useActorAnimation(actors:ActorView[], playing:boolean, sessionId:string, step:number) {
  const sprites=useRef(new Map<string,Sprite>()), moving=useRef(new Map<string,Tween>());
  const clock=useRef({sessionId, step, arrival:0}), frame=useRef<number|null>(null);
  const bind=useCallback((id:string,element:SVGGElement|null)=>{
    if(element) sprites.current.set(id,{element});
    else {sprites.current.delete(id); moving.current.delete(id);}
  },[]);

  function paint(sprite:Sprite, pose:Pose) {
    if(sprite.pose?.x===pose.x && sprite.pose.z===pose.z) return;
    sprite.element.setAttribute('transform',`translate(${pose.x},${-pose.z})`);
    sprite.pose=pose;
  }
  function draw() {
    frame.current=null;
    const now=performance.now();
    for(const [id,tween] of moving.current) {
      const sprite=sprites.current.get(id);
      if(!sprite) {moving.current.delete(id); continue;}
      const alpha=Math.min(1,(now-tween.start)/tween.duration);
      if(alpha===1) {paint(sprite,tween.to); moving.current.delete(id);}
      else paint(sprite,{x:tween.from.x+(tween.to.x-tween.from.x)*alpha,z:tween.from.z+(tween.to.z-tween.from.z)*alpha});
    }
    if(moving.current.size) frame.current=requestAnimationFrame(draw);
  }
  useLayoutEffect(()=>{
    const now=performance.now(), previous=clock.current;
    const reset=previous.sessionId!==sessionId || step<previous.step;
    const duration=Math.max(16,Math.min(100,now-previous.arrival));
    if(reset || step!==previous.step || !playing) clock.current={sessionId,step,arrival:now};
    for(const actor of actors) {
      const sprite=sprites.current.get(actor.id);
      if(!sprite) continue;
      const from=sprite.pose, tween=moving.current.get(actor.id);
      if(!playing || reset || !from || actor.stopped || actor.dead || Math.hypot(actor.x-from.x,actor.z-from.z)>=4) {
        moving.current.delete(actor.id);
        paint(sprite,{x:actor.x,z:actor.z});
      } else if(tween?.to.x===actor.x && tween.to.z===actor.z) {
        continue;
      } else if(from.x===actor.x && from.z===actor.z) {
        moving.current.delete(actor.id);
      } else {
        moving.current.set(actor.id,{from,to:{x:actor.x,z:actor.z},start:now,duration});
      }
    }
    if(moving.current.size && frame.current===null) frame.current=requestAnimationFrame(draw);
  },[actors,playing,sessionId,step]);
  useLayoutEffect(()=>()=>{
    if(frame.current!==null) cancelAnimationFrame(frame.current);
    frame.current=null;
  },[]);
  return bind;
}
