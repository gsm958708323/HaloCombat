import { useLayoutEffect, useRef, useState } from 'react';
import { ReactFlow, ReactFlowProvider, Background, BackgroundVariant } from '@xyflow/react';
import { Plus, Save, Download, Upload, Undo2, Redo2, Maximize, Workflow } from 'lucide-react';
import { api, clone, kinds, deliveryLabel } from './model';
import { publicationSlotLabel, publicationSlots } from './publicationSlots';
import type { PublicationSlot } from './publicationSlots';

import type { Project, Skill, BuffDefinition } from './model';
import { SkillNode } from './SkillNode';
import type { EditorNode } from './SkillNode';
import {PreviewSession} from './PreviewSession';
import {SkillOverview} from './SkillOverview';
import { SummonBlueprints } from './SummonBlueprints';
import { BuffProperties } from './BuffProperties';
import { SkillLibrary } from './SkillLibrary';
import { ContentDialog } from './ContentDialog';
import type { ContentAction } from './ContentDialog';
import { GraphConnections } from './GraphConnections';
import { useProjectDocument } from './useProjectDocument';
import { useGraphAuthoring } from './useGraphAuthoring';
import { actionNumbers,contentLabel,copyGraph, definitionAllocator, duplicateContent, emptyProject, numberAllocator, selectGraph,remapTimeline } from './authoring/contentEdits';
import type { ContentKind, ContentReference, GraphKey } from './authoring/contentEdits';
import { mergeProjects } from './authoring/mergeProjects';
import { TimelineProperties } from './TimelineProperties';
import { RuntimeProperties } from './RuntimeProperties';
import { SharedTimelineControls } from './SharedTimelineControls';
import { newTimeline } from './authoring/timelineEdits';
import {newProjectile} from './authoring/projectileEdits';
import {newArea} from './authoring/areaEdits';
import {ExportDialog} from './ExportDialog';
import {prepareImportedLayout} from './authoring/importLayout';
import {traceLocation} from './authoring/traceLocation';
import type {AuthorNodeSource} from './model';
import {SkillSummary} from './SkillSummary';
import {Dialog} from './Dialog';
import {ImportDialog} from './ImportDialog';
import type {ImportCandidate} from './authoring/importCandidate';
import {WorkspacePicker} from './WorkspacePicker';
import {SkillCopyDialog} from './SkillCopyDialog';
import type {SkillCopyPlan} from './authoring/skillCopy';
import type {Workspace} from './authoring/projectIO';
import {ComboEditor} from './ComboEditor';
import {createPreviewRuntime} from './previewRuntime';
import {restoreDefaultFormation} from './authoring/defaultScene';

const nodeTypes={skill:SkillNode};
function Workbench({editor,workspace,project,message,setMessage}:{editor:ReturnType<typeof useProjectDocument>;workspace:Workspace;project:Project;message:string;setMessage:(message:string)=>void}) {
  const [view,setView]=useState<'skill'|'combo'>('skill');const [previewStore]=useState(createPreviewRuntime);
  const [copying,setCopying]=useState(false);
  const graphPanel=useRef<HTMLDivElement>(null);
  const focusAfterCopy=useRef(false);
  useLayoutEffect(()=>{
    if(!copying&&focusAfterCopy.current){
      focusAfterCopy.current=false;
      graphPanel.current?.focus({preventScroll:true});
    }
  },[copying]);
  const [library,setLibrary]=useState(false);const [adding,setAdding]=useState(false);
  const [newName,setNewName]=useState<string|null>(null);const [template,setTemplate]=useState('copy');
  const [examples,setExamples]=useState<Skill[]>([]);const [creating,setCreating]=useState(false);
  const [contentAction,setContentAction]=useState<ContentAction|null>(null);
  const [newProjectName,setNewProjectName]=useState<string|null>(null);const [projectTemplate,setProjectTemplate]=useState('empty');
  const [pendingImport,setPendingImport]=useState<ImportCandidate|null>(null);
  const detachedReferences=pendingImport?.detachedReferences??[];
  const detachesReferences=detachedReferences.length>0;
  const [importing,setImporting]=useState(false);
  const [exporting,setExporting]=useState(false);
  const [settings,setSettings]=useState<'graph'|'project'|'connections'|null>(null);
  const [previewRoot,setPreviewRoot]=useState<string|null>(null);
  const [traceFocus,setTraceFocus]=useState<{key:GraphKey;nodeId:string;request:number}|null>(null);
  const traceRequest=useRef(0);
  const skill=project?.skills.find(s=>s.id===project.editor?.selectedSkill)??project?.skills[0];
  const buff=project?.buffs?.find(b=>b.id===project.editor?.selectedBuff);
  const timeline=project?.timelines?.find(t=>t.id===project.editor?.selectedTimeline);
  const projectile=project?.projectiles?.find(t=>t.id===project.editor?.selectedProjectile);
  const area=project?.areas?.find(a=>a.id===project.editor?.selectedArea);
  const key:GraphKey={kind:area?'area':projectile?'projectile':timeline?'timeline':buff?'buff':'skill',id:area?.id??projectile?.id??timeline?.id??buff?.id??skill?.id??''};
  const graphEditor=useGraphAuthoring(editor,key,setMessage,traceFocus);const graph=graphEditor.graph;
  const root=previewRoot&&project?.skills.some(s=>s.id===previewRoot)?previewRoot:skill?.id??'';
  function adoptReplay(p:Project,id:string){setPreviewRoot(id);setTraceFocus(null);editor.endGroup();editor.edit(selectGraph(prepareImportedLayout(p),id));}
  const mode=String(skill?.nodes.find(n=>n.kind==='cast'&&n.enabled!==false)?.parameters.mode??'instant');
  const castMode=skill?.nodes.find(n=>n.kind==='cast')?.parameters.execution==='operation'?'即时操作':({instant:'瞬发',channel:'引导',charge:'蓄力'} as Record<string,string>)[mode]??mode;
  const actionNodes=skill?.timelineRef?project?.timelines?.find(t=>t.id===skill.timelineRef)?.nodes:skill?.nodes;
  const transfers=[...new Set(actionNodes?.filter(n=>n.enabled!==false&&(n.kind==='projectile'||n.kind==='projectile-ref'||n.kind==='area-ref'||n.kind==='delivery'||n.kind==='construct')).map(deliveryLabel))].join(' / ')||'效果';
  function select(skillId:string,buffId='',timelineId='',projectileId='',areaId=''){setPreviewRoot(null);setTraceFocus(null);editor.endGroup();editor.edit(p=>selectGraph(p,skillId,buffId,timelineId,projectileId,areaId),false);setLibrary(false);}
  function inspectSource(source:AuthorNodeSource){
    try{
      const p=editor.current.current;if(!p)return;const target=traceLocation(p,source);
      setPreviewRoot(root);editor.edit(selectGraph(p,target.kind==='skill'?target.id:skill?.id??'',target.kind==='buff'?target.id:'',target.kind==='timeline'?target.id:'',target.kind==='projectile'?target.id:'',target.kind==='area'?target.id:''),false);
      setTraceFocus({key:target,nodeId:source.nodeId,request:++traceRequest.current});setMessage('已定位 '+source.graphId+' / '+source.nodeId+'；预览仍使用 '+root);
    }catch(error){setMessage(String(error));}
  }
  function newShared(){editor.endGroup();editor.edit(p=>newTimeline(p,skill?.id??'').project);}
  function newBullet(){editor.endGroup();editor.edit(p=>newProjectile(p,skill?.id??'').project);}
  function newZone(){editor.endGroup();editor.edit(p=>newArea(p,skill?.id??'').project);}
  async function openCreate(){
    try{const available=examples.length?examples:(await api<Project>('/examples',undefined,'GET')).skills;setExamples(available);setTemplate(skill?'copy':available[0]?.id??'');setNewName('');}
    catch(error){setMessage(String(error));}
  }
  async function publish(slot:PublicationSlot){
    const p=editor.current.current;if(!p||!skill)return;
    try{const result=await api<{message:string;revision:string;skillId:string}>('/publish/'+slot+'?skillId='+encodeURIComponent(skill.id),p);setMessage(result.message+' · '+result.skillId+' · '+result.revision.slice(0,8));}
    catch(error){setMessage(String(error));}
  }
  async function create(){
    if(!newName?.trim())return;setCreating(true);
    try{
      if(template==='copy'&&skill){const p=editor.current.current!;const result=duplicateContent(p,'skill',skill.id,newName.trim());editor.endGroup();editor.edit(selectGraph(result.project,result.id));setNewName(null);return;}
      const source=await api<Project>('/templates/'+encodeURIComponent(template),undefined,'GET');const p=editor.current.current;if(!p)return;
      const runtimeId=numberAllocator(actionNumbers(p));const definitionId=definitionAllocator(p);
      const copy=(s:Skill,id:string,name=s.name):Skill=>{const copied=copyGraph(s.nodes,s.edges,definitionId),number=runtimeId();return {...clone(s),...copied,id,runtimeId:number,name,
        timeline:s.timeline?remapTimeline(s.timeline,new Map(s.nodes.map((n,i)=>[n.id,copied.nodes[i].id])),number):undefined};};
      const prototype=source.skills.find(s=>s.id===template)!;
      const dependencies=source.skills.filter(s=>s.id!==template&&!p.skills.some(v=>v.id===s.id)).map(s=>copy(s,s.id));
      const blueprints=source.summonBlueprints?.filter(b=>!p.summonBlueprints?.some(v=>v.id===b.id)).map(clone)??[];
      const buffRuntime=numberAllocator((p.buffs??[]).map(b=>b.runtimeId),8000);
      const buffs=source.buffs?.filter(b=>!p.buffs?.some(v=>v.id===b.id)).map(b=>({...clone(b),runtimeId:buffRuntime(),...copyGraph(b.nodes,b.edges,definitionId)}))??[];
      const timelines=source.timelines?.filter(t=>!p.timelines?.some(v=>v.id===t.id)).map(t=>{const graph=copyGraph(t.nodes,t.edges,definitionId);return {...clone(t),...graph,timeline:remapTimeline(t.timeline,new Map(t.nodes.map((n,i)=>[n.id,graph.nodes[i].id])),runtimeId())};})??[];
      const projectiles=source.projectiles?.filter(t=>!p.projectiles?.some(v=>v.id===t.id)).map(t=>{const graph=copyGraph(t.nodes,t.edges,definitionId);return {...clone(t),...graph,root:graph.nodes[t.nodes.findIndex(n=>n.id===t.root)].id};})??[];
      const areas=source.areas?.filter(t=>!p.areas?.some(v=>v.id===t.id)).map(t=>{const graph=copyGraph(t.nodes,t.edges,definitionId);return {...clone(t),...graph,root:graph.nodes[t.nodes.findIndex(n=>n.id===t.root)].id};})??[];
      const created=copy(prototype,crypto.randomUUID(),newName.trim());created.category='自定义';
      const next=selectGraph({...p,skills:[...p.skills,...dependencies,created],timelines:[...(p.timelines??[]),...timelines],projectiles:[...(p.projectiles??[]),...projectiles],areas:[...(p.areas??[]),...areas],summonBlueprints:[...(p.summonBlueprints??[]),...blueprints],buffs:[...(p.buffs??[]),...buffs]},created.id);
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
      editor.edit(kind==='blueprint'?result.project:selectGraph(result.project,kind==='skill'?result.id:skill?.id??'',kind==='buff'?result.id:'',kind==='timeline'?result.id:'',kind==='projectile'?result.id:'',kind==='area'?result.id:''));setMessage('已复制内容并分配新身份');}
    catch(error){setMessage(String(error));}
  }
  function locate(ref:ContentReference){
    if(ref.kind==='actor'||ref.kind==='combo'){setContentAction(null);setSettings('project');requestAnimationFrame(()=>{const section=window.document.querySelector('.runtime-properties');section?.setAttribute('open','');section?.scrollIntoView({block:'center'});});return;}
    setContentAction(null);select(ref.kind==='skill'?ref.id:skill?.id??'',ref.kind==='buff'?ref.id:'',ref.kind==='timeline'?ref.id:'',ref.kind==='projectile'?ref.id:'',ref.kind==='area'?ref.id:'');
    if(ref.kind==='blueprint'){setSettings('project');}
    if(ref.kind==='blueprint')requestAnimationFrame(()=>{const target=window.document.querySelector<HTMLElement>('[data-blueprint-id="'+CSS.escape(ref.id)+'"]');target?.closest('details')?.setAttribute('open','');target?.scrollIntoView({block:'center'});});
    else if(ref.nodeId)requestAnimationFrame(()=>void graphEditor.flow.fitView({nodes:[{id:ref.nodeId!}],padding:.3}));
  }
  async function createProject(){
    if(!newProjectName?.trim())return;setCreating(true);
    const before=editor.current.current;
    try{const base=projectTemplate==='empty'?emptyProject(newProjectName.trim()):{...await api<Project>('/examples',undefined,'GET'),id:crypto.randomUUID(),name:newProjectName.trim()};
      if(editor.current.current!==before)throw new Error('载入示例期间工程已更改，请重新创建工程');
      editor.endGroup();editor.edit({...base,preview:restoreDefaultFormation(base.preview)});setNewProjectName(null);setMessage('新工程已创建，可撤销返回原工程；保存后写入磁盘');}
    catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  async function applyImport(merge:boolean){
    if(!pendingImport)return;setCreating(true);
    try{
      const current=editor.current.current!;
      const candidate=pendingImport.project;
      const next=merge?mergeProjects(current,candidate):candidate;
      await api('/document/validate',next);
      if(editor.current.current!==current)throw new Error('导入校验期间工程已更改，请重新确认导入');
      editor.endGroup();editor.edit(next);setPendingImport(null);
      const action=merge?'内容已合并，冲突身份已重分配并同步引用':'工程已替换，可撤销返回原工程';
      setMessage(detachesReferences?'已解除 '+detachedReferences.length+' 处绑定。'+action:action);
    }
    catch(error){setMessage(String(error));}finally{setCreating(false);}
  }
  function applySkillCopy(before:Project,plan:SkillCopyPlan){
    if(editor.loading||editor.current.current!==before)throw new Error('目标工程已更改，请重新预览');
    editor.endGroup();editor.edit(plan.project);
    focusAfterCopy.current=true;
    setPreviewRoot(null);setTraceFocus(null);setCopying(false);setLibrary(false);
    setMessage('已复制技能及依赖，可撤销。保存后写入当前工作区。');
  }
  function shortcuts(event:React.KeyboardEvent){
    if(!(event.ctrlKey||event.metaKey))return;const key=event.key.toLowerCase();
    if((event.target as HTMLElement).closest('[role=dialog]'))return;
    if(key==='s'){event.preventDefault();if(!editor.saving&&!editor.publishing&&!editor.loading)void editor.save();return;}
    if((event.target as HTMLElement).closest('input,textarea,select,[contenteditable=true]'))return;
    if(key==='z'){event.preventDefault();if(event.shiftKey)editor.redo();else editor.undo();}
    if(key==='y'){event.preventDefault();editor.redo();}
  }
  return <div className="application" onKeyDown={shortcuts} onFocusCapture={event=>{if((event.target as HTMLElement).matches('input,textarea,select'))editor.beginGroup();}} onBlurCapture={editor.endGroup}>
    <header className="app-header"><div className="brand"><span aria-hidden="true">H</span><strong>HaloCombat</strong><small>技能实验室</small></div>
      <div className="header-actions"><WorkspacePicker editor={editor} report={setMessage} disabled={creating||copying}/><details className="project-menu"><summary>工程 ▾</summary><div><label>工程名称<input aria-label="工程名称" value={project.name??''} onChange={event=>editor.edit(p=>({...p,name:event.target.value}))}/></label><small>{project.skills.length} 技能 · {project.buffs?.length??0} Buff · {project.summonBlueprints?.length??0} 蓝图</small>
        <button onClick={()=>{setNewProjectName('新工程');setProjectTemplate('empty');}}>新建工程</button><button disabled={editor.saving||editor.publishing||editor.loading} onClick={()=>void editor.load().catch(e=>setMessage(String(e)))}>重新载入磁盘</button><p>手动保存。刷新恢复最近磁盘版本。</p></div></details>
        <small className={editor.dirty?'unsaved':''} role="status">{editor.saveStatus}</small><button className="save-project" disabled={editor.saving||editor.publishing||editor.loading} onClick={()=>void editor.save()}><Save size={13}/>保存</button>
        {publicationSlots.map(slot=><button key={slot} disabled={!skill} onClick={()=>void publish(slot)}>发布到{publicationSlotLabel(slot)}</button>)}
        <button onClick={()=>void api<{message:string}>('/publish/sync',editor.current.current).then(result=>setMessage(result.message)).catch(error=>setMessage(String(error)))}>同步现有槽位</button>
        {workspace==='arena'&&<button disabled={editor.loading||editor.saving||editor.publishing||!project.runtime?.arena||!project.runtime?.actors.length} onClick={()=>void editor.publishArena()}>{editor.publishing?'发布中…':'发布正式内容'}</button>}
        <button onClick={()=>setSettings('project')}>工程设置</button><button onClick={()=>setExporting(true)}><Download size={13}/>导出</button><button onClick={()=>setImporting(true)}><Upload size={13}/>导入</button>
      </div></header>
    <nav className="app-navigation"><button className={view==='skill'?'active':''} onClick={()=>setView('skill')}>技能</button><button className={view==='combo'?'active':''} onClick={()=>setView('combo')}>连招</button><div/><small>{project.name} · {project.skills.length} 个技能</small></nav>
    {editor.publishFeedback&&<div className="publish-feedback">
      <span className={editor.publishFeedback.error?'error':''} role={editor.publishFeedback.error?'alert':'status'}>{editor.publishFeedback.text}</span>
      <button type="button" aria-label="关闭发布消息" onClick={editor.dismissPublishFeedback}>关闭</button>
    </div>}
    <div className={view==='combo'?'workspace workspace--combo-preview':'workspace'} inert={editor.loading}><PreviewSession key={'preview:'+project.id+':'+view} mode={view} project={project} update={editor.edit} inspectSource={inspectSource} root={view==='combo'?'':root} report={setMessage} adoptReplay={adoptReplay} store={previewStore}/>
      {view==='combo'?<ComboEditor key={'combo:'+project.id} project={project} update={editor.edit} report={setMessage} store={previewStore} />:<section className="editor-panel"><div className="editor-heading">{graph?<label className="graph-title">{contentLabel[key.kind]}名称<input aria-label={contentLabel[key.kind]+'名称'} value={graph.name??''} onChange={event=>graphEditor.editGraph(g=>({...g,name:event.target.value}))}/></label>:<span>技能编排</span>}<div className="popover-anchor"><button onClick={()=>setLibrary(!library)}>技能库 ▾</button>{library&&<SkillLibrary project={project} selected={skill?.id??''} select={id=>select(id)} copy={()=>setCopying(true)}/>}</div>
        <button onClick={()=>void openCreate()}><Plus size={12}/>新建</button><button onClick={()=>{if(buff)select(skill?.id??'');else if(project.buffs?.length)select(skill?.id??'',project.buffs[0].id);else newBuff();}}>{buff?'返回技能':'Buff定义'}</button>
        <button onClick={()=>{if(timeline)select(skill?.id??'');else if(project.timelines?.length)select(skill?.id??'','',project.timelines[0].id);else newShared();}}>{timeline?'返回技能':'共享时间轴'}</button>
        {timeline&&<><select aria-label="当前共享时间轴" value={timeline.id} onChange={e=>select(skill?.id??'','',e.target.value)}>{project.timelines?.map(t=><option key={t.id} value={t.id}>{t.name}</option>)}</select><button onClick={newShared}>新建共享轴</button></>}
        <button onClick={()=>{if(projectile)select(skill?.id??'');else if(project.projectiles?.length)select(skill?.id??'','','',project.projectiles[0].id);else newBullet();}}>{projectile?'返回技能':'共享弹体'}</button>
        {projectile&&<><select aria-label="当前共享弹体" value={projectile.id} onChange={e=>select(skill?.id??'','','',e.target.value)}>{project.projectiles?.map(p=><option key={p.id} value={p.id}>{p.name}</option>)}</select><button onClick={newBullet}>新建弹体定义</button></>}
        <button onClick={()=>{if(area)select(skill?.id??'');else if(project.areas?.length)select(skill?.id??'','','','',project.areas[0].id);else newZone();}}>{area?'返回技能':'共享区域'}</button>
        {area&&<><select aria-label="当前共享区域" value={area.id} onChange={e=>select(skill?.id??'','','','',e.target.value)}>{project.areas?.map(a=><option key={a.id} value={a.id}>{a.name}</option>)}</select><button onClick={newZone}>新建区域定义</button></>}
        {buff&&<><select className="buff-select" aria-label="当前Buff定义" value={buff.id} onChange={event=>select(skill?.id??'',event.target.value)}>{project.buffs?.map(b=><option key={b.id} value={b.id}>{b.name}</option>)}</select><button onClick={newBuff}>新建Buff</button></>}
        {graph&&<><button className="content-id" title={graph.id} onClick={()=>setContentAction({...key,mode:'rename'})}>ID {graph.id}</button><button onClick={()=>duplicate(key.kind,key.id)}>复制{contentLabel[key.kind]}</button><button onClick={()=>setContentAction({...key,mode:'delete'})}>删除{contentLabel[key.kind]}</button></>}
        <span className="grow"/>{skill&&<><span className="badge">{castMode}</span><span className="badge">{transfers}</span></>}
      </div><div className="editor-body"><div className="graph-panel" ref={graphPanel} tabIndex={0} aria-label="节点画布" onKeyDown={graphEditor.keyDown}>
        <div className="canvas-toolbar"><span className="canvas-title"><Workflow size={14} aria-hidden="true"/>技能画布</span><div className="popover-anchor"><button disabled={!graph} onClick={()=>setAdding(!adding)}><Plus size={12}/>添加</button>{adding&&<div className="node-menu">{Object.entries(kinds).filter(([kind])=>kind!=='buff-events'&&(!(buff||timeline||projectile)||!['target','cast','skill-events'].includes(kind))).map(([kind,schema])=><button key={kind} onClick={()=>{graphEditor.add(kind);setAdding(false);}}>{schema.title}</button>)}</div>}</div>
          <button disabled={!graph} onClick={()=>void graphEditor.fitGraph()}><Maximize size={12}/>适应</button><button disabled={!graph} onClick={graphEditor.layout}>自动排列</button><button disabled={!graph} onClick={()=>setSettings('graph')}>{key.kind==='skill'?'技能设置':'定义设置'}</button><button disabled={!graph} onClick={()=>setSettings('connections')}>连线顺序</button>
          <button disabled={!graphEditor.selectedNodes.length} onClick={graphEditor.copy}>复制节点</button><button disabled={!graph||!graphEditor.canPaste} onClick={graphEditor.paste}>粘贴</button>
          <button aria-label="撤销" disabled={!editor.canUndo} onClick={editor.undo}><Undo2 size={13}/></button><button aria-label="重做" disabled={!editor.canRedo} onClick={editor.redo}><Redo2 size={13}/></button><span className="grow"/><small>{Math.round(graphEditor.zoom*100)}%</small>
        </div>
        {graph?<ReactFlow<EditorNode> nodes={graphEditor.nodes} edges={graphEditor.edges} nodeTypes={nodeTypes} onNodesChange={graphEditor.nodeChanges} onEdgesChange={graphEditor.edgeChanges}
          onConnect={connection=>graphEditor.connect(connection)} onReconnect={(edge,connection)=>graphEditor.connect(connection,edge.id)} onReconnectStart={(_event,edge)=>graphEditor.reconnectStart(edge.id)} onReconnectEnd={graphEditor.reconnectEnd} isValidConnection={graphEditor.validConnection}
          onMove={(_event,viewport)=>graphEditor.moving(viewport)} onMoveEnd={(event,viewport)=>graphEditor.moved(viewport,!!event)}
          defaultViewport={{x:20,y:65,zoom:.9}} minZoom={.1} maxZoom={4} deleteKeyCode={null} colorMode="dark" proOptions={{hideAttribution:true}}>
          <Background variant={BackgroundVariant.Dots} gap={18} size={1} color="var(--desk-grid)"/></ReactFlow>:
          <div className="empty-project"><Workflow size={34}/><h2>工程中暂无技能</h2><p>创建技能后编辑节点并运行预览。</p><button className="primary" onClick={()=>void openCreate()}>新建技能</button></div>}
        <small className="canvas-help">Shift 框选 · Ctrl C / V 复制粘贴 · Delete 删除 · Ctrl Z 撤销</small>
      </div><SkillOverview project={project} skill={skill} openTimeline={id=>select(skill?.id??'','',id)}/></div></section>}
    </div><footer className={message.startsWith('Error')?'error':''} role="status">{message}<span>JSON 工程 · 真实 C# 预览</span></footer>
    {settings&&<Dialog label={settings==='graph'?'当前定义设置':settings==='connections'?'连线顺序':'工程设置'} close={()=>setSettings(null)}><div className="modal settings-dialog"><div className="settings-heading"><h2>{settings==='graph'?'当前定义设置':settings==='connections'?'连线顺序':'工程设置'}</h2><button onClick={()=>setSettings(null)}>关闭</button></div>
      {settings==='graph'&&<>{area?<><h3>共享区域定义</h3><p>本图保存范围、运动和生命周期效果；生成位置由引用节点配置。修改后重新发布使用者。</p><small>定义 ID {area.nodes.find(n=>n.id===area.root)?.runtimeId} · 当前预览技能：{skill?.name??'无'}</small></>:projectile?<><h3>共享弹体定义</h3><p>本图保存飞行、接触和终结行为；发射数量/散射由引用节点配置。修改后重新发布使用者。</p><small>定义 ID {projectile.nodes.find(n=>n.id===projectile.root)?.runtimeId} · 当前预览技能：{skill?.name??'无'}</small></>:timeline?<><h3>共享时间轴</h3><p>{project.skills.filter(s=>s.timelineRef===timeline.id).length} 个技能引用；修改后重新发布使用者。</p><TimelineProperties skill={timeline} change={value=>graphEditor.editGraph(g=>({...g,timeline:value}))}/><small>当前预览技能：{skill?.name??'无'}</small></>:buff?<><BuffProperties buff={buff} change={value=>editor.edit(p=>({...p,buffs:p.buffs?.map(b=>b.id===value.id?value:b)}))}/><small>当前预览技能：{skill?.name??'无'}</small></>:skill?<>
        <h3>▾ 技能属性</h3><p className="type-summary">{castMode} · {transfers}</p><label className="category-field">分类<input aria-label="技能分类" value={skill.category??''} onChange={event=>editor.edit(p=>({...p,skills:p.skills.map(s=>s.id===skill.id?{...s,category:event.target.value}:s)}))}/></label>
        <SharedTimelineControls project={project} skill={skill} update={editor.edit} open={id=>select(skill.id,'',id)} report={setMessage}/>
        <TimelineProperties skill={skill} change={timeline=>graphEditor.editGraph(g=>({...g,timeline}))}/>
        <SkillSummary project={project} skill={skill}/>
        </>:<p>空工程可保存或导出。</p>}
</>}
      {settings==='connections'&&graph&&<GraphConnections graph={graph} change={update=>{editor.endGroup();graphEditor.editGraph(update);}}/>}
      {settings==='project'&&<><SummonBlueprints project={project} update={editor.edit} action={setContentAction} duplicate={id=>duplicate('blueprint',id)}/><RuntimeProperties project={project} update={editor.edit}/><p>在此处编辑空闲起手。带前置的接续可在「连招」页编辑。</p></>}
    </div></Dialog>}
    {contentAction&&<ContentDialog key={contentAction.kind+contentAction.id+contentAction.mode} project={project} action={contentAction} close={()=>setContentAction(null)} edit={change=>{editor.endGroup();editor.edit(change);setMessage('内容与引用已更新，可撤销恢复');}} locate={locate}/>}
    {copying&&<SkillCopyDialog workspace={workspace} current={()=>editor.current.current!} apply={applySkillCopy} close={()=>setCopying(false)}/>}
    {exporting&&<ExportDialog project={project} skillId={skill?.id??''} close={()=>setExporting(false)} report={setMessage}/>}
    {importing&&<ImportDialog close={()=>setImporting(false)} ready={value=>{setPendingImport(value);setImporting(false);}}/>}
    {newName!==null&&<Dialog label="新建技能" close={()=>setNewName(null)} busy={creating}><form className="modal" onSubmit={event=>{event.preventDefault();void create();}}><h2>新建技能</h2><label>名称<input aria-label="新技能名称" autoFocus value={newName} onChange={event=>setNewName(event.target.value)}/></label>
      <label>模板<select aria-label="技能模板" value={template} onChange={event=>setTemplate(event.target.value)}>{skill&&<option value="copy">复制当前技能</option>}{examples.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <div><button type="button" disabled={creating} onClick={()=>setNewName(null)}>取消</button><button className="primary" disabled={!newName.trim()||creating}>{creating?'创建中…':'确定'}</button></div></form></Dialog>}
    {newProjectName!==null&&<Dialog label="新建工程" close={()=>setNewProjectName(null)} busy={creating}><form className="modal" onSubmit={event=>{event.preventDefault();void createProject();}}><h2>新建工程</h2><label>名称<input aria-label="新工程名称" autoFocus value={newProjectName} onChange={event=>setNewProjectName(event.target.value)}/></label><label>初始内容<select aria-label="工程初始内容" value={projectTemplate} onChange={event=>setProjectTemplate(event.target.value)}><option value="empty">空工程</option><option value="examples">恢复全部预置示例</option></select></label><p>当前编辑保留在撤销历史中。保存后更新磁盘工程。</p><div><button type="button" disabled={creating} onClick={()=>setNewProjectName(null)}>取消</button><button className="primary" disabled={creating||!newProjectName.trim()}>创建工程</button></div></form></Dialog>}
    {pendingImport&&<Dialog label="导入工程" close={()=>setPendingImport(null)} busy={creating}>
      <div className="modal">
        <h2>导入工程 · {pendingImport.project.name}</h2>
        <p>{pendingImport.project.skills.length} 技能、{pendingImport.project.buffs?.length??0} Buff、{pendingImport.project.summonBlueprints?.length??0} 蓝图。</p>
        {detachesReferences&&<section aria-label="待解除绑定的引用">
          <p className="error">运行包缺少以下 {detachedReferences.length} 处绑定所需的定义。继续导入会保留节点并解除这些绑定，合并时也会让这些字段保持未绑定。</p>
          <ul tabIndex={0} style={{maxHeight:'25vh',overflow:'auto',overflowWrap:'anywhere'}}>
            {detachedReferences.map(reference=><li key={reference.path}>
              <code>{reference.path}</code><br/>原 ID <code>{reference.originalId}</code>
            </li>)}
          </ul>
        </section>}
        <p>替换会载入文件中的工程和场景，运行包缺省的节点布局由网页生成。合并会保留当前场景，为冲突身份分配新 ID 并同步引用。两种操作均可撤销。</p>
        <div>
          <button disabled={creating} onClick={()=>setPendingImport(null)}>取消</button>
          <button disabled={creating} onClick={()=>void applyImport(true)}>{detachesReferences?'解除绑定并合并':'合并内容'}</button>
          <button className="primary" disabled={creating} onClick={()=>void applyImport(false)}>{detachesReferences?'解除绑定并替换':'替换工程'}</button>
        </div>
      </div>
    </Dialog>}
  </div>;
}
export default function App(){
  const [message,setMessage]=useState('正在连接本地 C# 宿主…');
  const editor=useProjectDocument(setMessage);
  if(!editor.project||!editor.workspace)return <div className="loading"><Workflow size={32}/><h1>技能实验室</h1><p role="status">{message}</p><button disabled={editor.loading} onClick={()=>void editor.load().catch(error=>setMessage(String(error)))}>重新连接</button></div>;
  return <ReactFlowProvider key={editor.workspace}><Workbench editor={editor} workspace={editor.workspace} project={editor.project} message={message} setMessage={setMessage}/></ReactFlowProvider>;
}
