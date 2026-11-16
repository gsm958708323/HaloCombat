import {useRef,useState} from 'react';
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

export function PreviewWorld({project,preview,inspected,update,select,selectedId,onBlur}:{project:Project;preview:ReturnType<typeof usePreview>;inspected?:ActorView;update:(p:Project)=>void;select:(id:string)=>void;selectedId:string;onBlur:()=>void}) {
  const state=preview.state,[error,setError]=useState(''),aimMarker=useRef<SVGPathElement>(null);
  const casterTeam=state?.actors.find(a=>a.kind==='caster')?.team??1;
  const actorElement=useActorAnimation(state?.actors??[],preview.playing,preview.sessionId);
  function edit(change:(project:Project)=>Project){try{setError('');update(change(project));}catch(error){setError(String(error));}}
  function aim(point:WorldPoint){preview.aim.current=point;aimMarker.current?.setAttribute('d',`M${point.x-.2},${-point.z}h.4m-.2,-.2v.4`);}
  const viewport=useSceneViewport({project,sessionId:preview.sessionId,ready:preview.ready,actors:state?.actors??[],select,aim,
    cast:preview.cast,release:preview.release,cancelCast:preview.cancel,
    pause:()=>preview.setPlaying(false),place:(actor,point)=>preview.place(actor.sceneId!,actor.id,point.x,point.z,update)});
  return <div className="world-canvas"><div className="scene-actions"><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>({...p,preview:restoreDefaultFormation(p.preview)}))}>默认布阵</button><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>addSceneTarget(p,{x:viewport.view.x+viewport.view.width/2,z:-viewport.view.y-viewport.view.height/2}))}>+ 目标</button><button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>addSceneObstacle(p,{x:viewport.view.x+viewport.view.width/2,z:-viewport.view.y-viewport.view.height/2}))}>+ 障碍</button><button onClick={viewport.reset}>复位视图</button>{project.preview.targets.some(a=>a.id===selectedId)&&<button disabled={preview.replaying||preview.placing} onClick={()=>edit(p=>removeSceneTarget(p,selectedId))}>移除目标</button>}<span>左键空地施放 · 拖动角色布置 · 滚轮缩放 · 右键或Shift方向键平移</span></div>
    <svg ref={viewport.svg} viewBox={`${viewport.view.x} ${viewport.view.y} ${viewport.view.width} ${viewport.view.height}`} role="img" tabIndex={0} aria-label="真实 C# 战斗核的俯视运行预览" onKeyDown={e=>{viewport.keyDown(e);if(e.key.startsWith('Arrow'))e.preventDefault();}} onBlur={()=>{viewport.cancel();onBlur();}} onPointerDown={viewport.down} onPointerMove={viewport.move} onPointerUp={e=>void viewport.up(e)} onPointerCancel={viewport.cancel} onLostPointerCapture={viewport.cancel} onWheel={viewport.wheel} onContextMenu={e=>e.preventDefault()}>
          <defs><pattern id="grid" width="1" height="1" patternUnits="userSpaceOnUse"><path d="M1 0H0V1" fill="none" stroke="#314047" strokeWidth=".025"/></pattern></defs><rect x={viewport.view.x} y={viewport.view.y} width={viewport.view.width} height={viewport.view.height} fill="url(#grid)"/>
          <SpatialVolumes volumes={state?.volumes??[]}/>
          {state?.obstacles.map(obstacle=><g key={obstacle.id} aria-label={`障碍 ${obstacle.id}`}><rect x={obstacle.x-obstacle.width/2} y={-obstacle.z-obstacle.depth/2} width={obstacle.width} height={obstacle.depth} fill="#4b555a" stroke="#c0c7c9" strokeWidth=".06"/></g>)}
          {state?.rays.map(ray=><line key={ray.sequence} x1={ray.fromX} y1={-ray.fromZ} x2={ray.toX} y2={-ray.toZ} stroke="#f6d797" strokeWidth={ray.width} strokeLinecap="round" opacity={Math.max(.1,1-(state.step-ray.step)/26)}/>)}
          {state?.areas.map(a=><circle key={a.sequence} cx={a.x} cy={-a.z} r={a.radius} fill="#e3b573" fillOpacity=".08" stroke="#deb977" strokeWidth=".06" opacity={Math.max(.05,1-(state.step-a.step)/26)}/>)}
          {state?.actors.filter(a=>a.kind==='aoe').map(a=><g key={a.id} ref={element=>actorElement(a.id,element)} transform={`translate(${a.x},${-a.z})`}><circle r={a.radius} fill="#817ba3" fillOpacity=".15" stroke="#b1a4db" strokeWidth=".05"/><text y={-a.radius-.2} textAnchor="middle" fill="#c3b8df" fontSize=".25">{a.remaining?.toFixed(1)}s</text></g>)}
          {state?.actors.filter(a=>a.kind!=='aoe').map(a=><g key={a.id} ref={element=>actorElement(a.id,element)} data-scene-id={a.sceneId??undefined} data-entity-id={a.id} aria-label={a.sceneId?`布置 ${a.name}`:undefined} style={{cursor:a.sceneId&&!a.dead?'grab':undefined}} opacity={a.dead?.4:1} transform={`translate(${a.x},${-a.z})`}>
            {a.kind==="projectile"&&a.y>0&&<><ellipse rx={Math.max(.12,a.radius*1.4)} ry={Math.max(.05,a.radius*.6)} fill="#0008"/><line x1="0" x2="0" y1="0" y2={-a.y*.5} stroke="#b5c9c5" strokeWidth=".025"/><text x=".2" y={-a.y*.5} fill="#cfdfde" fontSize=".22">h {a.y.toFixed(2)}</text></>}<circle cy={a.kind==="projectile"?-Math.max(0,a.y??0)*.5:0} r={Math.max(.08,a.radius)} strokeDasharray={a.stopped?".1 .07":undefined} fill={a.kind==='caster'?'#3b746a':a.kind==='projectile'?'#e8d7a1':a.kind==='summon'||a.team===casterTeam?'#4c9568':a.kind==='construct'?'#b37b3c':'#95664b'} stroke={a.hurtAge!=null&&a.hurtAge<.18?'#fff':a.kind==='caster'?'#a5d5c7':'#e5c2a0'} strokeWidth=".05"/>
            {a.sceneId===selectedId&&<circle r={Math.max(.35,a.radius)+.12} fill="none" stroke="#c8d7a3" strokeWidth=".04" strokeDasharray=".1 .05"/>}{a.kind!=='projectile'&&<><text y=".12" textAnchor="middle" fill="#fff" fontSize=".3">{a.kind==='caster'?'C':a.name.match(/\d+$/)?.[0]??a.name.slice(0,2)}</text><rect x="-.5" y="-.8" width="1" height=".06" fill="#39454a"/><rect x="-.5" y="-.8" width={Math.max(0,(a.hp??0)/(a.maxHp??1))} height=".06" fill="#d3bf92"/><text y=".85" textAnchor="middle" fill="#becad0" fontSize=".22">{a.hp?.toFixed(0)} / {a.maxHp?.toFixed(0)}</text>{a.buffs?.length>0&&<text y="1.1" textAnchor="middle" fill="#b1d3a6" fontSize=".2">{a.buffs.map(b=>`${b.name}×${b.stacks}`).join(' ' )}</text>}</>}
          </g>)}
          <DamageFloaters events={state?.events??[]} actors={state?.actors??[]} sessionId={preview.sessionId}/>
          <path ref={aimMarker} pointerEvents="none" d={`M${preview.aim.current.x-.2},${-preview.aim.current.z}h.4m-.2,-.2v.4`} stroke="#b0c8cc" strokeWidth=".04"/>
        {viewport.ghost&&<g className="placement-ghost" pointerEvents="none"><line x1={viewport.ghost.actor.x} y1={-viewport.ghost.actor.z} x2={viewport.ghost.point.x} y2={-viewport.ghost.point.z} stroke={viewport.ghost.valid?"#a6daca":"#ed9e83"} strokeWidth=".06" strokeDasharray=".2 .15"/><circle cx={viewport.ghost.point.x} cy={-viewport.ghost.point.z} r={Math.max(.15,viewport.ghost.actor.radius)} fill={viewport.ghost.valid?"#7cbfa450":"#c75d5050"} stroke={viewport.ghost.valid?"#a6daca":"#ed9e83"} strokeWidth=".08"/></g>}{inspected&&<circle pointerEvents="none" aria-label="已定位实体" cx={inspected.x} cy={-inspected.z} r={Math.max(.5,inspected.radius)+.15} fill="none" stroke="#fff3af" strokeWidth=".12"/>}</svg>
      <small className="scale-note">1 GRID = 1 m</small>{(viewport.message||error)&&<div className="scene-message" role="status">{error||viewport.message}</div>}
      </div>
}
