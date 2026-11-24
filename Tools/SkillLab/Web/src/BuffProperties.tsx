import type {BuffDefinition,BuffModifier} from './model';

export function BuffProperties({buff,change}:{buff:BuffDefinition;change:(value:BuffDefinition)=>void}){
  const number=(key:'duration'|'interval'|'maxStacks'|'mutexGroup',text:string)=>{if(text!=='')change({...buff,[key]:Number(text)});};
  const modifier=(index:number,values:Partial<BuffModifier>)=>change({...buff,modifiers:buff.modifiers.map((m,i)=>i===index?{...m,...values}:m)});
  return <section className="buff-properties"><h3>Buff 定义</h3>
    <label>叠层策略<select aria-label="Buff叠层策略" value={buff.stack} onChange={e=>change({...buff,stack:e.target.value})}>
      <option value="refresh">刷新时长</option><option value="add">增加层数并刷新</option><option value="independent">独立实例</option><option value="reject">已有则拒绝</option></select></label>
    <label>永久<input aria-label="Buff永久" type="checkbox" checked={buff.permanent} onChange={e=>change({...buff,permanent:e.target.checked})}/></label>
    {!buff.permanent&&<label>持续时间 (秒)<input aria-label="Buff持续时间" type="number" min=".001" step=".05" value={buff.duration} onChange={e=>number('duration',e.target.value)}/></label>}
    <label>周期 (秒，0=无)<input aria-label="Buff周期" type="number" min="0" step=".001" value={buff.interval} onChange={e=>number('interval',e.target.value)}/></label>
    <label>最大层数<input aria-label="Buff最大层数" type="number" min="1" max="128" value={buff.maxStacks} onChange={e=>number('maxStacks',e.target.value)}/></label>
    <label>互斥组 (0=无)<input aria-label="Buff互斥组" type="number" min="0" value={buff.mutexGroup} onChange={e=>number('mutexGroup',e.target.value)}/></label>
    <h4>授予 Tag</h4>{buff.tags.map((tag,index)=><div className="buff-tag" key={index}><input aria-label={`BuffTag${index+1}`} type="number" min="1" value={tag} onChange={e=>{if(e.target.value!=='')change({...buff,tags:buff.tags.map((v,i)=>i===index?Number(e.target.value):v)});}}/>
      <button aria-label={`移除BuffTag${index+1}`} onClick={()=>change({...buff,tags:buff.tags.filter((_,i)=>i!==index)})}>×</button></div>)}
    <button onClick={()=>change({...buff,tags:[...buff.tags,Math.max(3100,...buff.tags)+1]})}>添加 Tag</button>
    <h4>属性修正 · 每实例一次</h4>{buff.modifiers.map((m,index)=><fieldset key={index}>
      <select aria-label={`修正${index+1}属性`} value={m.attribute} onChange={e=>modifier(index,{attribute:e.target.value})}>{[['maxHp','生命上限'],['attack','攻击'],['defense','防御'],['moveSpeed','移动速度'],['critRate','暴击率'],['damageDealt','伤害加成'],['damageTaken','受到伤害'],['actionSpeed','动作速度']].map(([key,label])=><option key={key} value={key}>{label}</option>)}</select>
      <select aria-label={`修正${index+1}运算`} value={m.operation} onChange={e=>modifier(index,{operation:e.target.value})}><option value="add">加值</option><option value="mul">乘值</option><option value="override">覆盖</option></select>
      <input aria-label={`修正${index+1}数值`} type="number" step=".1" value={m.value} onChange={e=>{if(e.target.value!=='')modifier(index,{value:Number(e.target.value)});}}/>
      <input aria-label={`修正${index+1}优先级`} type="number" value={m.priority} onChange={e=>{if(e.target.value!=='')modifier(index,{priority:Number(e.target.value)});}}/>
      <button onClick={()=>change({...buff,modifiers:buff.modifiers.filter((_,i)=>i!==index)})}>移除此修正</button>
    </fieldset>)}
    <button onClick={()=>change({...buff,modifiers:[...buff.modifiers,{attribute:'moveSpeed',operation:'mul',value:.5,priority:0}]})}>添加属性修正</button>
    <p>左侧预览仍施放当前技能。生命周期效果在本图连线，持续伤害可勾选“按Buff层数缩放”。</p>
  </section>;
}
