import {useState,useSyncExternalStore} from 'react';
import type {Project} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {comboReason,comboTrace,decisionTitle,tagLabel} from './authoring/comboTrace';
import {inputLabel} from './authoring/comboRules';

export function ComboRunFeedback({project,store,inputs,selectedInput}:{project:Project;store:PreviewRuntimeStore;inputs:string[];selectedInput?:string}){
  const runtime=useSyncExternalStore(store.subscribe,store.get),trace=comboTrace(runtime.state),d=trace.decision;
  const [choice,setChoice]=useState(''),tokens=[...new Set([selectedInput,...inputs].filter((v):v is string=>!!v))];
  const token=tokens.includes(choice)?choice:selectedInput??tokens[0]??'';
  const name=(id:string|null)=>project.skills.find(s=>s.id===id)?.name??id??'空闲';
  return <section className="combo-feedback" aria-label="连招真实运行反馈">
    <div className="combo-feedback-tools"><strong>试放与运行反馈</strong><select aria-label="连招试放输入" value={token} disabled={!tokens.length} onChange={e=>setChoice(e.target.value)}>{tokens.map(t=><option key={t} value={t}>{inputLabel(project,t)}</option>)}</select>
      <button disabled={!runtime.ready||!token} onClick={()=>{runtime.play();runtime.send(token);}}>发送输入</button></div>
    <div className={'combo-decision '+(d?.phase??'')} role="status"><strong>{!d&&trace.caster?.bufferedInput?'输入已入缓冲，等待逻辑步':decisionTitle(d)}</strong>{d&&<><span>{d.input} · {name(d.fromSkill)}{d.toSkill?' → '+name(d.toSkill):''}</span>
      <small>{comboReason(d.reason)||(!d.candidates.length?'没有符合该输入与前置状态的规则。':d.phase==='cast'?'已通过实际资源、冷却与目标校验。':'根据实际规则与状态匹配。')}</small>
      {d.phase!=='cast'&&!trace.caster?.bufferedInput&&<small>当前输入缓冲已空，以上保留最近一次判断结果。</small>}
      {d.phase==='cast'&&d.actualSkill!==d.toSkill&&<small>运行路由实际执行：{name(d.actualSkill)}。</small>}</>}</div>
    {d&&<details className="combo-candidates"><summary>查看本次候选与判断依据 · {d.candidates.length}条</summary><p>窗口：{d.windowOpen?'已开启 / 空闲':'未开启'} · 查找前命中确认：{d.confirmed?'有':'无'}</p>
      {d.candidates.map(c=><div key={c.rule}><strong>{name(c.skill)}</strong><span>优先级 {c.priority} · {c.missingTags.length?'缺少 '+c.missingTags.map(tagLabel).join('、'):c.rule===d.rule?'已选中':'条件满足，优先级未胜出'}</span><small>{c.rule}</small></div>)}</details>}
    <div className="combo-execution-path" aria-label="本次实际执行路径"><small>本次执行</small>{trace.hasIdleStart&&<><span>起手</span><i>→</i></>}
      {trace.steps.length?trace.steps.map((step,index)=><span className="combo-path-step" key={step.event+':'+index}>{index>0&&<i>→</i>}<b>{name(step.skill)}</b></span>):<small>尚无通过接续输入成功施放的技能</small>}</div>
    {d?.phase==='rejected'&&<small className="combo-note">输入仍在缓冲期内时，后续逻辑步会重新求值。命中确认已在查表时消费，后续候选可能变化。</small>}
    <small className="combo-note">输入经过真实战斗核；图形布局不随施放重排。诊断使用有界事件记录，与输入录制开关独立。</small>
  </section>;
}
