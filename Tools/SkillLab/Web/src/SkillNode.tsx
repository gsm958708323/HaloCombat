import { Handle, Position, useUpdateNodeInternals } from '@xyflow/react';
import { useEffect,useState } from 'react';
import type { Node, NodeProps } from '@xyflow/react';
import type { GraphNode, Field, SummonBlueprint,BuffDefinition,Skill,SharedProjectile,SharedArea,ParameterValue,RuntimeView } from './model';
import { kinds, portName, parameterValue,effectKinds } from './model';
import {NumericParameter} from './NumericParameter';
import {nodeFieldGroups,nodePortGroups,targetTimingDescription} from './authoring/nodePresentation';
type Data = GraphNode & { beginEdit:()=>void;endEdit:()=>void; definitionRoot?:boolean;behaviorLinked?:boolean;extractBehavior:(id:string)=>void;authoredTimeline?:boolean;counts?:Record<string,number>;blueprints?: SummonBlueprint[];buffs?:BuffDefinition[];skills?:Skill[];projectiles?:SharedProjectile[];areas?:SharedArea[];views?:RuntimeView[];openArea:(id:string)=>void;extractArea:(id:string)=>void;openProjectile:(id:string)=>void;extractProjectile:(id:string)=>void;changeKind:(id:string,kind:string)=>void; change: (id: string, key: string, value: ParameterValue) => void; toggle: (id: string, enabled: boolean) => void; remove: (id: string) => void };
export type EditorNode = Node<Data & Record<string, unknown>, 'skill'>;
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
export function SkillNode({ data, selected }: NodeProps<EditorNode>) {
  const schema = kinds[data.kind];
  const update=useUpdateNodeInternals(),portGroups=nodePortGroups(data,data.counts);const portKey=portGroups.main.map(([key])=>key).join(',')+'|'+portGroups.extra.map(([key])=>key).join(',');
  useEffect(()=>update(data.id),[data.id,portKey,update]);
  if (!schema) return <div className="skill-node">未知节点：{data.kind}</div>;
  const title=data.kind==='projectile-behavior'?(data.parameters.mode==='chain'?'连锁行为':'穿透行为'):schema.title;
  const fields=nodeFieldGroups(data,{definitionRoot:data.definitionRoot,authoredTimeline:data.authoredTimeline,behaviorLinked:data.behaviorLinked});
  const updatePorts=()=>requestAnimationFrame(()=>update(data.id));
  const output=([port,label]:[string,string])=><div className="port-row output" key={port}><span>{label} <small aria-label={`${label}连接数`}>{data.counts?.[port]??0}</small></span><Handle type="source" id={port} position={Position.Right} style={port==='behavior'?{background:'#88ad91'}:undefined}/></div>;
  function control(field:Field){
    const options=field.key==='view'?[...(data.kind==='construct'?[['barrel','木桶'],['crate','箱子']] as [string,string][]:[]),...(data.views??[]).filter(v=>v.kind==='body').map(v=>[v.id,v.id] as [string,string])]:field.key==='area'?data.areas?.map(a=>[a.id,a.name] as [string,string]):field.key==='projectile'?data.projectiles?.map(p=>[p.id,p.name] as [string,string]):field.key==='blueprint'?data.blueprints?.map(b=>[b.id,b.name??b.id] as [string,string]):field.key==='buff'?data.buffs?.map(b=>[b.id,b.name??b.id] as [string,string]):
      field.key==='fallbackSkill'||field.key==='recastSkill'?data.skills?.map(s=>[s.id,s.name] as [string,string]):undefined;
    return <Parameter key={field.key} field={options?{...field,options:[['','无'],...options]}:field} value={parameterValue(field,data.parameters)} change={value=>data.change(data.id,field.key,value)} beginEdit={data.beginEdit} endEdit={data.endEdit}/>;
  }
  return <div className={`skill-node ${selected ? 'selected' : ''} ${data.enabled === false ? 'disabled' : ''}`} style={{ '--node-color': schema.color } as React.CSSProperties}>
    <div className="node-heading"><strong>{title}</strong>{data.kind!=='buff-events'&&!data.definitionRoot&&<button className="nodrag icon-button" aria-label={`删除${title}节点`} onClick={() => data.remove(data.id)}>×</button>}</div>
    <div className="node-ports">
      {schema.inputs.filter(port=>!data.definitionRoot||port==='behavior').map(port => <div className="port-row" key={port}><Handle type="target" id={port} position={Position.Left} style={port==='behavior'?{background:'#88ad91'}:undefined}/><span>{portName(port)}</span></div>)}
      {portGroups.main.map(output)}
      {portGroups.extra.length>0&&<details className="node-extra-events nodrag nowheel" onToggle={updatePorts}><summary>更多事件 · {portGroups.extra.length}</summary>{portGroups.extra.map(output)}</details>}
    </div>
    <div className="node-fields">
      {effectKinds.some(([kind])=>kind===data.kind)&&<label className="parameter nodrag nowheel"><span>结算类型</span><select aria-label="结算类型" value={data.kind} onChange={e=>data.changeKind(data.id,e.target.value)}>{effectKinds.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label>}
      {!data.definitionRoot&&data.kind !== 'target' && data.kind !== 'cast' && data.kind!=='buff-events'&&data.kind!=='skill-events'&&<label className="enable-row nodrag"><span>启用节点</span><input type="checkbox" checked={data.enabled !== false} onChange={e => data.toggle(data.id,e.target.checked)} /></label>}
      {data.kind === 'projectile' && <div className="module-label">效果传递 <b>投射物</b></div>}
      {data.kind === 'damage' && <div className="module-label">效果结算 <b>伤害结算</b></div>}
      {data.kind==='cast'&&data.authoredTimeline&&<small>时序由右侧显式时间轴管理</small>}
      {fields.main.map(control)}
      {data.kind==='target'&&targetTimingDescription(data)&&<small>{targetTimingDescription(data)}</small>}
      {data.kind==='projectile'&&!data.behaviorLinked&&<button className="nodrag" onClick={()=>data.extractBehavior(data.id)}>提取行为节点</button>}{fields.behaviors.length>0&&<details className="node-more-fields nodrag nowheel" onToggle={updatePorts}><summary>投射物行为</summary><div>
        <label className="enable-row"><span>无限穿透</span><input aria-label="无限穿透" type="checkbox" checked={data.parameters.maxHits===0} onChange={e=>data.change(data.id,'maxHits',e.target.checked?0:1)}/></label>{fields.behaviors.map(control)}</div></details>}
      {fields.advanced.length>0&&<details className="node-more-fields nodrag nowheel" onToggle={updatePorts}><summary>更多参数</summary><div>{fields.advanced.map(control)}</div></details>}
      {data.kind==='delivery'&&data.parameters.mode==='area'&&data.parameters.persistent===true&&!data.definitionRoot&&<button className="nodrag" onClick={()=>data.extractArea(data.id)}>提取为共享区域</button>}
      {data.kind==='area-ref'&&<button className="nodrag" disabled={!data.parameters.area} onClick={()=>data.openArea(String(data.parameters.area))}>编辑区域定义</button>}
      {data.kind==='projectile'&&!data.definitionRoot&&<button className="nodrag" onClick={()=>data.extractProjectile(data.id)}>提取为共享弹体</button>}
      {data.kind==='projectile-ref'&&<button className="nodrag" disabled={!data.parameters.projectile} onClick={()=>data.openProjectile(String(data.parameters.projectile))}>编辑弹体定义</button>}
    </div>
  </div>;
}
