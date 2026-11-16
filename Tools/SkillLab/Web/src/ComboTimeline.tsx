import {TimelineProperties} from './TimelineProperties';
import type {Project,Skill,AuthoredTimeline} from './model';

export function ComboTimeline({project,skill,change,progress,changeWindow}:{project:Project;skill?:Skill;change:(timeline:AuthoredTimeline,sharedId?:string)=>void;progress:number;changeWindow:(field:string,value:number)=>void}){
  const shared=project.timelines?.find(t=>t.id===skill?.timelineRef),owner=shared??skill,timeline=owner?.timeline;
  const cast=skill?.nodes.find(n=>n.kind==='cast');
  return <section className="combo-timeline"><h3>{skill?.name??'选择技能'} · 动作与接续窗口</h3>
    {!skill?<p>选择图中的技能后配置接续窗口。</p>:timeline?<><small>{shared?'共享时间轴 · '+shared.name:'本技能时间轴'} · 窗口修改直接使用同一技能定义</small>
      <div className="combo-tracks" aria-label="技能时间轴区间">{['cancel','hitbox','move','iframe'].map(kind=><div className="combo-track" key={kind}><span>{({cancel:'接续窗口',hitbox:'攻击判定',move:'位移',iframe:'无敌'} as Record<string,string>)[kind]}</span><div>
        {timeline.clips.filter(c=>c.kind===kind).map(c=><i key={c.id} style={{left:c.start/timeline.duration*100+'%',width:(c.end-c.start)/timeline.duration*100+'%'}} title={`${c.start}～${c.end}秒`}/>)}
        <b style={{left:Math.min(100,progress/timeline.duration*100)+'%'}}/></div></div>)}</div>
      <details className="combo-timeline-edit"><summary>编辑实际时间轴与窗口</summary><TimelineProperties skill={owner!} change={value=>change(value,shared?.id)}/></details></>:cast?.parameters.execution==='operation'?<p>即时操作没有动作窗口，可在空闲状态通过输入触发。</p>:<>
        <p>自动时序的接续窗口会编译为同一技能时间轴的取消区间。</p>
        {['cancelStart','cancelEnd'].map(field=><label key={field}>{field==='cancelStart'?'接续开始 / 秒':'接续结束 / 秒（0关闭）'}<input aria-label={field==='cancelStart'?'自动时序接续开始':'自动时序接续结束'} type="number" min="0" max="120" step=".02" value={Number(cast?.parameters[field]??0)} onChange={e=>{if(e.target.value!=='')changeWindow(field,Number(e.target.value));}}/></label>)}
        <small>窗口结束必须在动作总长内；校验失败时保留现有预览。</small>
      </>}
  </section>;
}
