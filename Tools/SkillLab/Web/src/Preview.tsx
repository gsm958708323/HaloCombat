import { useEffect,useRef, useState } from 'react';
import { Play, Pause, SkipForward, RotateCcw, Crosshair } from 'lucide-react';
import type { AuthorNodeSource,Project } from './model';
import type { usePreview } from './usePreview';
import { SceneResources } from './SceneResources';
import {PreviewWorld} from './PreviewWorld';
import { SceneObstacles } from './SceneObstacles';
import { SceneActorProperties } from './SceneActorProperties';
import { DamageInspector } from './DamageInspector';
import './preview-input.css';
import { api } from './model';
import {ComboInput} from './ComboInput';
import {RuntimeEventTable} from './RuntimeEventTable';
import './trace.css';
import {ReplayControls} from './ReplayControls';

export function Preview({ preview, project, update,inspectSource,root }: { inspectSource:(source:AuthorNodeSource)=>void;root:string; preview: ReturnType<typeof usePreview>; project: Project; update: (p: Project) => void }) {
  const [entity,setEntity]=useState(''),[sceneActor,setSceneActor]=useState(project.preview.caster.id);
  useEffect(()=>setEntity(''),[preview.sessionId]);
  const inspected=preview.state?.actors.find(a=>a.id===entity);
  const [configuration,setConfiguration] = useState(false);
  const [text,setText] = useState('');
  const [error,setError] = useState('');
  const [filter,setFilter] = useState('all');
  const [instances,setInstances] = useState(false);
  const [applying,setApplying] = useState(false);
  const latestProject=useRef(project);latestProject.current=project;
  async function applyConfiguration(){
    setApplying(true);
    const before=latestProject.current;
    try{const p=JSON.parse(text) as Project;await api('/validate',p);if(latestProject.current!==before)throw new Error('校验期间工程已更改，请重新应用配置');update(p);setError('');}
    catch(e){setError(String(e));}
    finally{setApplying(false);}
  }
  const moveKeys=useRef(new Set<string>());
  useEffect(()=>{
    const clear=()=>moveKeys.current.clear(),visibility=()=>{if(document.hidden)clear();};clear();
    window.addEventListener('blur',clear);document.addEventListener('visibilitychange',visibility);
    return()=>{window.removeEventListener('blur',clear);document.removeEventListener('visibilitychange',visibility);};
  },[preview.sessionId]);
  function moveKey(key:string,down:boolean){
    if(!['w','a','s','d','arrowup','arrowdown','arrowleft','arrowright'].includes(key))return;
    if(!down&&!moveKeys.current.has(key))return;
    if(down)moveKeys.current.add(key);else moveKeys.current.delete(key);
    const has=(...keys:string[])=>keys.some(k=>moveKeys.current.has(k))?1:0;
    void preview.move(has('d','arrowright')-has('a','arrowleft'),has('w','arrowup')-has('s','arrowdown'));
  }
  function stopMovement(){if(!moveKeys.current.size)return;moveKeys.current.clear();void preview.move(0,0);}
  const state=preview.state;
  const events=state?.events.filter(e=>filter==='all'||e.kind===filter||filter==='damage'&&e.kind==='critical').slice(-80).reverse()??[];
  const number=(value:number)=>value.toLocaleString('zh-CN',{ maximumFractionDigits: 1 });
  return <aside className="left-column" onKeyDown={e=>{if(e.target instanceof HTMLInputElement||e.target instanceof HTMLTextAreaElement||e.target instanceof HTMLSelectElement)return;moveKey(e.key.toLowerCase(),true);}} onKeyUp={e=>moveKey(e.key.toLowerCase(),false)}>
    <section className="events-panel"><div className="panel-title"><div className="text-tabs"><button className={!instances?'active':''} onClick={()=>setInstances(false)}>事件记录</button><button className={instances?'active':''} onClick={()=>setInstances(true)}>运行实例</button></div><select aria-label="筛选事件" value={filter} onChange={e=>setFilter(e.target.value)}><option value="all">全部事件</option><option value="damage">伤害</option><option value="cast">施放</option><option value="spawn">生成</option><option value="rejected">拒绝</option><option value="execute">节点执行</option><option value="placement">布置</option></select><small>{state?.events.length??0} 条</small></div>
      <div className="event-scroll"><DamageInspector events={state?.events??[]}/>{instances?<div className="instances">{state?.actors.map(a=><div key={a.id}><button className="entity-link" onClick={()=>setEntity(a.id)}>{a.name}</button>{a.author&&<button className="runtime-source" disabled={!preview.ready} onClick={()=>inspectSource(a.author!)}>定位定义</button>}<span>{a.kind}{a.remaining!=null?` · ${a.remaining.toFixed(2)}s`:a.dead?' · 已死亡':''}</span><small>{a.id}</small>{a.kind==="projectile"&&<div className="instance-owner">归属 {a.owner} · 追踪键 {a.trackingKey||"无"} · 技能 {a.skill} · Cast {a.castId} · 第{a.generation}代 · 剩余连锁 {a.chainsLeft} · 路径 {a.distance?.toFixed(2)}/{a.range?.toFixed(2)}m · 速度 {a.speed?.toFixed(2)} · 高度 {a.y?.toFixed(2)} · {a.returning?"返程":"出程"} · 地面/墙剩余 {a.groundBouncesLeft}/{a.wallBouncesLeft}</div>}{a.buffs?.map(buff=><div className="instance-buffs" key={buff.instanceId}>{buff.name} ×{buff.stacks} · {buff.remaining?.toFixed(2)??'永久'}s · 来源 {buff.source}<button aria-label={`移除${a.name}的${buff.name}`} onClick={()=>void preview.removeBuff(a.id,buff.instanceId)}>驱散</button></div>)}<div className="instance-owner">{a.stopped?"停止":"运行"} · 顿帧剩余 {a.hitstopFrames} · 角色时间 {a.logicalTime?.toFixed(2)}s{a.hurtLevel&&a.hurtAge!=null&&a.hurtAge<.3?` · 反馈 ${a.hurtLevel}`:""}</div><div className="instance-owner">动作 {a.actionTime?.toFixed(2)??"-"}/{a.actionDuration?.toFixed(2)??"-"}s · {a.actionRate?.toFixed(2)}× · 控制 {a.controlTags?.join(" / ")||"无"}</div>{(a.currentSkill||a.bufferedInput||a.comboConfirmed)&&<div className="instance-owner">技能 {a.currentSkill??'空闲'} · Cast {a.castId} · 确认 {a.comboConfirmed?'可用':'无'} · 输入 {a.bufferedInput??'无'}</div>}{a.hp!=null&&<div className="instance-owner">护盾 {a.attributes?.Shield?.toFixed(1)} · 高度 {a.y?.toFixed(2)} · 攻击 {a.attributes?.Atk?.toFixed(1)} · 防御 {a.attributes?.Def?.toFixed(1)} · 移速 {a.attributes?.MoveSpeed?.toFixed(1)} · Tag {Object.entries(a.tags??{}).map(([key,value])=>`${key}×${value}`).join(' / ')}</div>}{a.kind==='aoe'&&<div className="instance-owner">区域半径 {a.radius?.toFixed(3)}m · 吸收 {a.absorbedProjectiles} · 冲量速度 {a.areaVelocityX?.toFixed(3)} / {a.areaVelocityZ?.toFixed(3)}</div>}{a.kind==='construct'&&<div className="instance-owner">归属 {a.owner} · 连爆组 {a.group} · 受伤上限 {a.damageCap===0?'不限':a.damageCap} · 下次自伤 {a.nextDecay?.toFixed(2)??'关闭'}s</div>}{(a.kind==='summon'||a.kind==='construct')&&<>{a.kind==='summon'&&<div className="instance-owner">归属 {a.owner} · 技能 {a.skill} · 能量 {a.energy?.toFixed(1)}</div>}<button onClick={()=>void preview.remove(a.id)}>移除</button></>}</div>)}</div>:<RuntimeEventTable events={events} inspectSource={inspectSource} inspectEntity={setEntity} ready={preview.ready}/>}</div>
    </section>
    <div className="preview-root">预览技能：{project.skills.find(s=>s.id===root)?.name??root}</div>
    {entity&&<div className="entity-focus" role="status">{inspected?('实体 '+inspected.name+' · '+entity):('实体已回收 · '+entity)}<button onClick={()=>setEntity('')}>取消定位</button></div>}
    <section className="preview-panel"><div className="panel-title"><b>运行预览</b><span className="grow"/><button className={!configuration?'active':''} onClick={()=>setConfiguration(false)}>测试场景</button><button className={configuration?'active':''} onClick={()=>{setConfiguration(true);setText(JSON.stringify(project,null,2));}}>配置</button></div>
      {configuration?<div className="configuration"><div><button disabled={applying} onClick={()=>void applyConfiguration()}>{applying?'校验中…':'应用当前工程配置'}</button><button onClick={()=>void navigator.clipboard.writeText(text)}>复制</button></div><textarea aria-label="工程 JSON" spellCheck={false} value={text} onChange={e=>setText(e.target.value)}/>{error&&<p role="alert">{error}</p>}</div>:<>
      <div className="preview-controls"><button className="primary" disabled={!preview.ready} onPointerDown={e=>{e.currentTarget.setPointerCapture(e.pointerId);void preview.cast();}} onPointerUp={()=>void preview.release()} onPointerCancel={()=>void preview.cancel()} onBlur={()=>void preview.cancel()} onKeyDown={e=>{if(!e.repeat&&(e.key===' '||e.key==='Enter')){e.preventDefault();void preview.cast();}}} onKeyUp={e=>{if(e.key===' '||e.key==='Enter'){e.preventDefault();void preview.release();}}}><Crosshair size={13}/>施放</button><button disabled={!preview.ready} onClick={()=>preview.setPlaying(!preview.playing)}>{preview.playing?<Pause size={12}/>:<Play size={12}/>} {preview.playing?'暂停':'播放'}</button><button disabled={!preview.ready||preview.playing} onClick={()=>void preview.step()}><SkipForward size={12}/>逐帧</button><button disabled={preview.replaying||preview.placing} onClick={()=>void preview.reset()}><RotateCcw size={12}/>重置</button><button disabled={!preview.ready} onClick={()=>void preview.interrupt()}>打断</button></div>
      <div className="preview-options"><label><input type="checkbox" disabled={!preview.ready} checked={preview.continuous} onChange={e=>preview.setContinuous(e.target.checked)}/>连续施放</label><select aria-label="播放速度" value={preview.speed} onChange={e=>preview.setSpeed(Number(e.target.value))}>{[.25,.5,1,2].map(n=><option key={n} value={n}>{n}×</option>)}</select><span>{state?.time.toFixed(2)??'0.00'} s</span></div>
      <div className="preview-readiness" role="status">{!project.skills.length?'空工程：新建技能后可运行预览':preview.ready?'当前配置已应用':preview.error?'配置无效：'+preview.error:'正在校验并应用配置…'}</div>
      <details className="preview-settings"><summary>预览设置 · 输入、复现与布置</summary><ComboInput project={project} ready={preview.ready} send={preview.input}/>
      <ReplayControls preview={preview} project={project} update={update}/>
      <SceneResources project={project} update={update}/>
      <SceneObstacles project={project} update={update}/>
      <SceneActorProperties project={project} update={update} selectedId={sceneActor} select={setSceneActor}/></details>
      {state?.charging&&<div className="charge-box"><div>蓄力 {Math.round(state.chargeProgress*100)}%<span>松开释放</span></div><progress aria-label="蓄力进度" max={1} value={state.chargeProgress}/></div>}
      <PreviewWorld project={project} preview={preview} inspected={inspected} update={update} select={setSceneActor} selectedId={sceneActor} onBlur={stopMovement}/>
        <div className="energy-box"><div><b>{state?.channeling?'引导中 · 能量':state?.charging?'蓄力中 · 能量':'能量'}</b><span>{state?.energy.toFixed(0)??project.preview.initialEnergy??project.preview.energy} / {project.preview.energy}</span></div><progress max={project.preview.energy} value={state?.energy??project.preview.initialEnergy??project.preview.energy}/><small>弹药 {state?.ammo??project.preview.initialAmmo??project.preview.ammo??60} / {state?.ammoCapacity??project.preview.ammo??60}</small> · <small>冷却 {Math.max(0,state?.cooldowns[preview.skill.current]??0).toFixed(2)} s · {state?.actors.filter(a=>a.kind==='projectile').length??0} 个投射物 · {state?.castPower.toFixed(2)??'1.00'}×</small></div>
      <div className="statistics">{[['总伤害',state?.statistics.damage??0],['有效命中',state?.statistics.hits??0],['技能施行',state?.statistics.casts??0],['投射物',state?.statistics.projectiles??0],['连锁触发',state?.statistics.chains??0],['分裂触发',state?.statistics.splits??0]].map(([label,value])=><div key={String(label)}><small>{label}</small><strong>{number(Number(value))}</strong></div>)}</div>
      </>}
    </section>
  </aside>;
}
