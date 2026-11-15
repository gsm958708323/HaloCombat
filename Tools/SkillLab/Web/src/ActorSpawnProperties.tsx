import {useState} from 'react';
import type {Project,RuntimeActor,WanderTiming} from './model';
import './actor-spawn.css';

const defaultWander:WanderTiming={firstTurn:2,turnIntervalMin:1.6,turnIntervalMax:3.2,turnAngle:90,firstShot:3,shotIntervalMin:2,shotIntervalMax:5};
export function ActorSpawnProperties({project,update}:{project:Project;update:(change:(p:Project)=>Project)=>void}) {
  const actors=project.runtime?.actors??[];const [selected,setSelected]=useState('');const actor=actors.find(a=>a.id===selected)??actors[0];
  if(!actor)return null;
  const edit=(change:(a:RuntimeActor)=>RuntimeActor)=>update(p=>({...p,runtime:p.runtime?{...p.runtime,actors:p.runtime.actors.map(a=>a.id===actor.id?change(a):a)}:undefined}));
  function number(label:string,value:number,min:number,max:number,step:number,change:(value:number)=>void) {
    return <label key={label}>{label}<input aria-label={label} type="number" value={value} min={min} max={max} step={step} onChange={event=>{if(event.target.value==='')return;const next=Number(event.target.value);if(Number.isFinite(next))change(next);}}/></label>;
  }
  const stats=actor.spawnStats,wandering=actor.ai==='wander-shooter';
  return <details className="actor-spawn-properties"><summary>角色生成与 AI · {actors.length}</summary>
    <label>角色<select aria-label="生成规则角色" value={actor.id} onChange={e=>setSelected(e.target.value)}>{actors.map(a=><option key={a.id} value={a.id}>{a.id}</option>)}</select></label>
    <p>只在生成时采样。敌人成长按累计生成序号递增，重开从0开始；现有角色在明确重载后更新。</p>
    <label>生成数值采样<input aria-label="生成数值采样" type="checkbox" checked={!!stats} onChange={e=>edit(a=>({...a,spawnStats:e.target.checked?{hpPerSpawn:0,attackPerSpawn:0,attackRandomRange:0}:undefined}))}/></label>
    {stats&&<>{([['hpPerSpawn','每次生成生命增长',0,1000000,.1],['attackPerSpawn','每次生成攻击增长',0,1000000,.1],['attackRandomRange','攻击整数随机范围',0,1000000,1]] as const).map(([key,label,min,max,step])=>number(label,stats[key],min,max,step,v=>edit(a=>({...a,spawnStats:{...a.spawnStats!,[key]:v}}))))}
      <label>速度饱和曲线<input aria-label="速度饱和曲线" type="checkbox" checked={!!stats.moveSpeedCurve} onChange={e=>edit(a=>({...a,spawnStats:{...a.spawnStats!,moveSpeedCurve:e.target.checked?{base:50,randomRange:20,scale:5.6,divisor:100,offset:.2}:undefined}}))}/></label>
      {stats.moveSpeedCurve&&<>{([['base','速度整数基值',0,1000000,1],['randomRange','速度整数随机范围',0,1000000,1],['scale','速度曲线倍率',0,1000,.1],['divisor','速度曲线分母偏移',.001,1000000,.1],['offset','速度曲线结果偏移',0,1000,.1]] as const).map(([key,label,min,max,step])=>number(label,stats.moveSpeedCurve![key],min,max,step,v=>edit(a=>({...a,spawnStats:{...a.spawnStats!,moveSpeedCurve:{...a.spawnStats!.moveSpeedCurve!,[key]:v}}}))))}<small>速度 = 抽样值 × 倍率 / (抽样值 + 分母偏移) + 结果偏移。</small></>}
    </>}
    {(actor.role==='ai'||actor.role==='summon')&&<><label>AI 行为<select aria-label="AI行为" value={actor.ai??'ranged'} onChange={e=>edit(a=>({...a,ai:e.target.value,wander:e.target.value==='wander-shooter'?{...defaultWander}:undefined}))}>{[['puncher','近战追击'],['guard','近战守卫'],['ranged','射击追击'],['summon','召唤跟随'],['wander-shooter','游走与定时射击']].map(([id,name])=><option key={id} value={id}>{name}</option>)}</select></label>
      <label>攻击技能<select aria-label="AI攻击技能" value={actor.skillId??''} onChange={e=>edit(a=>({...a,skillId:e.target.value}))}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
    </>}
    {wandering&&actor.wander&&<>{([['firstTurn','首次换向时间',0,3600,.02],['turnIntervalMin','换向间隔下限',.02,3600,.02],['turnIntervalMax','换向间隔上限',.02,3600,.02],['turnAngle','随机换向角度',0,360,1],['firstShot','首次射击时间',0,3600,.02],['shotIntervalMin','射击间隔下限',.02,3600,.02],['shotIntervalMax','射击间隔上限',.02,3600,.02]] as const).map(([key,label,min,max,step])=>number(label,actor.wander![key],min,max,step,v=>edit(a=>({...a,wander:{...a.wander!,[key]:v}}))))}<small>游走和射击独立计时；受击/倒地与角色顿帧会冻结计时。</small></>}
  </details>;
}
