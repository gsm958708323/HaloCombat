import {useState,useSyncExternalStore} from 'react';
import type {Project} from './model';
import type {PreviewRuntimeStore} from './previewRuntime';
import {comboFeedbackReason,comboFrameMarks,comboTrace,decisionTitle,tagLabel} from './authoring/comboTrace';
import {inputLabel} from './authoring/comboRules';

export function ComboRunFeedback({project,store,inputs,selectedInput,selection}:{project:Project;store:PreviewRuntimeStore;inputs:string[];selectedInput?:string;selection:string}){
  const runtime=useSyncExternalStore(store.subscribe,store.get),trace=comboTrace(runtime.state),d=trace.decision;
  const [choice,setChoice]=useState({selection:'',token:''}),[lastInput,setLastInput]=useState(''),tokens=[...new Set([selectedInput,...inputs].filter((v):v is string=>!!v))];
  const token=choice.selection===selection&&tokens.includes(choice.token)?choice.token:selectedInput??tokens[0]??'';
  const name=(id:string|null)=>project.skills.find(s=>s.id===id)?.name??id??'空闲';
  const [aimId,setAimId]=useState('');
  const targets=runtime.state?.actors.filter(actor=>actor.kind==='target'&&!actor.dead)??[];
  const aim=targets.find(target=>target.sceneId===aimId);
  const marks=comboFrameMarks(runtime.state),first=marks[0]?.step??0;
  const duration=Math.max(1,(runtime.state?.step??first)-first);
  function send(input:string,advance:boolean){
    setLastInput(input);
    const point=aim?{x:aim.x,z:aim.z}:undefined;
    if(advance)runtime.inputAndStep(input,point);
    else {runtime.send(input,point);runtime.setPlaying(true);}
  }
  return <section className="combo-feedback" aria-label="连招真实运行反馈">
    <div className="combo-feedback-tools"><strong>试放与运行反馈</strong></div>
    <div className="combo-trial-fields"><label>输入<select aria-label="连招试放输入" value={token} disabled={!tokens.length} onChange={e=>setChoice({selection,token:e.target.value})}>{tokens.map(t=><option key={t} value={t}>{inputLabel(project,t)}</option>)}</select></label>
      <label>瞄准点<select aria-label="连招试放瞄准点" value={aim?.sceneId??''} onChange={e=>setAimId(e.target.value)}>
        <option value="">场上指针位置</option>{targets.filter(target=>target.sceneId).map(target=><option key={target.id} value={target.sceneId!}>{target.name}</option>)}
      </select></label></div>
    <div className="combo-feedback-tools">
      <button disabled={!runtime.ready||!token} onClick={()=>send(token,false)}>发送输入</button>
      <button className="primary" disabled={!runtime.ready||!token} onClick={()=>send(token,true)}>输入并单步</button>
      <button disabled={!runtime.ready} onClick={()=>runtime.setPlaying(!runtime.playing)}>{runtime.playing?'暂停':'播放'}</button>
      <button disabled={!runtime.ready||runtime.playing} onClick={runtime.step}>单步</button>
      <button disabled={!runtime.ready||!lastInput} onClick={()=>send(lastInput,!runtime.playing)} title={lastInput}>重试上次输入</button>
      <button disabled={!runtime.ready} onClick={runtime.reset}>重置试放</button></div>
    <div className={'combo-decision '+(d?.phase??'')} role="status"><strong>{!d&&trace.caster?.bufferedInput?'输入已入缓冲，等待逻辑步':decisionTitle(d)}</strong>{d&&<><span>{inputLabel(project,d.input)} · {name(d.fromSkill)}{d.toSkill?' → '+name(d.toSkill):''}</span>
      <small>{comboFeedbackReason(d,!!trace.caster?.bufferedInput)}</small>
      {d.phase==='cast'&&d.actualSkill!==d.toSkill&&<small>运行路由实际执行：{name(d.actualSkill)}。</small>}</>}</div>
    {marks.length>0&&<section className="combo-frame-strip" aria-label="试放帧记录">
      <header><strong>试放帧记录</strong><span>当前第 {runtime.state?.step} 帧</span></header>
      <div className="combo-frame-track" aria-hidden="true">{marks.map(mark=><i key={mark.id} className={mark.kind} style={{left:(mark.step-first)/duration*100+'%'}}/>)}</div>
      <ol>{marks.slice().reverse().map(mark=><li key={mark.id}><span>{mark.step} 帧</span><b className={mark.kind}>{mark.kind==='input'?'输入 '+inputLabel(project,mark.label):mark.label}</b></li>)}</ol>
    </section>}
    {d&&<details className="combo-candidates"><summary>查看本次候选与判断依据 · {d.candidates.length}条</summary><p>窗口：{d.windowOpen?'已开启 / 空闲':'未开启'} · 查找前命中确认：{d.confirmed?'有':'无'}</p>
      {d.candidates.map(c=><div key={c.rule}><strong>{name(c.skill)}</strong><span>优先级 {c.priority} · {c.missingTags.length?'缺少 '+c.missingTags.map(tagLabel).join('、'):c.rule===d.rule?'已选中':'条件满足，优先级未胜出'}</span><small>{c.rule}</small></div>)}</details>}
    <div className="combo-execution-path" aria-label="本次实际执行路径"><small>本次执行</small>{trace.hasIdleStart&&<><span>起手</span><i>→</i></>}
      {trace.steps.length?trace.steps.map((step,index)=><span className="combo-path-step" key={step.event+':'+index}>{index>0&&<i>→</i>}<b>{name(step.skill)}</b></span>):<small>尚无通过接续输入成功施放的技能</small>}</div>
  </section>;
}
