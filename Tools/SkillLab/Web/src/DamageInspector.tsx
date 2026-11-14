import type {RunEvent} from './model';

export function DamageInspector({events}:{events:RunEvent[]}){
  const damage=events.filter(e=>(e.kind==='damage'||e.kind==='critical')&&e.damage).slice(-10).reverse();
  return <details className="damage-inspector"><summary>伤害明细 · {damage.length}</summary>
    {damage.map(event=><article key={event.sequence}>
      <b>#{event.hitIndex} · {event.source} → {event.damage!.target}</b>
      <small>{event.skill??'无技能来源'} · Cast {event.castId} · {event.damage!.kind==='elemental'?'通用元素':'物理'}{event.damage!.killed?' · 击杀':''}</small>
      <dl>{[['基础',event.damage!.base],['倍率后',event.damage!.scaled],['减防后',event.damage!.afterDefense],['暴击后',event.damage!.afterCritical],
        ['最终计算',event.damage!.final],['护盾吸收',event.damage!.absorbed],['实际扣血',event.damage!.hpLost],['过量伤害',event.damage!.overkill]].map(([name,value])=>
          <div key={String(name)}><dt>{name}</dt><dd>{Number(value).toFixed(2)}</dd></div>)}</dl>
    </article>)}
  </details>;
}
