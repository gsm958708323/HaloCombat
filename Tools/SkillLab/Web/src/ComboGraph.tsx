import {useEffect,useMemo,useState,useSyncExternalStore} from 'react';
import {Background,BackgroundVariant,Handle,MarkerType,Position,ReactFlow,ReactFlowProvider,useNodesInitialized,useReactFlow} from '@xyflow/react';
import type {Connection,Edge,Node,NodeChange,NodeProps} from '@xyflow/react';
import type {Project,RuntimeCombo} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {comboStart,moveComboNode,nextComboInput} from './authoring/comboEdits';
import {layoutCombo} from './authoring/comboLayout';
import {comboTrace,tagLabel} from './authoring/comboTrace';
import type {comboScope} from './authoring/comboViews';
import {ComboTransitionEdge} from './ComboTransitionEdge';

type ComboNode=Node<{name:string;active:boolean;passed:boolean;open:()=>void},'combo'>;
function ComboSkillNode({id,data,selected}:NodeProps<ComboNode>){
  return <div className={'combo-skill-node'+(selected?' selected':'')+(data.active?' running':'')+(data.passed?' executed':'')}>
    {id!==comboStart&&<Handle type="target" position={Position.Left}/>}<strong title={data.name}>{data.name}</strong>
    <small>{id===comboStart?'空闲状态入口':data.active?'正在执行':data.passed?'本次已执行':'引用已有技能'}</small>
    {id!==comboStart&&<button className="nodrag" onClick={data.open}>编辑技能</button>}<Handle type="source" position={Position.Right}/>
  </div>;
}
const nodeTypes={combo:ComboSkillNode},edgeTypes={transition:ComboTransitionEdge};
type Props={project:Project;scope:ReturnType<typeof comboScope>;store:PreviewRuntimeStore;selected:string;selectedSource:string;skillId:string;
  selectRule:(id:string,source:string)=>void;selectSkill:(id:string)=>void;openSkill:(id:string)=>void;
  update:(f:(p:Project)=>Project)=>void;draft:(source:string,target:string)=>void;addReference:(id:string)=>void;
  beginEdit:()=>void;endEdit:()=>void};
function Canvas(props:Props){
  const {project,scope,store,selected,selectedSource,skillId,update}=props,flow=useReactFlow<ComboNode>(),initialized=useNodesInitialized();
  const runtime=useSyncExternalStore(store.subscribe,store.get),trace=comboTrace(runtime.state);
  const [adding,setAdding]=useState(project.skills[0]?.id??'');
  const ids=[comboStart,...scope.ids],rows=[...scope.entries,...scope.rows];
  const idsKey=scope.root+':'+(scope.view?.id??'$automatic')+':'+ids.join('|');
  const passed=trace.steps.map(s=>s.skill).join('|'),executed=[...trace.executedEdges].join('|'),active=trace.caster?.currentSkill;
  const layout=useMemo(()=>layoutCombo(scope.ids,rows,scope.root),[idsKey,project.runtime?.combos]);
  const positions=scope.view?.positions??project.editor?.comboPositions??[];
  const nodes=useMemo<ComboNode[]>(()=>ids.map(id=>({id,type:'combo',deletable:false,position:positions.find(p=>p.skillId===id)??layout.get(id)??{x:50,y:50},selected:id===skillId,
    data:{name:id===comboStart?'起手':project.skills.find(s=>s.id===id)?.name??id,active:active===id,passed:passed.split('|').includes(id),
      open:()=>props.openSkill(id)}})),[idsKey,positions,layout,project.skills,active,passed,skillId]);
  const name=(id:string)=>id===comboStart?'起手':project.skills.find(skill=>skill.id===id)?.name??id;
  const edges=useMemo<Edge[]>(()=>rows.flatMap(c=>(c.preSkills.length?c.preSkills.filter(id=>scope.ids.includes(id)):[comboStart]).map(from=>({
    id:c.id+'::'+from,source:from,target:c.toSkill,type:'transition',markerEnd:{type:MarkerType.ArrowClosed,color:trace.executedEdges.has(c.id+'::'+from)?'#8ed4b6':'#aebcb5'},
    label:(project.runtime?.arena?.bindings.find(b=>b.input===c.input)?.action??c.input)+(c.requiredTags.includes(1017)?' · 命中':'')+(c.requiredTags.some(t=>t!==1017&&t!==1001)?' · 状态条件':''),
    domAttributes:{onKeyDown:event=>{
      if(event.key==='Enter'||event.key===' '){event.preventDefault();props.selectRule(c.id,from);}
    }},
    ariaLabel:`${name(from)} → ${name(c.toSkill)} · ${c.input}${c.requiredTags.length?' · '+c.requiredTags.map(tagLabel).join(' + '):''}`,selected:c.id===selected&&from===selectedSource,
    style:{stroke:trace.executedEdges.has(c.id+'::'+from)?'#8ed4b6':c.id===selected&&from===selectedSource?'#e3c68b':'#899a95',strokeWidth:trace.executedEdges.has(c.id+'::'+from)?2.5:1.3},
    labelStyle:{fill:'#e3ece6'},labelBgStyle:{fill:'#192426'},data:{row:c.id,lane:rows.filter(row=>row.toSkill===c.toSkill&&(from===comboStart?!row.preSkills.length:row.preSkills.includes(from))).findIndex(row=>row.id===c.id)}
  }))),[idsKey,project.runtime?.combos,project.runtime?.arena?.bindings,project.skills,selected,selectedSource,executed,props.selectRule]);
  const fit=()=>void flow.fitView({padding:.22,minZoom:.1,maxZoom:1});
  // A new scope fits once. Runtime frames and condition changes never reframe the author's canvas.
  useEffect(()=>{if(!initialized)return;const frame=requestAnimationFrame(fit);return()=>cancelAnimationFrame(frame);},[scope.root,scope.view?.id,initialized]);
  function change(changes:NodeChange<ComboNode>[]){for(const c of changes)if(c.type==='position'&&c.position)update(p=>moveComboNode(p,c.id,c.position!,scope.view?.id));}
  function connect(c:Connection){if(c.source&&c.target)props.draft(c.source,c.target);}
  function arrange(){props.beginEdit();update(p=>[...layout].reduce((next,[id,position])=>moveComboNode(next,id,position,scope.view?.id),p));props.endEdit();requestAnimationFrame(fit);}
  return <div className="combo-graph-shell"><div className="combo-graph-toolbar"><button onClick={fit}>适应</button><button onClick={arrange}>自动排列</button>
    <select aria-label="添加技能引用" value={adding} onChange={e=>setAdding(e.target.value)}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select>
    <button disabled={!adding} onClick={()=>props.addReference(adding)}>添加引用</button><small>拖动端口建立接续 · 选中连线编辑条件</small></div>
    <ReactFlow<ComboNode> nodes={nodes} edges={edges} nodeTypes={nodeTypes} edgeTypes={edgeTypes} onNodesChange={change} onConnect={connect}
      onNodeDragStart={props.beginEdit} onNodeDragStop={props.endEdit} onNodeClick={(_e,node)=>props.selectSkill(node.id)}
      onNodeDoubleClick={(_e,node)=>{if(node.id!==comboStart)props.openSkill(node.id);}} onEdgeClick={(_e,edge)=>props.selectRule(String(edge.data?.row),edge.source)}
      deleteKeyCode={null} fitView fitViewOptions={{padding:.22,minZoom:.1,maxZoom:1}} minZoom={.1} maxZoom={3} colorMode="dark" proOptions={{hideAttribution:true}}>
      <Background variant={BackgroundVariant.Dots} gap={18} size={1} color="#364548"/>
    </ReactFlow><div className="combo-legend"><span>细线：可能接续</span><span>绿色：本次成功执行</span><span>同一规则的多个前置是不同来源</span></div>
  </div>;
}
export function ComboGraph(props:Props){return <ReactFlowProvider><Canvas {...props}/></ReactFlowProvider>;}
export function comboDraft(project:Project,source:string,target:string,recent?:string):RuntimeCombo{
  if(target===comboStart)throw new Error('起手不能作为目标');
  return {id:crypto.randomUUID(),purpose:source===comboStart?'entry':'transition',input:nextComboInput(project,source,recent),
    preSkills:source===comboStart?[]:[source],toSkill:target,requiredTags:[],priority:0};
}
