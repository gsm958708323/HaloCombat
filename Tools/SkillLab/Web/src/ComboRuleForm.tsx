import {useRef,useState} from 'react';
import type {Project,RuntimeCombo} from './model';
import {clone} from './model';
import {Dialog} from './Dialog';
import {changedResolution,inputLabel,overlappingRules,purposeLabel,rulePurpose,validateRule} from './authoring/comboRules';
import {tagLabel} from './authoring/comboTrace';

export function ComboRuleForm({project,initial,apply,close}:{project:Project;initial:RuntimeCombo;apply:(row:RuntimeCombo)=>Promise<void>;close:()=>void}){
  const [row,setRow]=useState(()=>clone(initial)),[tags,setTags]=useState(initial.requiredTags.filter(t=>t!==1017).join(', '));
  const [order,setOrder]=useState(''),[acknowledged,setAcknowledged]=useState(false),[message,setMessage]=useState(''),[busy,setBusy]=useState(false);
  const original=project.runtime?.combos.find(r=>r.id===initial.id),changed=changedResolution(original,row);
  const overlaps=overlappingRules(project,row),needsDecision=changed&&overlaps.length>0;
  const usedBy=project.editor?.comboViews?.filter(v=>v.ruleIds.includes(row.id))??[];
  const baseline=useRef(JSON.stringify(project.runtime?.combos));
  const stale=baseline.current!==JSON.stringify(project.runtime?.combos);
  const name=(id:string)=>project.skills.find(s=>s.id===id)?.name??id;
  function edit(patch:Partial<RuntimeCombo>){setRow(r=>({...r,...patch}));setOrder('');setAcknowledged(false);setMessage('');}
  function choose(value:string){
    setOrder(value);setAcknowledged(false);
    if(value==='existing')setRow(r=>({...r,priority:Math.min(...overlaps.map(c=>c.priority))-1}));
    if(value==='draft')setRow(r=>({...r,priority:Math.max(...overlaps.map(c=>c.priority))+1}));
  }
  async function submit(){
    try{
      if(stale)throw new Error('规则表已改变，请关闭草稿后重新打开');
      if(needsDecision&&(!order||!acknowledged))throw new Error('请明确选择优先顺序并确认影响范围');
      const parts=tags.trim()?tags.trim().split(/[\s,，]+/).map(Number):[];
      if(parts.some(t=>!Number.isInteger(t)||t<=0||t>2147483647))throw new Error('状态Tag必须是正整数');
      const next={...row,requiredTags:[...new Set([...parts,...(row.requiredTags.includes(1017)?[1017]:[])])]};
      validateRule(project,next);setBusy(true);await apply(next);close();
    }catch(error){setMessage(String(error));}finally{setBusy(false);}
  }
  return <Dialog label={original?'编辑接续规则':'新增接续规则'} close={close} busy={busy}><form className="modal combo-rule-dialog" onSubmit={e=>{e.preventDefault();void submit();}}>
    <div className="settings-heading"><h2>{original?'编辑规则':'新增规则'}</h2><button type="button" disabled={busy} onClick={close}>关闭</button></div>
    <label>用途<select aria-label="规则用途" value={row.purpose??(row.preSkills.length?'':'entry')} onChange={e=>edit({purpose:e.target.value?e.target.value as RuntimeCombo['purpose']:undefined,preSkills:e.target.value==='entry'?[]:row.preSkills})}>
      {!row.purpose&&row.preSkills.length>0&&<option value="">未标用途 · 保留原行为</option>}<option value="entry">起手绑定</option><option value="transition">动作接续</option><option value="cancel">公共取消</option></select></label>
    {row.purpose!=='entry'&&!(row.preSkills.length===0&&!row.purpose)&&<label>适用前置技能 · 满足其中一个<select aria-label="规则前置技能" multiple size={Math.min(6,project.skills.length)} value={row.preSkills} onChange={e=>edit({preSkills:[...e.target.selectedOptions].map(o=>o.value)})}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>}
    {rulePurpose(row)==='entry'&&<p className="combo-note">前置状态：空闲。动作播放中不会使用空闲入口替代接续规则。</p>}
    <div className="combo-form-grid"><label>触发输入<input aria-label="规则触发输入" list="combo-input-identities" value={row.input} maxLength={128} onChange={e=>edit({input:e.target.value})}/><datalist id="combo-input-identities">{[...new Set(project.runtime?.combos.map(r=>r.input)??[])].map(input=><option key={input} value={input}>{inputLabel(project,input)}</option>)}</datalist></label>
      <label>目标技能<select aria-label="规则目标技能" value={row.toSkill} onChange={e=>edit({toSkill:e.target.value})}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label></div>
    <label className="combo-check"><input type="checkbox" aria-label="规则需要本次命中" checked={row.requiredTags.includes(1017)} onChange={e=>edit({requiredTags:[...row.requiredTags.filter(t=>t!==1017),...(e.target.checked?[1017]:[])]})}/>需要本次施法命中确认</label>
    <p className="combo-note">动作播放中统一要求前置技能的接续窗口开启；其他所需状态必须全部满足。</p>
    <details><summary>其他状态与优先级</summary><label>所需状态 Tag<input aria-label="规则其他状态" value={tags} onChange={e=>{setTags(e.target.value);const parsed=e.target.value.trim()?e.target.value.trim().split(/[\s,，]+/).map(Number):[];edit({requiredTags:[...parsed,...(row.requiredTags.includes(1017)?[1017]:[])]});}}/></label>
      <label>优先级<input aria-label="规则优先级" type="number" step="1" value={row.priority} onChange={e=>{edit({priority:Number(e.target.value)});setOrder('manual');}}/></label></details>
    {overlaps.length>0&&<section className="combo-conflict" aria-label="规则重叠与影响范围"><strong>{needsDecision?'条件可能同时满足，请决定优先顺序':'现有重叠规则'}</strong>
      <ul>{overlaps.map(r=><li key={r.id}>{r.preSkills.map(name).join(' / ')||'空闲'} → {name(r.toSkill)} · {purposeLabel[rulePurpose(r)]} · 优先级 {r.priority}<small>{r.requiredTags.map(tagLabel).join(' + ')||'无附加状态'} · {r.id}</small></li>)}</ul>
      {needsDecision&&<><label>冲突处理<select aria-label="规则冲突处理" value={order} onChange={e=>choose(e.target.value)}><option value="">请选择</option><option value="existing">已有规则优先，当前草稿作为后备</option><option value="draft">当前草稿优先，覆盖重叠候选</option><option value="manual">使用手动优先级</option></select></label>
        <p>草稿优先级：{row.priority}。本次查找不回退其他候选；输入保留在缓冲时，后续逻辑步仍会按变化后的状态重新匹配。</p><label className="combo-check"><input type="checkbox" aria-label="确认规则重叠影响" checked={acknowledged} onChange={e=>setAcknowledged(e.target.checked)}/>确认对全部重叠规则的影响</label></>}
    </section>}
    {usedBy.length>0&&<p className="combo-note">修改同一规则会同步影响：{usedBy.map(v=>v.name).join('、')}。</p>}
    {stale&&<p role="alert" className="error">规则表已改变，请重新打开草稿。</p>}{message&&<p role="alert" className="error">{message}</p>}
    <div className="dialog-actions"><button type="button" disabled={busy} onClick={close}>取消</button><button className="primary" disabled={busy||stale||needsDecision&&(!order||!acknowledged)}>{busy?'校验中…':'应用规则'}</button></div>
  </form></Dialog>;
}
