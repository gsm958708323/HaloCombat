import type {Project,Skill} from './model';
import {skillSummary} from './authoring/skillSummary';
import type {SummaryProperty} from './authoring/skillSummary';

export function SkillSummary({project,skill}:{project:Project;skill:Skill}) {
  const summary=skillSummary(project,skill);
  const property=(field:SummaryProperty)=><div className="property" key={field.key}><span>{field.label}</span><b>{field.value}</b></div>;
  return <div className="skill-summary"><p className="skill-description">{summary.description}</p>{summary.items.map(item=><section key={item.key}>
    <h4>{item.labels.join(' / ')} · {item.title}{item.references>1?' ×'+item.references+'引用':''}</h4>
    {item.warning&&<p className="summary-warning" role="alert">{item.warning}</p>}{item.main.map(property)}
    {item.more.length>0&&<details className="summary-more"><summary>更多参数 · {item.more.length}</summary>{item.more.map(property)}</details>}
  </section>)}</div>;
}
