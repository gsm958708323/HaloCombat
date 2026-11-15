import type {ActorView,AuthorNodeSource} from './model';

const labels:Record<string,string>={caster:'施法者',target:'目标',projectile:'投射物',aoe:'持续区域',summon:'召唤物',construct:'可破坏物'};
export function RuntimeInstances({actors,ready,inspectEntity,inspectSource,remove,removeBuff}:{actors:ActorView[];ready:boolean;inspectEntity:(id:string)=>void;inspectSource:(source:AuthorNodeSource)=>void;remove:(id:string)=>Promise<unknown>;removeBuff:(id:string,instance:number)=>Promise<unknown>}) {
  return <div className="instances">{!actors.length&&<p className="runtime-empty">当前没有运行实例。</p>}{actors.map(a=><div key={a.id}>
    <div className="instance-heading"><button className="entity-link" onClick={()=>inspectEntity(a.id)}>{a.name}</button><span>{labels[a.kind]??a.kind}{a.remaining!=null?` · ${a.remaining.toFixed(2)}s`:a.dead?' · 已死亡':''}</span></div>
    {a.author&&<button className="runtime-source" disabled={!ready} onClick={()=>inspectSource(a.author!)}>定位定义</button>}
    {a.currentSkill&&<div className="instance-owner">{a.currentSkill} · Cast {a.castId} · {a.actionTime?.toFixed(2)??'-'}/{a.actionDuration?.toFixed(2)??'-'}s</div>}
    {a.buffs?.map(buff=><div className="instance-buffs" key={buff.instanceId}>{buff.name} ×{buff.stacks} · {buff.remaining?.toFixed(2)??'永久'}s · 来源 {buff.source}<button disabled={!ready} aria-label={`移除${a.name}的${buff.name}`} onClick={()=>void removeBuff(a.id,buff.instanceId)}>驱散</button></div>)}
    <details className="instance-details"><summary>查看状态</summary><small>{a.id}</small>
      {a.kind==='projectile'&&<div className="instance-owner">归属 {a.owner} · 追踪键 {a.trackingKey||'无'} · 技能 {a.skill} · Cast {a.castId} · 第{a.generation}代 · 剩余连锁 {a.chainsLeft} · 路径 {a.distance?.toFixed(2)}/{a.range?.toFixed(2)}m · 速度 {a.speed?.toFixed(2)} · 高度 {a.y?.toFixed(2)} · {a.returning?'返程':'出程'} · 地面/墙剩余 {a.groundBouncesLeft}/{a.wallBouncesLeft}</div>}
      <div className="instance-owner">{a.stopped?'停止':'运行'} · 顿帧剩余 {a.hitstopFrames} · 角色时间 {a.logicalTime?.toFixed(2)}s{a.hurtLevel&&a.hurtAge!=null&&a.hurtAge<.3?` · 反馈 ${a.hurtLevel}`:''}</div>
      <div className="instance-owner">动作 {a.actionTime?.toFixed(2)??'-'}/{a.actionDuration?.toFixed(2)??'-'}s · {a.actionRate?.toFixed(2)}× · 控制 {a.controlTags?.join(' / ')||'无'}</div>
      {(a.currentSkill||a.bufferedInput||a.comboConfirmed)&&<div className="instance-owner">技能 {a.currentSkill??'空闲'} · 确认 {a.comboConfirmed?'可用':'无'} · 输入 {a.bufferedInput??'无'}</div>}
      {a.hp!=null&&<div className="instance-owner">生命 {a.hp}/{a.maxHp} · 护盾 {a.attributes?.Shield?.toFixed(1)} · 高度 {a.y?.toFixed(2)} · 攻击 {a.attributes?.Atk?.toFixed(1)} · 防御 {a.attributes?.Def?.toFixed(1)} · 移速 {a.attributes?.MoveSpeed?.toFixed(1)} · Tag {Object.entries(a.tags??{}).map(([key,value])=>`${key}×${value}`).join(' / ')}</div>}
      {a.kind==='aoe'&&<div className="instance-owner">区域半径 {a.radius?.toFixed(3)}m · 吸收 {a.absorbedProjectiles} · 冲量速度 {a.areaVelocityX?.toFixed(3)} / {a.areaVelocityZ?.toFixed(3)}</div>}
      {a.kind==='construct'&&<div className="instance-owner">归属 {a.owner} · 连爆组 {a.group} · 受伤上限 {a.damageCap===0?'不限':a.damageCap} · 下次自伤 {a.nextDecay?.toFixed(2)??'关闭'}s</div>}
      {a.kind==='summon'&&<div className="instance-owner">归属 {a.owner} · 技能 {a.skill} · 能量 {a.energy?.toFixed(1)}</div>}
    </details>
    {(a.kind==='summon'||a.kind==='construct')&&<button disabled={!ready} onClick={()=>void remove(a.id)}>移除</button>}
  </div>)}</div>;
}
