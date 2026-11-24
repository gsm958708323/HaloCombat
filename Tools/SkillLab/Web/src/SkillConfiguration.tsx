import {useRef,useState} from 'react';
import type {Project} from './model';
import {applySkillConfiguration,currentSkillJson} from './authoring/skillConfiguration';

export function SkillConfiguration({project,skillId,update}:{project:Project;skillId:string;update:(project:Project)=>void}) {
  const [text,setText]=useState(()=>currentSkillJson(project,skillId)),[error,setError]=useState(''),[busy,setBusy]=useState(false);
  const latest=useRef(project);latest.current=project;
  async function apply(){
    setBusy(true);const before=latest.current;
    try{
      const candidate=applySkillConfiguration(before,skillId,text);
      const response=await fetch('/api/validate',{method:'POST',headers:{'Content-Type':'application/json'},body:candidate.json});
      if(!response.ok){const result=await response.json();throw new Error(result.error??'技能校验失败');}
      if(latest.current!==before)throw new Error('校验期间工程已更改，请重新读取当前技能');
      update(candidate.project);setError('');
    }catch(error){setError(String(error));}finally{setBusy(false);}
  }
  return <div className="configuration"><div><button disabled={busy||!skillId} onClick={()=>void apply()}>{busy?'校验中…':'应用当前技能配置'}</button>
    <button onClick={()=>void navigator.clipboard.writeText(text)}>复制</button><button disabled={busy} onClick={()=>{setText(currentSkillJson(project,skillId));setError('');}}>读取当前技能</button></div>
    <small>只编辑当前技能；保留其他技能、共享定义和试验布置。完整工程请从“导出”获取。</small>
    <textarea aria-label="当前技能 JSON" spellCheck={false} value={text} onChange={e=>setText(e.target.value)}/>{error&&<p role="alert">{error}</p>}
  </div>;
}
