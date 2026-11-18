import { useEffect,useRef, useState } from 'react';
import { Play, Pause, SkipForward, RotateCcw, Crosshair } from 'lucide-react';
import type { AuthorNodeSource,Project } from './model';
import type { usePreview } from './usePreview';
import { SceneResources } from './SceneResources';
import {PreviewWorld} from './PreviewWorld';
import { SceneObstacles } from './SceneObstacles';
import { SceneActorProperties } from './SceneActorProperties';
import {RuntimePanel} from './RuntimePanel';
import './preview-input.css';
import {SkillConfiguration} from './SkillConfiguration';
import {ComboInput} from './ComboInput';
import './trace.css';
import {ReplayControls} from './ReplayControls';

const number=new Intl.NumberFormat('zh-CN',{maximumFractionDigits:1}).format;

export function Preview({ preview, project, update,inspectSource,root }: { inspectSource:(source:AuthorNodeSource)=>void;root:string; preview: ReturnType<typeof usePreview>; project: Project; update: (p: Project) => void }) {
  const [entity,setEntity]=useState(''),[sceneActor,setSceneActor]=useState(project.preview.caster.id);
  useEffect(()=>setEntity(''),[preview.sessionId]);
  const inspected=preview.state?.actors.find(a=>a.id===entity);
  const [configuration,setConfiguration] = useState(false);
  const moveKeys=useRef(new Set<string>());
  useEffect(()=>{
    const clear=()=>moveKeys.current.clear(),visibility=()=>{if(document.hidden)clear();};clear();
    window.addEventListener('blur',clear);document.addEventListener('visibilitychange',visibility);
    return()=>{window.removeEventListener('blur',clear);document.removeEventListener('visibilitychange',visibility);};
  },[preview.sessionId]);
  function moveKey(key:string,down:boolean){
    if(!['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'].includes(key))return;
    if(down&&moveKeys.current.has(key))return;
    if(!down&&!moveKeys.current.has(key))return;
    if(down)moveKeys.current.add(key);else moveKeys.current.delete(key);
    const has=(...keys:string[])=>keys.some(k=>moveKeys.current.has(k))?1:0;
    void preview.move(has('d','arrowright')-has('a','arrowleft'),has('w','arrowup')-has('s','arrowdown'));
  }
  function stopMovement(){if(!moveKeys.current.size)return;moveKeys.current.clear();void preview.move(0,0);}
  const state=preview.state;
  return <aside className="left-column" onKeyDown={e=>{if(e.target instanceof HTMLInputElement||e.target instanceof HTMLTextAreaElement||e.target instanceof HTMLSelectElement)return;moveKey(e.key.toLowerCase(),true);}} onKeyUp={e=>moveKey(e.key.toLowerCase(),false)}>
    <RuntimePanel state={state} sessionId={preview.sessionId} ready={preview.ready} inspectSource={inspectSource} inspectEntity={setEntity} remove={preview.remove} removeBuff={preview.removeBuff}/>
    <div className="preview-root">预览技能：{project.skills.find(s=>s.id===root)?.name??root}</div>
    {entity&&<div className="entity-focus" role="status">{inspected?('实体 '+inspected.name+' · '+entity):('实体已回收 · '+entity)}<button onClick={()=>setEntity('')}>取消定位</button></div>}
    <section className="preview-panel"><div className="panel-title"><b>运行预览</b><span className="grow"/><button className={!configuration?'active':''} onClick={()=>setConfiguration(false)}>测试场景</button><button className={configuration?'active':''} onClick={()=>setConfiguration(true)}>配置</button></div>
      {configuration?<SkillConfiguration key={root} project={project} skillId={root} update={update}/>:<>
      <div className="preview-controls"><button className="primary" disabled={!preview.ready} onPointerDown={e=>{e.currentTarget.setPointerCapture(e.pointerId);void preview.cast();}} onPointerUp={()=>void preview.release()} onPointerCancel={()=>void preview.cancel()} onBlur={()=>void preview.cancel()} onKeyDown={e=>{if(!e.repeat&&(e.key===' '||e.key==='Enter')){e.preventDefault();void preview.cast();}}} onKeyUp={e=>{if(e.key===' '||e.key==='Enter'){e.preventDefault();void preview.release();}}}><Crosshair size={13}/>施放</button><button disabled={!preview.ready} onClick={()=>preview.setPlaying(!preview.playing)}>{preview.playing?<Pause size={12}/>:<Play size={12}/>} {preview.playing?'暂停':'播放'}</button><button disabled={!preview.ready||preview.playing} onClick={()=>void preview.step()}><SkipForward size={12}/>逐帧</button><button disabled={preview.replaying||preview.placing} onClick={()=>void preview.reset()}><RotateCcw size={12}/>重置</button><button disabled={!preview.ready} onClick={()=>void preview.interrupt()}>打断</button></div>
      <div className="preview-options"><label title="试射时免除弹药和能量消耗；保留冷却与状态规则，切换会重置预览"><input aria-label="无限弹药" type="checkbox" checked={preview.unlimitedAmmo} disabled={preview.replaying||preview.placing} onChange={e=>preview.setUnlimitedAmmo(e.target.checked)}/>无限弹药</label><label><input type="checkbox" disabled={!preview.ready} checked={preview.continuous} onChange={e=>preview.setContinuous(e.target.checked)}/>连续施放</label><select aria-label="播放速度" value={preview.speed} onChange={e=>preview.setSpeed(Number(e.target.value))}>{[.25,.5,1,2].map(n=><option key={n} value={n}>{n}×</option>)}</select><span>{state?.time.toFixed(2)??'0.00'} s</span></div>
      <div className="preview-readiness" role="status">{!project.skills.length?'空工程：新建技能后可运行预览':preview.ready?'当前配置已应用':preview.error?'配置无效：'+preview.error:'正在校验并应用配置…'}</div>
      <details className="preview-settings"><summary>预览设置 · 输入、复现与布置</summary><ComboInput project={project} ready={preview.ready} send={preview.input}/>
      <ReplayControls preview={preview} project={project} update={update}/>
      <SceneResources project={project} update={update}/>
      <SceneObstacles project={project} update={update}/>
      <SceneActorProperties project={project} update={update} selectedId={sceneActor} select={setSceneActor}/></details>
      {state?.charging&&<div className="charge-box"><div>蓄力 {Math.round(state.chargeProgress*100)}%<span>松开释放</span></div><progress aria-label="蓄力进度" max={1} value={state.chargeProgress}/></div>}
      <PreviewWorld project={project} preview={preview} inspected={inspected} update={update} select={setSceneActor} selectedId={sceneActor} onBlur={stopMovement}/>
        <div className="energy-box"><div><b>{state?.channeling?'引导中 · 能量':state?.charging?'蓄力中 · 能量':'能量'}</b><span>{preview.unlimitedAmmo?'∞':(state?.energy.toFixed(0)??project.preview.initialEnergy??project.preview.energy)} / {project.preview.energy}</span></div><progress max={project.preview.energy} value={state?.energy??project.preview.initialEnergy??project.preview.energy}/><small>弹药 {preview.unlimitedAmmo?'∞':(state?.ammo??project.preview.initialAmmo??project.preview.ammo??60)} / {state?.ammoCapacity??project.preview.ammo??60}</small> · <small>冷却 {Math.max(0,state?.cooldowns[preview.skill.current]??0).toFixed(2)} s · {state?.actors.filter(a=>a.kind==='projectile').length??0} 个投射物 · {state?.castPower.toFixed(2)??'1.00'}×</small></div>
      <div className="statistics">{[['总伤害',state?.statistics.damage??0],['有效命中',state?.statistics.hits??0],['技能施行',state?.statistics.casts??0],['投射物',state?.statistics.projectiles??0],['连锁触发',state?.statistics.chains??0],['分裂触发',state?.statistics.splits??0]].map(([label,value])=><div key={String(label)}><small>{label}</small><strong>{number(Number(value))}</strong></div>)}</div>
      </>}
    </section>
  </aside>;
}
