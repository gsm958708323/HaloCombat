import type { Project,Skill } from './model';
import { bindTimeline,extractTimeline } from './authoring/timelineEdits';
import './shared-timelines.css';

export function SharedTimelineControls({project,skill,update,open,report}:{project:Project;skill:Skill;update:(f:(p:Project)=>Project)=>void;open:(id:string)=>void;report:(s:string)=>void}) {
  if(skill.nodes.some(n=>n.kind==='cast'&&n.parameters.execution==='operation'))return <p>即时投递效果，不创建动作或时间轴；改变执行方式后可配置动作。</p>;
  return <details className="timeline-properties shared-timeline-binding" open><summary>动作复用</summary>
    <label>共享时间轴<select aria-label="技能共享时间轴" value={skill.timelineRef??''} onChange={e=>update(p=>bindTimeline(p,skill.id,e.target.value))}>
      <option value="">{skill.timeline?'本技能内联时间轴':'本技能自动时序'}</option>{project.timelines?.map(t=><option key={t.id} value={t.id}>{t.name} · {t.id}</option>)}
    </select></label>
    <small>绑定共享动作会替换本技能动作图，可撤销。消耗、冷却和缺弹回退仍由技能配置。</small>
    {skill.timelineRef&&<><p>{project.skills.filter(s=>s.timelineRef===skill.timelineRef).length} 个技能共用此轴；每次施法独立播放。</p><button onClick={()=>open(skill.timelineRef!)}>编辑共享时间轴</button></>}
    {skill.timeline&&<button onClick={()=>{try{update(p=>extractTimeline(p,skill.id).project);}catch(e){report(String(e));}}}>提取为共享时间轴</button>}
  </details>;
}
