import {useState} from 'react';
import {Pause, Play, RotateCcw, SkipForward} from 'lucide-react';
import type {ComboDecision,Project} from './model';
import {inputLabel} from './authoring/comboRules';
import type {usePreview} from './usePreview';

function inputStatus(decision:ComboDecision|null, buffered:string|null) {
  if (!decision) return buffered?'输入已缓冲，等待逻辑帧':'';
  switch (decision.phase) {
    case 'waiting': return buffered?'接续窗口未开，输入仍在缓冲':'接续窗口未开，缓冲已结束';
    case 'blocked': return '当前角色状态无法接续';
    case 'unmatched': return '没有满足条件的连招规则';
    case 'rejected': return decision.reason?('施放失败 · '+decision.reason):'施放失败';
    default: return '';
  }
}

export function ComboRunFeedback({project,preview}:{project:Project;preview:ReturnType<typeof usePreview>}) {
  const [choice,setChoice]=useState('');
  const tokens=[...new Set(project.runtime?.combos.map(rule=>rule.input))];
  const token=tokens.includes(choice)?choice:tokens.includes('Attack')?'Attack':tokens[0]??'';
  const caster=preview.state?.actors.find(actor=>actor.kind==='caster');
  const current=caster?.currentSkill;
  const decision=caster?.comboDecision;
  const feedback=inputStatus(decision?.inputSequence===caster?.comboInputSequence?decision??null:null,caster?.bufferedInput??null);
  const ready=preview.ready&&!!token;
  return <div className="combo-preview-controls">
    <div className="panel-title"><b>连招预览</b><span className="grow"/><span>{preview.state?.step??0} 帧</span></div>
    <div className="combo-preview-content">
      <div className="combo-preview-current">当前动作 <strong>{current?(project.skills.find(skill=>skill.id===current)?.name??current):'空闲'}</strong></div>
      <label className="combo-preview-input">逻辑输入 <select aria-label="连招试放输入" value={token} disabled={!tokens.length} onChange={event=>setChoice(event.target.value)}>
        {!tokens.length&&<option value="">无连招输入</option>}
        {tokens.map(input=><option key={input} value={input}>{inputLabel(project,input)}</option>)}
      </select></label>
      <div className="combo-preview-buttons">
        <button disabled={!ready} onClick={()=>void preview.input(token)}>发送输入</button>
        <button className="primary" disabled={!ready} onClick={()=>void preview.inputAndStep(token)}>输入并单步</button>
        <button disabled={!preview.ready} onClick={()=>preview.setPlaying(!preview.playing)}>{preview.playing?<Pause size={12}/>:<Play size={12}/>} {preview.playing?'暂停':'继续'}</button>
        <button disabled={!preview.ready||preview.playing} onClick={()=>void preview.step()}><SkipForward size={12}/>单步</button>
        <button disabled={!preview.ready&&!preview.error||preview.placing} onClick={()=>void preview.reset()}><RotateCcw size={12}/>重置试放</button>
      </div>
      <div className="combo-preview-status" role="status">{!project.skills.length?'新建技能后可预览':preview.error?'配置无效 · '+preview.error:
        !preview.ready?'正在应用配置…':feedback||(!tokens.length?'暂无连招规则':preview.playing?'运行中':'暂停 · 可输入并单步')}</div>
    </div>
  </div>;
}
