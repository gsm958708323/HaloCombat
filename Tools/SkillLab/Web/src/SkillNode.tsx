import { Handle, Position, useUpdateNodeInternals } from '@xyflow/react';
import { useEffect } from 'react';
import type { Node, NodeProps } from '@xyflow/react';
import type { GraphNode, Field, SummonBlueprint,BuffDefinition } from './model';
import { kinds, portName, parameterValue, nodeOutputs,effectKinds } from './model';
type Data = GraphNode & { counts?:Record<string,number>;blueprints?: SummonBlueprint[];buffs?:BuffDefinition[];changeKind:(id:string,kind:string)=>void; change: (id: string, key: string, value: string | number | boolean) => void; toggle: (id: string, enabled: boolean) => void; remove: (id: string) => void };
export type EditorNode = Node<Data & Record<string, unknown>, 'skill'>;
function Parameter({ field, value, change }: { field: Field; value: string | number | boolean; change: (value: string | number | boolean) => void }) {
  return <label className="parameter nodrag nowheel"><span>{field.label}<small>{field.unit}</small></span>
    {field.type === 'select' ? <select aria-label={field.label} value={String(value)} onChange={e => change(e.target.value)}>{field.options?.map(([key,label]) => <option key={key} value={key}>{label}</option>)}</select>
      : field.type === 'check' ? <input aria-label={field.label} type="checkbox" checked={!!value} onChange={e => change(e.target.checked)} />
      : <div className="number-pair"><input aria-label={`${field.label}滑条`} type="range" min={field.min} max={field.max} step={field.step} value={Number(value)} onChange={e => change(Number(e.target.value))} /><input aria-label={field.label} type="number" min={field.min} max={field.max} step={field.step} value={Number(value)} onChange={e => { if (e.target.value !== '') change(Number(e.target.value)); }} /></div>}
  </label>;
}
export function SkillNode({ data, selected }: NodeProps<EditorNode>) {
  const schema = kinds[data.kind];
  const update=useUpdateNodeInternals();const ports=nodeOutputs(data);const portKey=ports.map(([key])=>key).join(',');
  useEffect(()=>update(data.id),[data.id,portKey,update]);
  if (!schema) return <div className="skill-node">未知节点：{data.kind}</div>;
  return <div className={`skill-node ${selected ? 'selected' : ''} ${data.enabled === false ? 'disabled' : ''}`} style={{ '--node-color': schema.color } as React.CSSProperties}>
    <div className="node-heading"><strong>{schema.title}</strong>{data.kind!=='buff-events'&&<button className="nodrag icon-button" aria-label={`删除${schema.title}节点`} onClick={() => data.remove(data.id)}>×</button>}</div>
    <div className="node-ports">
      {schema.inputs.map(port => <div className="port-row" key={port}><Handle type="target" id={port} position={Position.Left} /><span>{portName(port)}</span></div>)}
      {ports.map(([port,label]) => <div className="port-row output" key={port}><span>{label} <small aria-label={`${label}连接数`}>{data.counts?.[port]??0}</small></span><Handle type="source" id={port} position={Position.Right} /></div>)}
    </div>
    <div className="node-fields">
      {effectKinds.some(([kind])=>kind===data.kind)&&<label className="parameter nodrag nowheel"><span>结算类型</span><select aria-label="结算类型" value={data.kind} onChange={e=>data.changeKind(data.id,e.target.value)}>{effectKinds.map(([key,name])=><option key={key} value={key}>{name}</option>)}</select></label>}
      {data.kind !== 'target' && data.kind !== 'cast' && data.kind!=='buff-events'&&data.kind!=='skill-events'&&<label className="enable-row nodrag"><span>启用节点</span><input type="checkbox" checked={data.enabled !== false} onChange={e => data.toggle(data.id,e.target.checked)} /></label>}
      {data.kind === 'projectile' && <div className="module-label">效果传递 <b>投射物</b></div>}
      {data.kind === 'projectile' && <label className="enable-row nodrag"><span>无限穿透</span><input aria-label="无限穿透" type="checkbox" checked={data.parameters.maxHits===0} onChange={e=>data.change(data.id,'maxHits',e.target.checked?0:1)}/></label>}
      {data.kind === 'damage' && <div className="module-label">效果结算 <b>伤害结算</b></div>}
      {schema.fields.filter(field => !field.when || field.when(data.parameters)).map(field => <Parameter key={field.key} field={field.key==='blueprint'?{...field,options:[['','选择蓝图'],...(data.blueprints??[]).map(b=>[b.id,b.name??b.id] as [string,string])]}:field.key==='buff'?{...field,options:[['','选择Buff'],...(data.buffs??[]).map(b=>[b.id,b.name??b.id] as [string,string])]}:field} value={parameterValue(field,data.parameters)} change={value => data.change(data.id,field.key,value)} />)}
    </div>
  </div>;
}
