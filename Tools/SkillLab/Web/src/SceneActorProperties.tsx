import {useState} from 'react';
import type {Project,ActorInitial} from './model';

export function SceneActorProperties({project,update}:{project:Project;update:(value:Project)=>void}){
  const [selected,setSelected]=useState(project.preview.caster.id);
  const actors=[project.preview.caster,...project.preview.targets],actor=actors.find(a=>a.id===selected)??actors[0];
  function edit(key:keyof ActorInitial,text:string){
    if(text==='')return;const value=Number(text);if(!Number.isFinite(value))return;
    update({...project,preview:{...project.preview,
      caster:project.preview.caster.id===actor.id?{...project.preview.caster,[key]:value}:project.preview.caster,
      targets:project.preview.targets.map(a=>a.id===actor.id?{...a,[key]:value}:a)}});
  }
  return <details className="scene-actors"><summary>试验角色属性</summary><div>
    <select aria-label="试验角色" value={actor.id} onChange={e=>setSelected(e.target.value)}>{actors.map(a=><option key={a.id} value={a.id}>{a.id}</option>)}</select>
    {([['hp','初始生命',1000],['atk','攻击',80],['def','物理防御',0],['shield','初始护盾',0],['damageDealt','伤害加成倍率',1],['damageTaken','受伤倍率',1]] as const).map(([key,name,fallback])=>
      <label key={key}>{name}<input aria-label={name} type="number" min={key==='hp'?1:0} step={(key==='damageDealt'||key==='damageTaken') ? .1 : 1} value={actor[key]??fallback} onChange={e=>edit(key,e.target.value)}/></label>)}
    <small>修改后校验并重建预览；护盾与倍率由真实战斗核结算。</small>
  </div></details>;
}
