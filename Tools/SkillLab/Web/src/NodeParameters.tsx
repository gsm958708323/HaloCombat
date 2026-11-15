import {useEffect,useState} from 'react';
import type {GraphNode,Field,SummonBlueprint,BuffDefinition,Skill,SharedProjectile,SharedArea,ParameterValue,RuntimeView} from './model';
import {parameterValue,effectKinds} from './model';
import {NumericParameter} from './NumericParameter';
import {nodeFieldGroups,targetTimingDescription} from './authoring/nodePresentation';
import {deliveryMode,deliveryModes} from './authoring/deliveryEdits';

export type NodeData = GraphNode & { beginEdit:()=>void;endEdit:()=>void; definitionRoot?:boolean;behaviorLinked?:boolean;extractBehavior:(id:string)=>void;changeDelivery:(id:string,mode:string)=>void;authoredTimeline?:boolean;counts?:Record<string,number>;blueprints?: SummonBlueprint[];buffs?:BuffDefinition[];skills?:Skill[];projectiles?:SharedProjectile[];areas?:SharedArea[];views?:RuntimeView[];openArea:(id:string)=>void;extractArea:(id:string)=>void;openProjectile:(id:string)=>void;extractProjectile:(id:string)=>void;changeKind:(id:string,kind:string)=>void; change: (id: string, key: string, value: ParameterValue) => void; toggle: (id: string, enabled: boolean) => void; remove: (id: string) => void };
function TimesInput({field,value,change}:{field:Field;value:ParameterValue;change:(value:ParameterValue)=>void}) {
  const display=Array.isArray(value)?value.join(', '):String(value);const [text,setText]=useState(display);
  useEffect(()=>setText(display),[display]);
  return <input aria-label={field.label} type="text" value={text} onBlur={()=>setText(display)} onChange={e=>{setText(e.target.value);const parts=e.target.value.split(',');const values=parts.map(v=>Number(v.trim()));if(parts.every(p=>p.trim()!=='')&&values.every(Number.isFinite))change(values);}}/>;
}
function Parameter({ field, value, change,beginEdit,endEdit }: { field: Field; value:ParameterValue; change: (value:ParameterValue) => void;beginEdit:()=>void;endEdit:()=>void }) {
  return <label className="parameter nodrag nowheel"><span>{field.label}<small>{field.unit}</small></span>
    {field.type === 'times'?<TimesInput field={field} value={value} change={change}/>:field.type === 'select' ? <select aria-label={field.label} value={String(value)} onChange={e => change(e.target.value)}>{field.options?.map(([key,label]) => <option key={key} value={key}>{label}</option>)}</select>
      : field.type === 'check' ? <input aria-label={field.label} type="checkbox" checked={!!value} onChange={e => change(e.target.checked)} />
      : field.type === 'text' ? <input aria-label={field.label} type="text" maxLength={128} value={String(value)} onChange={e=>change(e.target.value)}/>
      : <NumericParameter field={field} value={Number(value)} change={change} beginEdit={beginEdit} endEdit={endEdit}/>}
  </label>;
}
export function NodeParameters({data,onToggle}:{data:NodeData;onToggle?:()=>void}) {
  const fields=nodeFieldGroups(data,{definitionRoot:data.definitionRoot,authoredTimeline:data.authoredTimeline,behaviorLinked:data.behaviorLinked});
  function control(field:Field){
    const options=field.key==='view'?[...(data.kind==='construct'?[['barrel','木桶'],['crate','箱子']] as [string,string][]:[]),...(data.views??[]).filter(v=>v.kind==='body').map(v=>[v.id,v.id] as [string,string])]:field.key==='area'?data.areas?.map(a=>[a.id,a.name] as [string,string]):field.key==='projectile'?data.projectiles?.map(p=>[p.id,p.name] as [string,string]):field.key==='blueprint'?data.blueprints?.map(b=>[b.id,b.name??b.id] as [string,string]):field.key==='buff'?data.buffs?.map(b=>[b.id,b.name??b.id] as [string,string]):
      field.key==='fallbackSkill'||field.key==='recastSkill'?data.skills?.map(s=>[s.id,s.name] as [string,string]):undefined;
    return <Parameter key={field.key} field={options?{...field,options:[['','无'],...options]}:field} value={parameterValue(field,data.parameters)} change={value=>data.change(data.id,field.key,value)} beginEdit={data.beginEdit} endEdit={data.endEdit}/>;
  }
  return (
    <div className="node-fields">
      {effectKinds.some(([kind])=>kind===data.kind)&&<label className="parameter nodrag nowheel"><span>结算类型</span><select aria-label="结算类型" value={data.kind} onChange={e=>data.changeKind(data.id,e.target.value)}>{effectKinds.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label>}
      {!data.definitionRoot&&data.kind !== 'target' && data.kind !== 'cast' && data.kind!=='buff-events'&&data.kind!=='skill-events'&&<label className="enable-row nodrag"><span>启用节点</span><input type="checkbox" checked={data.enabled !== false} onChange={e => data.toggle(data.id,e.target.checked)} /></label>}
      {['projectile','delivery'].includes(data.kind)&&!data.definitionRoot&&<label className="parameter nodrag nowheel"><span>效果传递</span><select aria-label="效果传递" value={deliveryMode(data)} onChange={e=>data.changeDelivery(data.id,e.target.value)}>{deliveryModes.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label>}
      {data.kind === 'damage' && <div className="module-label">效果结算 <b>伤害结算</b></div>}
      {data.kind==='cast'&&data.authoredTimeline&&<small>时序由“技能设置 / 定义设置”中的显式时间轴管理</small>}
      {fields.main.filter(f=>data.kind!=='delivery'||f.key!=='mode').map(control)}
      {data.kind==='delivery'&&data.parameters.mode==='summon'&&!data.parameters.blueprint&&<small>选择或新建召唤蓝图后可运行；攻击技能不能循环召唤自身。</small>}
      {data.kind==='target'&&targetTimingDescription(data)&&<small>{targetTimingDescription(data)}</small>}
      {data.kind==='projectile'&&!data.behaviorLinked&&<button className="nodrag" onClick={()=>data.extractBehavior(data.id)}>提取行为节点</button>}{fields.behaviors.length>0&&<details className="node-more-fields nodrag nowheel" onToggle={onToggle}><summary>投射物行为</summary><div>
        <label className="enable-row"><span>无限穿透</span><input aria-label="无限穿透" type="checkbox" checked={data.parameters.maxHits===0} onChange={e=>data.change(data.id,'maxHits',e.target.checked?0:1)}/></label>{fields.behaviors.map(control)}</div></details>}
      {fields.advanced.length>0&&<details className="node-more-fields nodrag nowheel" onToggle={onToggle}><summary>更多参数</summary><div>{fields.advanced.map(control)}</div></details>}
      {data.kind==='delivery'&&data.parameters.mode==='area'&&data.parameters.persistent===true&&!data.definitionRoot&&<button className="nodrag" onClick={()=>data.extractArea(data.id)}>提取为共享区域</button>}
      {data.kind==='area-ref'&&<button className="nodrag" disabled={!data.parameters.area} onClick={()=>data.openArea(String(data.parameters.area))}>编辑区域定义</button>}
      {data.kind==='projectile'&&!data.definitionRoot&&<button className="nodrag" onClick={()=>data.extractProjectile(data.id)}>提取为共享弹体</button>}
      {data.kind==='projectile-ref'&&<button className="nodrag" disabled={!data.parameters.projectile} onClick={()=>data.openProjectile(String(data.parameters.projectile))}>编辑弹体定义</button>}
    </div>
  );
}
