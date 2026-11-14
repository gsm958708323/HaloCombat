import {useEffect,useRef,useState} from 'react';
import {api,clone} from './model';
import type {Project,RuntimeCombo} from './model';

interface Draft {key:string;value:RuntimeCombo;tags:string}
const drafts=(values:RuntimeCombo[])=>values.map(value=>({key:crypto.randomUUID(),value:clone(value),tags:value.requiredTags.join(', ')}));
export function ComboProperties({project,update}:{project:Project;update:(change:(p:Project)=>Project)=>void}) {
  const [rows,setRows]=useState<Draft[]>(()=>drafts(project.runtime?.combos??[])),[message,setMessage]=useState(''),[busy,setBusy]=useState(false);
  const latest=useRef(project);latest.current=project;
  const base=useRef({id:project.id,text:JSON.stringify(project.runtime?.combos)});
  function read(){const p=latest.current;setRows(drafts(p.runtime?.combos??[]));base.current={id:p.id,text:JSON.stringify(p.runtime?.combos)};setMessage('');}
  useEffect(()=>{read();},[project.id]);
  if(!project.runtime)return null;
  function edit(key:string,change:(row:Draft)=>Draft){setRows(values=>values.map(row=>row.key===key?change(row):row));}
  function field(key:string,values:Partial<RuntimeCombo>){edit(key,row=>({...row,value:{...row.value,...values}}));}
  async function apply(){
    const p=latest.current;setBusy(true);
    try{
      if(p.id!==base.current.id||JSON.stringify(p.runtime?.combos)!==base.current.text)throw new Error('连招表已改变，请读取当前表后编辑');
      const combos=rows.map(row=>{const tokens=row.tags.trim()?row.tags.trim().split(/[\s,，]+/):[];const tags=tokens.map(Number);
        if(tags.some(tag=>!Number.isInteger(tag)||tag<=0||tag>2147483647))throw new Error(row.value.id+'：Tag需为正整数');
        if(!Number.isInteger(row.value.priority))throw new Error(row.value.id+'：优先级需为整数');
        return {...row.value,requiredTags:tags};});
      const candidate={...p,runtime:{...p.runtime!,combos}};await api('/validate',candidate);
      if(latest.current!==p)throw new Error('校验期间工程已更改，请重新应用');
      update(current=>({...current,runtime:{...current.runtime!,combos}}));base.current={id:p.id,text:JSON.stringify(combos)};setMessage('连招表已应用，可撤销');
    }catch(error){setMessage(String(error));}finally{setBusy(false);}
  }
  return <details className="combo-properties"><summary>连招输入与接续 · {project.runtime.combos.length}</summary>
    <p>先编辑表，再应用。播放中的接续自动要求取消窗；1017表示当前施法命中确认。</p>
    <button onClick={()=>setRows(values=>[...values,{key:crypto.randomUUID(),value:{id:'combo-'+crypto.randomUUID().slice(0,8),input:'NewInput',preSkills:[],requiredTags:[],priority:0,toSkill:project.skills[0]?.id??''},tags:''}])}>添加连招边</button>
    {rows.map((row,index)=><details key={row.key} className="combo-row"><summary>{row.value.id} · {row.value.input}</summary>
      <label>边ID<input aria-label={`连招${index+1}边ID`} value={row.value.id} onChange={e=>field(row.key,{id:e.target.value})}/></label>
      <label>输入令牌<input aria-label={`连招${index+1}输入`} value={row.value.input} onChange={e=>field(row.key,{input:e.target.value})}/></label>
      <label>前置技能（多选，空为起手）<select multiple aria-label={`连招${index+1}前置技能`} value={row.value.preSkills} onChange={e=>field(row.key,{preSkills:Array.from(e.target.selectedOptions,o=>o.value)})}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <button onClick={()=>field(row.key,{preSkills:[]})}>设为起手</button>
      <label>所需Tag<input aria-label={`连招${index+1}所需Tag`} value={row.tags} onChange={e=>edit(row.key,r=>({...r,tags:e.target.value}))}/></label>
      <label><input type="checkbox" aria-label={`连招${index+1}命中确认`} checked={row.tags.split(/[\s,，]+/).includes('1017')} onChange={e=>edit(row.key,r=>({...r,tags:[...r.tags.split(/[\s,，]+/).filter(t=>t&&t!=='1017'),...(e.target.checked?['1017']:[])].join(', ')}))}/>需要命中确认</label>
      <label>优先级<input aria-label={`连招${index+1}优先级`} type="number" step="1" value={row.value.priority} onChange={e=>{if(e.target.value!=='')field(row.key,{priority:Number(e.target.value)});}}/></label>
      <label>接续技能<select aria-label={`连招${index+1}接续技能`} value={row.value.toSkill} onChange={e=>field(row.key,{toSkill:e.target.value})}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
      <button onClick={()=>setRows(values=>values.filter(r=>r.key!==row.key))}>移除连招边</button>
    </details>)}
    <button disabled={busy} onClick={()=>void apply()}>{busy?'校验中…':'应用连招表'}</button><button onClick={read}>读取当前表</button>{message&&<p role="status">{message}</p>}
  </details>;
}
