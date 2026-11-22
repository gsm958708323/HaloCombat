import {useRef,useState} from 'react';
import {PreviewActor} from './PreviewActor';
import type {ActorView,Project} from './model';
import type {usePreview} from './usePreview';
import {SpatialVolumes} from './SpatialVolumes';
import {useSceneViewport} from './useSceneViewport';
import {addSceneTarget,addSceneObstacle,removeSceneTarget} from './authoring/scenePlacement';
import type {WorldPoint} from './authoring/scenePlacement';
import './scene-placement.css';
import {useActorAnimation} from './useActorAnimation';
import {DamageFloaters} from './DamageFloaters';
import {restoreDefaultFormation} from './authoring/defaultScene';

export function PreviewWorld({project,preview,inspected,update,select,selectedId,onBlur,aimOnly=false}:{project:Project;preview:ReturnType<typeof usePreview>;inspected?:ActorView;update:(p:Project)=>void;select:(id:string)=>void;selectedId:string;onBlur:()=>void;aimOnly?:boolean}) {
  const state=preview.state,[error,setError]=useState(''),aimMarker=useRef<SVGPathElement>(null);
  const casterTeam=state?.actors.find(a=>a.kind==='caster')?.team??1;
  const actorElement=useActorAnimation(state?.actors??[],preview.playing,preview.sessionId,state?.step??0);
  function edit(change:(project:Project)=>Project){try{setError('');update(change(project));}catch(error){setError(String(error));}}
  function aim(point:WorldPoint){preview.aim.current=point;aimMarker.current?.setAttribute('d',`M${point.x-.2},${-point.z}h.4m-.2,-.2v.4`);}
  const viewport=useSceneViewport({project,sessionId:preview.sessionId,ready:preview.ready,actors:state?.actors??[],select,aim,
    cast:aimOnly?undefined:preview.cast,release:preview.release,cancelCast:preview.cancel,
    pause:()=>preview.setPlaying(false),place:(actor,point)=>preview.place(actor.sceneId!,actor.id,point.x,point.z,update)});
  return <div className="world-canvas"><div className="scene-actions"><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>({...p,preview:restoreDefaultFormation(p.preview)}))}>默认布阵</button><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>addSceneTarget(p,{x:viewport.view.x+viewport.view.width/2,z:-viewport.view.y-viewport.view.height/2}))}>+ 目标</button><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>addSceneObstacle(p,{x:viewport.view.x+viewport.view.width/2,z:-viewport.view.y-viewport.view.height/2}))}>+ 障碍</button><button onClick={viewport.reset}>复位视图</button>{project.preview.targets.some(a=>a.id===selectedId)&&<button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>removeSceneTarget(p,selectedId))}>移除目标</button>}<span>{aimOnly?'左键空地瞄准':'左键空地施放'} · 拖动角色布置 · 滚轮缩放 · 右键或Shift方向键平移</span></div>
    <svg ref={viewport.svg} viewBox={`${viewport.view.x} ${viewport.view.y} ${viewport.view.width} ${viewport.view.height}`} role="img" tabIndex={0} aria-label="真实 C# 战斗核的俯视运行预览" onKeyDown={e=>{viewport.keyDown(e);if(e.key.startsWith('Arrow'))e.preventDefault();}} onBlur={()=>{viewport.cancel();onBlur();}} onPointerDown={viewport.down} onPointerMove={viewport.move} onPointerUp={e=>void viewport.up(e)} onPointerCancel={viewport.cancel} onLostPointerCapture={viewport.cancel} onWheel={viewport.wheel} onContextMenu={e=>e.preventDefault()}>
          <defs><pattern id="grid" width="1" height="1" patternUnits="userSpaceOnUse"><path d="M1 0H0V1" fill="none" stroke="#314047" strokeWidth=".025"/></pattern></defs><rect x={viewport.view.x} y={viewport.view.y} width={viewport.view.width} height={viewport.view.height} fill="url(#grid)"/>
          <SpatialVolumes volumes={state?.volumes??[]}/>
          {state?.obstacles.map(obstacle=><g key={obstacle.id} aria-label={`障碍 ${obstacle.id}`}><rect x={obstacle.x-obstacle.width/2} y={-obstacle.z-obstacle.depth/2} width={obstacle.width} height={obstacle.depth} fill="#4b555a" stroke="#c0c7c9" strokeWidth=".06"/></g>)}
          {state?.rays.map(ray=><line key={ray.sequence} x1={ray.fromX} y1={-ray.fromZ} x2={ray.toX} y2={-ray.toZ} stroke="#f6d797" strokeWidth={ray.width} strokeLinecap="round" opacity={Math.max(.1,1-(state.step-ray.step)/26)}/>)}
          {state?.areas.map(a=><circle key={a.sequence} cx={a.x} cy={-a.z} r={a.radius} fill="#e3b573" fillOpacity=".08" stroke="#deb977" strokeWidth=".06" opacity={Math.max(.05,1-(state.step-a.step)/26)}/>)}
          {state && [...state.actors.filter(a=>a.kind==='aoe'),...state.actors.filter(a=>a.kind!=='aoe')].map(a=><PreviewActor
            key={a.id} id={a.id} sceneId={a.sceneId} name={a.name} kind={a.kind} radius={a.radius} innerRadius={a.innerRadius} height={a.y??0}
            hp={a.hp} maxHp={a.maxHp} remaining={a.kind==='aoe'?a.remaining?.toFixed(1):undefined} dead={a.dead} stopped={a.stopped}
            hurt={a.hurtAge!=null&&a.hurtAge<.18} ally={a.team===casterTeam} selected={a.sceneId===selectedId}
            buffs={a.buffs?.map(buff=>`${buff.name}×${buff.stacks}`).join(' ')} bind={actorElement}/>)}
          <DamageFloaters events={state?.events??[]} actors={state?.actors??[]} sessionId={preview.sessionId}/>
          <path ref={aimMarker} pointerEvents="none" d={`M${preview.aim.current.x-.2},${-preview.aim.current.z}h.4m-.2,-.2v.4`} stroke="#b0c8cc" strokeWidth=".04"/>
        {viewport.ghost&&<g className="placement-ghost" pointerEvents="none"><line x1={viewport.ghost.actor.x} y1={-viewport.ghost.actor.z} x2={viewport.ghost.point.x} y2={-viewport.ghost.point.z} stroke={viewport.ghost.valid?"#a6daca":"#ed9e83"} strokeWidth=".06" strokeDasharray=".2 .15"/><circle cx={viewport.ghost.point.x} cy={-viewport.ghost.point.z} r={Math.max(.15,viewport.ghost.actor.radius)} fill={viewport.ghost.valid?"#7cbfa450":"#c75d5050"} stroke={viewport.ghost.valid?"#a6daca":"#ed9e83"} strokeWidth=".08"/></g>}{inspected&&<circle pointerEvents="none" aria-label="已定位实体" cx={inspected.x} cy={-inspected.z} r={Math.max(.5,inspected.radius)+.15} fill="none" stroke="#fff3af" strokeWidth=".12"/>}</svg>
      <small className="scale-note">1 GRID = 1 m</small>{(viewport.message||error)&&<div className="scene-message" role="status">{error||viewport.message}</div>}
      </div>
}
