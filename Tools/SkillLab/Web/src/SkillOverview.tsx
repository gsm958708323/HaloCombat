import type {Project,Skill} from './model';
import {SkillSummary} from './SkillSummary';
import './inspector.css';

export function SkillOverview({project,skill,openTimeline}:{project:Project;skill?:Skill;openTimeline:(id:string)=>void}) {
  const shared=project.timelines?.find(t=>t.id===skill?.timelineRef);
  return <aside className="properties skill-overview" aria-label="当前技能信息总览"><h3>技能属性</h3>{skill?<>
    <h4>{skill.name}</h4><small className="node-identity">{skill.id}</small>
    {skill.timelineRef&&<section className="overview-timeline"><strong>共享时间轴 · {shared?.name??skill.timelineRef}</strong>
      <small>{project.skills.filter(s=>s.timelineRef===skill.timelineRef).length} 个技能共用</small>
      <button disabled={!shared} onClick={()=>openTimeline(skill.timelineRef!)}>打开共享时间轴</button></section>}
    <SkillSummary project={project} skill={skill}/>
  </>:<p className="runtime-empty">新建技能后显示技能信息。</p>}</aside>;
}
