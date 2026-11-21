import { useState } from 'react';
import { Search } from 'lucide-react';
import type { Project } from './model';

export function SkillLibrary({project,selected,select,copy}:{project:Project;selected:string;select:(id:string)=>void;copy:()=>void}) {
  const [query,setQuery]=useState('');
  const match=query.trim().toLocaleLowerCase();const skills=project.skills.filter(s=>(s.name+' '+s.id).toLocaleLowerCase().includes(match));
  return <div className="skill-library"><div className="search"><Search size={13}/><input autoFocus aria-label="搜索技能" placeholder="名称 / ID" value={query} onChange={event=>setQuery(event.target.value)}/></div><div className="skill-library-actions"><small>{skills.length} / {project.skills.length} 个技能</small><button onClick={copy}>从另一工作区复制…</button></div>
    {[...new Set(skills.map(s=>s.category||'未分类'))].map(category=><details key={category} open><summary>{category} · {skills.filter(s=>(s.category||'未分类')===category).length}</summary>
      {skills.filter(s=>(s.category||'未分类')===category).map(s=><button key={s.id} className={s.id===selected?'active':''} onClick={()=>select(s.id)}>{s.name}<small>{s.id}</small></button>)}
    </details>)}{!skills.length&&<p>没有匹配技能</p>}
  </div>;
}
