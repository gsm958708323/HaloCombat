import { useState } from 'react';
import type { AuthoredTimeline,Skill,TimelineClip } from './model';

type Props = {
  skill: Pick<Skill, 'nodes' | 'timeline'>;
  change: (timeline: AuthoredTimeline) => void;
  defaultCollapsed?: boolean;
};

export function TimelineProperties({skill, change, defaultCollapsed = false}: Props) {
  const [expanded, setExpanded] = useState(!defaultCollapsed);
  const value=skill.timeline;if(!value)return null;
  const controls:[[number,string],...Array<[number,string]>]=[[1011,'禁止走动/跳跃'],[1012,'禁止转向'],[1013,'禁止新施放'],[1014,'禁止技能增量位移'],[1015,'禁止受击位移'],[1016,'暂停重力']];
  const number=(text:string,set:(number:number)=>void)=>{if(text!=='')set(Number(text));};
  const clip=(id:string,patch:Partial<TimelineClip>)=>change({...value,clips:value.clips.map(c=>c.id===id?{...c,...patch}:c)});
  const candidates=skill.nodes.filter(n=>!['target','cast','skill-events'].includes(n.kind));
  const entries=(ids:string[],set:(ids:string[])=>void)=><div className="timeline-entries">{ids.map((id,i)=><div key={i}><span>{i+1}. {skill.nodes.find(n=>n.id===id)?.kind??'缺失'}<small>{id}</small></span><button disabled={i===0} onClick={()=>{const next=[...ids];[next[i-1],next[i]]=[next[i],next[i-1]];set(next);}}>↑</button><button onClick={()=>set(ids.filter((_,index)=>index!==i))}>×</button></div>)}
    <select aria-label="添加时间轴效果入口" value="" onChange={event=>{if(event.target.value)set([...ids,event.target.value]);}}><option value="">添加效果入口…</option>{candidates.map(n=><option key={n.id} value={n.id}>{n.kind} · {n.id}</option>)}</select></div>;
  return <details className="timeline-properties" open={expanded} onToggle={event => setExpanded(event.currentTarget.open)}>
    <summary>{defaultCollapsed ? '编辑显式时间轴' : '显式时间轴'} · {value.clips.length} 区间 / {value.payloads.length} 触发</summary>
    <p>时间轴决定区间与离散触发。引用图中的效果节点，列表顺序可重复；卡片位置不改变时序。</p>
    <label>总时长 (秒)<input aria-label="时间轴总时长" type="number" step=".01" min=".001" value={value.duration} onChange={event=>number(event.target.value,duration=>change({...value,duration}))}/></label>
    <small>Timeline ID {value.runtimeId}</small>
    <label>动画语义键<input aria-label="时间轴动画语义" placeholder="如 Fire / Reload / Roll" value={value.animation??''} onChange={event=>change({...value,animation:event.target.value||undefined})}/></label>
    <small>Unity 按视图目录解析状态，动作相位与时间轴同步。</small>
    <label>随动作速度缩放<input aria-label="时间轴动作速度缩放" type="checkbox" checked={value.scaleWithActionSpeed??false} onChange={event=>change({...value,scaleWithActionSpeed:event.target.checked})}/></label>
    <fieldset><legend>动作控制 · 持续到时间轴结束</legend>{controls.map(([tag,label])=><label key={tag}><span>{label}</span><input aria-label={label} type="checkbox" checked={value.controlTags?.includes(tag)??false} onChange={event=>change({...value,controlTags:[...(value.controlTags??[]).filter(id=>id!==tag),...(event.target.checked?[tag]:[])].sort((a,b)=>a-b)})}/></label>)}</fieldset>
    <h4>持续区间</h4>{value.clips.map((c,i)=><fieldset key={c.id}><legend>区间 {i+1}</legend>
      <select aria-label={`区间${i+1}类型`} value={c.kind} onChange={event=>clip(c.id,{kind:event.target.value as TimelineClip['kind'],effects:[],hitRadius:event.target.value==='hitbox'?(c.hitRadius||.8):0})}>{[['cancel','取消窗'],['move','移动'],['hitbox','命中盒'],['iframe','无敌窗']].map(([id,name])=><option key={id} value={id}>{name}</option>)}</select>
      {(['start','end'] as const).map(field=><label key={field}>{field==='start'?'开始':'结束'} (秒)<input aria-label={`区间${i+1}${field}`} type="number" step=".01" value={c[field]} onChange={event=>number(event.target.value,n=>clip(c.id,{[field]:n}))}/></label>)}
      {c.kind==='cancel'&&<label>取消轨道名 (留空=通用窗)<input aria-label={`区间${i+1}轨道`} placeholder="如 dodge / delay" value={c.channel??''} onChange={event=>clip(c.id,{channel:event.target.value.trim()||undefined})}/></label>}
      {c.kind==='move'&&(['moveX','moveY','moveZ','steer'] as const).map(field=><label key={field}>{field}<input aria-label={`区间${i+1}${field}`} type="number" step=".01" value={c[field]??0} onChange={event=>number(event.target.value,n=>clip(c.id,{[field]:n}))}/></label>)}
      {c.kind==='hitbox'&&<><label>半径 (米)<input aria-label={`区间${i+1}半径`} type="number" step=".01" value={c.hitRadius??.8} onChange={event=>number(event.target.value,hitRadius=>clip(c.id,{hitRadius}))}/></label>
        {(['hitOffsetX','hitOffsetY','hitOffsetZ'] as const).map(field=><label key={field}>{field}<input type="number" step=".01" value={c[field]??0} onChange={event=>number(event.target.value,n=>clip(c.id,{[field]:n}))}/></label>)}
        {entries(c.effects??[],effects=>clip(c.id,{effects}))}</>}
      <button onClick={()=>change({...value,clips:value.clips.filter(v=>v.id!==c.id)})}>移除区间</button>
    </fieldset>)}<button onClick={()=>change({...value,clips:[...value.clips,{id:crypto.randomUUID(),kind:'cancel',start:0,end:Math.min(.1,value.duration),effects:[]}]})}>添加区间</button>
    <h4>离散触发</h4>{value.payloads.map((p,i)=><fieldset key={p.id}><legend>触发 {i+1}</legend>
      <label>时点 (秒)<input aria-label={`触发${i+1}时间`} type="number" step=".01" value={p.time} onChange={event=>number(event.target.value,time=>change({...value,payloads:value.payloads.map(v=>v.id===p.id?{...v,time}:v)}))}/></label>
      <label>重复间隔 (0=一次)<input aria-label={`触发${i+1}间隔`} type="number" step=".01" value={p.repeatInterval??0} onChange={event=>number(event.target.value,repeatInterval=>change({...value,payloads:value.payloads.map(v=>v.id===p.id?{...v,repeatInterval}:v)}))}/></label>
      {entries(p.effects,effects=>change({...value,payloads:value.payloads.map(v=>v.id===p.id?{...v,effects}:v)}))}
      <button onClick={()=>change({...value,payloads:value.payloads.filter(v=>v.id!==p.id)})}>移除此触发</button>
    </fieldset>)}<button onClick={()=>change({...value,payloads:[...value.payloads,{id:crypto.randomUUID(),time:0,effects:[]}]})}>添加触发</button>
  </details>;
}
