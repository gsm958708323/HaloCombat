import {Handle,Position,useUpdateNodeInternals} from '@xyflow/react';
import {useEffect} from 'react';
import type {Node,NodeProps} from '@xyflow/react';
import type {NodeData} from './NodeParameters';
import {NodeParameters} from './NodeParameters';
import {kinds,portName} from './model';
import {nodePortGroups} from './authoring/nodePresentation';

export type EditorNode=Node<NodeData & Record<string,unknown>,'skill'>;
export function SkillNode({data,selected}:NodeProps<EditorNode>) {
  const schema=kinds[data.kind];
  const update=useUpdateNodeInternals(),ports=nodePortGroups(data,data.counts);
  const portKey=ports.main.map(([key])=>key).join(',')+'|'+ports.extra.map(([key])=>key).join(',');
  useEffect(()=>update(data.id),[data.id,portKey,update]);
  if(!schema)return <div className="skill-node">未知节点：{data.kind}</div>;
  const title=data.kind==='projectile-behavior'?(data.parameters.mode==='chain'?'连锁行为':'穿透行为'):schema.title;
  const updatePorts=()=>requestAnimationFrame(()=>update(data.id));
  const output=([port,label]:[string,string])=><div className="port-row output" key={port}><span>{label} <small aria-label={`${label}连接数`}>{data.counts?.[port]??0}</small></span><Handle type="source" id={port} position={Position.Right} style={port==='behavior'?{background:'#88ad91'}:undefined}/></div>;
  return <div className={`skill-node ${selected?'selected':''} ${data.enabled===false?'disabled':''}`} style={{'--node-color':schema.color} as React.CSSProperties}>
    <div className="node-heading"><strong>{title}</strong>{data.kind!=='buff-events'&&!data.definitionRoot&&<button className="nodrag icon-button" aria-label={`删除${title}节点`} onClick={()=>data.remove(data.id)}>×</button>}</div>
    <div className="node-ports">{schema.inputs.filter(port=>!data.definitionRoot||port==='behavior').map(port=><div className="port-row" key={port}><Handle type="target" id={port} position={Position.Left} style={port==='behavior'?{background:'#88ad91'}:undefined}/><span>{portName(port)}</span></div>)}
      {ports.main.map(output)}{ports.extra.length>0&&<details className="node-extra-events nodrag nowheel" onToggle={updatePorts}><summary>更多事件 · {ports.extra.length}</summary>{ports.extra.map(output)}</details>}
    </div><NodeParameters data={data} onToggle={updatePorts}/>
  </div>;
}