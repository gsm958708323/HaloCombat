import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { ReactFlow, ReactFlowProvider, Background, BackgroundVariant, applyNodeChanges, useReactFlow } from '@xyflow/react';
import type { Connection, Edge, NodeChange } from '@xyflow/react';
import { Plus, Search, Save, Download, Upload, Undo2, Redo2, Maximize, Workflow } from 'lucide-react';
import { api, clone, kinds, parameterValue, nodeOutputs, deliveryLabel } from './model';
import type { Project, Skill, BuffDefinition } from './model';
import { SkillNode } from './SkillNode';
import type { EditorNode } from './SkillNode';
import { usePreview } from './usePreview';
import { Preview } from './Preview';
import { SummonBlueprints } from './SummonBlueprints';
import { BuffProperties } from './BuffProperties';
type EditableGraph=Pick<Skill,'id'|'name'|'nodes'|'edges'>;

const nodeTypes = { skill: SkillNode };
function Workbench() {
  const [project,setProject] = useState<Project | null>(null);
  const [selected,setSelected] = useState('');
  const [editingBuff,setEditingBuff]=useState('');
  const [message,setMessage] = useState('正在连接本地 C# 宿主…');
  const [saved,setSaved] = useState(true);
  const [query,setQuery] = useState('');
  const [library,setLibrary] = useState(false);
  const [adding,setAdding] = useState(false);
  const [newName,setNewName] = useState<string | null>(null);
  const [examples,setExamples] = useState<Skill[]>([]);
  const [template,setTemplate] = useState('copy');
  const [creating,setCreating] = useState(false);
  const [selectedNodes,setSelectedNodes] = useState<string[]>([]);
  const history=useRef<Project[]>([]); const future=useRef<Project[]>([]);
  const fileInput=useRef<HTMLInputElement>(null);
  const current=useRef(project); current.current=project;
  const flow=useReactFlow<EditorNode>();
  const skill=project?.skills.find(s=>s.id===selected)??project?.skills[0];
  const activeId=skill?.id??selected;
  const buff=project?.buffs?.find(b=>b.id===editingBuff);
  const graph=buff??skill;
  const preview=usePreview(project,activeId,setMessage);
  preview.skill.current=skill?.id??'';
  const mode=String(skill?.nodes.find(n=>n.kind==='cast'&&n.enabled!==false)?.parameters.mode??'instant');
  const castMode=({instant:'瞬发',channel:'引导',charge:'蓄力'} as Record<string,string>)[mode]??mode;
  const transfers=[...new Set(skill?.nodes.filter(n=>n.enabled!==false&&(n.kind==='projectile'||n.kind==='delivery')).map(deliveryLabel))].join(' / ')||'效果';
  const description=mode==='charge'?'按住积累蓄力，达到最小时长后松开释放；满蓄力与倍率由节点配置。':mode==='channel'?'按住持续引导，松开停止后续触发，可边移动边施放。':'按目标规则执行'+transfers+'，并依次投递连接的效果。';
  useEffect(()=>{void api<Project>('/project',undefined,'GET').then(p=>{setProject(p);setSelected(p.skills[0].id);}).catch(e=>setMessage(String(e)));},[]);
  useEffect(()=>{void api<Project>('/examples',undefined,'GET').then(p=>setExamples(p.skills)).catch(e=>setMessage(String(e)));},[]);
  const edit=useCallback((next:Project,remember=true)=>{
    if(remember&&current.current){history.current.push(clone(current.current));if(history.current.length>60)history.current.shift();future.current=[];}
    setProject(next);setSaved(false);
  },[]);
  const editGraph=useCallback((update:(graph:EditableGraph)=>EditableGraph)=>{
    const p=current.current;if(!p)return;
    if(editingBuff&&p.buffs?.some(b=>b.id===editingBuff))edit({...p,buffs:p.buffs.map(b=>b.id===editingBuff?{...b,...update(b)}:b)});
    else edit({...p,skills:p.skills.map(s=>s.id===activeId?{...s,...update(s)}:s)});
  },[edit,activeId,editingBuff]);
  const parameter=useCallback((id:string,key:string,value:string|number|boolean)=>editGraph(s=>{
    const nodes=s.nodes.map(n=>n.id===id?{...n,parameters:{...n.parameters,[key]:value}}:n);
    const source=nodes.find(n=>n.id===id)!;const ports=nodeOutputs(source).map(([port])=>port);
    return {...s,nodes,edges:s.edges.filter(e=>e.source!==id||ports.includes(e.sourcePort))};
  }),[editGraph]);
  const remove=useCallback((id:string)=>editGraph(s=>({...s,nodes:s.nodes.filter(n=>n.id!==id),edges:s.edges.filter(e=>e.source!==id&&e.target!==id)})),[editGraph]);
  const changeKind=useCallback((id:string,kind:string)=>editGraph(s=>{
    const nodes=s.nodes.map(n=>n.id===id?{...n,kind,parameters:Object.fromEntries(kinds[kind].fields.map(f=>[f.key,f.initial]))}:n);
    const ports=nodeOutputs(nodes.find(n=>n.id===id)!).map(([port])=>port);
    return {...s,nodes,edges:s.edges.filter(e=>e.source!==id||ports.includes(e.sourcePort))};
  }),[editGraph]);
  const nodes=useMemo<EditorNode[]>(()=>graph?.nodes.map(n=>({id:n.id,type:'skill',deletable:n.kind!=='buff-events',position:n.position,selected:selectedNodes.includes(n.id),data:{...n,blueprints:project?.summonBlueprints,buffs:project?.buffs,change:parameter,changeKind,remove,toggle:(id,enabled)=>editGraph(s=>({...s,nodes:s.nodes.map(v=>v.id===id?{...v,enabled}:v)}))}}))??[],[graph,project?.summonBlueprints,project?.buffs,parameter,changeKind,remove,editGraph,selectedNodes]);
  const edges=useMemo<Edge[]>(()=>graph?.edges.map(e=>({id:e.id,source:e.source,sourceHandle:e.sourcePort,target:e.target,targetHandle:e.targetPort,type:'default',style:{stroke:e.sourcePort==='target'?'#85abbc':'#d1b66e',strokeWidth:2},animated:false}))??[],[graph]);
  function nodeChanges(changes:NodeChange<EditorNode>[]){
    const selection=changes.filter(c=>c.type==='select');
    if(selection.length)setSelectedNodes(ids=>{const set=new Set(ids);for(const c of selection){if(c.type==='select'){if(c.selected)set.add(c.id);else set.delete(c.id);}}return [...set];});
    if(!changes.some(c=>c.type==='position'||c.type==='remove'))return;
    const updated=applyNodeChanges(changes,nodes);
    editGraph(s=>({...s,nodes:s.nodes.filter(n=>updated.some(v=>v.id===n.id)).map(n=>({...n,position:updated.find(v=>v.id===n.id)!.position})),edges:s.edges.filter(e=>updated.some(v=>v.id===e.source)&&updated.some(v=>v.id===e.target))}));
  }
  function connect(connection:Connection){
    if(!connection.sourceHandle||!connection.targetHandle)return;
    editGraph(s=>({...s,edges:[...s.edges,{id:crypto.randomUUID(),source:connection.source,sourcePort:connection.sourceHandle!,target:connection.target,targetPort:connection.targetHandle!,order:Math.max(-1,...s.edges.filter(e=>e.source===connection.source&&e.sourcePort===connection.sourceHandle).map(e=>e.order))+1}]}));
  }
  function undo(){const prev=history.current.pop();if(prev&&project){future.current.push(clone(project));setProject(prev);setSaved(false);}}
  function redo(){const next=future.current.pop();if(next&&project){history.current.push(clone(project));setProject(next);setSaved(false);}}
  async function save(){if(!project)return;try{await api('/project',project);setSaved(true);setMessage('工程已保存到磁盘');}catch(e){setMessage(String(e));}}
  function download(){if(!project)return;const blob=new Blob([JSON.stringify(project,null,2)],{type:'application/json'});const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download=project.id+'.json';anchor.click();URL.revokeObjectURL(url);setMessage('JSON 已导出');}
  async function publish(slot:string){if(!project||!skill||!graph)return;try{const result=await api<{message:string}>('/publish/'+slot+'?skillId='+encodeURIComponent(skill.id),project);setMessage(result.message);}catch(e){setMessage(String(e));}}
  async function create(){
    if(!newName?.trim()||!skill)return;
    setCreating(true);
    try{
      const source=template==='copy'?null:await api<Project>('/templates/'+encodeURIComponent(template),undefined,'GET');
      const p=current.current;if(!p)return;
      let runtimeId=Math.max(...p.skills.map(s=>s.runtimeId));
      let definitionId=Math.max(4000,...[...p.skills,...(p.buffs??[])].flatMap(s=>s.nodes.map(n=>n.runtimeId??0)));
      const copyNodes=(nodes:Skill['nodes'])=>nodes.map(n=>({...clone(n),runtimeId:n.kind==='projectile'||n.kind==='delivery'?++definitionId:n.runtimeId}));
      const copy=(sourceSkill:Skill,id:string,name=sourceSkill.name):Skill=>({...clone(sourceSkill),id,runtimeId:++runtimeId,name,
        nodes:copyNodes(sourceSkill.nodes)});
      const prototype=source?.skills.find(s=>s.id===template)??skill;
      const dependencies=source?.skills.filter(s=>s.id!==template&&!p.skills.some(v=>v.id===s.id)).map(s=>copy(s,s.id))??[];
      const blueprints=source?.summonBlueprints?.filter(b=>!p.summonBlueprints?.some(v=>v.id===b.id)).map(clone)??[];
      let buffRuntime=Math.max(8000,...(p.buffs??[]).map(b=>b.runtimeId));
      const buffs=source?.buffs?.filter(b=>!p.buffs?.some(v=>v.id===b.id)).map(b=>({...clone(b),runtimeId:++buffRuntime,nodes:copyNodes(b.nodes)}))??[];
      const created=copy(prototype,crypto.randomUUID(),newName.trim());created.category='自定义';
      const next={...p,skills:[...p.skills,...dependencies,created],summonBlueprints:[...(p.summonBlueprints??[]),...blueprints],buffs:[...(p.buffs??[]),...buffs]};
      await api('/validate',next);edit(next);setSelected(created.id);setEditingBuff('');setNewName(null);setLibrary(false);setTimeout(()=>void flow.fitView({padding:.15}),50);
      setMessage('技能已创建；依赖已补齐，同身份技能/Buff/蓝图复用工程现有版本');
    }catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  function add(kind:string){
    if(!project)return;const definitionId=Math.max(4000,...[...project.skills,...(project.buffs??[])].flatMap(s=>s.nodes.map(n=>n.runtimeId??0)))+1;
    const position=flow.screenToFlowPosition({x:window.innerWidth*.55,y:250});
    editGraph(s=>({...s,nodes:[...s.nodes,{id:crypto.randomUUID(),kind,runtimeId:kind==='projectile'||kind==='delivery'?definitionId:0,enabled:true,position,parameters:Object.fromEntries(kinds[kind].fields.map(f=>[f.key,f.initial]))}]}));setAdding(false);
  }
  function newBuff(){
    const p=current.current;if(!p)return;const id=crypto.randomUUID();
    const value:BuffDefinition={id,runtimeId:Math.max(8000,...(p.buffs??[]).map(b=>b.runtimeId))+1,name:'新Buff',permanent:false,duration:2,interval:0,stack:'refresh',maxStacks:1,mutexGroup:0,tags:[],modifiers:[],
      nodes:[{id:'events',kind:'buff-events',position:{x:0,y:80},parameters:{}}],edges:[]};
    edit({...p,buffs:[...(p.buffs??[]),value]});setEditingBuff(id);setSelectedNodes([]);setTimeout(()=>void flow.fitView({padding:.15}),50);
  }
  function layout(){editGraph(s=>({...s,nodes:s.nodes.map((n,i)=>({...n,position:{x:i*270,y:80}}))}));setTimeout(()=>void flow.fitView({padding:.15}),50);}
  const imported=async(file:File)=>{try{const p=JSON.parse(await file.text()) as Project;await api('/validate',p);edit(p);setSelected(p.skills[0].id);setEditingBuff('');setSelectedNodes([]);setMessage('JSON 已导入并通过 C# 校验');}catch(e){setMessage(String(e));}};
  if(!project||!skill||!graph)return <div className="loading"><Workflow size={32}/><h1>技能实验室</h1><p role="status">{message}</p></div>;
  return <div className="application">
    <header className="app-header"><div className="brand"><span>S / L</span><strong>技能实验室</strong><small>HaloCombat</small></div><div className="header-actions"><small className={saved?'':'unsaved'}>{saved?'已保存到磁盘':'未保存'}</small><button onClick={()=>void save()}><Save size={13}/>保存</button><button onClick={()=>void publish('f6')}>发布到 F6</button><button onClick={()=>void publish('f7')}>发布到 F7</button><button onClick={download}><Download size={13}/>导出</button><button onClick={()=>fileInput.current?.click()}><Upload size={13}/>导入</button><input ref={fileInput} type="file" accept=".json,application/json" hidden onChange={e=>{const file=e.target.files?.[0];if(file)void imported(file);e.target.value='';}}/></div></header>
    <nav className="app-navigation"><span className="active">技能</span><div/><small>C# Core · JSON 内容管线</small></nav>
    <div className="workspace"><Preview preview={preview} project={project} update={edit}/>
      <section className="editor-panel"><div className="editor-heading"><span>节点编辑</span><div className="popover-anchor"><button onClick={()=>setLibrary(!library)}>技能库 ▾</button>{library&&<div className="skill-library"><div className="search"><Search size={13}/><input autoFocus placeholder="搜索技能" value={query} onChange={e=>setQuery(e.target.value)}/></div>{[...new Set(project.skills.map(s=>s.category))].map(category=><details key={category} open><summary>{category}</summary>{project.skills.filter(s=>s.category===category&&(s.name+s.id).includes(query)).map(s=><button key={s.id} className={s.id===selected?'active':''} onClick={()=>{setSelected(s.id);setEditingBuff('');setSelectedNodes([]);setLibrary(false);setTimeout(()=>void flow.fitView({padding:.15}),50);}}>{s.name}<small>{s.id}</small></button>)}</details>)}</div>}</div><button onClick={()=>setNewName('')}><Plus size={12}/>新建</button><button onClick={()=>{if(buff)setEditingBuff('');else if(project.buffs?.length)setEditingBuff(project.buffs[0].id);else newBuff();setSelectedNodes([]);setTimeout(()=>void flow.fitView({padding:.15}),50);}}>{buff?'返回技能':'Buff定义'}</button>
      {buff&&<><select className="buff-select" aria-label="当前Buff定义" value={buff.id} onChange={e=>{setEditingBuff(e.target.value);setSelectedNodes([]);setTimeout(()=>void flow.fitView({padding:.15}),50);}}>{project.buffs?.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select><button onClick={newBuff}>新建Buff</button></>}
      <label>{buff?'Buff名称':'技能名称'}<input aria-label={buff?'Buff名称':'技能名称'} value={graph.name} onChange={e=>editGraph(g=>({...g,name:e.target.value}))}/></label><label>ID<code>{graph.id.length>22?graph.id.slice(0,22)+'…':graph.id}</code></label><span className="grow"/><span className="badge">{castMode}</span><span className="badge">{transfers}</span></div>
      <div className="editor-body"><div className="graph-panel"><div className="canvas-toolbar"><div className="popover-anchor"><button onClick={()=>setAdding(!adding)}><Plus size={12}/>添加</button>{adding&&<div className="node-menu">{Object.entries(kinds).filter(([kind])=>kind!=='buff-events'&&(!buff||(kind!=='target'&&kind!=='cast'&&kind!=='skill-events'))).map(([kind,schema])=><button key={kind} onClick={()=>add(kind)}>{schema.title}</button>)}</div>}</div><button onClick={()=>void flow.fitView({padding:.15})}><Maximize size={12}/>适应</button><button onClick={layout}>自动排列</button><button aria-label="撤销" disabled={!history.current.length} onClick={undo}><Undo2 size={13}/></button><button aria-label="重做" disabled={!future.current.length} onClick={redo}><Redo2 size={13}/></button><span className="grow"/><small>{Math.round(flow.getZoom()*100)}%</small></div>
      <ReactFlow<EditorNode> nodes={nodes} edges={edges} nodeTypes={nodeTypes} onNodesChange={nodeChanges} onEdgesDelete={deleted=>editGraph(s=>({...s,edges:s.edges.filter(e=>!deleted.some(d=>d.id===e.id))}))} onConnect={connect} defaultViewport={{x:20,y:65,zoom:.9}} minZoom={.3} maxZoom={1.8} deleteKeyCode={['Backspace','Delete']} colorMode="dark" proOptions={{hideAttribution:true}}><Background variant={BackgroundVariant.Dots} gap={18} size={1} color="#48565d"/></ReactFlow>
      </div><aside className="properties">{buff?<><BuffProperties buff={buff} change={value=>edit({...project,buffs:project.buffs?.map(b=>b.id===value.id?value:b)})}/><small>当前预览技能：{skill.name}</small></>:<><h3>▾ 技能属性</h3><p className="type-summary">{castMode} · {transfers}</p><p className="skill-description">{description}</p>{skill.nodes.filter(n=>n.enabled!==false).map(node=><section key={node.id}><h4>{kinds[node.kind]?.title??node.kind}</h4>{kinds[node.kind]?.fields.filter(f=>!f.when||f.when(node.parameters)).map(f=><div className="property" key={f.key}><span>{f.label}</span><b>{f.key==='buff'?(project.buffs?.find(b=>b.id===node.parameters.buff)?.name??String(node.parameters.buff)):f.options?.find(([v])=>v===parameterValue(f,node.parameters))?.[1]??String(parameterValue(f,node.parameters))}{f.unit}</b></div>)}</section>)}<SummonBlueprints project={project} update={edit}/></>}</aside></div></section>
    </div><footer className={message.startsWith('Error')?'error':''} role="status">{message}<span>瞬发 / 引导 / 蓄力 / 连发 · 更多节点开发中</span></footer>
    {newName!==null&&<div className="modal-backdrop" onPointerDown={e=>{if(!creating&&e.target===e.currentTarget)setNewName(null);}}><form className="modal" onSubmit={e=>{e.preventDefault();void create();}}><h2>新建技能</h2><label>名称<input aria-label="新技能名称" autoFocus value={newName} onChange={e=>setNewName(e.target.value)}/></label><label>模板<select aria-label="技能模板" value={template} onChange={e=>setTemplate(e.target.value)}><option value="copy">复制当前技能</option>{examples.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label><div><button type="button" disabled={creating} onClick={()=>setNewName(null)}>取消</button><button className="primary" disabled={!newName.trim()||creating}>{creating?'创建中…':'确定'}</button></div></form></div>}
  </div>;
}
export default function App(){return <ReactFlowProvider><Workbench/></ReactFlowProvider>;}
