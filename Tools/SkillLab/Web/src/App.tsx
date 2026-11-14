import { useRef, useState } from 'react';
import { ReactFlow, ReactFlowProvider, Background, BackgroundVariant } from '@xyflow/react';
import { Plus, Save, Download, Upload, Undo2, Redo2, Maximize, Workflow } from 'lucide-react';
import { api, clone, kinds, parameterValue, deliveryLabel } from './model';
import type { Project, Skill, BuffDefinition } from './model';
import { SkillNode } from './SkillNode';
import type { EditorNode } from './SkillNode';
import { usePreview } from './usePreview';
import { Preview } from './Preview';
import { SummonBlueprints } from './SummonBlueprints';
import { BuffProperties } from './BuffProperties';
import { SkillLibrary } from './SkillLibrary';
import { ContentDialog } from './ContentDialog';
import type { ContentAction } from './ContentDialog';
import { GraphConnections } from './GraphConnections';
import { useProjectDocument } from './useProjectDocument';
import { useGraphAuthoring } from './useGraphAuthoring';
import { copyGraph, definitionAllocator, duplicateContent, emptyProject, numberAllocator, selectGraph,remapTimeline } from './authoring/contentEdits';
import type { ContentKind, ContentReference, GraphKey } from './authoring/contentEdits';
import { mergeProjects } from './authoring/mergeProjects';
import { TimelineProperties } from './TimelineProperties';
import { RuntimeProperties } from './RuntimeProperties';

const nodeTypes={skill:SkillNode};
function Workbench() {
  const [message,setMessage]=useState('正在连接本地 C# 宿主…');
  const editor=useProjectDocument(setMessage);const project=editor.project;
  const [library,setLibrary]=useState(false);const [adding,setAdding]=useState(false);
  const [newName,setNewName]=useState<string|null>(null);const [template,setTemplate]=useState('copy');
  const [examples,setExamples]=useState<Skill[]>([]);const [creating,setCreating]=useState(false);
  const [contentAction,setContentAction]=useState<ContentAction|null>(null);
  const [newProjectName,setNewProjectName]=useState<string|null>(null);const [projectTemplate,setProjectTemplate]=useState('empty');
  const [pendingImport,setPendingImport]=useState<Project|null>(null);
  const fileInput=useRef<HTMLInputElement>(null);
  const skill=project?.skills.find(s=>s.id===project.editor?.selectedSkill)??project?.skills[0];
  const buff=project?.buffs?.find(b=>b.id===project.editor?.selectedBuff);
  const key:GraphKey={kind:buff?'buff':'skill',id:buff?.id??skill?.id??''};
  const graphEditor=useGraphAuthoring(editor,key,setMessage);const graph=graphEditor.graph;
  const preview=usePreview(project,skill?.id??'',setMessage);preview.skill.current=skill?.id??'';
  const mode=String(skill?.nodes.find(n=>n.kind==='cast'&&n.enabled!==false)?.parameters.mode??'instant');
  const castMode=({instant:'瞬发',channel:'引导',charge:'蓄力'} as Record<string,string>)[mode]??mode;
  const transfers=[...new Set(skill?.nodes.filter(n=>n.enabled!==false&&(n.kind==='projectile'||n.kind==='delivery'||n.kind==='construct')).map(deliveryLabel))].join(' / ')||'效果';
  function select(skillId:string,buffId=''){editor.endGroup();editor.edit(p=>selectGraph(p,skillId,buffId),false);setLibrary(false);}
  async function openCreate(){
    try{const available=examples.length?examples:(await api<Project>('/examples',undefined,'GET')).skills;setExamples(available);setTemplate(skill?'copy':available[0]?.id??'');setNewName('');}
    catch(error){setMessage(String(error));}
  }
  function download(){
    const p=editor.current.current;if(!p)return;
    const url=URL.createObjectURL(new Blob([JSON.stringify(p,null,2)],{type:'application/json'}));
    const anchor=window.document.createElement('a');anchor.href=url;anchor.download=p.id+'.json';anchor.click();URL.revokeObjectURL(url);setMessage('工程 JSON 已导出');
  }
  async function publish(slot:string){
    const p=editor.current.current;if(!p||!skill)return;
    try{const result=await api<{message:string}>('/publish/'+slot+'?skillId='+encodeURIComponent(skill.id),p);setMessage(result.message);}
    catch(error){setMessage(String(error));}
  }
  async function create(){
    if(!newName?.trim())return;setCreating(true);
    try{
      if(template==='copy'&&skill){const p=editor.current.current!;const result=duplicateContent(p,'skill',skill.id,newName.trim());editor.endGroup();editor.edit(selectGraph(result.project,result.id));setNewName(null);return;}
      const source=await api<Project>('/templates/'+encodeURIComponent(template),undefined,'GET');const p=editor.current.current;if(!p)return;
      const runtimeId=numberAllocator(p.skills.flatMap(s=>[s.runtimeId,s.timeline?.runtimeId??0]));const definitionId=definitionAllocator(p);
      const copy=(s:Skill,id:string,name=s.name):Skill=>{const copied=copyGraph(s.nodes,s.edges,definitionId),number=runtimeId();return {...clone(s),...copied,id,runtimeId:number,name,
        timeline:s.timeline?remapTimeline(s.timeline,new Map(s.nodes.map((n,i)=>[n.id,copied.nodes[i].id])),number):undefined};};
      const prototype=source.skills.find(s=>s.id===template)!;
      const dependencies=source.skills.filter(s=>s.id!==template&&!p.skills.some(v=>v.id===s.id)).map(s=>copy(s,s.id));
      const blueprints=source.summonBlueprints?.filter(b=>!p.summonBlueprints?.some(v=>v.id===b.id)).map(clone)??[];
      const buffRuntime=numberAllocator((p.buffs??[]).map(b=>b.runtimeId),8000);
      const buffs=source.buffs?.filter(b=>!p.buffs?.some(v=>v.id===b.id)).map(b=>({...clone(b),runtimeId:buffRuntime(),...copyGraph(b.nodes,b.edges,definitionId)}))??[];
      const created=copy(prototype,crypto.randomUUID(),newName.trim());created.category='自定义';
      const next=selectGraph({...p,skills:[...p.skills,...dependencies,created],summonBlueprints:[...(p.summonBlueprints??[]),...blueprints],buffs:[...(p.buffs??[]),...buffs]},created.id);
      await api('/document/validate',next);if(editor.current.current!==p)throw new Error('创建校验期间工程已更改，请重新创建');
      editor.endGroup();editor.edit(next);setNewName(null);setLibrary(false);setMessage('技能已创建；共享依赖复用工程现有版本');
    }catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  function newBuff(){
    const p=editor.current.current;if(!p)return;const id=crypto.randomUUID();
    const value:BuffDefinition={id,runtimeId:numberAllocator((p.buffs??[]).map(b=>b.runtimeId),8000)(),name:'新Buff',permanent:false,duration:2,interval:0,stack:'refresh',maxStacks:1,mutexGroup:0,tags:[],modifiers:[],
      nodes:[{id:crypto.randomUUID(),kind:'buff-events',position:{x:0,y:80},parameters:{}}],edges:[]};
    editor.endGroup();editor.edit(selectGraph({...p,buffs:[...(p.buffs??[]),value]},skill?.id??'',id));
  }
  function duplicate(kind:ContentKind,id:string){
    try{const p=editor.current.current;if(!p)return;const result=duplicateContent(p,kind,id);editor.endGroup();
      editor.edit(kind==='blueprint'?result.project:selectGraph(result.project,kind==='skill'?result.id:skill?.id??'',kind==='buff'?result.id:''));setMessage('已复制内容并分配新身份');}
    catch(error){setMessage(String(error));}
  }
  function locate(ref:ContentReference){
    if(ref.kind==='actor'||ref.kind==='combo'){setContentAction(null);requestAnimationFrame(()=>{const section=window.document.querySelector('.runtime-properties');section?.setAttribute('open','');section?.scrollIntoView({block:'center'});});return;}
    setContentAction(null);select(ref.kind==='skill'?ref.id:skill?.id??'',ref.kind==='buff'?ref.id:'');
    if(ref.kind==='blueprint')requestAnimationFrame(()=>{const target=window.document.querySelector<HTMLElement>('[data-blueprint-id="'+CSS.escape(ref.id)+'"]');target?.closest('details')?.setAttribute('open','');target?.scrollIntoView({block:'center'});});
    else if(ref.nodeId)requestAnimationFrame(()=>void graphEditor.flow.fitView({nodes:[{id:ref.nodeId!}],padding:.3}));
  }
  async function createProject(){
    if(!newProjectName?.trim())return;setCreating(true);
    const before=editor.current.current;
    try{const base=projectTemplate==='empty'?emptyProject(newProjectName.trim()):{...await api<Project>('/examples',undefined,'GET'),id:crypto.randomUUID(),name:newProjectName.trim()};
      if(editor.current.current!==before)throw new Error('载入示例期间工程已更改，请重新创建工程');
      editor.endGroup();editor.edit(base);setNewProjectName(null);setMessage('新工程已创建，可撤销返回原工程；保存后写入磁盘');}
    catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  async function importFile(file:File){
    try{const value=JSON.parse(await file.text()) as Project;await api('/document/validate',value);setPendingImport(value);}
    catch(error){setMessage(String(error));}
  }
  async function applyImport(merge:boolean){
    if(!pendingImport)return;setCreating(true);
    try{const p=editor.current.current!;const next=merge?mergeProjects(p,pendingImport):pendingImport;
      await api('/document/validate',next);if(editor.current.current!==p)throw new Error('导入校验期间工程已更改，请重新确认导入');
      editor.endGroup();editor.edit(next);setPendingImport(null);setMessage(merge?'内容已合并；冲突身份已重分配并同步引用':'工程已替换，可撤销返回原工程');}
    catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  function shortcuts(event:React.KeyboardEvent){
    if(!(event.ctrlKey||event.metaKey))return;const key=event.key.toLowerCase();
    if(key==='s'){event.preventDefault();if(!editor.saving)void editor.save();return;}
    if((event.target as HTMLElement).closest('input,textarea,select,[contenteditable=true]'))return;
    if(key==='z'){event.preventDefault();if(event.shiftKey)editor.redo();else editor.undo();}
    if(key==='y'){event.preventDefault();editor.redo();}
  }
  if(!project)return <div className="loading"><Workflow size={32}/><h1>技能实验室</h1><p role="status">{message}</p><button onClick={()=>void editor.load().catch(e=>setMessage(String(e)))}>重新连接</button></div>;
  return <div className="application" onKeyDown={shortcuts} onFocusCapture={event=>{if((event.target as HTMLElement).matches('input,textarea,select'))editor.beginGroup();}} onBlurCapture={editor.endGroup}>
    <header className="app-header"><div className="brand"><span>S / L</span><strong>技能实验室</strong><small>HaloCombat</small></div>
      <div className="header-actions"><details className="project-menu"><summary>工程 ▾</summary><div><label>工程名称<input aria-label="工程名称" value={project.name??''} onChange={event=>editor.edit(p=>({...p,name:event.target.value}))}/></label><small>{project.skills.length} 技能 · {project.buffs?.length??0} Buff · {project.summonBlueprints?.length??0} 蓝图</small>
        <button onClick={()=>{setNewProjectName('新工程');setProjectTemplate('empty');}}>新建工程</button><button disabled={editor.saving} onClick={()=>void editor.load().catch(e=>setMessage(String(e)))}>重新载入磁盘</button><p>手动保存。刷新恢复最近磁盘版本。</p></div></details>
        <button onClick={()=>void api<Project>('/runtime',undefined,'GET').then(value=>{editor.endGroup();editor.edit(value);setMessage('已打开正式JSON内容，可撤销返回原工程');}).catch(error=>setMessage(String(error)))}>打开正式内容</button>
        <small className={editor.dirty?'unsaved':''} role="status">{editor.saveStatus}</small><button disabled={editor.saving} onClick={()=>void editor.save()}><Save size={13}/>保存</button>
        <button disabled={!skill} onClick={()=>void publish('f6')}>发布到 F6</button><button disabled={!skill} onClick={()=>void publish('f7')}>发布到 F7</button>
        {project.runtime&&<button onClick={()=>void api<{message:string}>('/runtime/publish',project).then(result=>setMessage(result.message)).catch(error=>setMessage(String(error)))}>发布正式内容</button>}
        <button onClick={download}><Download size={13}/>导出</button><button onClick={()=>fileInput.current?.click()}><Upload size={13}/>导入</button>
        <input ref={fileInput} type="file" accept=".json,application/json" hidden onChange={event=>{const file=event.target.files?.[0];if(file)void importFile(file);event.target.value='';}}/>
      </div></header>
    <nav className="app-navigation"><span className="active">技能</span><div/><small>{project.name} · {project.skills.length} 个技能</small></nav>
    <div className="workspace"><Preview key={project.id} preview={preview} project={project} update={editor.edit}/>
      <section className="editor-panel"><div className="editor-heading"><span>节点编辑</span><div className="popover-anchor"><button onClick={()=>setLibrary(!library)}>技能库 ▾</button>{library&&<SkillLibrary project={project} selected={skill?.id??''} select={id=>select(id)}/>}</div>
        <button onClick={()=>void openCreate()}><Plus size={12}/>新建</button><button onClick={()=>{if(buff)select(skill?.id??'');else if(project.buffs?.length)select(skill?.id??'',project.buffs[0].id);else newBuff();}}>{buff?'返回技能':'Buff定义'}</button>
        {buff&&<><select className="buff-select" aria-label="当前Buff定义" value={buff.id} onChange={event=>select(skill?.id??'',event.target.value)}>{project.buffs?.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select><button onClick={newBuff}>新建Buff</button></>}
        {graph&&<><label>{buff?'Buff名称':'技能名称'}<input aria-label={buff?'Buff名称':'技能名称'} value={graph.name??''} onChange={event=>graphEditor.editGraph(g=>({...g,name:event.target.value}))}/></label>
          <button className="content-id" title={graph.id} onClick={()=>setContentAction({...key,mode:'rename'})}>ID {graph.id}</button><button onClick={()=>duplicate(key.kind,key.id)}>复制{buff?'Buff':'技能'}</button><button onClick={()=>setContentAction({...key,mode:'delete'})}>删除{buff?'Buff':'技能'}</button></>}
        <span className="grow"/>{skill&&<><span className="badge">{castMode}</span><span className="badge">{transfers}</span></>}
      </div><div className="editor-body"><div className="graph-panel" tabIndex={0} aria-label="节点画布" onKeyDown={graphEditor.keyDown}>
        <div className="canvas-toolbar"><div className="popover-anchor"><button disabled={!graph} onClick={()=>setAdding(!adding)}><Plus size={12}/>添加</button>{adding&&<div className="node-menu">{Object.entries(kinds).filter(([kind])=>kind!=='buff-events'&&(!buff||!['target','cast','skill-events'].includes(kind))).map(([kind,schema])=><button key={kind} onClick={()=>{graphEditor.add(kind);setAdding(false);}}>{schema.title}</button>)}</div>}</div>
          <button disabled={!graph} onClick={()=>void graphEditor.flow.fitView({padding:.15})}><Maximize size={12}/>适应</button><button disabled={!graph} onClick={graphEditor.layout}>自动排列</button>
          <button disabled={!graphEditor.selectedNodes.length} onClick={graphEditor.copy}>复制节点</button><button disabled={!graph||!graphEditor.canPaste} onClick={graphEditor.paste}>粘贴</button>
          <button aria-label="撤销" disabled={!editor.canUndo} onClick={editor.undo}><Undo2 size={13}/></button><button aria-label="重做" disabled={!editor.canRedo} onClick={editor.redo}><Redo2 size={13}/></button><span className="grow"/><small>{Math.round(graphEditor.zoom*100)}%</small>
        </div>
        {graph?<ReactFlow<EditorNode> nodes={graphEditor.nodes} edges={graphEditor.edges} nodeTypes={nodeTypes} onNodesChange={graphEditor.nodeChanges} onEdgesChange={graphEditor.edgeChanges}
          onConnect={connection=>graphEditor.connect(connection)} onReconnect={(edge,connection)=>graphEditor.connect(connection,edge.id)} onReconnectStart={(_event,edge)=>graphEditor.reconnectStart(edge.id)} onReconnectEnd={graphEditor.reconnectEnd} isValidConnection={graphEditor.validConnection}
          onMove={(_event,viewport)=>graphEditor.moving(viewport)} onMoveEnd={(_event,viewport)=>graphEditor.moved(viewport)}
          defaultViewport={{x:20,y:65,zoom:.9}} minZoom={.1} maxZoom={4} deleteKeyCode={null} colorMode="dark" proOptions={{hideAttribution:true}}>
          <Background variant={BackgroundVariant.Dots} gap={18} size={1} color="#48565d"/></ReactFlow>:
          <div className="empty-project"><Workflow size={34}/><h2>工程中暂无技能</h2><p>创建技能后编辑节点并运行预览。</p><button className="primary" onClick={()=>void openCreate()}>新建技能</button></div>}
        <small className="canvas-help">Shift 框选 · Ctrl C / V 复制粘贴 · Delete 删除 · Ctrl Z 撤销</small>
      </div><aside className="properties">{buff?<><BuffProperties buff={buff} change={value=>editor.edit(p=>({...p,buffs:p.buffs?.map(b=>b.id===value.id?value:b)}))}/><small>当前预览技能：{skill?.name??'无'}</small></>:skill?<>
        <h3>▾ 技能属性</h3><p className="type-summary">{castMode} · {transfers}</p><label className="category-field">分类<input aria-label="技能分类" value={skill.category??''} onChange={event=>editor.edit(p=>({...p,skills:p.skills.map(s=>s.id===skill.id?{...s,category:event.target.value}:s)}))}/></label>
        <TimelineProperties skill={skill} change={timeline=>graphEditor.editGraph(g=>({...g,timeline}))}/>
        {skill.nodes.filter(n=>n.enabled!==false).map(node=><section key={node.id}><h4>{kinds[node.kind]?.title??node.kind}</h4>{kinds[node.kind]?.fields.filter(f=>(!f.when||f.when(node.parameters))&&(!skill.timeline||node.kind!=='cast'||['cost','cooldown'].includes(f.key))).map(f=><div className="property" key={f.key}><span>{f.label}</span><b>{f.key==='buff'?(project.buffs?.find(b=>b.id===node.parameters.buff)?.name??String(node.parameters.buff)):f.options?.find(([v])=>v===parameterValue(f,node.parameters))?.[1]??String(parameterValue(f,node.parameters))}{f.unit}</b></div>)}</section>)}
        <SummonBlueprints project={project} update={editor.edit} action={setContentAction} duplicate={id=>duplicate('blueprint',id)}/></>:<p>空工程可保存或导出。</p>}
        {graph&&<GraphConnections graph={graph} change={update=>{editor.endGroup();graphEditor.editGraph(update);}}/>}
        <RuntimeProperties project={project} update={editor.edit}/>
      </aside></div></section>
    </div><footer className={message.startsWith('Error')?'error':''} role="status">{message}<span>JSON 工程 · 真实 C# 预览</span></footer>
    {contentAction&&<ContentDialog key={contentAction.kind+contentAction.id+contentAction.mode} project={project} action={contentAction} close={()=>setContentAction(null)} edit={change=>{editor.endGroup();editor.edit(change);setMessage('内容与引用已更新，可撤销恢复');}} locate={locate}/>}
    {newName!==null&&<div className="modal-backdrop"><form className="modal" onSubmit={event=>{event.preventDefault();void create();}}><h2>新建技能</h2><label>名称<input aria-label="新技能名称" autoFocus value={newName} onChange={event=>setNewName(event.target.value)}/></label>
      <label>模板<select aria-label="技能模板" value={template} onChange={event=>setTemplate(event.target.value)}>{skill&&<option value="copy">复制当前技能</option>}{examples.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <div><button type="button" disabled={creating} onClick={()=>setNewName(null)}>取消</button><button className="primary" disabled={!newName.trim()||creating}>{creating?'创建中…':'确定'}</button></div></form></div>}
    {newProjectName!==null&&<div className="modal-backdrop"><form className="modal" onSubmit={event=>{event.preventDefault();void createProject();}}><h2>新建工程</h2><label>名称<input aria-label="新工程名称" autoFocus value={newProjectName} onChange={event=>setNewProjectName(event.target.value)}/></label><label>初始内容<select aria-label="工程初始内容" value={projectTemplate} onChange={event=>setProjectTemplate(event.target.value)}><option value="empty">空工程</option><option value="examples">恢复全部预置示例</option></select></label><p>当前编辑保留在撤销历史中。保存后更新磁盘工程。</p><div><button type="button" disabled={creating} onClick={()=>setNewProjectName(null)}>取消</button><button className="primary" disabled={creating||!newProjectName.trim()}>创建工程</button></div></form></div>}
    {pendingImport&&<div className="modal-backdrop"><div className="modal"><h2>导入工程 · {pendingImport.name}</h2><p>{pendingImport.skills.length} 技能、{pendingImport.buffs?.length??0} Buff、{pendingImport.summonBlueprints?.length??0} 蓝图。</p><p>替换恢复文件完整布局和场景；合并保留当前场景，并为冲突身份分配新 ID、同步导入内容的引用。两种操作均可撤销。</p><div><button disabled={creating} onClick={()=>setPendingImport(null)}>取消</button><button disabled={creating} onClick={()=>void applyImport(true)}>合并内容</button><button className="primary" disabled={creating} onClick={()=>void applyImport(false)}>替换工程</button></div></div></div>}
  </div>;
}
export default function App(){return <ReactFlowProvider><Workbench/></ReactFlowProvider>;}
