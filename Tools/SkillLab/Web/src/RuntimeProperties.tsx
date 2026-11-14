import { useRef,useState } from 'react';
import { api } from './model';
import type { Project,RuntimeContent } from './model';

export function RuntimeProperties({project,update}:{project:Project;update:(change:(p:Project)=>Project)=>void}) {
  const [text,setText]=useState('');const [error,setError]=useState('');const [busy,setBusy]=useState(false);
  const latest=useRef(project);latest.current=project;
  if(!project.runtime)return null;
  async function apply(){
    setBusy(true);const p=latest.current;
    try{const runtime=JSON.parse(text) as RuntimeContent;await api('/validate',{...p,runtime});if(latest.current!==p)throw new Error('校验期间工程已变更，请重新应用');update(current=>({...current,runtime}));setError('');}
    catch(error){setError(String(error));}finally{setBusy(false);}
  }
  return <details className="runtime-properties" onToggle={event=>{if(event.currentTarget.open&&!text)setText(JSON.stringify(project.runtime,null,2));}}><summary>正式运行内容</summary>
    <p>{project.runtime.actors.length} 角色 · {project.runtime.combos.length} 连招边 · {project.runtime.cues.length} 表现事件</p>
    <p>角色、Combo、Motor、Cue 与技能图保存在同一工程。应用通过完整校验后生效；使用顶部“发布正式内容”更新游戏磁盘文件。</p>
    <textarea aria-label="正式运行配置" spellCheck={false} value={text} onChange={event=>setText(event.target.value)}/>
    <button disabled={busy} onClick={()=>void apply()}>{busy?'校验中…':'应用运行配置'}</button><button onClick={()=>setText(JSON.stringify(project.runtime,null,2))}>读取当前值</button>
    {error&&<p role="alert">{error}</p>}
  </details>;
}
