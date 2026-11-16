import {useEffect,useMemo,useState,useSyncExternalStore} from 'react';
import {ReactFlow,ReactFlowProvider,Handle,Position,Background,BackgroundVariant,useReactFlow,useNodesInitialized,MarkerType} from '@xyflow/react';
import type {Node,NodeProps,NodeChange,Connection,Edge} from '@xyflow/react';
import type {Project,Skill} from './model';
import {comboStart,connectCombo,patchCombo,removeCombo,moveComboNode,removeComboSkill} from './authoring/comboEdits';
import {allComboInputs,comboView,layoutCombo} from './authoring/comboLayout';
import type {PreviewRuntimeStore} from './previewRuntime';
import {ComboTimeline} from './ComboTimeline';
import {ComboTransitionEdge} from './ComboTransitionEdge';
import {api} from './model';
import './combo-editor.css';

type ComboNode=Node<{name:string;active:boolean;open:()=>void},'combo'>;
function ComboSkillNode({id,data,selected}:NodeProps<ComboNode>){
  return <div className={`combo-skill-node${selected?' selected':''}${data.active?' running':''}`}>
    {id!==comboStart&&<Handle type="target" position={Position.Left}/>}<strong title={data.name}>{data.name}</strong><small>{id===comboStart?'空闲状态起手':'引用技能'}</small>
    {id!==comboStart&&<button className="nodrag" onClick={data.open}>编辑技能</button>}<Handle type="source" position={Position.Right}/>
  </div>;
}
const nodeTypes={combo:ComboSkillNode};
const edgeTypes={transition:ComboTransitionEdge};
function ComboCanvas({project,update,openSkill,store,report,beginEdit,endEdit}:{project:Project;update:(f:(p:Project)=>Project)=>void;openSkill:(id:string)=>void;store:PreviewRuntimeStore;report:(s:string)=>void;beginEdit:()=>void;endEdit:()=>void}){
  const flow=useReactFlow<ComboNode>(),initialized=useNodesInitialized();
  const [inputFilter,setInputFilter]=useState('');
  const [extraIds,setExtraIds]=useState<string[]>([]);
  const runtime=useSyncExternalStore(store.subscribe,store.get),[selected,setSelected]=useState(''),[skillId,setSkillId]=useState(''),[adding,setAdding]=useState(project.skills[0]?.id??'');
  const {tokens,selected:input,rows,ids,total}=useMemo(()=>comboView(project,inputFilter,extraIds),[project,inputFilter,extraIds]),edge=rows.find(c=>c.id===selected);
  const caster=runtime.state?.actors.find(a=>a.kind==='caster'),active=project.skills.find(s=>s.id===caster?.currentSkill||s.name===caster?.currentSkill);
  const lastResolved=runtime.state?.events.filter(e=>e.kind==='combo-resolved'&&runtime.state!.time-e.time<.35).at(-1),skill=project.skills.find(s=>s.id===skillId)??project.skills.find(s=>s.id===edge?.preSkills[0])??project.skills.find(s=>s.id===ids[0]);
  const idsKey=project.id+':'+input+':'+ids.join('|');
  const fit=()=>void flow.fitView({padding:.22,minZoom:.1,maxZoom:1});
  useEffect(()=>{if(!initialized)return;const frame=requestAnimationFrame(fit);return()=>cancelAnimationFrame(frame);},[idsKey,initialized,flow]);
  const positions=project.editor?.comboPositions??[],layout=useMemo(()=>layoutCombo(ids,rows),[idsKey,project.runtime?.combos]);
  const nodes=useMemo<ComboNode[]>(()=>[comboStart,...ids].map(id=>({id,type:'combo',deletable:id!==comboStart,position:positions.find(p=>p.skillId===id)??layout.get(id)!,selected:id===skillId,
    data:{name:id===comboStart?'起手':project.skills.find(s=>s.id===id)?.name??id,active:active?.id===id,open:()=>openSkill(id)}})),[idsKey,positions,layout,project.skills,active?.id,skillId]);
  const edges=useMemo<Edge[]>(()=>rows.flatMap(c=>(c.preSkills.length?c.preSkills:[comboStart]).map(from=>({id:c.id+'::'+from,source:from,target:c.toSkill,type:'transition',markerEnd:{type:MarkerType.ArrowClosed,color:'#c6cbb3'},
    label:c.input+(c.requiredTags.includes(1017)?' · 命中确认':''),animated:lastResolved?.result.includes(c.id)===true,selected:c.id===selected,
    style:{stroke:c.id===selected?'#f2d38e':'#b1bcae'},labelStyle:{fill:'#d3ded7'},labelBgStyle:{fill:'#233036'},data:{row:c.id,
      lane:rows.filter(row=>row.toSkill===c.toSkill&&(from===comboStart?row.preSkills.length===0:row.preSkills.includes(from))).findIndex(row=>row.id===c.id)}}))),[rows,selected,lastResolved?.sequence]);
  function change(changes:NodeChange<ComboNode>[]){for(const change of changes)if(change.type==='position'&&change.position)update(p=>moveComboNode(p,change.id,change.position!));}
  function connect(connection:Connection){try{if(input===allComboInputs)throw new Error('请先选择具体输入链路，再添加接续');if(connection.source&&connection.target)update(p=>connectCombo(p,connection.source!,connection.target!,crypto.randomUUID(),input));}catch(e){report(String(e));}}
  const updateEdge=(value:Parameters<typeof patchCombo>[2])=>{if(edge){update(p=>patchCombo(p,edge.id,value));if(value.input&&input!==allComboInputs)setInputFilter(value.input);}};
  function arrange(){beginEdit();update(p=>[...layout].reduce((next,[id,position])=>moveComboNode(next,id,position),p));endEdit();requestAnimationFrame(fit);}
  function addReference(id:string,position:{x:number;y:number}){setExtraIds(previous=>[...new Set([...previous,id])]);update(p=>moveComboNode(p,id,position));setSkillId(id);}
  function removeReference(id:string){setExtraIds(previous=>previous.filter(value=>value!==id));update(p=>removeComboSkill(p,id,input===allComboInputs?undefined:input));}
  const conditions=(edge?.requiredTags??[]).filter(t=>t!==1017);
  return <section className="combo-editor"><div className="combo-toolbar"><strong>连招编辑</strong>
    <label>输入链路<select aria-label="连招输入链路" value={input} onChange={e=>{setInputFilter(e.target.value);setExtraIds([]);setSelected('');setSkillId('');}}>{(tokens.length?tokens:['Attack']).map(token=><option key={token} value={token}>{token}</option>)}<option value={allComboInputs}>全部输入总览</option></select></label>
    <button onClick={fit}>适应</button><button onClick={arrange}>自动排列</button>
    <select aria-label="添加连招技能" value={adding} onChange={e=>setAdding(e.target.value)}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select>
    <button disabled={!adding} draggable={!!adding} onDragStart={e=>{e.dataTransfer.setData('application/halo-combo-skill',adding);e.dataTransfer.effectAllowed='copy';}} onClick={()=>addReference(adding,{x:350+(ids.length%2)*290,y:90+Math.floor(ids.length/2)*145})}>添加技能引用</button>
    <label className="combo-buffer">输入缓冲 / 秒<input aria-label="连招输入缓冲" type="number" min=".01" max="10" step=".05" value={project.runtime?.arena?Number(project.runtime.actors.find(a=>a.id===project.runtime!.arena!.player)?.inputBufferSeconds??.2):project.preview.inputBufferSeconds??.2}
      onChange={e=>{if(e.target.value!==''){const seconds=Number(e.target.value);update(p=>p.runtime?.arena?{...p,runtime:{...p.runtime,actors:p.runtime.actors.map(a=>a.id===p.runtime!.arena!.player?{...a,inputBufferSeconds:seconds}:a)}}:{...p,preview:{...p.preview,inputBufferSeconds:seconds}});}}}/></label>
    <button onClick={()=>void api('/validate',project).then(()=>report('连招与全部技能校验通过')).catch(e=>report(String(e)))}>校验连招</button><span>拖动端口建立接续 · 双击技能打开</span></div>
    <div className="combo-workspace"><div className="combo-canvas" onDragOver={e=>{e.preventDefault();e.dataTransfer.dropEffect='copy';}} onDrop={e=>{e.preventDefault();const id=e.dataTransfer.getData('application/halo-combo-skill');if(project.skills.some(s=>s.id===id))addReference(id,flow.screenToFlowPosition({x:e.clientX,y:e.clientY}));}}><ReactFlow<ComboNode> nodes={nodes} edges={edges} nodeTypes={nodeTypes} edgeTypes={edgeTypes} onNodesChange={change} onConnect={connect}
      onNodeDragStart={beginEdit} onNodeDragStop={endEdit} onNodesDelete={nodes=>{setExtraIds(previous=>previous.filter(id=>!nodes.some(n=>n.id===id)));update(p=>nodes.reduce((value,node)=>removeComboSkill(value,node.id,input===allComboInputs?undefined:input),p));}}
      onNodeClick={(_e,n)=>{setSkillId(n.id);setSelected('');}} onNodeDoubleClick={(_e,n)=>{if(n.id!==comboStart)openSkill(n.id);}} onEdgeClick={(_e,e)=>{setSelected(String(e.data?.row));setSkillId('');}}
      onEdgesDelete={values=>update(p=>values.reduce((next,e)=>removeCombo(next,String(e.data?.row)),p))} deleteKeyCode={['Backspace','Delete']} minZoom={.1} fitView fitViewOptions={{minZoom:.1,maxZoom:1,padding:.22}} colorMode="dark" proOptions={{hideAttribution:true}}>
      <Background variant={BackgroundVariant.Dots} gap={18} size={1}/></ReactFlow>
      <div className="combo-live"><strong>连招试放</strong>{(input===allComboInputs?tokens:tokens.filter(t=>t===input)).map(token=><button key={token} disabled={!runtime.ready} onClick={()=>{runtime.play();runtime.send(token);}}>{token}</button>)}{!tokens.length&&<small>从“起手”连接技能后可发送输入</small>}
        <small>当前：{active?.name??caster?.currentSkill??'空闲'} · 缓冲：{caster?.bufferedInput??'无'} · {Number(caster?.tags?.['1001']??0)>0?'接续窗口开启':'动作播放中按时间轴窗口接续'}</small>
        <span role="status">{caster?.comboFailure??runtime.state?.events.filter(e=>['combo-unmatched','rejected'].includes(e.kind)).at(-1)?.result??'输入经过实际 InputBuffer / Combo / SkillCast'}</span></div>
      <ComboTimeline project={project} skill={skill} progress={active?.id===skill?.id?caster?.actionTime??0:0}
        changeWindow={(field,value)=>update(p=>({...p,skills:p.skills.map(s=>s.id!==skill?.id?s:{...s,nodes:s.nodes.map(n=>n.kind==='cast'?{...n,parameters:{...n.parameters,[field]:value}}:n)})}))}
        change={(timeline,sharedId)=>update(p=>sharedId?{...p,timelines:p.timelines?.map(t=>t.id===sharedId?{...t,timeline}:t)}:{...p,skills:p.skills.map(s=>s.id===skill?.id?{...s,timeline}:s)})}/>
    </div><aside className="combo-overview" aria-label="当前连招信息"><h3>{input===allComboInputs?'全部连招':input+' 连招'}</h3><p>{ids.length} 个技能 · {rows.length} 条接续</p><small>工程共有 {total} 条接续；当前按输入查看。技能复用原定义，播放中使用技能取消窗。</small>
      {edge?<section><h4>接续规则</h4><label>输入令牌<input aria-label="接续输入" value={edge.input} onChange={e=>updateEdge({input:e.target.value})}/></label>
        <label>接续技能<select aria-label="接续目标技能" value={edge.toSkill} onChange={e=>updateEdge({toSkill:e.target.value})}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
        <label>优先级<input aria-label="接续优先级" type="number" value={edge.priority} onChange={e=>{if(e.target.value!=='')updateEdge({priority:Number(e.target.value)});}}/></label>
        <label><input type="checkbox" aria-label="接续需要命中确认" checked={edge.requiredTags.includes(1017)} onChange={e=>updateEdge({requiredTags:[...conditions,...(e.target.checked?[1017]:[])]})}/>需要命中确认</label>
        <label>状态 Tag（逗号分隔）<input aria-label="接续状态条件" value={conditions.join(',')} onChange={e=>{const values=e.target.value.split(',').filter(Boolean).map(Number);if(values.every(n=>Number.isInteger(n)&&n>0))updateEdge({requiredTags:[...values,...(edge.requiredTags.includes(1017)?[1017]:[])]});}}/></label>
        <button onClick={()=>update(p=>removeCombo(p,edge.id))}>删除接续</button><small>{edge.id}</small></section>:<p>点击连线编辑输入、条件和优先级。</p>}
      {edge&&<button onClick={()=>{const id=crypto.randomUUID();update(p=>({...p,runtime:{...p.runtime!,combos:[...p.runtime!.combos,{...edge,id,priority:Math.max(...p.runtime!.combos.map(c=>c.priority))+10}]}}));setSelected(id);}}>添加条件分支</button>}
      {skillId&&skillId!==comboStart&&<button onClick={()=>{removeReference(skillId);setSkillId('');}}>移除此技能引用</button>}
      <section><h4>已配置接续</h4>{rows.map(c=><button className={c.id===selected?'active':''} key={c.id} onClick={()=>setSelected(c.id)}>{c.preSkills.map(id=>project.skills.find(s=>s.id===id)?.name??id).join(' / ')||'起手'} → {project.skills.find(s=>s.id===c.toSkill)?.name??c.toSkill}<small>{c.input} · 优先级 {c.priority}</small></button>)}</section>
    </aside></div></section>;
}
export function ComboEditor(props:Parameters<typeof ComboCanvas>[0]){return <ReactFlowProvider><ComboCanvas {...props}/></ReactFlowProvider>;}
