import { useEffect, useState } from 'react';
import { api } from './model';
import type { Project } from './model';
import { contentItems, contentLabel, deleteContent, referencesTo, renameContent } from './authoring/contentEdits';
import type { ContentKind, ContentReference } from './authoring/contentEdits';

export interface ContentAction { kind:ContentKind;id:string;mode:'rename'|'delete' }
interface Publication {slot:string;projectId:string;skills:string[];buffs:string[];blueprints:string[];error:string|null}
export function ContentDialog({project,action,close,edit,locate}:{project:Project;action:ContentAction;close:()=>void;edit:(change:(p:Project)=>Project)=>void;locate:(ref:ContentReference)=>void}) {
  const [id,setId]=useState(action.id);const [replacement,setReplacement]=useState('');const [error,setError]=useState('');
  const [slots,setSlots]=useState<Publication[]|null>(null);const [loadError,setLoadError]=useState('');
  const value=contentItems(project,action.kind).find(item=>item.id===action.id);
  const references=referencesTo(project,action.kind,action.id).filter(r=>r.kind!==action.kind||r.id!==action.id);
  async function inspect(){try{setLoadError('');setSlots(await api<Publication[]>('/publications',undefined,'GET'));}catch(error){setLoadError(String(error));}}
  useEffect(()=>{void inspect();},[]);
  const affected=slots?.filter(slot=>slot.projectId===project.id&&(action.kind==='skill'?slot.skills:action.kind==='buff'?slot.buffs:slot.blueprints).includes(action.id))??[];
  function submit(){
    try{edit(p=>action.mode==='rename'?renameContent(p,action.kind,action.id,id):deleteContent(p,action.kind,action.id,replacement));close();}
    catch(error){setError(String(error));}
  }
  return <div className="modal-backdrop"><form className="modal content-dialog" onSubmit={event=>{event.preventDefault();submit();}}>
    <h2>{action.mode==='rename'?'修改':'删除'}{contentLabel[action.kind]}{action.mode==='rename'?' ID':''} · {value?.name??action.id}</h2>
    {action.mode==='rename'?<label>新 ID<input aria-label="新的内容ID" autoFocus value={id} onChange={event=>setId(event.target.value)}/></label>:<p>删除后可撤销恢复。{references.length?'请选择替代内容，或先修改下列引用。':'没有其他内容引用此项。'}</p>}
    <section><h3>工程引用 · {references.length}</h3>{references.length>0&&<ul>{references.map((r,i)=><li key={i}><button type="button" onClick={()=>locate(r)}>{contentLabel[r.kind]}：{r.name}<small>{r.nodeId??r.id} / {r.field}</small></button></li>)}</ul>}
      {action.mode==='rename'&&<p>同步修改这些引用，运行时数字身份保持不变。</p>}
    </section>
    {action.mode==='delete'&&references.length>0&&<label>替换引用为<select aria-label="删除时的替代内容" value={replacement} onChange={event=>setReplacement(event.target.value)}><option value="">选择替代内容</option>{contentItems(project,action.kind).filter(v=>v.id!==action.id).map(v=><option key={v.id} value={v.id}>{v.name} · {v.id}</option>)}</select></label>}
    <section><h3>磁盘发布槽位</h3>{!slots&&!loadError&&<p>正在读取 F6 / F7…</p>}{loadError&&<><p role="alert">{loadError}</p><button type="button" onClick={()=>void inspect()}>重试读取</button></>}
      {slots&&<p>{affected.length?affected.map(s=>s.slot.toUpperCase()).join('、')+' 包含此内容。':slots.some(s=>s.error)?'未能确认全部槽位的引用。':'当前两槽没有引用此内容。'} 已发布快照保留原版本，更新时需重新发布。</p>}
      {slots?.filter(s=>s.error).map(s=><p key={s.slot} role="alert">{s.slot.toUpperCase()} 无法检查：{s.error}</p>)}
    </section>
    {error&&<p role="alert">{error}</p>}
    <div><button type="button" onClick={close}>取消</button><button className={action.mode==='delete'?'danger':'primary'} disabled={!slots||action.mode==='delete'&&references.length>0&&!replacement}>{action.mode==='rename'?'应用 ID 与引用':'删除'+(replacement?'并替换引用':'')}</button></div>
  </form></div>;
}
