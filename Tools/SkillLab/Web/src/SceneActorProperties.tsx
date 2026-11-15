import type {Project,ActorInitial} from './model';
import {removeSceneTarget} from './authoring/scenePlacement';

export function SceneActorProperties({project,update,selectedId,select}:{project:Project;update:(value:Project)=>void;selectedId:string;select:(id:string)=>void}){
  const actors=[project.preview.caster,...project.preview.targets],actor=actors.find(a=>a.id===selectedId)??actors[0];
  function edit(key:keyof ActorInitial,text:string){
    if(text==='')return;const value=Number(text);if(!Number.isFinite(value)||key==='team'&&!Number.isInteger(value))return;
    update({...project,preview:{...project.preview,
      caster:project.preview.caster.id===actor.id?{...project.preview.caster,[key]:value}:project.preview.caster,
      targets:project.preview.targets.map(a=>a.id===actor.id?{...a,[key]:value}:a)}});
  }
  return <details className="scene-actors"><summary>试验角色属性</summary><div>
    <select aria-label="试验角色" value={actor.id} onChange={e=>select(e.target.value)}>{actors.map(a=><option key={a.id} value={a.id}>{a.id}</option>)}</select>
    {(['x','z'] as const).map(key=><label key={key}>{key.toUpperCase()}<input aria-label={'角色位置'+key.toUpperCase()} type="number" step=".1" value={actor[key]} onChange={e=>edit(key,e.target.value)}/></label>)}
    <label>阵营<input aria-label="试验角色阵营" type="number" min="0" max="30" step="1" value={actor.team??(actor.id===project.preview.caster.id?1:2)} onChange={e=>edit('team',e.target.value)}/></label>
    <label>碰撞半径<input aria-label="角色碰撞半径" type="number" min="0" max="5" step=".01" value={actor.collisionRadius??.4} onChange={e=>edit('collisionRadius',e.target.value)}/></label>
    {([['hp','初始生命',1000],['atk','攻击',80],['def','物理防御',0],['shield','初始护盾',0],['damageDealt','伤害加成倍率',1],['damageTaken','受伤倍率',1],['actionSpeed','动作速度',1]] as const).map(([key,name,fallback])=>
      <label key={key}>{name}<input aria-label={name} type="number" min={key==='hp'?1:0} step={key==='damageDealt'||key==='damageTaken'||key==='actionSpeed'?.1:1} value={actor[key]??fallback} onChange={e=>edit(key,e.target.value)}/></label>)}
    <small>修改后校验并重建预览；护盾与倍率由真实战斗核结算。</small>
    {actor.id!==project.preview.caster.id&&<button className="remove-scene-target" onClick={()=>update(removeSceneTarget(project,actor.id))}>移除当前目标</button>}
  </div></details>;
}
