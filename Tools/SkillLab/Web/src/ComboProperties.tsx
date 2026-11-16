import type {Project,RuntimeCombo} from './model';
import {inputLabel,purposeLabel,rulePurpose} from './authoring/comboRules';

// All writes go through ComboRuleForm; there is no second mutable rule table.
export function ComboProperties({project,rows,edit,remove,showPurpose=false}:{project:Project;rows:RuntimeCombo[];edit:(row:RuntimeCombo)=>void;remove:(id:string)=>void;showPurpose?:boolean}){
  const name=(id:string)=>project.skills.find(s=>s.id===id)?.name??id;
  return <div className="combo-rule-list">{rows.length?rows.map(row=><article key={row.id} className="combo-rule-row">
    <div><strong>{row.preSkills.length===0?'空闲':row.preSkills.length===1?name(row.preSkills[0]):'适用技能集合 · '+row.preSkills.length} → {name(row.toSkill)}</strong>
      <small>{inputLabel(project,row.input)} · {row.requiredTags.includes(1017)?'需要本次命中 · ':''}优先级 {row.priority}{showPurpose?' · '+purposeLabel[rulePurpose(row)]:''}</small>
      {row.preSkills.length>1&&<details><summary>查看适用范围</summary><p>{row.preSkills.map(name).join('、')}</p></details>}
    </div><div><button onClick={()=>edit(row)}>编辑规则</button><button onClick={()=>remove(row.id)}>删除规则</button></div>
  </article>):<p className="combo-empty">尚未配置规则。添加规则后即可发送实际输入验证。</p>}</div>;
}
