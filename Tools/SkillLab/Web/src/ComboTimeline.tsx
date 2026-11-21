import {TimelineProperties} from './TimelineProperties';
import type {Project,Skill,AuthoredTimeline} from './model';

export function ComboTimeline({project,skill,change,progress,changeWindow}:{project:Project;skill?:Skill;change:(timeline:AuthoredTimeline,sharedId?:string)=>void;progress:number;changeWindow:(field:string,value:number)=>void}){
  const shared=project.timelines?.find(t=>t.id===skill?.timelineRef),owner=shared??skill,timeline=owner?.timeline;
  const cast=skill?.nodes.find(n=>n.kind==='cast');
  const windows=timeline?.clips.filter(clip=>clip.kind==='cancel')??[];
  const automaticEnd=Number(cast?.parameters.cancelEnd??0);
  const automaticStart=Number(cast?.parameters.cancelStart??0);
  return <section className="combo-timeline"><h3>{skill?.name??'选择技能'} · 动作与接续窗口</h3>
    {!skill?<p>选择图中的技能后配置接续窗口。</p>:timeline?<><small>{shared?'共享时间轴 · '+shared.name:'本技能时间轴'} · 窗口修改直接使用同一技能定义</small>
      <p className="combo-window-summary">接续窗口 {windows.length?windows.map(clip=>`${clip.start}–${clip.end} 秒`).join(' / '):'未配置'}{progress>0?` · 当前 ${progress.toFixed(2)} 秒`:''}</p>
      <div className="combo-tracks" aria-label="技能时间轴区间">
        <TimelineTrack timeline={timeline} kind="cancel" label="接续窗口" progress={progress}/>
      </div>
      <details className="combo-timeline-edit"><summary>查看并编辑完整时间轴</summary>
        <div className="combo-tracks">
          <TimelineTrack timeline={timeline} kind="hitbox" label="攻击判定" progress={progress}/>
          <TimelineTrack timeline={timeline} kind="move" label="位移" progress={progress}/>
          <TimelineTrack timeline={timeline} kind="iframe" label="无敌" progress={progress}/>
        </div>
        <TimelineProperties skill={owner!} change={value=>change(value,shared?.id)}/>
      </details></>:cast?.parameters.execution==='operation'?<p>即时操作没有动作窗口，可在空闲状态通过输入触发。</p>:<>
        <p className="combo-window-summary">接续窗口 {automaticEnd>automaticStart?`${automaticStart}–${automaticEnd} 秒`:'未配置'}{progress>0?` · 当前 ${progress.toFixed(2)} 秒`:''}</p>
        {['cancelStart','cancelEnd'].map(field=><label key={field}>{field==='cancelStart'?'接续开始 / 秒':'接续结束 / 秒（0关闭）'}<input aria-label={field==='cancelStart'?'自动时序接续开始':'自动时序接续结束'} type="number" min="0" max="120" step=".02" value={Number(cast?.parameters[field]??0)} onChange={e=>{if(e.target.value!=='')changeWindow(field,Number(e.target.value));}}/></label>)}
        <small>窗口结束必须在动作总长内；校验失败时保留现有预览。</small>
      </>}
  </section>;
}

function TimelineTrack({timeline,kind,label,progress}:{timeline:AuthoredTimeline;kind:string;label:string;progress:number}){
  return <div className="combo-track"><span>{label}</span><div>
    {timeline.clips.filter(clip=>clip.kind===kind).map(clip=><i key={clip.id}
      style={{left:clip.start/timeline.duration*100+'%',width:(clip.end-clip.start)/timeline.duration*100+'%'}}
      title={`${clip.start}～${clip.end}秒`}/>)}
    <b style={{left:Math.min(100,progress/timeline.duration*100)+'%'}}/>
  </div></div>;
}
