import type { Project, SummonBlueprint } from './model';

const numbers: [keyof SummonBlueprint,string][]=[['hp','生命'],['attack','攻击'],['defense','防御'],['moveSpeed','移速'],['collisionRadius','净空半径'],
  ['energy','能量上限'],['initialEnergy','初始能量'],['recovery','每秒恢复'],['acquireRadius','索敌距离'],['attackRange','施放距离'],['followRange','跟随距离'],['holdDuration','保持时长']];

export function SummonBlueprints({project,update}:{project:Project;update:(project:Project)=>void}){
  const blueprints=project.summonBlueprints??[];
  function change(items:SummonBlueprint[]){update({...project,summonBlueprints:items});}
  function field(id:string,key:keyof SummonBlueprint,value:string|number){change(blueprints.map(b=>b.id===id?{...b,[key]:value}:b));}
  function add(){change([...blueprints,{id:crypto.randomUUID(),name:'新召唤蓝图',skillId:project.skills[0].id,hp:100,attack:80,defense:0,moveSpeed:3,collisionRadius:.4,
    energy:100,initialEnergy:100,recovery:10,acquireRadius:20,attackRange:12,followRange:2,holdDuration:.75}]);}
  return <details className="blueprint-panel"><summary>召唤蓝图 · {blueprints.length}</summary><button onClick={add}>添加蓝图</button>
    {blueprints.map((blueprint,index)=>{
      const used=project.skills.some(s=>s.nodes.some(n=>n.kind==='delivery'&&n.parameters.mode==='summon'&&n.parameters.blueprint===blueprint.id));
      return <fieldset key={blueprint.id}><legend>{blueprint.name||'蓝图 '+(index+1)}</legend>
        <label>名称<input aria-label={`蓝图${index+1}名称`} value={blueprint.name??''} onChange={e=>field(blueprint.id,'name',e.target.value)}/></label>
        <label>攻击技能<select aria-label={`蓝图${index+1}攻击技能`} value={blueprint.skillId} onChange={e=>field(blueprint.id,'skillId',e.target.value)}>{project.skills.map(s=><option key={s.id} value={s.id}>{s.name}</option>)}</select></label>
        <div className="blueprint-fields">{numbers.map(([key,label])=><label key={key}>{label}<input aria-label={`蓝图${index+1}${label}`} type="number" min={key==='hp'?.01:0} step=".1" value={blueprint[key]??blueprint.energy}
          onChange={e=>{if(e.target.value!=='')field(blueprint.id,key,Number(e.target.value));}}/></label>)}</div>
        <button disabled={used} title={used?'已有技能引用此蓝图':''} onClick={()=>change(blueprints.filter(b=>b.id!==blueprint.id))}>移除蓝图</button>
        {used&&<small>已有技能引用</small>}
      </fieldset>;
    })}
  </details>;
}
