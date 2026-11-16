import {useMemo,useRef,useState,useSyncExternalStore} from 'react';
import type {ComboPurpose,ComboView,Project,RuntimeCombo} from './model';
import {api} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {applyComboRule,comboStart,moveComboNode,removeCombo} from './authoring/comboEdits';
import {automaticComboView,comboScope,deleteComboView,saveComboView} from './authoring/comboViews';
import {inputLabel,purposeLabel,rulePurpose} from './authoring/comboRules';
import {ComboGraph,comboDraft} from './ComboGraph';
import {ComboRuleForm} from './ComboRuleForm';
import {ComboBindings} from './ComboBindings';
import {ComboCancellation} from './ComboCancellation';
import {ComboProperties} from './ComboProperties';
import {ComboRunFeedback} from './ComboRunFeedback';
import {ComboTimeline} from './ComboTimeline';
import {SkillOverview} from './SkillOverview';
import {Dialog} from './Dialog';
import './combo-editor.css';
import './combo-authoring.css';

type Mode=ComboPurpose|'all';
type Props={project:Project;update:(f:(p:Project)=>Project)=>void;store:PreviewRuntimeStore;report:(s:string)=>void;
  beginEdit:()=>void;endEdit:()=>void;selectContext:(id:string)=>void;openSkill:(id:string)=>void;openTimeline:(skillId:string,timelineId:string)=>void};
function LiveTimeline({project,skillId,store,update}:{project:Project;skillId:string;store:PreviewRuntimeStore;update:Props['update']}){
  const runtime=useSyncExternalStore(store.subscribe,store.get),actor=runtime.state?.actors.find(a=>a.kind==='caster'),skill=project.skills.find(s=>s.id===skillId);
  return <ComboTimeline project={project} skill={skill} progress={actor?.currentSkill===skillId?actor.actionTime??0:0}
    changeWindow={(field,value)=>update(p=>({...p,skills:p.skills.map(s=>s.id!==skillId?s:{...s,nodes:s.nodes.map(n=>n.kind==='cast'?{...n,parameters:{...n.parameters,[field]:value}}:n)})}))}
    change={(timeline,sharedId)=>update(p=>sharedId?{...p,timelines:p.timelines?.map(t=>t.id===sharedId?{...t,timeline}:t)}:{...p,skills:p.skills.map(s=>s.id===skillId?{...s,timeline}:s)})}/>;
}
export function ComboEditor(props:Props){
  const {project,update,report,store}=props,latest=useRef(project);latest.current=project;
  const views=project.editor?.comboViews??[];
  const initialRoot=project.editor?.selectedSkill??project.skills[0]?.id??'',initialView=views.find(v=>v.rootSkill===initialRoot);
  const [mode,setMode]=useState<Mode>('transition'),[root,setRoot]=useState(initialRoot);
  const [viewId,setViewId]=useState(initialView?.id??automaticComboView),[expanded,setExpanded]=useState(false),[extraIds,setExtraIds]=useState<string[]>([]);
  const [selected,setSelected]=useState(''),[skillId,setSkillId]=useState(''),[draft,setDraft]=useState<RuntimeCombo|null>(null);
  const [savingView,setSavingView]=useState(false),[viewName,setViewName]=useState('');
  const activeView=views.some(v=>v.id===viewId)?viewId:automaticComboView;
  const currentRoot=project.skills.some(s=>s.id===root)?root:project.skills[0]?.id??'';
  const scope=useMemo(()=>comboScope(project,currentRoot,activeView,expanded,extraIds),[project,currentRoot,activeView,expanded,extraIds]);
  const rules=project.runtime?.combos??[],selectedRule=rules.find(r=>r.id===selected);
  const contextSkill=project.skills.find(s=>s.id===(mode==='transition'?scope.root:currentRoot));
  const timelineId=skillId&&skillId!==comboStart?skillId:selectedRule?.preSkills[0]??scope.root;
  const unassigned=rules.filter(r=>rulePurpose(r)==='unassigned');
  function editRule(row:RuntimeCombo){setSelected(row.id);setDraft(row);}
  function add(purpose:ComboPurpose,source=scope.root,target=contextSkill?.id??project.skills[0]?.id??''){
    setDraft({id:crypto.randomUUID(),purpose,input:purpose==='transition'?rules.find(r=>r.preSkills.includes(source))?.input??'Attack':'Attack',
      preSkills:purpose==='entry'?[]:source?[source]:[],toSkill:target,requiredTags:[],priority:0});
  }
  async function apply(row:RuntimeCombo){
    const before=latest.current,candidate=applyComboRule(before,row,activeView===automaticComboView?undefined:activeView);
    await api('/document/validate',candidate);
    if(latest.current!==before)throw new Error('校验期间工程已改变，请重新应用草稿');
    props.endEdit();update(()=>candidate);setSelected(row.id);report('已应用'+purposeLabel[rulePurpose(row)]+'；修改同一规则会同步影响全部引用');
  }
  function remove(id:string){props.endEdit();update(p=>removeCombo(p,id));if(selected===id)setSelected('');report('已删除运行规则，可撤销恢复；所有视图引用已同步');}
  function saveView(){
    const id=scope.view?.id??crypto.randomUUID(),value:ComboView={id,name:viewName,rootSkill:scope.root,ruleIds:scope.rows.map(r=>r.id),
      positions:scope.view?.positions??(project.editor?.comboPositions??[]).filter(p=>scope.ids.includes(p.skillId)||p.skillId===comboStart)};
    try{props.endEdit();update(p=>saveComboView(p,value));setViewId(id);setSavingView(false);report('已保存编排视图；保存工程后写入磁盘，视图不改变运行匹配');}catch(error){report(String(error));}
  }
  const name=(id:string)=>project.skills.find(s=>s.id===id)?.name??id;
  const inputs=mode==='transition'?[...scope.entries,...scope.rows,...scope.cancels].map(r=>r.input):mode==='entry'?rules.filter(r=>!r.preSkills.length&&r.toSkill===contextSkill?.id).map(r=>r.input):mode==='cancel'?rules.filter(r=>rulePurpose(r)==='cancel').map(r=>r.input):rules.map(r=>r.input);
  return <section className="combo-editor" aria-label="连招编排工作台">
    <header className="combo-toolbar"><strong>技能与连招编排</strong><span>规则定义行为 · 视图组织内容</span><label>输入缓冲 / 秒<input aria-label="连招输入缓冲" type="number" min=".01" max="10" step=".05"
      value={project.runtime?.arena?Number(project.runtime.actors.find(a=>a.id===project.runtime!.arena!.player)?.inputBufferSeconds??.2):project.preview.inputBufferSeconds??.2}
      onChange={e=>{if(e.target.value!==''){const value=Number(e.target.value);update(p=>p.runtime?.arena?{...p,runtime:{...p.runtime,actors:p.runtime.actors.map(a=>a.id===p.runtime!.arena!.player?{...a,inputBufferSeconds:value}:a)}}:{...p,preview:{...p.preview,inputBufferSeconds:value}});}}}/></label>
      <button onClick={()=>void api('/validate',project).then(()=>report('全部技能与全表规则校验通过')).catch(e=>report(String(e)))}>校验工程</button></header>
    <div className="combo-author-shell"><aside className="combo-task-nav" aria-label="连招作者操作">
      <small>操作配置</small>{([['entry','起手绑定'],['transition','动作接续'],['cancel','公共取消'],['all','规则总览']] as const).map(([id,label])=><button key={id} aria-pressed={mode===id} onClick={()=>setMode(id)}>{label}{id==='all'&&unassigned.length>0?' · 待标注'+unassigned.length:''}</button>)}
      <label>当前技能<select aria-label="连招当前技能" value={currentRoot} onChange={e=>{setRoot(e.target.value);props.selectContext(e.target.value);setViewId(automaticComboView);setSelected('');setSkillId('');setExtraIds([]);}}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <small>功能编排视图</small><button aria-pressed={activeView===automaticComboView} onClick={()=>{setViewId(automaticComboView);setExpanded(false);}}>从当前技能展开</button>
      {views.map(v=><button key={v.id} aria-pressed={activeView===v.id} onClick={()=>{setRoot(v.rootSkill);props.selectContext(v.rootSkill);setViewId(v.id);setMode('transition');setExpanded(false);setSelected('');setSkillId('');setExtraIds([]);}}>{v.name}</button>)}
      <p>视图引用同一技能与规则。公共规则仍在全表生效。</p>
    </aside><div className="combo-author-main">
      <div className="combo-task-heading"><div><h3>{mode==='transition'?scope.view?.name??name(scope.root):mode==='entry'?name(contextSkill?.id??'')+' · 起手':mode==='cancel'?'公共取消':'全部运行规则'}</h3>
        <small>{mode==='transition'?scope.ids.length+'个技能引用 · '+scope.rows.length+'条接续':rules.length+'条运行规则；不等于连招套数'}</small></div>
        {mode==='transition'&&<><button onClick={()=>{setViewName(scope.view?.name??name(scope.root)+'接续');setSavingView(true);}}>保存视图</button>{scope.view&&<button onClick={()=>{props.endEdit();update(p=>deleteComboView(p,scope.view!.id));setViewId(automaticComboView);}}>删除视图</button>}<button disabled={!scope.root} onClick={()=>add('transition')}>添加接续</button></>}
      </div>
      {mode==='transition'?<><div className="combo-scope-summary"><button onClick={()=>setMode('cancel')}>公共取消 · {scope.cancels.length}条影响当前范围</button><button onClick={()=>setExpanded(!expanded)}>{expanded?'折叠':'展开'}其他接入来源 · {scope.incoming.length}条规则</button></div>
        {scope.external.length>0&&<details className="combo-external"><summary>当前视图外仍生效的接续 · {scope.external.length}条</summary>{scope.external.map(row=><div key={row.id}><span>{row.preSkills.map(name).join(' / ')} → {name(row.toSkill)} · {row.input}</span><button onClick={()=>update(p=>saveComboView(p,{...scope.view!,ruleIds:[...scope.view!.ruleIds,row.id]}))}>加入当前视图</button><button onClick={()=>editRule(row)}>编辑规则</button></div>)}</details>}
        {scope.unassigned.length>0&&<p className="combo-notice">当前范围有 {scope.unassigned.length} 条旧接续未标用途，运行行为保持；可编辑规则明确归类。</p>}
        <ComboGraph project={project} scope={scope} store={store} selected={selected} skillId={skillId}
          update={update} beginEdit={props.beginEdit} endEdit={props.endEdit} openSkill={props.openSkill}
          selectRule={id=>{setSelected(id);setSkillId('');}} selectSkill={id=>{setSkillId(id);setSelected('');}}
          draft={(source,target)=>{try{setDraft(comboDraft(project,source,target));}catch(error){report(String(error));}}}
          addReference={id=>{setExtraIds(previous=>[...new Set([...previous,id])]);setSkillId(id);update(p=>moveComboNode(p,id,{x:350+extraIds.length*45,y:400},scope.view?.id));}}/>
        <section className="combo-selected-rule"><h3>{selectedRule?(selectedRule.preSkills.length>1?'适用技能集合 · '+selectedRule.preSkills.length:selectedRule.preSkills.length?name(selectedRule.preSkills[0]):'空闲')+' → '+name(selectedRule.toSkill):skillId&&skillId!==comboStart?'技能引用 · '+name(skillId):'选择连线查看接续条件'}</h3>
          {selectedRule&&<><p>{inputLabel(project,selectedRule.input)} · {selectedRule.requiredTags.includes(1017)?'本次命中确认 · ':''}优先级 {selectedRule.priority} · {purposeLabel[rulePurpose(selectedRule)]}</p>
            <small>所需状态全部满足；动作播放中要求接续窗口。{views.filter(v=>v.ruleIds.includes(selectedRule.id)).length}个编排视图引用此规则。</small><div><button onClick={()=>editRule(selectedRule)}>编辑条件与优先顺序</button>{scope.view?.ruleIds.includes(selectedRule.id)&&<button onClick={()=>{update(p=>saveComboView(p,{...scope.view!,ruleIds:scope.view!.ruleIds.filter(id=>id!==selectedRule.id)}));setSelected('');}}>移出当前视图</button>}<button onClick={()=>remove(selectedRule.id)}>删除运行规则</button></div></>}
          {skillId&&skillId!==comboStart&&<button onClick={()=>add('transition',skillId)}>从此技能添加接续</button>}</section>
        <LiveTimeline project={project} skillId={timelineId} store={store} update={update}/>
      </>:mode==='entry'?<ComboBindings project={project} skillId={contextSkill?.id??''} edit={editRule} remove={remove} add={()=>add('entry')}/>:
        mode==='cancel'?<ComboCancellation project={project} ids={scope.ids} edit={editRule} remove={remove} add={()=>add('cancel')}/>:
        <section className="combo-task-content"><p className="combo-note">总览包含起手、接续与公共取消。未标用途的旧规则不会被猜测归类或自动删除。</p><ComboProperties project={project} rows={rules} edit={editRule} remove={remove} showPurpose/><button onClick={()=>add('transition')}>添加规则</button></section>}
      <ComboRunFeedback project={project} store={store} inputs={inputs} selectedInput={selectedRule?.input}/>
    </div><SkillOverview project={project} skill={contextSkill} openTimeline={id=>props.openTimeline(contextSkill?.id??'',id)}/></div>
    {draft&&<ComboRuleForm key={draft.id} project={project} initial={draft} apply={apply} close={()=>setDraft(null)}/>}
    {savingView&&<Dialog label="保存功能编排视图" close={()=>setSavingView(false)}><form className="modal" onSubmit={e=>{e.preventDefault();saveView();}}><h2>保存功能编排视图</h2><label>名称<input aria-label="编排视图名称" value={viewName} maxLength={128} onChange={e=>setViewName(e.target.value)}/></label><p>保存根技能、当前接续规则引用和布局。其他全局规则仍会参与运行匹配。</p><div className="dialog-actions"><button type="button" onClick={()=>setSavingView(false)}>取消</button><button className="primary" disabled={!viewName.trim()}>保存视图</button></div></form></Dialog>}
  </section>;
}
